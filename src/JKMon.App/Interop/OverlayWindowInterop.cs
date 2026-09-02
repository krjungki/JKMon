using System.Runtime.InteropServices;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;

namespace JKMon.App.Interop;

/// <summary>
/// Applies the overlay window style and placement using documented Win32 calls only.
/// Desktop pinning is done by keeping the window bottom-most rather than by reparenting to WorkerW.
/// </summary>
internal static class OverlayWindowInterop
{
    private const int GwlExStyle = -20;

    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExTransparent = 0x00000020;
    internal const int WsExNoActivate = 0x08000000;
    internal const int WsExLayered = 0x00080000;

    private static readonly IntPtr HwndBottom = new(1);
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    internal const int WmDpiChanged = 0x02E0;
    internal const int WmSettingChange = 0x001A;
    internal const int WmDisplayChange = 0x007E;
    internal const int WmWindowPosChanging = 0x0046;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    /// <summary>Window bounds in physical pixels, which is what placement arithmetic uses.</summary>
    internal static (int Width, int Height) GetSize(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect))
        {
            return (rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        return (0, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    internal static PlacementMath.Rect GetBounds(IntPtr hwnd) =>
        hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect)
            ? new PlacementMath.Rect(rect.Left, rect.Top, rect.Right, rect.Bottom)
            : default;

    /// <summary>The overlay is click-through, so the pointer has to be polled rather than tracked by messages.</summary>
    internal static (int X, int Y)? CursorPosition() =>
        GetCursorPos(out var point) ? (point.X, point.Y) : null;

    /// <summary>Moving in physical pixels avoids WPF's device independent conversion across mixed-DPI monitors.</summary>
    internal static void MoveTo(IntPtr hwnd, int x, int y)
    {
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoActivate | SwpNoZOrder);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPos
    {
        internal IntPtr Hwnd;
        internal IntPtr HwndInsertAfter;
        internal int X;
        internal int Y;
        internal int Cx;
        internal int Cy;
        internal uint Flags;
    }

    /// <summary>
    /// Keeps the window at the bottom of the z-order by editing the pending WINDOWPOS. Calling SetWindowPos from
    /// inside WM_WINDOWPOSCHANGING cancels the move that triggered the message.
    /// </summary>
    internal static void PinToBottom(IntPtr lParam) => Pin(lParam, HwndBottom);

    /// <summary>
    /// The same treatment for the top. WPF recomputes the z-order from its own Topmost property whenever it moves
    /// or resizes the window, and the overlay resizes as its readings change width, so setting the style once is
    /// not enough: every position change would drop the window out of the topmost band again.
    /// </summary>
    internal static void PinToTop(IntPtr lParam) => Pin(lParam, HwndTopMost);

    private static void Pin(IntPtr lParam, IntPtr insertAfter)
    {
        if (lParam == IntPtr.Zero)
        {
            return;
        }

        var position = Marshal.PtrToStructure<WindowPos>(lParam);
        position.HwndInsertAfter = insertAfter;
        position.Flags &= ~SwpNoZOrder;
        Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
    }

    /// <summary>Click-through and non-activating so the overlay never steals focus from real work.</summary>
    internal static void ApplyOverlayStyles(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, style | WsExToolWindow | WsExTransparent | WsExNoActivate);
    }

    internal static void ApplyLayer(IntPtr hwnd, WindowLayer layer)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var insertAfter = layer == WindowLayer.AlwaysOnTop ? HwndTopMost : HwndNoTopMost;
        SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);

        if (layer == WindowLayer.Desktop)
        {
            SendToBottom(hwnd);
        }
    }

    internal static void SendToBottom(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
    }

    private const int WsExTopMost = 0x00000008;

    internal static bool IsTopMost(IntPtr hwnd) =>
        hwnd != IntPtr.Zero && (GetWindowLong(hwnd, GwlExStyle) & WsExTopMost) != 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        internal int Cx;
        internal int Cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendFunction
    {
        internal byte BlendOp;
        internal byte BlendFlags;
        internal byte SourceConstantAlpha;
        internal byte AlphaFormat;
    }

    private const byte AcSrcOver = 0x00;
    private const byte AcSrcAlpha = 0x01;
    private const int UlwAlpha = 0x00000002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        IntPtr hwnd, IntPtr hdcDst, ref NativePoint pptDst, ref NativeSize psize,
        IntPtr hdcSrc, ref NativePoint pptSrc, int crKey, ref BlendFunction pblend, int dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr handle);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    /// <summary>
    /// Moves, resizes and repaints the overlay in a single call. A layered window is composited by the window
    /// manager from the bitmap handed over here, which is what gives per-pixel alpha without any WM_PAINT.
    /// </summary>
    internal static void PushLayeredSurface(IntPtr hwnd, Bitmap bitmap, int x, int y)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var surface = bitmap.GetHbitmap(Color.FromArgb(0));
        var previous = SelectObject(memory, surface);

        try
        {
            var position = new NativePoint { X = x, Y = y };
            var size = new NativeSize { Cx = bitmap.Width, Cy = bitmap.Height };
            var origin = new NativePoint { X = 0, Y = 0 };
            var blend = new BlendFunction
            {
                BlendOp = AcSrcOver,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = AcSrcAlpha
            };

            UpdateLayeredWindow(hwnd, screen, ref position, ref size, memory, ref origin, 0, ref blend, UlwAlpha);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(surface);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }
}
