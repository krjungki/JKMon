using System.Windows;
using JKMon.Core.Presentation;

namespace JKMon.App.Interop;

/// <summary>Resolves the work area the overlay should sit in, in physical pixels.</summary>
internal static class OverlayPlacement
{
    /// <summary>Falls back to the hosting monitor when no monitor is chosen or the chosen one is disconnected.</summary>
    internal static PlacementMath.Rect WorkAreaFor(Window window, string? preferredDevice)
    {
        var chosen = MonitorCatalog.Find(preferredDevice);
        if (chosen is not null)
        {
            return chosen.WorkArea;
        }

        var screen = System.Windows.Forms.Screen.FromHandle(OverlayWindowInterop.GetHandle(window));
        var area = screen.WorkingArea;
        return new PlacementMath.Rect(area.Left, area.Top, area.Right, area.Bottom);
    }
}
