using JKMon.Core.Presentation;
using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

public class ActivityLevelTests
{
    private const double First = 1024 * 1024;
    private const double Second = 10d * 1024 * 1024;

    [Fact]
    public void NothingMovingIsIdle()
    {
        Assert.Equal(ActivityLevel.Idle, ActivityLevelMath.Of(0, First, Second));
    }

    /// <summary>Any traffic at all has to leave Idle, which is what tells "quiet" from "busy" at a glance.</summary>
    [Fact]
    public void OneByteIsAlreadyNormal()
    {
        Assert.Equal(ActivityLevel.Normal, ActivityLevelMath.Of(1, First, Second));
    }

    [Theory]
    [InlineData(1024, ActivityLevel.Normal)]
    [InlineData(First - 1, ActivityLevel.Normal)]
    [InlineData(First, ActivityLevel.Elevated)]
    [InlineData(Second - 1, ActivityLevel.Elevated)]
    [InlineData(Second, ActivityLevel.High)]
    [InlineData(Second * 100, ActivityLevel.High)]
    public void StepsUpAtEachThreshold(double rate, ActivityLevel expected)
    {
        Assert.Equal(expected, ActivityLevelMath.Of(rate, First, Second));
    }

    [Fact]
    public void NegativeAndNonFiniteRatesAreIdle()
    {
        Assert.Equal(ActivityLevel.Idle, ActivityLevelMath.Of(-5, First, Second));
        Assert.Equal(ActivityLevel.Idle, ActivityLevelMath.Of(double.NaN, First, Second));
    }

    [Fact]
    public void ThresholdsGivenInTheWrongOrderStillStepUpward()
    {
        Assert.Equal(ActivityLevel.Elevated, ActivityLevelMath.Of(First, Second, First));
        Assert.Equal(ActivityLevel.High, ActivityLevelMath.Of(Second, Second, First));
    }

    [Fact]
    public void SettingsExposeThresholdsInBytes()
    {
        var thresholds = new JkMonSettings
        {
            NetworkFirstThresholdKib = 2,
            NetworkSecondThresholdKib = 4,
            DiskFirstThresholdKib = 8,
            DiskSecondThresholdKib = 16
        }.Normalized().ActivityThresholds;

        Assert.Equal(2 * 1024, thresholds.NetworkFirst);
        Assert.Equal(4 * 1024, thresholds.NetworkSecond);
        Assert.Equal(8 * 1024, thresholds.DiskFirst);
        Assert.Equal(16 * 1024, thresholds.DiskSecond);
    }

    [Fact]
    public void SettingsClampAbsurdThresholds()
    {
        var settings = new JkMonSettings
        {
            NetworkFirstThresholdKib = -100,
            DiskSecondThresholdKib = double.NaN
        }.Normalized();

        Assert.Equal(JkMonSettings.MinActivityThresholdKib, settings.NetworkFirstThresholdKib);
        Assert.Equal(50 * 1024, settings.DiskSecondThresholdKib);
    }

    [Fact]
    public void BarsAreOnByDefault()
    {
        Assert.True(new JkMonSettings().Normalized().ShowActivityBars);
    }
}
