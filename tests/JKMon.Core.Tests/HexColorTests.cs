using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

public class HexColorTests
{
    [Theory]
    [InlineData("#35FF6A", 255, 0x35, 0xFF, 0x6A)]
    [InlineData("35FF6A", 255, 0x35, 0xFF, 0x6A)]
    [InlineData("#80112233", 0x80, 0x11, 0x22, 0x33)]
    [InlineData("#abc", 255, 0xAA, 0xBB, 0xCC)]
    [InlineData("  #FFF  ", 255, 0xFF, 0xFF, 0xFF)]
    public void TryParse_AcceptsSupportedForms(string input, int a, int r, int g, int b)
    {
        Assert.True(HexColor.TryParse(input, out var color));
        Assert.Equal((byte)a, color.A);
        Assert.Equal((byte)r, color.R);
        Assert.Equal((byte)g, color.G);
        Assert.Equal((byte)b, color.B);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    [InlineData("red")]
    public void TryParse_RejectsUnsupportedInput(string? input)
    {
        Assert.False(HexColor.TryParse(input, out _));
    }

    [Fact]
    public void ParseOrDefault_FallsBackOnInvalidInput()
    {
        var fallback = new HexColor(255, 1, 2, 3);

        Assert.Equal(fallback, HexColor.ParseOrDefault("nonsense", fallback));
    }

    [Fact]
    public void ToHex_OmitsAlphaWhenOpaque()
    {
        Assert.Equal("#35FF6A", new HexColor(255, 0x35, 0xFF, 0x6A).ToHex());
    }

    [Fact]
    public void ToHex_IncludesAlphaWhenTranslucent()
    {
        Assert.Equal("#8035FF6A", new HexColor(0x80, 0x35, 0xFF, 0x6A).ToHex());
    }

    [Fact]
    public void Luminance_SeparatesLightFromDark()
    {
        Assert.True(new HexColor(255, 255, 255, 255).Luminance > 0.9);
        Assert.True(new HexColor(255, 0, 0, 0).Luminance < 0.1);
    }
}
