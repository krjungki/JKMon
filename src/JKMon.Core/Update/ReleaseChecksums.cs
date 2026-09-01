using System.Security.Cryptography;

namespace JKMon.Core.Update;

/// <summary>Reads the `SHA256SUMS.txt` published with a release and answers whether a file matches it.</summary>
public static class ReleaseChecksums
{
    /// <summary>
    /// Lines look like `&lt;hash&gt;  &lt;name&gt;`. Prose lines in the same file are ignored, so the published notes and the
    /// checksums can share one document.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(content))
        {
            return result;
        }

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();
            var split = line.IndexOf(' ');
            if (split != 64)
            {
                continue;
            }

            var hash = line[..64];
            if (!IsHex(hash))
            {
                continue;
            }

            var name = line[65..].TrimStart('*', ' ').Trim();
            if (name.Length > 0 && !result.ContainsKey(name))
            {
                result[name] = hash.ToUpperInvariant();
            }
        }

        return result;
    }

    public static string HashOf(Stream stream) =>
        Convert.ToHexString(SHA256.HashData(stream));

    public static bool Matches(string expected, string actual) =>
        expected.Length == 64 && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);

    private static bool IsHex(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
