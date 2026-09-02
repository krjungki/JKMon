using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

public class CaptionShadowSettingsTests
{
    [Fact]
    public void DefaultsToOffBecauseNeitherThemeShadowsText()
    {
        Assert.False(new JkMonSettings().Normalized().CustomTextShadow);
    }

    [Fact]
    public void CanBeTurnedOn()
    {
        Assert.True(new JkMonSettings { CustomTextShadow = true }.Normalized().CustomTextShadow);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IsIndependentOfThePanelTextShadow(bool panel, bool caption)
    {
        var settings = new JkMonSettings { TextShadow = panel, CustomTextShadow = caption }.Normalized();

        Assert.Equal(panel, settings.TextShadow);
        Assert.Equal(caption, settings.CustomTextShadow);
    }
}
