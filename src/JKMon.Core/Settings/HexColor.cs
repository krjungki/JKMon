using System.Globalization;

namespace JKMon.Core.Settings;

/// <summary>Parses hex colour strings without depending on a UI framework so settings stay testable.</summary>
public readonly record struct HexColor(byte A, byte R, byte G, byte B)
{
    public string ToHex() => A == 255
        ? string.Create(CultureInfo.InvariantCulture, $"#{R:X2}{G:X2}{B:X2}")
        : string.Create(CultureInfo.InvariantCulture, $"#{A:X2}{R:X2}{G:X2}{B:X2}");

    /// <summary>Accepts #RGB, #RRGGBB and #AARRGGBB, with or without the leading hash.</summary>
    public static bool TryParse(string? value, out HexColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (!text.All(Uri.IsHexDigit))
        {
            return false;
        }

        switch (text.Length)
        {
            case 3:
                color = new HexColor(
                    255,
                    Repeat(text[0]),
                    Repeat(text[1]),
                    Repeat(text[2]));
                return true;

            case 6:
                color = new HexColor(255, Byte(text, 0), Byte(text, 2), Byte(text, 4));
                return true;

            case 8:
                color = new HexColor(Byte(text, 0), Byte(text, 2), Byte(text, 4), Byte(text, 6));
                return true;

            default:
                return false;
        }
    }

    public static HexColor ParseOrDefault(string? value, HexColor fallback) =>
        TryParse(value, out var color) ? color : fallback;

    /// <summary>Perceived brightness, used to pick readable text over an arbitrary fill.</summary>
    public double Luminance => (0.299 * R + 0.587 * G + 0.114 * B) / 255d;

    private static byte Byte(string text, int index) =>
        byte.Parse(text.AsSpan(index, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static byte Repeat(char c) =>
        byte.Parse(new string(c, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
