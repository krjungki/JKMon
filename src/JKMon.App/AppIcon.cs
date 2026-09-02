namespace JKMon.App;

/// <summary>Window icon drawn by the tray icon code, so the taskbar and the notification area show the same mark.</summary>
internal static class AppIcon
{
    private static readonly Color Accent = Color.FromArgb(0x35, 0xFF, 0x6A);

    private static readonly Lazy<Icon> Lazy = new(Build);

    internal static Icon Value => Lazy.Value;

    private static Icon Build()
    {
        using var bitmap = TrayIconFactory.Render(Accent, 64);
        var handle = bitmap.GetHicon();

        try
        {
            // Cloned off the handle so the icon survives the handle being destroyed below.
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            TrayIconFactory.Destroy(handle);
        }
    }
}
