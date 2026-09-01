using System.Text;
using System.Windows.Forms;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;

// File-level aliases beat the WPF-oriented global aliases for this WinForms only file.
using Color = System.Drawing.Color;

namespace JKMon.App;

/// <summary>WPF has no tray icon, so the WinForms NotifyIcon provides the only user-facing control surface.</summary>
internal sealed class TrayController : IDisposable
{
    private static readonly Color Green = Color.FromArgb(0x35, 0xFF, 0x6A);
    private static readonly Color Red = Color.FromArgb(0xFF, 0x4B, 0x4B);
    private static readonly Color Gray = Color.FromArgb(0xC0, 0xC0, 0xC0);

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _desktopLayerItem;
    private readonly ToolStripMenuItem _topLayerItem;
    private readonly ToolStripMenuItem _visibilityItem;
    private readonly ToolStripMenuItem[] _refreshItems;

    private IntPtr _iconHandle;
    private CircleColor? _accent;

    internal TrayController()
    {
        _desktopLayerItem = new ToolStripMenuItem("Pin to desktop", null, (_, _) => LayerChanged?.Invoke(WindowLayer.Desktop));
        _topLayerItem = new ToolStripMenuItem("Always on top", null, (_, _) => LayerChanged?.Invoke(WindowLayer.AlwaysOnTop));
        _visibilityItem = new ToolStripMenuItem("Hide overlay", null, (_, _) => VisibilityToggled?.Invoke());

        _refreshItems = Enumerable
            .Range(JkMonSettings.MinRefreshSeconds, JkMonSettings.MaxRefreshSeconds)
            .Select(seconds =>
            {
                var item = new ToolStripMenuItem($"{seconds} second{(seconds == 1 ? string.Empty : "s")}");
                item.Click += (_, _) => RefreshIntervalChanged?.Invoke(seconds);
                return item;
            })
            .ToArray();

        var refreshMenu = new ToolStripMenuItem("Refresh interval");
        refreshMenu.DropDownItems.AddRange(_refreshItems);

        var menu = new ContextMenuStrip();
        menu.Items.Add(_visibilityItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Settings...", null, (_, _) => SettingsRequested?.Invoke()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_desktopLayerItem);
        menu.Items.Add(_topLayerItem);
        menu.Items.Add(refreshMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke()));

        _icon = new NotifyIcon
        {
            Text = "JKMon",
            Visible = true,
            ContextMenuStrip = menu
        };

        _icon.DoubleClick += (_, _) => SettingsRequested?.Invoke();
        ApplyAccent(CircleColor.Gray);
    }

    internal event Action<WindowLayer>? LayerChanged;

    internal event Action<int>? RefreshIntervalChanged;

    internal event Action? VisibilityToggled;

    internal event Action? SettingsRequested;

    internal event Action? ExitRequested;

    internal void Sync(JkMonSettings settings, bool overlayVisible)
    {
        _desktopLayerItem.Checked = settings.Layer == WindowLayer.Desktop;
        _topLayerItem.Checked = settings.Layer == WindowLayer.AlwaysOnTop;
        _visibilityItem.Text = overlayVisible ? "Hide overlay" : "Show overlay";

        for (var i = 0; i < _refreshItems.Length; i++)
        {
            _refreshItems[i].Checked = i + JkMonSettings.MinRefreshSeconds == settings.RefreshSeconds;
        }
    }

    /// <summary>Mirrors the overlay state so the app stays useful while the overlay is hidden or covered.</summary>
    internal void ShowStatus(OverlayModel model)
    {
        ApplyAccent(WorstOf(model.Circles));

        var tip = new StringBuilder();
        tip.Append($"JKMon CPU {model.Cpu} Mem {model.Memory}");
        foreach (var circle in model.Circles)
        {
            tip.Append($"\n{circle.Initial}: {DescribeColor(circle.Color)}");
        }

        // NotifyIcon silently fails on anything past 63 characters.
        var text = tip.ToString();
        _icon.Text = text.Length > 63 ? text[..63] : text;
    }

    private static CircleColor WorstOf(IReadOnlyList<SyncCircle> circles)
    {
        if (circles.Count == 0)
        {
            return CircleColor.Gray;
        }

        if (circles.Any(c => c.Color == CircleColor.Red))
        {
            return CircleColor.Red;
        }

        return circles.All(c => c.Color == CircleColor.Green) ? CircleColor.Green : CircleColor.Gray;
    }

    private static string DescribeColor(CircleColor color) => color switch
    {
        CircleColor.Green => "up to date",
        CircleColor.Red => "syncing",
        _ => "unknown"
    };

    private void ApplyAccent(CircleColor color)
    {
        if (_accent == color)
        {
            return;
        }

        _accent = color;
        var previousHandle = _iconHandle;
        var previousIcon = _icon.Icon;

        var (icon, handle) = TrayIconFactory.Create(color switch
        {
            CircleColor.Green => Green,
            CircleColor.Red => Red,
            _ => Gray
        });

        _icon.Icon = icon;
        _iconHandle = handle;

        previousIcon?.Dispose();
        TrayIconFactory.Destroy(previousHandle);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        var icon = _icon.Icon;
        _icon.Dispose();
        icon?.Dispose();
        TrayIconFactory.Destroy(_iconHandle);
    }
}
