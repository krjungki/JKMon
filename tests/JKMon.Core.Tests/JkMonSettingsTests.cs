using JKMon.Core.Presentation;
using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

public class JkMonSettingsTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(11, 10)]
    [InlineData(600, 10)]
    public void Normalized_ClampsRefreshToSupportedRange(int input, int expected)
    {
        var settings = new JkMonSettings { RefreshSeconds = input }.Normalized();

        Assert.Equal(expected, settings.RefreshSeconds);
    }

    [Fact]
    public void Normalized_KeepsProviderPollAtOrAboveTheMinimum()
    {
        var settings = new JkMonSettings { ProviderPollSeconds = 1 }.Normalized();

        Assert.Equal(JkMonSettings.MinProviderPollSeconds, settings.ProviderPollSeconds);
    }

    [Fact]
    public void Normalized_RepairsUndefinedLayer()
    {
        var settings = new JkMonSettings { Layer = (WindowLayer)99 }.Normalized();

        Assert.Equal(WindowLayer.Desktop, settings.Layer);
    }

    [Fact]
    public void Normalized_RepairsUndefinedPosition()
    {
        var settings = new JkMonSettings { Position = (OverlayPosition)42 }.Normalized();

        Assert.Equal(OverlayPosition.BottomRight, settings.Position);
    }

    [Theory]
    [InlineData(OverlayPosition.BottomLeft)]
    [InlineData(OverlayPosition.BottomCenter)]
    [InlineData(OverlayPosition.BottomRight)]
    public void Normalized_KeepsSupportedPositions(OverlayPosition position)
    {
        Assert.Equal(position, new JkMonSettings { Position = position }.Normalized().Position);
    }

    [Fact]
    public void MonitorDeviceName_DefaultsToAutomatic()
    {
        Assert.Equal(string.Empty, new JkMonSettings().Normalized().MonitorDeviceName);
    }

    [Fact]
    public void Normalized_DefaultsBothGaugesToNumbers()
    {
        var settings = new JkMonSettings().Normalized();

        Assert.Equal(CpuGaugeStyle.Number, settings.CpuGauge);
        Assert.Equal(MemoryGaugeStyle.Number, settings.MemoryGauge);
        Assert.False(settings.ShowIndividualCores);
    }

    [Theory]
    [InlineData(CpuGaugeStyle.Number)]
    [InlineData(CpuGaugeStyle.Bar)]
    public void Normalized_KeepsSupportedCpuGauges(CpuGaugeStyle style)
    {
        Assert.Equal(style, new JkMonSettings { CpuGauge = style }.Normalized().CpuGauge);
    }

    [Theory]
    [InlineData(MemoryGaugeStyle.Number)]
    [InlineData(MemoryGaugeStyle.Bar)]
    [InlineData(MemoryGaugeStyle.Pie)]
    public void Normalized_KeepsSupportedMemoryGauges(MemoryGaugeStyle style)
    {
        Assert.Equal(style, new JkMonSettings { MemoryGauge = style }.Normalized().MemoryGauge);
    }

    [Fact]
    public void Normalized_RejectsUndefinedGaugeStyles()
    {
        var settings = new JkMonSettings
        {
            CpuGauge = (CpuGaugeStyle)42,
            MemoryGauge = (MemoryGaugeStyle)42
        }.Normalized();

        Assert.Equal(CpuGaugeStyle.Number, settings.CpuGauge);
        Assert.Equal(MemoryGaugeStyle.Number, settings.MemoryGauge);
    }

    [Fact]
    public void Normalized_FallsBackToTheDefaultGaugeColours()
    {
        var settings = new JkMonSettings
        {
            GaugeOutlineColor = "nonsense",
            CpuGaugeColor = "#GGGGGG",
            MemoryGaugeColor = string.Empty
        }.Normalized();

        Assert.Equal(JkMonSettings.DefaultGaugeOutlineColor, settings.GaugeOutlineColor);
        Assert.Equal(JkMonSettings.DefaultCpuGaugeColor, settings.CpuGaugeColor);
        Assert.Equal(JkMonSettings.DefaultMemoryGaugeColor, settings.MemoryGaugeColor);
    }

    [Fact]
    public void Normalized_KeepsTheOutlineAndCpuColoursIndependent()
    {
        var settings = new JkMonSettings
        {
            GaugeOutlineColor = "#112233",
            CpuGaugeColor = "#445566"
        }.Normalized();

        Assert.Equal("#112233", settings.GaugeOutlineColor);
        Assert.Equal("#445566", settings.CpuGaugeColor);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1.5, 1.5)]
    [InlineData(6, 6)]
    [InlineData(-3, 0)]
    [InlineData(99, 6)]
    [InlineData(double.NaN, 2)]
    public void Normalized_ClampsTheOutlineThickness(double input, double expected)
    {
        var settings = new JkMonSettings { GaugeOutlineThickness = input }.Normalized();

        Assert.Equal(expected, settings.GaugeOutlineThickness);
    }

    [Theory]
    [InlineData(6, 6)]
    [InlineData(14, 14)]
    [InlineData(32, 32)]
    [InlineData(0, 6)]
    [InlineData(400, 32)]
    [InlineData(double.PositiveInfinity, 9)]
    public void Normalized_ClampsTheGaugeLabelSize(double input, double expected)
    {
        var settings = new JkMonSettings { GaugeLabelFontSize = input }.Normalized();

        Assert.Equal(expected, settings.GaugeLabelFontSize);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalized_TreatsBlankMonitorAsAutomatic(string? input)
    {
        Assert.Equal(string.Empty, new JkMonSettings { MonitorDeviceName = input! }.Normalized().MonitorDeviceName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalized_TreatsBlankCustomTextAsHidden(string? input)
    {
        var settings = new JkMonSettings { CustomText = input! }.Normalized();

        Assert.Equal(string.Empty, settings.CustomText);
        Assert.False(settings.HasCustomText);
    }

    [Fact]
    public void Normalized_KeepsTheCustomTextOnOneLine()
    {
        var settings = new JkMonSettings { CustomText = "  work\r\nstation\t1  " }.Normalized();

        Assert.Equal("work  station 1", settings.CustomText);
        Assert.True(settings.HasCustomText);
    }

    [Fact]
    public void Normalized_TruncatesAnOverlongCustomText()
    {
        var settings = new JkMonSettings { CustomText = new string('x', 200) }.Normalized();

        Assert.Equal(JkMonSettings.MaxCustomTextLength, settings.CustomText.Length);
    }

    [Theory]
    [InlineData(9, 9)]
    [InlineData(72, 72)]
    [InlineData(0, 9)]
    [InlineData(500, 72)]
    [InlineData(double.NaN, 16)]
    public void Normalized_ClampsTheCustomTextSize(double input, double expected)
    {
        Assert.Equal(expected, new JkMonSettings { CustomTextFontSize = input }.Normalized().CustomTextFontSize);
    }

    [Fact]
    public void Normalized_RepairsTheCustomTextFontAndColour()
    {
        var settings = new JkMonSettings
        {
            CustomTextFontFamily = "   ",
            CustomTextColor = "nonsense"
        }.Normalized();

        Assert.Equal(JkMonSettings.DefaultFontFamily, settings.CustomTextFontFamily);
        Assert.Equal(JkMonSettings.DefaultCustomTextColor, settings.CustomTextColor);
    }

    [Theory]
    [InlineData(CaptionAlignment.Left)]
    [InlineData(CaptionAlignment.Center)]
    [InlineData(CaptionAlignment.Right)]
    public void Normalized_KeepsSupportedCaptionAlignments(CaptionAlignment alignment)
    {
        var settings = new JkMonSettings { CustomTextAlignment = alignment }.Normalized();

        Assert.Equal(alignment, settings.CustomTextAlignment);
    }

    [Fact]
    public void Normalized_CentresAnUndefinedCaptionAlignment()
    {
        var settings = new JkMonSettings { CustomTextAlignment = (CaptionAlignment)77 }.Normalized();

        Assert.Equal(CaptionAlignment.Center, settings.CustomTextAlignment);
    }

    [Fact]
    public void Normalized_TrimsTheMonitorDeviceName()
    {
        var settings = new JkMonSettings { MonitorDeviceName = "  \\\\.\\DISPLAY2  " }.Normalized();

        Assert.Equal(@"\\.\DISPLAY2", settings.MonitorDeviceName);
    }

    [Fact]
    public void Defaults_MatchThePlan()
    {
        var settings = new JkMonSettings();

        Assert.Equal(2, settings.RefreshSeconds);
        Assert.Equal(3, settings.ProviderPollSeconds);
        Assert.Equal(WindowLayer.Desktop, settings.Layer);
        Assert.False(settings.StartWithWindows);
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(45, 45)]
    [InlineData(100, 100)]
    [InlineData(140, 100)]
    public void Normalized_ClampsBackgroundOpacity(int input, int expected)
    {
        Assert.Equal(expected, new JkMonSettings { BackgroundOpacityPercent = input }.Normalized().BackgroundOpacityPercent);
    }

    [Theory]
    [InlineData(4, JkMonSettings.MinFontSize)]
    [InlineData(64, JkMonSettings.MaxFontSize)]
    [InlineData(15, 15d)]
    public void Normalized_ClampsFontSize(double input, double expected)
    {
        Assert.Equal(expected, new JkMonSettings { FontSize = input }.Normalized().FontSize);
    }

    [Fact]
    public void Normalized_RepairsNonFiniteFontSize()
    {
        Assert.Equal(13d, new JkMonSettings { FontSize = double.NaN }.Normalized().FontSize);
    }

    [Theory]
    [InlineData(2, JkMonSettings.MinCircleDiameter)]
    [InlineData(120, JkMonSettings.MaxCircleDiameter)]
    [InlineData(30, 30)]
    public void Normalized_ClampsCircleDiameter(int input, int expected)
    {
        Assert.Equal(expected, new JkMonSettings { CircleDiameter = input }.Normalized().CircleDiameter);
    }

    [Fact]
    public void Normalized_ReplacesUnparsableColorsWithDefaults()
    {
        var settings = new JkMonSettings { TextColor = "not-a-color", BackgroundColor = "#GG0000" }.Normalized();

        Assert.Equal(JkMonSettings.DefaultTextColor, settings.TextColor);
        Assert.Equal(JkMonSettings.DefaultBackgroundColor, settings.BackgroundColor);
    }

    [Fact]
    public void Normalized_CanonicalisesValidColors()
    {
        var settings = new JkMonSettings { TextColor = "abc" }.Normalized();

        Assert.Equal("#AABBCC", settings.TextColor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalized_RestoresFontFamilyWhenBlank(string? input)
    {
        Assert.Equal(JkMonSettings.DefaultFontFamily, (new JkMonSettings { FontFamily = input! }).Normalized().FontFamily);
    }

    [Fact]
    public void Normalized_ReplacesUnparsableActivityColorsWithDefaults()
    {
        var settings = new JkMonSettings
        {
            ActivityIdleColor = "nope",
            ActivityNormalColor = "#12345",
            ActivityElevatedColor = "",
            ActivityHighColor = "#GGHHII"
        }.Normalized();

        Assert.Equal(JkMonSettings.DefaultActivityIdleColor, settings.ActivityIdleColor);
        Assert.Equal(JkMonSettings.DefaultActivityNormalColor, settings.ActivityNormalColor);
        Assert.Equal(JkMonSettings.DefaultActivityElevatedColor, settings.ActivityElevatedColor);
        Assert.Equal(JkMonSettings.DefaultActivityHighColor, settings.ActivityHighColor);
    }

    [Fact]
    public void Normalized_CanonicalisesValidActivityColors()
    {
        var settings = new JkMonSettings
        {
            ActivityNormalColor = "f00",
            ActivityHighColor = "#ff8a5b"
        }.Normalized();

        Assert.Equal("#FF0000", settings.ActivityNormalColor);
        Assert.Equal("#FF8A5B", settings.ActivityHighColor);
    }

    /// <summary>The four steps have to be told apart at a glance, so they must not ship as similar colours.</summary>
    [Fact]
    public void ActivityColorDefaults_AreDistinct()
    {
        string[] defaults =
        [
            JkMonSettings.DefaultActivityIdleColor,
            JkMonSettings.DefaultActivityNormalColor,
            JkMonSettings.DefaultActivityElevatedColor,
            JkMonSettings.DefaultActivityHighColor
        ];

        Assert.Equal(defaults.Length, defaults.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
