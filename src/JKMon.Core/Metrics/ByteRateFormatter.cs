using System.Globalization;

namespace JKMon.Core.Metrics;

/// <summary>Formats byte rates using IEC units for the compact overlay text.</summary>
public static class ByteRateFormatter
{
    private static readonly string[] Units = ["B/s", "KiB/s", "MiB/s", "GiB/s", "TiB/s"];

    public static string Format(double bytesPerSecond)
    {
        if (double.IsNaN(bytesPerSecond) || double.IsInfinity(bytesPerSecond) || bytesPerSecond <= 0)
        {
            return "0 B/s";
        }

        var unit = 0;
        var value = bytesPerSecond;
        while (value >= 1024d && unit < Units.Length - 1)
        {
            value /= 1024d;
            unit++;
        }

        var decimals = unit == 0 ? 0 : value < 10d ? 1 : 0;
        return string.Create(CultureInfo.InvariantCulture, $"{Math.Round(value, decimals)} {Units[unit]}");
    }

    public static string FormatPercent(double percent)
    {
        var clamped = percent < 0 ? 0 : percent > 100 ? 100 : percent;
        return string.Create(CultureInfo.InvariantCulture, $"{Math.Round(clamped)}%");
    }
}
