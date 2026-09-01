using System.Runtime.Versioning;

namespace JKMon.Core.Settings;

/// <summary>
/// Resolves where the app keeps its own state. A portable copy keeps everything beside the executable, so the whole
/// app is one movable folder. When that folder is read-only, as under Program Files, the state falls back to
/// LOCALAPPDATA rather than failing to start.
/// </summary>
[SupportedOSPlatform("windows")]
public static class AppPaths
{
    private static readonly Lazy<string> Root = new(Resolve);

    public static string DataRoot => Root.Value;

    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");

    public static string DiagnosticsFile => Path.Combine(DataRoot, "diagnostics.log");

    /// <summary>True when state lives beside the executable instead of the per-user profile.</summary>
    public static bool IsPortable =>
        string.Equals(DataRoot, Normalize(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase);

    private static string Resolve()
    {
        var beside = Normalize(AppContext.BaseDirectory);
        return IsWritable(beside)
            ? beside
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JKMon");
    }

    private static string Normalize(string directory) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

    /// <summary>Probed rather than inferred from ACLs, because only an actual write proves the folder is usable.</summary>
    private static bool IsWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".jkmon-probe-{Guid.NewGuid():N}");
            using (File.Create(probe, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
