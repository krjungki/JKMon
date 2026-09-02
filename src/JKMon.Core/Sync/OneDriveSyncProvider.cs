using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using JKMon.Core.Interop;
using Microsoft.Win32;

namespace JKMon.Core.Sync;

/// <summary>
/// Reads OneDrive sync state through the Cloud Filter API. Registered sync roots are enumerated at every poll
/// because the count changes when accounts are added, removed or signed out.
/// OneDrive never advances its Cloud Filter provider status beyond IDLE and refuses to hand a status UI source to
/// non-shell callers, so an in-progress sync is inferred from OneDrive's transfer activity instead.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class OneDriveSyncProvider : ISyncProvider
{
    private const string SyncRootManagerKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\SyncRootManager";

    // Offset of ProviderStatus inside CF_SYNC_ROOT_STANDARD_INFO.
    private const int ProviderStatusOffset = 24;
    private const int ProviderNameOffset = 28;
    private const int BufferSize = 8192;

    // Measured on an idle client: ~60 samples sat at 0 KiB/s with an 8.4 KiB/s worst case, so the noise floor is far
    // lower than the 64 KiB/s this once used. That old threshold reported "settled" for any sync slower than itself.
    private static readonly long ActivityThresholdBytesPerSecond = 16 * 1024;

    // A slow sync moves file by file, so the gaps between bursts are longer than a fast hydration's.
    private static readonly TimeSpan ActivityHold = TimeSpan.FromSeconds(15);

    private readonly OneDriveActivityProbe _activity;
    private readonly ActivityGate _gate;
    private readonly TimeProvider _time;

    public OneDriveSyncProvider(OneDriveActivityProbe? activity = null, TimeProvider? timeProvider = null)
    {
        _activity = activity ?? new OneDriveActivityProbe();
        _time = timeProvider ?? TimeProvider.System;
        _gate = new ActivityGate(ActivityThresholdBytesPerSecond, ActivityHold);
    }

    public string ProviderId => "onedrive";

    public char Initial => 'O';

    public Task<SyncProviderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
        Task.FromResult(GetSnapshot());

    public SyncProviderSnapshot GetSnapshot()
    {
        if (!_activity.IsRunning())
        {
            return SyncProviderSnapshot.Absent(ProviderId, Initial);
        }

        List<string> roots;
        try
        {
            roots = EnumerateSyncRoots();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return new SyncProviderSnapshot(ProviderId, Initial, SyncState.Unknown, "sync roots unreadable");
        }

        if (roots.Count == 0)
        {
            return SyncProviderSnapshot.Absent(ProviderId, Initial);
        }

        var states = new List<SyncState>(roots.Count);
        foreach (var root in roots)
        {
            var status = TryReadProviderStatus(root);
            states.Add(status is null ? SyncState.Unknown : OneDriveStatusMapper.ToSyncState(status.Value));
        }

        var aggregate = OneDriveStatusMapper.Aggregate(states);
        var transferring = _gate.Update(_activity.TotalTransferBytes(), _time.GetUtcNow());

        // Errors and disconnections outrank activity; otherwise activity is what distinguishes syncing from settled.
        if (aggregate is SyncState.UpToDate && transferring)
        {
            var rate = ByteRate(_gate.LastRateBytesPerSecond);
            return new SyncProviderSnapshot(ProviderId, Initial, SyncState.Synchronizing, $"transferring ({rate})");
        }

        var detail = aggregate switch
        {
            SyncState.UpToDate => $"{roots.Count} sync root(s) settled",
            SyncState.Synchronizing => "a sync root reported activity",
            SyncState.Error => "a sync root reported an error",
            _ => "state unavailable"
        };

        return new SyncProviderSnapshot(ProviderId, Initial, aggregate, detail);
    }

    private static string ByteRate(double bytesPerSecond) =>
        bytesPerSecond >= 1024 * 1024
            ? $"{bytesPerSecond / (1024 * 1024):0.#} MiB/s"
            : $"{bytesPerSecond / 1024:0} KiB/s";

    private static List<string> EnumerateSyncRoots()
    {
        var results = new List<string>();
        using var manager = Registry.LocalMachine.OpenSubKey(SyncRootManagerKey);
        if (manager is null)
        {
            return results;
        }

        foreach (var name in manager.GetSubKeyNames())
        {
            if (!name.StartsWith("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var userRoots = manager.OpenSubKey($@"{name}\UserSyncRoots");
            if (userRoots is null)
            {
                continue;
            }

            foreach (var valueName in userRoots.GetValueNames())
            {
                if (userRoots.GetValue(valueName) is string path &&
                    !string.IsNullOrWhiteSpace(path) &&
                    Directory.Exists(path))
                {
                    results.Add(path);
                }
            }
        }

        return results;
    }

    private static CloudProviderStatus? TryReadProviderStatus(string path)
    {
        var buffer = Marshal.AllocHGlobal(BufferSize);
        try
        {
            var hr = NativeMethods.CfGetSyncRootInfoByPath(
                path, NativeMethods.CfSyncRootInfoStandard, buffer, BufferSize, out var returned);

            if (hr != 0 || returned < ProviderNameOffset)
            {
                return null;
            }

            var raw = (uint)Marshal.ReadInt32(buffer, ProviderStatusOffset);
            return (CloudProviderStatus)raw;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
        catch (EntryPointNotFoundException)
        {
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
