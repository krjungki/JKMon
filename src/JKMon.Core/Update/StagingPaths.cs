using System.IO;

namespace JKMon.Core.Update;

/// <summary>
/// Naming rules for the folder an update is staged in. The cleanup path deletes recursively, so deciding what
/// counts as a staging folder is kept here as a pure function that can be tested.
/// </summary>
public static class StagingPaths
{
    public const string Prefix = "JKMon-update-";

    public static string WorkRootFor(string tempRoot, ReleaseVersion version) =>
        Path.Combine(tempRoot, $"{Prefix}{version}");

    /// <summary>True only for a direct child of <paramref name="tempRoot"/> whose name this app would have chosen.</summary>
    public static bool IsStagingRoot(string? directory, string tempRoot)
    {
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(tempRoot))
        {
            return false;
        }

        try
        {
            var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(tempRoot));

            return string.Equals(Path.GetDirectoryName(full), root, StringComparison.OrdinalIgnoreCase)
                   && Path.GetFileName(full).StartsWith(Prefix, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return false;
        }
    }
}
