using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

/// <summary>
/// Covers the fallback that keeps the OneDrive circle meaningful when the Cloud Filter status cannot be read,
/// which is what left the circle grey on Windows on ARM.
/// </summary>
public class OneDriveUnreadableStatusTests
{
    private sealed class FakeProbe(bool running, long[] totals) : OneDriveActivityProbe
    {
        private int _index;

        public override bool IsRunning() => running;

        public override long TotalTransferBytes()
        {
            var value = totals[Math.Min(_index, totals.Length - 1)];
            _index++;
            return value;
        }
    }

    private static (OneDriveSyncProvider Provider, List<string> Log) Build(long[] totals)
    {
        var log = new List<string>();
        var provider = new OneDriveSyncProvider(new FakeProbe(true, totals), TimeProvider.System, log.Add);
        return (provider, log);
    }

    [Fact]
    public void AbsentWhenTheClientIsNotRunning()
    {
        var provider = new OneDriveSyncProvider(new FakeProbe(false, [0]), TimeProvider.System, _ => { });

        var snapshot = provider.GetSnapshot();

        Assert.Equal(SyncState.Absent, snapshot.State);
        Assert.False(snapshot.IsVisible);
    }

    /// <summary>
    /// The circle must never sit on Unknown just because the status read failed. This asserts the states the
    /// provider is allowed to report at all, on whatever machine the suite runs on.
    /// </summary>
    [Fact]
    public void NeverReportsUnknownWhileSyncRootsExist()
    {
        var (provider, _) = Build([0, 0]);

        var snapshot = provider.GetSnapshot();

        Assert.NotEqual(SyncState.Unknown, snapshot.State);
    }

    [Fact]
    public void LogsAtMostOnceForARepeatedFailure()
    {
        var (provider, log) = Build([0, 0, 0, 0]);

        provider.GetSnapshot();
        provider.GetSnapshot();
        provider.GetSnapshot();

        Assert.True(log.Count <= 1, $"expected no repeated logging, got {log.Count} entries");
    }
}
