using JKMon.Core.Presentation;
using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

public class ThemeCatalogTests
{
    private static JkMonSettings Customised() => new JkMonSettings
    {
        CustomText = "workstation",
        CustomTextAlignment = CaptionAlignment.Left,
        Position = OverlayPosition.BottomLeft,
        Layer = WindowLayer.AlwaysOnTop,
        MonitorDeviceName = @"\\.\DISPLAY2",
        MarginPixels = 40,
        RefreshSeconds = 5,
        FontSize = 20,
        CustomTextFontSize = 28,
        CircleDiameter = 40,
        GaugeLabelFontSize = 12,
        GaugeCaptionFontSize = 11,
        CpuGauge = CpuGaugeStyle.Bar,
        MemoryGauge = MemoryGaugeStyle.Pie,
        ShowIndividualCores = true,
        ShowActivityBars = false,
        NetworkFirstThresholdKib = 2048,
        DiskSecondThresholdKib = 4096,
        HideWhenPointerOver = true,
        PauseWhenFullscreen = false,
        StartWithWindows = true,
        AccentStripe = AccentStripeMode.Tricolour,
        AccentStripeFirstColor = "#111111",
        AccentStripeSecondColor = "#222222",
        AccentStripeThirdColor = "#333333"
    }.Normalized();

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void KeepsEveryChoiceTheUserMade(AppTheme theme)
    {
        var before = Customised();

        var after = ThemeCatalog.Apply(before, theme).Normalized();

        Assert.Equal(before.CustomText, after.CustomText);
        Assert.Equal(before.CustomTextAlignment, after.CustomTextAlignment);
        Assert.Equal(before.Position, after.Position);
        Assert.Equal(before.Layer, after.Layer);
        Assert.Equal(before.MonitorDeviceName, after.MonitorDeviceName);
        Assert.Equal(before.MarginPixels, after.MarginPixels);
        Assert.Equal(before.RefreshSeconds, after.RefreshSeconds);
        Assert.Equal(before.FontSize, after.FontSize);
        Assert.Equal(before.CustomTextFontSize, after.CustomTextFontSize);
        Assert.Equal(before.CircleDiameter, after.CircleDiameter);
        Assert.Equal(before.GaugeLabelFontSize, after.GaugeLabelFontSize);
        Assert.Equal(before.GaugeCaptionFontSize, after.GaugeCaptionFontSize);
        Assert.Equal(before.CpuGauge, after.CpuGauge);
        Assert.Equal(before.MemoryGauge, after.MemoryGauge);
        Assert.Equal(before.ShowIndividualCores, after.ShowIndividualCores);
        Assert.Equal(before.ShowActivityBars, after.ShowActivityBars);
        Assert.Equal(before.NetworkFirstThresholdKib, after.NetworkFirstThresholdKib);
        Assert.Equal(before.DiskSecondThresholdKib, after.DiskSecondThresholdKib);
        Assert.Equal(before.HideWhenPointerOver, after.HideWhenPointerOver);
        Assert.Equal(before.PauseWhenFullscreen, after.PauseWhenFullscreen);
        Assert.Equal(before.StartWithWindows, after.StartWithWindows);
        Assert.Equal(before.ProviderOrder, after.ProviderOrder);
    }

    /// <summary>The stripe is a user decision, not part of the palette a theme owns.</summary>
    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void KeepsTheAccentStripe(AppTheme theme)
    {
        var after = ThemeCatalog.Apply(Customised(), theme).Normalized();

        Assert.Equal(AccentStripeMode.Tricolour, after.AccentStripe);
        Assert.Equal("#111111", after.AccentStripeFirstColor);
        Assert.Equal("#222222", after.AccentStripeSecondColor);
        Assert.Equal("#333333", after.AccentStripeThirdColor);
    }

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void ReplacesEveryColourAndTypeface(AppTheme theme)
    {
        var before = Customised() with
        {
            TextColor = "#ABCDEF",
            BackgroundColor = "#123456",
            CustomTextColor = "#FEDCBA",
            GaugeOutlineColor = "#0F0F0F",
            CpuGaugeColor = "#101010",
            MemoryGaugeColor = "#202020",
            ActivityIdleColor = "#303030",
            ActivityNormalColor = "#404040",
            ActivityElevatedColor = "#505050",
            ActivityHighColor = "#606060",
            FontFamily = "Comic Sans MS",
            CustomTextFontFamily = "Comic Sans MS"
        };

        var after = ThemeCatalog.Apply(before, theme).Normalized();

        Assert.NotEqual(before.TextColor, after.TextColor);
        Assert.NotEqual(before.BackgroundColor, after.BackgroundColor);
        Assert.NotEqual(before.CustomTextColor, after.CustomTextColor);
        Assert.NotEqual(before.GaugeOutlineColor, after.GaugeOutlineColor);
        Assert.NotEqual(before.CpuGaugeColor, after.CpuGaugeColor);
        Assert.NotEqual(before.MemoryGaugeColor, after.MemoryGaugeColor);
        Assert.NotEqual(before.ActivityIdleColor, after.ActivityIdleColor);
        Assert.NotEqual(before.ActivityNormalColor, after.ActivityNormalColor);
        Assert.NotEqual(before.ActivityElevatedColor, after.ActivityElevatedColor);
        Assert.NotEqual(before.ActivityHighColor, after.ActivityHighColor);
        Assert.NotEqual(before.FontFamily, after.FontFamily);
        Assert.NotEqual(before.CustomTextFontFamily, after.CustomTextFontFamily);
        Assert.Equal(theme, after.Theme);
    }

    [Fact]
    public void TheTwoThemesDisagreeOnEveryColour()
    {
        var light = ThemeCatalog.Apply(new JkMonSettings(), AppTheme.Light);
        var dark = ThemeCatalog.Apply(new JkMonSettings(), AppTheme.Dark);

        Assert.NotEqual(light.TextColor, dark.TextColor);
        Assert.NotEqual(light.BackgroundColor, dark.BackgroundColor);
        Assert.NotEqual(light.CpuGaugeColor, dark.CpuGaugeColor);
        Assert.NotEqual(light.FontFamily, dark.FontFamily);
    }

    /// <summary>Every value a theme writes has to survive the same validation the file on disk goes through.</summary>
    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void WritesValuesThatNormalisationAccepts(AppTheme theme)
    {
        var applied = ThemeCatalog.Apply(new JkMonSettings(), theme);
        var normalised = applied.Normalized();

        Assert.Equal(applied.TextColor, normalised.TextColor);
        Assert.Equal(applied.BackgroundColor, normalised.BackgroundColor);
        Assert.Equal(applied.CustomTextColor, normalised.CustomTextColor);
        Assert.Equal(applied.GaugeOutlineColor, normalised.GaugeOutlineColor);
        Assert.Equal(applied.CpuGaugeColor, normalised.CpuGaugeColor);
        Assert.Equal(applied.MemoryGaugeColor, normalised.MemoryGaugeColor);
        Assert.Equal(applied.ActivityIdleColor, normalised.ActivityIdleColor);
        Assert.Equal(applied.ActivityNormalColor, normalised.ActivityNormalColor);
        Assert.Equal(applied.ActivityElevatedColor, normalised.ActivityElevatedColor);
        Assert.Equal(applied.ActivityHighColor, normalised.ActivityHighColor);
        Assert.Equal(applied.FontFamily, normalised.FontFamily);
        Assert.Equal(applied.CustomTextFontFamily, normalised.CustomTextFontFamily);
        Assert.Equal(applied.BackgroundOpacityPercent, normalised.BackgroundOpacityPercent);
        Assert.Equal(applied.GaugeOutlineThickness, normalised.GaugeOutlineThickness);
        Assert.Equal(applied.Theme, normalised.Theme);
    }

    [Theory]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    public void PublishesReadableTokens(AppTheme theme)
    {
        var chrome = ThemeCatalog.ChromeFor(theme);
        var overlay = ThemeCatalog.OverlayFor(theme);

        foreach (var token in (string[])
                 [
                     chrome.Surface, chrome.SecondarySurface, chrome.Field, chrome.Hairline,
                     chrome.Ink, chrome.Muted, chrome.Accent,
                     overlay.Track, overlay.StatusOk, overlay.StatusBusy, overlay.StatusUnknown
                 ])
        {
            Assert.True(HexColor.TryParse(token, out _), $"{token} is not a colour");
        }

        Assert.False(string.IsNullOrWhiteSpace(chrome.DisplayFont));
        Assert.False(string.IsNullOrWhiteSpace(chrome.BodyFont));
    }

    [Fact]
    public void DefaultsToTheStripeBeingOff()
    {
        Assert.Equal(AccentStripeMode.None, new JkMonSettings().Normalized().AccentStripe);
    }
}
