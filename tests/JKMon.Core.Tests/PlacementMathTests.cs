using JKMon.Core.Presentation;

namespace JKMon.Core.Tests;

public class PlacementMathTests
{
    private static readonly PlacementMath.Rect Primary = new(0, 0, 2752, 1104);
    private static readonly PlacementMath.Rect Secondary = new(3440, 0, 5360, 1032);

    [Fact]
    public void AnchorsToBottomRightOfThePrimaryWorkArea()
    {
        var (x, y) = PlacementMath.BottomRight(Primary, 750, 65, 8);

        Assert.Equal(2752 - 750 - 8, x);
        Assert.Equal(1104 - 65 - 8, y);
    }

    [Fact]
    public void AnchorsInsideASecondaryMonitorWithANonZeroOrigin()
    {
        var (x, y) = PlacementMath.BottomRight(Secondary, 400, 60, 8);

        Assert.Equal(5360 - 400 - 8, x);
        Assert.Equal(1032 - 60 - 8, y);
        Assert.True(x >= Secondary.Left);
    }

    [Fact]
    public void NeverLeavesTheWorkAreaWhenTheWindowIsWiderThanTheScreen()
    {
        var (x, y) = PlacementMath.BottomRight(Primary, 5000, 4000, 8);

        Assert.Equal(Primary.Left, x);
        Assert.Equal(Primary.Top, y);
    }

    [Fact]
    public void TreatsNegativeMarginAsZero()
    {
        var (x, y) = PlacementMath.BottomRight(Primary, 400, 60, -50);

        Assert.Equal(2752 - 400, x);
        Assert.Equal(1104 - 60, y);
    }

    [Fact]
    public void ZeroMarginTouchesTheWorkAreaEdge()
    {
        var (x, y) = PlacementMath.BottomRight(Primary, 400, 60, 0);

        Assert.Equal(Primary.Right - 400, x);
        Assert.Equal(Primary.Bottom - 60, y);
    }

    [Fact]
    public void BottomLeftAnchorsAgainstTheLeftEdge()
    {
        var (x, y) = PlacementMath.Bottom(Primary, 600, 60, 8, OverlayPosition.BottomLeft);

        Assert.Equal(8, x);
        Assert.Equal(1104 - 60 - 8, y);
    }

    [Fact]
    public void BottomLeftRespectsASecondaryMonitorOrigin()
    {
        var (x, _) = PlacementMath.Bottom(Secondary, 600, 60, 8, OverlayPosition.BottomLeft);

        Assert.Equal(Secondary.Left + 8, x);
    }

    [Fact]
    public void BottomCentreLeavesEqualSpaceOnBothSides()
    {
        const int width = 600;
        var (x, _) = PlacementMath.Bottom(Primary, width, 60, 8, OverlayPosition.BottomCenter);

        Assert.Equal(x - Primary.Left, Primary.Right - (x + width));
    }

    [Fact]
    public void BottomCentreRespectsASecondaryMonitorOrigin()
    {
        const int width = 400;
        var (x, _) = PlacementMath.Bottom(Secondary, width, 60, 8, OverlayPosition.BottomCenter);

        Assert.Equal(Secondary.Left + ((Secondary.Width - width) / 2), x);
        Assert.True(x >= Secondary.Left);
    }

    [Fact]
    public void BottomCentreIgnoresTheHorizontalMargin()
    {
        var withMargin = PlacementMath.Bottom(Primary, 600, 60, 60, OverlayPosition.BottomCenter);
        var withoutMargin = PlacementMath.Bottom(Primary, 600, 60, 0, OverlayPosition.BottomCenter);

        Assert.Equal(withoutMargin.X, withMargin.X);
    }

    [Theory]
    [InlineData(OverlayPosition.BottomLeft)]
    [InlineData(OverlayPosition.BottomCenter)]
    [InlineData(OverlayPosition.BottomRight)]
    public void EveryPositionKeepsTheOriginInsideTheWorkAreaWhenOversized(OverlayPosition position)
    {
        var (x, y) = PlacementMath.Bottom(Primary, 5000, 4000, 8, position);

        // An oversized window has to overflow somewhere; the origin must still be on screen.
        Assert.InRange(x, Primary.Left, Primary.Right);
        Assert.Equal(Primary.Top, y);
    }

    [Fact]
    public void BottomRightOverloadMatchesTheExplicitPosition()
    {
        var overload = PlacementMath.BottomRight(Primary, 600, 60, 8);
        var explicitCall = PlacementMath.Bottom(Primary, 600, 60, 8, OverlayPosition.BottomRight);

        Assert.Equal(explicitCall, overload);
    }
}
