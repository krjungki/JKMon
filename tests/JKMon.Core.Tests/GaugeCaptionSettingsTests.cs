using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

public class GaugeCaptionSettingsTests
{
    [Fact]
    public void DefaultsToAVisibleCaption()
    {
        Assert.Equal(9, new JkMonSettings().Normalized().GaugeCaptionFontSize);
    }

    [Fact]
    public void ZeroSurvivesNormalisationBecauseItMeansHidden()
    {
        Assert.Equal(0, new JkMonSettings { GaugeCaptionFontSize = 0 }.Normalized().GaugeCaptionFontSize);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(64, 32)]
    public void ClampsToTheSupportedRange(double requested, double expected)
    {
        Assert.Equal(expected, new JkMonSettings { GaugeCaptionFontSize = requested }.Normalized().GaugeCaptionFontSize);
    }

    [Fact]
    public void FallsBackToTheDefaultWhenNotFinite()
    {
        Assert.Equal(9, new JkMonSettings { GaugeCaptionFontSize = double.NaN }.Normalized().GaugeCaptionFontSize);
    }

    [Fact]
    public void IsIndependentOfTheBarAndPieLabelSize()
    {
        var settings = new JkMonSettings { GaugeCaptionFontSize = 14, GaugeLabelFontSize = 7 }.Normalized();

        Assert.Equal(14, settings.GaugeCaptionFontSize);
        Assert.Equal(7, settings.GaugeLabelFontSize);
    }
}
