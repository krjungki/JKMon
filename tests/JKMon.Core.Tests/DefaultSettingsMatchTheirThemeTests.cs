using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

/// <summary>
/// A first run has no file to read, so the record's own defaults are what the user sees. They have to agree with
/// the theme the same defaults claim, or the app opens showing one theme while the settings say another.
/// </summary>
public class DefaultSettingsMatchTheirThemeTests
{
    [Fact]
    public void DefaultsAreTheThemeTheyClaim()
    {
        var defaults = new JkMonSettings().Normalized();
        var applied = ThemeCatalog.Apply(defaults, defaults.Theme).Normalized();

        Assert.Equal(applied.TextColor, defaults.TextColor);
        Assert.Equal(applied.BackgroundColor, defaults.BackgroundColor);
        Assert.Equal(applied.CustomTextColor, defaults.CustomTextColor);
        Assert.Equal(applied.GaugeOutlineColor, defaults.GaugeOutlineColor);
        Assert.Equal(applied.CpuGaugeColor, defaults.CpuGaugeColor);
        Assert.Equal(applied.MemoryGaugeColor, defaults.MemoryGaugeColor);
        Assert.Equal(applied.ActivityIdleColor, defaults.ActivityIdleColor);
        Assert.Equal(applied.ActivityNormalColor, defaults.ActivityNormalColor);
        Assert.Equal(applied.ActivityElevatedColor, defaults.ActivityElevatedColor);
        Assert.Equal(applied.ActivityHighColor, defaults.ActivityHighColor);
        Assert.Equal(applied.FontFamily, defaults.FontFamily);
        Assert.Equal(applied.CustomTextFontFamily, defaults.CustomTextFontFamily);
        Assert.Equal(applied.BackgroundOpacityPercent, defaults.BackgroundOpacityPercent);
        Assert.Equal(applied.GaugeOutlineThickness, defaults.GaugeOutlineThickness);
        Assert.Equal(applied.TextShadow, defaults.TextShadow);
        Assert.Equal(applied.CustomTextShadow, defaults.CustomTextShadow);
    }
}
