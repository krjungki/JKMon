namespace JKMon.Core.Presentation;

/// <summary>
/// Decides whether the overlay should get out of the way of the pointer. The overlay is click-through, so it never
/// receives mouse messages and the caller has to supply the cursor position it polled.
/// </summary>
public static class HoverGate
{
    public static bool Contains(PlacementMath.Rect bounds, int x, int y) =>
        bounds.Width > 0 && bounds.Height > 0 &&
        x >= bounds.Left && x < bounds.Right &&
        y >= bounds.Top && y < bounds.Bottom;

    /// <summary>
    /// <paramref name="bounds"/> stays at its full size while concealed, so the pointer has to actually leave the
    /// overlay before it comes back. Collapsing it instead would shrink the window and flip the answer immediately.
    /// </summary>
    public static bool ShouldConceal(bool enabled, PlacementMath.Rect bounds, int cursorX, int cursorY) =>
        enabled && Contains(bounds, cursorX, cursorY);
}
