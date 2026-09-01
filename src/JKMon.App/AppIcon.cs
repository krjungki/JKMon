using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JKMon.App;

/// <summary>Window icon drawn by the tray icon code, so the taskbar and the notification area show the same mark.</summary>
internal static class AppIcon
{
    private static readonly System.Drawing.Color Accent = System.Drawing.Color.FromArgb(0x35, 0xFF, 0x6A);

    private static readonly Lazy<ImageSource> Lazy = new(Build);

    internal static ImageSource Value => Lazy.Value;

    private static ImageSource Build()
    {
        using var bitmap = TrayIconFactory.Render(Accent, 64);
        var handle = bitmap.GetHicon();

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            TrayIconFactory.Destroy(handle);
        }
    }
}
