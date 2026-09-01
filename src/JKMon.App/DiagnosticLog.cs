using System.IO;
using JKMon.Core.Settings;

namespace JKMon.App;

/// <summary>Minimal last-resort log so a swallowed background failure is still diagnosable.</summary>
internal static class DiagnosticLog
{
    private const long MaxBytes = 1024 * 1024;

    private static readonly object Gate = new();

    private static readonly string LogPath = AppPaths.DiagnosticsFile;

    internal static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Polling writes continuously, so the log restarts instead of growing without bound.
                if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxBytes)
                {
                    File.Delete(LogPath);
                }

                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must never take the app down.
        }
    }
}
