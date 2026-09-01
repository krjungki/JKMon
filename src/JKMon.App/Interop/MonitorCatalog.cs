using System.Runtime.InteropServices;
using JKMon.Core.Presentation;

namespace JKMon.App.Interop;

internal sealed record MonitorEntry(string DeviceName, string Label, PlacementMath.Rect WorkArea);

/// <summary>
/// Lists monitors with their physical work areas. Work areas come from the Win32 monitor APIs in a per-monitor
/// aware process, so a display running at a different scale needs no extra conversion.
/// </summary>
internal static class MonitorCatalog
{
    private const int MonitorDefaultToNearest = 2;
    private const int EffectiveDpi = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X;
        internal int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);

    internal static IReadOnlyList<MonitorEntry> All()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var entries = new List<MonitorEntry>(screens.Length);

        for (var i = 0; i < screens.Length; i++)
        {
            var screen = screens[i];
            var bounds = screen.Bounds;
            var work = screen.WorkingArea;

            var scale = ScalePercent(bounds.Left + (bounds.Width / 2), bounds.Top + (bounds.Height / 2));
            var label = $"{i + 1} · {bounds.Width}×{bounds.Height} · {scale}%{(screen.Primary ? " · primary" : string.Empty)}";

            entries.Add(new MonitorEntry(
                screen.DeviceName,
                label,
                new PlacementMath.Rect(work.Left, work.Top, work.Right, work.Bottom)));
        }

        return entries;
    }

    internal static MonitorEntry? Find(string? deviceName) =>
        string.IsNullOrWhiteSpace(deviceName)
            ? null
            : All().FirstOrDefault(entry => string.Equals(entry.DeviceName, deviceName, StringComparison.Ordinal));

    private static int ScalePercent(int x, int y)
    {
        try
        {
            var monitor = MonitorFromPoint(new Point { X = x, Y = y }, MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out _) == 0)
            {
                return (int)Math.Round(dpiX * 100d / 96d);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Older shells without shcore simply show no scale figure.
        }

        return 100;
    }
}
