using JKMon.Core.Metrics;

namespace JKMon.Core.Tests;

public class ByteRateFormatterTests
{
    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(-5, "0 B/s")]
    [InlineData(512, "512 B/s")]
    [InlineData(1024, "1 KiB/s")]
    [InlineData(1536, "1.5 KiB/s")]
    [InlineData(1048576, "1 MiB/s")]
    public void Format_UsesIecUnits(double input, string expected)
    {
        Assert.Equal(expected, ByteRateFormatter.Format(input));
    }

    [Fact]
    public void Format_HandlesNonFiniteValues()
    {
        Assert.Equal("0 B/s", ByteRateFormatter.Format(double.NaN));
        Assert.Equal("0 B/s", ByteRateFormatter.Format(double.PositiveInfinity));
    }

    [Theory]
    [InlineData(-1, "0%")]
    [InlineData(0, "0%")]
    [InlineData(42.4, "42%")]
    [InlineData(150, "100%")]
    public void FormatPercent_ClampsToRange(double input, string expected)
    {
        Assert.Equal(expected, ByteRateFormatter.FormatPercent(input));
    }

    // The overlay reserves a fixed value column, so these upper bounds must hold or the text would be clipped.
    [Fact]
    public void Format_NeverExceedsTheReservedColumnWidth()
    {
        var widest = string.Empty;

        for (double value = 0; value < 2e15; value = value < 1 ? 1 : value * 1.07)
        {
            var text = ByteRateFormatter.Format(value);
            if (text.Length > widest.Length)
            {
                widest = text;
            }
        }

        Assert.True(widest.Length <= 10, $"widest formatted rate was '{widest}'");
    }

    [Fact]
    public void FormatPercent_NeverExceedsTheReservedColumnWidth()
    {
        for (double value = -10; value <= 110; value += 0.3)
        {
            Assert.True(ByteRateFormatter.FormatPercent(value).Length <= 4);
        }
    }
}
