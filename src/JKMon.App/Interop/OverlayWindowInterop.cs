using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
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

    private const int WsExToolWindow = 0x00000080;
    private const int WsExTransparent = 0x00000020;
    private const int WsExNoActivate = 0x08000000;

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
    internal static void PinToBottom(IntPtr lParam)
    {
        if (lParam == IntPtr.Zero)
        {
            return;
        }

        var position = Marshal.PtrToStructure<WindowPos>(lParam);
        position.HwndInsertAfter = HwndBottom;
        position.Flags &= ~SwpNoZOrder;
        Marshal.StructureToPtr(position, lParam, fDeleteOld: false);
    }

    internal static IntPtr GetHandle(Window window) => new WindowInteropHelper(window).Handle;

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
}
