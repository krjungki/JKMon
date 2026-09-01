using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace JKMon.Core.Update;

/// <summary>
/// A three part release version. Only major, minor and patch order releases; a prerelease suffix is kept for
/// display but never makes a build look newer than the release it precedes.
/// </summary>
public readonly record struct ReleaseVersion(int Major, int Minor, int Patch, string Suffix)
    : IComparable<ReleaseVersion>
{
    public static ReleaseVersion Zero => new(0, 0, 0, string.Empty);

    /// <summary>Accepts `1.2.3`, `v1.2.3` and `1.2.3-beta.1`. Anything else is not a version.</summary>
    public static bool TryParse(string? text, out ReleaseVersion version)
    {
        version = Zero;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.Trim();
        if (span.StartsWith('v') || span.StartsWith('V'))
        {
            span = span[1..];
        }

        var suffix = string.Empty;
        var dash = span.IndexOfAny(['-', '+']);
        if (dash >= 0)
        {
            suffix = span[(dash + 1)..];
            span = span[..dash];
        }

        var parts = span.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        if (!TryPart(parts[0], out var major) || !TryPart(parts[1], out var minor) || !TryPart(parts[2], out var patch))
        {
            return false;
        }

        version = new ReleaseVersion(major, minor, patch, suffix);
        return true;
    }

    private static bool TryPart(string text, [NotNullWhen(true)] out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    public int CompareTo(ReleaseVersion other)
    {
        var byNumber = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (byNumber != 0)
        {
            return byNumber;
        }

        // A release outranks any prerelease of the same number, which is what SemVer requires.
        return (Suffix.Length == 0, other.Suffix.Length == 0) switch
        {
            (true, false) => 1,
            (false, true) => -1,
            _ => string.CompareOrdinal(Suffix, other.Suffix)
        };
    }

    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => Suffix.Length == 0
        ? $"{Major}.{Minor}.{Patch}"
        : $"{Major}.{Minor}.{Patch}-{Suffix}";
}
