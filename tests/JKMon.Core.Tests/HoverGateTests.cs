using JKMon.Core.Presentation;
using JKMon.Core.Settings;

namespace JKMon.Core.Tests;

public class HoverGateTests
{
    private static readonly PlacementMath.Rect Overlay = new(100, 200, 400, 260);

    [Theory]
    [InlineData(100, 200)]
    [InlineData(399, 259)]
    [InlineData(250, 230)]
    public void Contains_AcceptsPointsInsideIncludingTheTopLeftEdge(int x, int y)
    {
        Assert.True(HoverGate.Contains(Overlay, x, y));
    }

    [Theory]
    [InlineData(99, 230)]
    [InlineData(400, 230)]
    [InlineData(250, 199)]
    [InlineData(250, 260)]
    public void Contains_RejectsPointsOutsideIncludingTheBottomRightEdge(int x, int y)
    {
        Assert.False(HoverGate.Contains(Overlay, x, y));
    }

    [Fact]
    public void Contains_RejectsEverythingWhenTheWindowHasNoSize()
    {
        Assert.False(HoverGate.Contains(default, 0, 0));
        Assert.False(HoverGate.Contains(new PlacementMath.Rect(10, 10, 10, 40), 10, 20));
    }

    [Fact]
    public void ShouldConceal_OnlyWhenTheOptionIsOn()
    {
        Assert.True(HoverGate.ShouldConceal(enabled: true, Overlay, 250, 230));
        Assert.False(HoverGate.ShouldConceal(enabled: false, Overlay, 250, 230));
    }

    [Fact]
    public void ShouldConceal_StaysOffWhilePointerIsElsewhere()
    {
        Assert.False(HoverGate.ShouldConceal(enabled: true, Overlay, 800, 600));
    }

    [Fact]
    public void Settings_DefaultToNotHiding()
    {
        Assert.False(new JkMonSettings().Normalized().HideWhenPointerOver);
    }

    [Fact]
    public void Settings_RoundTripTheOption()
    {
        Assert.True(new JkMonSettings { HideWhenPointerOver = true }.Normalized().HideWhenPointerOver);
    }
}
