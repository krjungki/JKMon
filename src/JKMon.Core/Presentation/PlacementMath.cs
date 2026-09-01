namespace JKMon.Core.Presentation;

public enum OverlayPosition
{
    BottomRight,
    BottomCenter,
    BottomLeft
}

/// <summary>
/// Pure placement arithmetic in physical pixels. Keeping this out of the UI layer avoids the mixed-DPI pitfalls
/// of positioning a window through device independent coordinates.
/// </summary>
public static class PlacementMath
{
    public readonly record struct Rect(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    /// <summary>Anchors the window along the bottom of the work area, clamped so it can never leave it.</summary>
    public static (int X, int Y) Bottom(
        Rect workArea, int windowWidth, int windowHeight, int margin, OverlayPosition position)
    {
        var safeMargin = margin < 0 ? 0 : margin;

        var x = position switch
        {
            OverlayPosition.BottomLeft => workArea.Left + safeMargin,
            // Integer division keeps the window on a whole pixel, which avoids blurry text.
            OverlayPosition.BottomCenter => workArea.Left + ((workArea.Width - windowWidth) / 2),
            _ => workArea.Right - windowWidth - safeMargin
        };

        var y = workArea.Bottom - windowHeight - safeMargin;

        if (x < workArea.Left)
        {
            x = workArea.Left;
        }

        if (y < workArea.Top)
        {
            y = workArea.Top;
        }

        return (x, y);
    }

    public static (int X, int Y) BottomRight(Rect workArea, int windowWidth, int windowHeight, int margin) =>
        Bottom(workArea, windowWidth, windowHeight, margin, OverlayPosition.BottomRight);
}
