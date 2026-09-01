using System.IO;
using System.IO.Compression;
using System.Net.Http;
using JKMon.Core.Update;

namespace JKMon.App.Update;

/// <summary>Where a downloaded update is assembled before it replaces the installed copy.</summary>
internal sealed record StagedUpdate(ReleaseVersion Version, string StagedDirectory, string WorkDirectory);

/// <summary>
/// Downloads a release, proves it matches the checksums published with it and unpacks it. Nothing is executed and
/// nothing in the app folder is touched until every check has passed.
/// </summary>
internal sealed class UpdateDownloader
{
    private const string StagedFolderName = "staged";
    private const string WorkPrefix = StagingPaths.Prefix;

    private readonly HttpClient _http;

    internal UpdateDownloader(HttpClient http) => _http = http;

    internal static string WorkRootFor(ReleaseVersion version) =>
        StagingPaths.WorkRootFor(Path.GetTempPath(), version);

    internal static bool IsStagingRoot(string? directory) =>
        StagingPaths.IsStagingRoot(directory, Path.GetTempPath());

    /// <summary>Deletes a staging folder and refuses anything that is not one.</summary>
    internal static void TryDeleteStagingRoot(string? directory)
    {
        if (!IsStagingRoot(directory))
        {
            DiagnosticLog.Write($"update: refused to clean up '{directory}'");
            return;
        }

        TryDelete(directory!);
    }

    /// <summary>
    /// Runs in the background because the applier is still executing from the staging folder when the new build
    /// starts, so the first delete attempts fail.
    /// </summary>
    internal static void ScheduleCleanup(string? directory) => Task.Run(async () =>
    {
        if (IsStagingRoot(directory))
        {
            for (var attempt = 0; attempt < 15 && Directory.Exists(directory!); attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                TryDeleteStagingRoot(directory);
            }
        }

        SweepStaleLeftovers();
    });

    /// <summary>
    /// Only folders left untouched for an hour are swept. A younger one may belong to an update that is running
    /// right now, and deleting it would break that update.
    /// </summary>
    private static void SweepStaleLeftovers()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);

        try
        {
            foreach (var stale in Directory.GetDirectories(Path.GetTempPath(), $"{WorkPrefix}*"))
            {
                if (Directory.GetLastWriteTimeUtc(stale) < cutoff)
                {
                    TryDelete(stale);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover that cannot be listed is left for a later run.
        }
    }

    internal async Task<StagedUpdate?> TryStageAsync(UpdateInfo info, CancellationToken cancellationToken)
    {
        var work = WorkRootFor(info.Version);

        try
        {
            if (Directory.Exists(work))
            {
                Directory.Delete(work, recursive: true);
            }

            Directory.CreateDirectory(work);

            var checksums = ReleaseChecksums.Parse(
                await _http.GetStringAsync(info.ChecksumUrl, cancellationToken).ConfigureAwait(false));

            if (!checksums.TryGetValue(info.PackageName, out var expectedPackageHash))
            {
                DiagnosticLog.Write($"update: {info.PackageName} is not listed in the published checksums");
                return null;
            }

            var archive = Path.Combine(work, info.PackageName);
            await DownloadAsync(info.PackageUrl, archive, cancellationToken).ConfigureAwait(false);

            if (!Verify(archive, expectedPackageHash))
            {
                DiagnosticLog.Write("update: archive hash does not match the published checksum");
                return null;
            }

            var staged = Path.Combine(work, StagedFolderName);
            ZipFile.ExtractToDirectory(archive, staged);

            var executable = Path.Combine(staged, "JKMon.exe");
            if (!File.Exists(executable))
            {
                DiagnosticLog.Write("update: the archive does not contain JKMon.exe");
                return null;
            }

            // The archive hash already covers this, but the executable is what will actually run.
            if (checksums.TryGetValue("JKMon.exe", out var expectedExeHash) && !Verify(executable, expectedExeHash))
            {
                DiagnosticLog.Write("update: JKMon.exe hash does not match the published checksum");
                return null;
            }

            File.Delete(archive);
            return new StagedUpdate(info.Version, staged, work);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException
                                       or UnauthorizedAccessException or InvalidDataException)
        {
            DiagnosticLog.Write($"update: staging failed: {ex.Message}");
            TryDelete(work);
            return null;
        }
    }

    private async Task DownloadAsync(Uri url, string destination, CancellationToken cancellationToken)
    {
        using var response = await _http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var file = File.Create(destination);
        await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
    }

    private static bool Verify(string path, string expected)
    {
        using var stream = File.OpenRead(path);
        return ReleaseChecksums.Matches(expected, ReleaseChecksums.HashOf(stream));
    }

    internal static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A locked leftover is cleaned up on a later run.
        }
    }
}
