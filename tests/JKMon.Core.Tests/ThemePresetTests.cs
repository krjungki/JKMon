using JKMon.Core.Presentation;
using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

public class ThemePresetTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"jkmon-themes-{Guid.NewGuid():N}.json");

    public void Dispose() => File.Delete(_path);

    private static JkMonSettings Look() => new JkMonSettings
    {
        TextColor = "#112233",
        BackgroundColor = "#445566",
        CustomTextColor = "#778899",
        GaugeOutlineColor = "#AABBCC",
        CpuGaugeColor = "#DDEEFF",
        MemoryGaugeColor = "#123456",
        ActivityIdleColor = "#654321",
        ActivityNormalColor = "#0F0F0F",
        ActivityElevatedColor = "#1E1E1E",
        ActivityHighColor = "#2D2D2D",
        BackgroundOpacityPercent = 63,
        GaugeOutlineThickness = 3,
        GaugeLabelFontSize = 14,
        GaugeCaptionFontSize = 13,
        TextShadow = true,
        CustomTextShadow = true,
        FontFamily = "Consolas",
        FontSize = 21,
        CustomTextFontFamily = "Georgia",
        CustomTextFontSize = 30,
        CircleDiameter = 44,
        AccentStripe = AccentStripeMode.Solid,
        AccentStripeFirstColor = "#ABCDEF",
        Theme = AppTheme.Light
    }.Normalized();

    private static JkMonSettings Choices() => new JkMonSettings
    {
        CustomText = "desk",
        Position = OverlayPosition.BottomCenter,
        Layer = WindowLayer.AlwaysOnTop,
        MonitorDeviceName = @"\\.\DISPLAY3",
        MarginPixels = 33,
        RefreshSeconds = 7,
        CpuGauge = CpuGaugeStyle.Bar,
        MemoryGauge = MemoryGaugeStyle.Pie,
        ShowIndividualCores = true,
        ShowActivityBars = false,
        NetworkFirstThresholdKib = 777,
        HideWhenPointerOver = true,
        StartWithWindows = true
    }.Normalized();

    [Fact]
    public void CarriesTheWholeLook()
    {
        var preset = ThemePreset.From(Look(), "studio");

        var applied = preset.ApplyTo(new JkMonSettings());

        Assert.Equal("#112233", applied.TextColor);
        Assert.Equal("#445566", applied.BackgroundColor);
        Assert.Equal(63, applied.BackgroundOpacityPercent);
        Assert.Equal(3, applied.GaugeOutlineThickness);
        Assert.Equal("Consolas", applied.FontFamily);
        Assert.Equal(21, applied.FontSize);
        Assert.Equal("Georgia", applied.CustomTextFontFamily);
        Assert.Equal(44, applied.CircleDiameter);
        Assert.True(applied.TextShadow);
        Assert.Equal(AccentStripeMode.Solid, applied.AccentStripe);
        Assert.Equal("#ABCDEF", applied.AccentStripeFirstColor);
        Assert.Equal(AppTheme.Light, applied.Theme);
    }

    /// <summary>A look must not move the overlay or change what it measures.</summary>
    [Fact]
    public void LeavesEveryOtherChoiceAlone()
    {
        var choices = Choices();

        var applied = ThemePreset.From(Look(), "studio").ApplyTo(choices);

        Assert.Equal(choices.CustomText, applied.CustomText);
        Assert.Equal(choices.Position, applied.Position);
        Assert.Equal(choices.Layer, applied.Layer);
        Assert.Equal(choices.MonitorDeviceName, applied.MonitorDeviceName);
        Assert.Equal(choices.MarginPixels, applied.MarginPixels);
        Assert.Equal(choices.RefreshSeconds, applied.RefreshSeconds);
        Assert.Equal(choices.CpuGauge, applied.CpuGauge);
        Assert.Equal(choices.MemoryGauge, applied.MemoryGauge);
        Assert.Equal(choices.ShowIndividualCores, applied.ShowIndividualCores);
        Assert.Equal(choices.ShowActivityBars, applied.ShowActivityBars);
        Assert.Equal(choices.NetworkFirstThresholdKib, applied.NetworkFirstThresholdKib);
        Assert.Equal(choices.HideWhenPointerOver, applied.HideWhenPointerOver);
        Assert.Equal(choices.StartWithWindows, applied.StartWithWindows);
    }

    [Fact]
    public void SurvivesARoundTripThroughTheFile()
    {
        var store = new ThemePresetStore(_path);

        store.Save(ThemePreset.From(Look(), "studio"));
        var reloaded = store.All().Single();

        Assert.Equal("studio", reloaded.Name);
        Assert.Equal("#112233", reloaded.TextColor);
        Assert.Equal("Consolas", reloaded.FontFamily);
        Assert.Equal(AppTheme.Light, reloaded.Theme);
    }

    [Fact]
    public void SavingTheSameNameReplacesIt()
    {
        var store = new ThemePresetStore(_path);

        store.Save(ThemePreset.From(Look(), "studio"));
        store.Save(ThemePreset.From(Look() with { TextColor = "#FFFFFF" }, "STUDIO"));

        var all = store.All();
        Assert.Single(all);
        Assert.Equal("#FFFFFF", all[0].TextColor);
    }

    [Fact]
    public void KeepsSeveralAndDeletesByName()
    {
        var store = new ThemePresetStore(_path);
        store.Save(ThemePreset.From(Look(), "b night"));
        store.Save(ThemePreset.From(Look(), "a day"));

        Assert.Equal(["a day", "b night"], store.All().Select(p => p.Name));

        store.Delete("a day");
        Assert.Equal(["b night"], store.All().Select(p => p.Name));
    }

    [Theory]
    [InlineData("  spaced  ", "spaced")]
    [InlineData("line\r\nbreak", "line  break")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void CleansNames(string input, string expected) =>
        Assert.Equal(expected, ThemePreset.CleanName(input));

    [Fact]
    public void RefusesToSaveAnEmptyName()
    {
        var store = new ThemePresetStore(_path);

        store.Save(ThemePreset.From(Look(), "   "));

        Assert.Empty(store.All());
    }

    [Fact]
    public void ReturnsNothingWhenTheFileIsUnreadable()
    {
        File.WriteAllText(_path, "{ not json");

        Assert.Empty(new ThemePresetStore(_path).All());
    }
}
