using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

// File-level aliases beat the WPF-oriented global aliases for this GDI+ only file.
using Color = System.Drawing.Color;
using FontStyle = System.Drawing.FontStyle;
using Pen = System.Drawing.Pen;
using Rectangle = System.Drawing.Rectangle;

namespace JKMon.App;

/// <summary>Builds the notification area icon in code so no binary asset needs to ship with the source.</summary>
internal static class TrayIconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);

    internal static (Icon Icon, IntPtr Handle) Create(Color accent)
    {
        using var bitmap = Render(accent, 32);
        var handle = bitmap.GetHicon();
        return (Icon.FromHandle(handle), handle);
    }

    /// <summary>Drawn at the requested size so the same mark serves the 16 px tray and the larger window icon.</summary>
    internal static Bitmap Render(Color accent, int size)
    {
        var scale = size / 32f;
        var bitmap = new Bitmap(size, size);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            var inset = (int)Math.Round(scale);
            using var background = new SolidBrush(Color.FromArgb(230, 18, 22, 30));
            using var path = RoundedRectangle(
                new Rectangle(inset, inset, size - inset * 2, size - inset * 2), (int)Math.Round(7 * scale));
            g.FillPath(background, path);

            using var border = new Pen(accent, 2f * scale);
            g.DrawPath(border, path);

            using var font = new Font("Segoe UI", 12f * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(accent);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString("JK", font, text, new RectangleF(0, 0, size, size), format);
        }

        return bitmap;
    }

    internal static void Destroy(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
