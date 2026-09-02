using System.Drawing.Imaging;
using System.Windows.Forms;
using JKMon.App.Interop;
using JKMon.App.Rendering;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;

namespace JKMon.App;

/// <summary>
/// The overlay itself: a click-through, per-pixel alpha layered window whose contents are pushed to the desktop
/// with UpdateLayeredWindow. Nothing here is a control, so the window never needs to be composed or hit tested.
/// </summary>
internal sealed class OverlayForm : Form
{
    private readonly OverlayRenderer _renderer = new();
    private readonly System.Windows.Forms.Timer _pointerWatch = new() { Interval = 120 };

    private JkMonSettings _settings = new();
    private OverlayModel? _model;
    private bool _concealed;

    internal OverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "JKMon";
        _pointerWatch.Tick += (_, _) => UpdatePointerConcealment();
    }

    /// <summary>
    /// The styles have to be in place before the first paint, so they are added to the creation parameters rather
    /// than applied afterwards. WS_EX_LAYERED is what makes UpdateLayeredWindow legal on this window.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= OverlayWindowInterop.WsExLayered
                | OverlayWindowInterop.WsExTransparent
                | OverlayWindowInterop.WsExToolWindow
                | OverlayWindowInterop.WsExNoActivate;

            return parameters;
        }
    }

    protected override bool ShowWithoutActivation => true;

    internal void ApplySettings(JkMonSettings settings)
    {
        _settings = settings.Normalized();
        _renderer.ApplySettings(_settings, ScaleFor());
        ApplyLayer();
        ApplyPointerWatch();
        Redraw();
    }

    internal void Update(OverlayModel model)
    {
        EnsureLayer();
        _model = model;
        Redraw();
    }

    /// <summary>
    /// WPF used to own the topmost flag and kept clearing it; this window has no such owner, but the shell still
    /// rearranges the z-order on its own, so the layer is re-asserted whenever the readings refresh.
    /// </summary>
    private void ApplyLayer()
    {
        if (IsHandleCreated)
        {
            OverlayWindowInterop.ApplyLayer(Handle, _settings.Layer);
        }
    }

    private void EnsureLayer()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        var wanted = _settings.Layer == WindowLayer.AlwaysOnTop;
        if (OverlayWindowInterop.IsTopMost(Handle) == wanted)
        {
            return;
        }

        DiagnosticLog.Write($"layer corrected: wanted {_settings.Layer}, window was not");
        ApplyLayer();
    }

    /// <summary>The overlay is click-through, so it never receives mouse messages and the pointer has to be polled.</summary>
    private void ApplyPointerWatch()
    {
        if (!_settings.HideWhenPointerOver)
        {
            _pointerWatch.Stop();
            _concealed = false;
            return;
        }

        _pointerWatch.Start();
        UpdatePointerConcealment();
    }

    private void UpdatePointerConcealment()
    {
        var cursor = OverlayWindowInterop.CursorPosition();
        if (cursor is null || !IsHandleCreated)
        {
            return;
        }

        var bounds = OverlayWindowInterop.GetBounds(Handle);
        var conceal = HoverGate.ShouldConceal(_settings.HideWhenPointerOver, bounds, cursor.Value.X, cursor.Value.Y);
        if (conceal == _concealed)
        {
            return;
        }

        _concealed = conceal;
        Redraw();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyLayer();
        Redraw();
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case OverlayWindowInterop.WmDpiChanged:
            case OverlayWindowInterop.WmDisplayChange:
            case OverlayWindowInterop.WmSettingChange:
                // The taskbar can move or change size, which redefines the work area. A shell that starts or
                // restarts after this window also rearranges the z-order, so the layer is re-asserted with it.
                base.WndProc(ref m);
                _renderer.ApplySettings(_settings, ScaleFor());
                EnsureLayer();
                Redraw();
                return;

            case OverlayWindowInterop.WmWindowPosChanging:
                if (_settings.Layer == WindowLayer.Desktop)
                {
                    OverlayWindowInterop.PinToBottom(m.LParam);
                }
                else
                {
                    OverlayWindowInterop.PinToTop(m.LParam);
                }

                break;
        }

        base.WndProc(ref m);
    }

    /// <summary>
    /// Renders into an ARGB bitmap and hands it to the window manager in one call. There is no WM_PAINT involved:
    /// a layered window keeps its own copy of the surface, which is also why the overlay never flickers.
    /// </summary>
    private void Redraw()
    {
        if (!IsHandleCreated || _model is not { } model)
        {
            return;
        }

        var size = _renderer.Layout(model);
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var (x, y) = Placement(size);

        using var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        if (!_concealed)
        {
            using var graphics = Graphics.FromImage(bitmap);
            _renderer.Paint(graphics);
        }

        OverlayWindowInterop.PushLayeredSurface(Handle, bitmap, x, y);
    }

    private (int X, int Y) Placement(Size size)
    {
        var workArea = OverlayPlacement.WorkAreaFor(Handle, _settings.MonitorDeviceName);
        return PlacementMath.Bottom(workArea, size.Width, size.Height, ScaledMargin(), _settings.Position);
    }

    private int ScaledMargin() => (int)Math.Round(_settings.MarginPixels * ScaleFor());

    /// <summary>WPF scaled for DPI on its own. GDI does not, so every drawn size is multiplied by this.</summary>
    private float ScaleFor() => IsHandleCreated ? DeviceDpi / 96f : 1f;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pointerWatch.Dispose();
            _renderer.Dispose();
        }

        base.Dispose(disposing);
    }
}
