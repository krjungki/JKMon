using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using JKMon.App.Interop;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;
using JKMon.Core.Sync;
using JKMon.Core.Update;

// File-level aliases beat the WinForms and GDI+ imports that UseWindowsForms brings in.
using Button = System.Windows.Controls.Button;
using Color = System.Windows.Media.Color;
using TextBox = System.Windows.Controls.TextBox;

namespace JKMon.App;

public partial class SettingsWindow : Window
{
    private const string AutomaticMonitor = "Automatic (follow window)";

    private readonly List<string> _monitorDevices = [];

    private readonly List<string> _providerOrder = [];

    /// <summary>Empty until the first refresh reports which clients are running, which hides the list rather than
    /// showing providers the machine does not have.</summary>
    private List<string> _presentProviders = [];

    private int[] _customColors = [];

    private DateTimeOffset _lastUpdateCheckUtc;

    private bool _loading;

    public SettingsWindow(JkMonSettings settings)
    {
        InitializeComponent();

        Icon = AppIcon.Value;

        var families = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        FontBox.ItemsSource = families;
        CustomFontBox.ItemsSource = families;

        LoadMonitors();
        Load(settings);
        Wire();

        DataRootText.Text = AppPaths.IsPortable
            ? $"Portable. Settings and log live in {AppPaths.DataRoot}"
            : $"The app folder is read-only, so settings and log live in {AppPaths.DataRoot}";
    }

    /// <summary>The first entry means "whichever monitor the overlay is on", so its device name is empty.</summary>
    private void LoadMonitors()
    {
        var labels = new List<string> { AutomaticMonitor };
        _monitorDevices.Add(string.Empty);

        foreach (var monitor in MonitorCatalog.All())
        {
            labels.Add(monitor.Label);
            _monitorDevices.Add(monitor.DeviceName);
        }

        MonitorBox.ItemsSource = labels;
    }

    /// <summary>Raised on every change so the overlay previews the design immediately.</summary>
    public event Action<JkMonSettings>? SettingsChanged;

    private void Wire()
    {
        CustomTextBox.TextChanged += (_, _) => Publish();
        CustomFontBox.SelectionChanged += (_, _) => Publish();
        CustomSizeSlider.ValueChanged += (_, _) => Publish();
        CustomColorBox.TextChanged += (_, _) => Publish();
        CaptionLeftRadio.Checked += (_, _) => Publish();
        CaptionCenterRadio.Checked += (_, _) => Publish();
        CaptionRightRadio.Checked += (_, _) => Publish();
        TextColorBox.TextChanged += (_, _) => Publish();
        BackColorBox.TextChanged += (_, _) => Publish();
        NetInColorBox.TextChanged += (_, _) => Publish();
        NetOutColorBox.TextChanged += (_, _) => Publish();
        DiskReadColorBox.TextChanged += (_, _) => Publish();
        DiskWriteColorBox.TextChanged += (_, _) => Publish();
        DirectionColorCheck.Checked += (_, _) => Publish();
        DirectionColorCheck.Unchecked += (_, _) => Publish();
        CpuNumberRadio.Checked += (_, _) => Publish();
        CpuBarRadio.Checked += (_, _) => Publish();
        MemoryNumberRadio.Checked += (_, _) => Publish();
        MemoryBarRadio.Checked += (_, _) => Publish();
        MemoryPieRadio.Checked += (_, _) => Publish();
        CoresCheck.Checked += (_, _) => Publish();
        CoresCheck.Unchecked += (_, _) => Publish();
        OutlineColorBox.TextChanged += (_, _) => Publish();
        OutlineWidthSlider.ValueChanged += (_, _) => Publish();
        LabelSizeSlider.ValueChanged += (_, _) => Publish();
        CaptionSizeSlider.ValueChanged += (_, _) => Publish();
        CpuGaugeColorBox.TextChanged += (_, _) => Publish();
        MemoryGaugeColorBox.TextChanged += (_, _) => Publish();
        OpacitySlider.ValueChanged += (_, _) => Publish();
        FontBox.SelectionChanged += (_, _) => Publish();
        FontSizeSlider.ValueChanged += (_, _) => Publish();
        CircleSlider.ValueChanged += (_, _) => Publish();
        BoldCheck.Checked += (_, _) => Publish();
        BoldCheck.Unchecked += (_, _) => Publish();
        ShadowCheck.Checked += (_, _) => Publish();
        ShadowCheck.Unchecked += (_, _) => Publish();
        CaptionShadowCheck.Checked += (_, _) => Publish();
        CaptionShadowCheck.Unchecked += (_, _) => Publish();
        RefreshSlider.ValueChanged += (_, _) => Publish();
        MarginSlider.ValueChanged += (_, _) => Publish();
        DesktopRadio.Checked += (_, _) => Publish();
        TopRadio.Checked += (_, _) => Publish();
        BottomLeftRadio.Checked += (_, _) => Publish();
        BottomCenterRadio.Checked += (_, _) => Publish();
        BottomRightRadio.Checked += (_, _) => Publish();
        MonitorBox.SelectionChanged += (_, _) => Publish();
        StartupCheck.Checked += (_, _) => Publish();
        StartupCheck.Unchecked += (_, _) => Publish();
        HideOnHoverCheck.Checked += (_, _) => Publish();
        HideOnHoverCheck.Unchecked += (_, _) => Publish();
        UpdateNeverRadio.Checked += (_, _) => Publish();
        UpdateDailyRadio.Checked += (_, _) => Publish();
        UpdateWeeklyRadio.Checked += (_, _) => Publish();
        UpdateStartupCheck.Checked += (_, _) => Publish();
        UpdateStartupCheck.Unchecked += (_, _) => Publish();

        OrderUpButton.Click += (_, _) => MoveProvider(-1);
        OrderDownButton.Click += (_, _) => MoveProvider(1);

        ResetButton.Click += (_, _) =>
        {
            Load(new JkMonSettings());
            Publish();
        };

        CloseButton.Click += (_, _) => Close();

        WirePicker(TextColorSwatch, TextColorBox, JkMonSettings.DefaultTextColor);
        WirePicker(CustomColorSwatch, CustomColorBox, JkMonSettings.DefaultCustomTextColor);
        WirePicker(BackColorSwatch, BackColorBox, JkMonSettings.DefaultBackgroundColor);
        WirePicker(OutlineColorSwatch, OutlineColorBox, JkMonSettings.DefaultGaugeOutlineColor);
        WirePicker(CpuGaugeColorSwatch, CpuGaugeColorBox, JkMonSettings.DefaultCpuGaugeColor);
        WirePicker(MemoryGaugeColorSwatch, MemoryGaugeColorBox, JkMonSettings.DefaultMemoryGaugeColor);
        WirePicker(NetOutColorSwatch, NetOutColorBox, JkMonSettings.DefaultNetworkOutColor);
        WirePicker(NetInColorSwatch, NetInColorBox, JkMonSettings.DefaultNetworkInColor);
        WirePicker(DiskReadColorSwatch, DiskReadColorBox, JkMonSettings.DefaultDiskReadColor);
        WirePicker(DiskWriteColorSwatch, DiskWriteColorBox, JkMonSettings.DefaultDiskWriteColor);
    }

    /// <summary>Writing the pick back into the box reuses its TextChanged handler, so the overlay updates itself.</summary>
    private void WirePicker(Button swatch, TextBox box, string fallback) => swatch.Click += (_, _) =>
    {
        var current = HexColor.ParseOrDefault(box.Text, HexColor.ParseOrDefault(fallback, new HexColor(255, 0, 0, 0)));

        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            CustomColors = _customColors,
            Color = System.Drawing.Color.FromArgb(current.R, current.G, current.B)
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        // Keep the palette the user builds up for the rest of the session.
        _customColors = dialog.CustomColors;
        box.Text = new HexColor(255, dialog.Color.R, dialog.Color.G, dialog.Color.B).ToHex();
    };

    private void Load(JkMonSettings settings)
    {
        _loading = true;
        try
        {
            var normalized = settings.Normalized();
            CustomTextBox.Text = normalized.CustomText;
            CustomFontBox.SelectedItem = normalized.CustomTextFontFamily;
            CustomSizeSlider.Value = normalized.CustomTextFontSize;
            CustomColorBox.Text = normalized.CustomTextColor;
            CaptionLeftRadio.IsChecked = normalized.CustomTextAlignment == CaptionAlignment.Left;
            CaptionCenterRadio.IsChecked = normalized.CustomTextAlignment == CaptionAlignment.Center;
            CaptionRightRadio.IsChecked = normalized.CustomTextAlignment == CaptionAlignment.Right;
            CaptionShadowCheck.IsChecked = normalized.CustomTextShadow;
            TextColorBox.Text = normalized.TextColor;
            BackColorBox.Text = normalized.BackgroundColor;
            NetInColorBox.Text = normalized.NetworkInColor;
            NetOutColorBox.Text = normalized.NetworkOutColor;
            DiskReadColorBox.Text = normalized.DiskReadColor;
            DiskWriteColorBox.Text = normalized.DiskWriteColor;
            DirectionColorCheck.IsChecked = normalized.UseDirectionColors;
            CpuNumberRadio.IsChecked = normalized.CpuGauge == CpuGaugeStyle.Number;
            CpuBarRadio.IsChecked = normalized.CpuGauge == CpuGaugeStyle.Bar;
            MemoryNumberRadio.IsChecked = normalized.MemoryGauge == MemoryGaugeStyle.Number;
            MemoryBarRadio.IsChecked = normalized.MemoryGauge == MemoryGaugeStyle.Bar;
            MemoryPieRadio.IsChecked = normalized.MemoryGauge == MemoryGaugeStyle.Pie;
            CoresCheck.IsChecked = normalized.ShowIndividualCores;
            OutlineColorBox.Text = normalized.GaugeOutlineColor;
            OutlineWidthSlider.Value = normalized.GaugeOutlineThickness;
            LabelSizeSlider.Value = normalized.GaugeLabelFontSize;
            CaptionSizeSlider.Value = normalized.GaugeCaptionFontSize;
            CpuGaugeColorBox.Text = normalized.CpuGaugeColor;
            MemoryGaugeColorBox.Text = normalized.MemoryGaugeColor;
            OpacitySlider.Value = normalized.BackgroundOpacityPercent;
            FontBox.SelectedItem = normalized.FontFamily;
            FontSizeSlider.Value = normalized.FontSize;
            CircleSlider.Value = normalized.CircleDiameter;
            BoldCheck.IsChecked = normalized.BoldText;
            ShadowCheck.IsChecked = normalized.TextShadow;
            RefreshSlider.Value = normalized.RefreshSeconds;
            MarginSlider.Value = normalized.MarginPixels;
            DesktopRadio.IsChecked = normalized.Layer == WindowLayer.Desktop;
            TopRadio.IsChecked = normalized.Layer == WindowLayer.AlwaysOnTop;
            BottomLeftRadio.IsChecked = normalized.Position == OverlayPosition.BottomLeft;
            BottomCenterRadio.IsChecked = normalized.Position == OverlayPosition.BottomCenter;
            BottomRightRadio.IsChecked = normalized.Position == OverlayPosition.BottomRight;

            var monitorIndex = _monitorDevices.IndexOf(normalized.MonitorDeviceName);
            MonitorBox.SelectedIndex = monitorIndex >= 0 ? monitorIndex : 0;

            StartupCheck.IsChecked = normalized.StartWithWindows;
            HideOnHoverCheck.IsChecked = normalized.HideWhenPointerOver;
            UpdateNeverRadio.IsChecked = normalized.UpdateCheck == UpdateCheckFrequency.Never;
            UpdateDailyRadio.IsChecked = normalized.UpdateCheck == UpdateCheckFrequency.Daily;
            UpdateWeeklyRadio.IsChecked = normalized.UpdateCheck == UpdateCheckFrequency.Weekly;
            UpdateStartupCheck.IsChecked = normalized.CheckUpdatesOnStartup;
            _lastUpdateCheckUtc = normalized.LastUpdateCheckUtc;

            _providerOrder.Clear();
            _providerOrder.AddRange(normalized.ProviderOrder);
            ShowProviderOrder(0);
        }
        finally
        {
            _loading = false;
        }

        UpdateReadouts();
    }

    private void Publish()
    {
        if (_loading)
        {
            return;
        }

        UpdateReadouts();
        SettingsChanged?.Invoke(Current());
    }

    private JkMonSettings Current() => new JkMonSettings
    {
        CustomText = CustomTextBox.Text,
        CustomTextFontFamily = CustomFontBox.SelectedItem as string ?? JkMonSettings.DefaultFontFamily,
        CustomTextFontSize = Math.Round(CustomSizeSlider.Value),
        CustomTextColor = CustomColorBox.Text,
        CustomTextAlignment = CaptionLeftRadio.IsChecked == true ? CaptionAlignment.Left
            : CaptionRightRadio.IsChecked == true ? CaptionAlignment.Right
            : CaptionAlignment.Center,
        TextColor = TextColorBox.Text,
        BackgroundColor = BackColorBox.Text,
        NetworkInColor = NetInColorBox.Text,
        NetworkOutColor = NetOutColorBox.Text,
        DiskReadColor = DiskReadColorBox.Text,
        DiskWriteColor = DiskWriteColorBox.Text,
        UseDirectionColors = DirectionColorCheck.IsChecked == true,
        CpuGauge = CpuBarRadio.IsChecked == true ? CpuGaugeStyle.Bar : CpuGaugeStyle.Number,
        MemoryGauge = MemoryBarRadio.IsChecked == true ? MemoryGaugeStyle.Bar
            : MemoryPieRadio.IsChecked == true ? MemoryGaugeStyle.Pie
            : MemoryGaugeStyle.Number,
        ShowIndividualCores = CoresCheck.IsChecked == true,
        GaugeOutlineColor = OutlineColorBox.Text,
        GaugeOutlineThickness = OutlineWidthSlider.Value,
        GaugeLabelFontSize = Math.Round(LabelSizeSlider.Value),
        GaugeCaptionFontSize = Math.Round(CaptionSizeSlider.Value),
        CpuGaugeColor = CpuGaugeColorBox.Text,
        MemoryGaugeColor = MemoryGaugeColorBox.Text,
        BackgroundOpacityPercent = (int)Math.Round(OpacitySlider.Value),
        FontFamily = FontBox.SelectedItem as string ?? JkMonSettings.DefaultFontFamily,
        FontSize = Math.Round(FontSizeSlider.Value),
        CircleDiameter = (int)Math.Round(CircleSlider.Value),
        BoldText = BoldCheck.IsChecked == true,
        TextShadow = ShadowCheck.IsChecked == true,
        CustomTextShadow = CaptionShadowCheck.IsChecked == true,
        RefreshSeconds = (int)Math.Round(RefreshSlider.Value),
        MarginPixels = (int)Math.Round(MarginSlider.Value),
        Layer = TopRadio.IsChecked == true ? WindowLayer.AlwaysOnTop : WindowLayer.Desktop,
        Position = BottomLeftRadio.IsChecked == true ? OverlayPosition.BottomLeft
            : BottomCenterRadio.IsChecked == true ? OverlayPosition.BottomCenter
            : OverlayPosition.BottomRight,
        MonitorDeviceName = MonitorBox.SelectedIndex > 0 && MonitorBox.SelectedIndex < _monitorDevices.Count
            ? _monitorDevices[MonitorBox.SelectedIndex]
            : string.Empty,
        StartWithWindows = StartupCheck.IsChecked == true,
        HideWhenPointerOver = HideOnHoverCheck.IsChecked == true,
        ProviderOrder = _providerOrder.ToList(),
        UpdateCheck = UpdateDailyRadio.IsChecked == true ? UpdateCheckFrequency.Daily
            : UpdateWeeklyRadio.IsChecked == true ? UpdateCheckFrequency.Weekly
            : UpdateCheckFrequency.Never,
        CheckUpdatesOnStartup = UpdateStartupCheck.IsChecked == true,
        LastUpdateCheckUtc = _lastUpdateCheckUtc
    }.Normalized();

    private void ShowProviderOrder(int selected)
    {
        var visible = ProviderOrderView.Visible(_providerOrder, _presentProviders);

        OrderList.ItemsSource = visible.Select(SyncProviderCatalog.DisplayName).ToList();
        OrderList.SelectedIndex = Math.Clamp(selected, 0, Math.Max(0, visible.Count - 1));

        var any = visible.Count > 0;
        OrderList.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        NoProvidersText.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        OrderUpButton.IsEnabled = visible.Count > 1;
        OrderDownButton.IsEnabled = visible.Count > 1;
    }

    /// <summary>The set can change while the window is open, so a client started meanwhile shows up without a reopen.</summary>
    internal void SetPresentProviders(IReadOnlyList<string> providerIds)
    {
        if (_presentProviders.SequenceEqual(providerIds, StringComparer.Ordinal))
        {
            return;
        }

        _presentProviders = [.. providerIds];
        ShowProviderOrder(OrderList.SelectedIndex);
    }

    /// <summary>Reordering is a list edit, so the selection follows the moved entry rather than the position.</summary>
    private void MoveProvider(int offset)
    {
        var from = OrderList.SelectedIndex;
        var to = from + offset;
        var visibleCount = ProviderOrderView.Visible(_providerOrder, _presentProviders).Count;
        if (from < 0 || to < 0 || to >= visibleCount)
        {
            return;
        }

        var reordered = ProviderOrderView.Move(_providerOrder, _presentProviders, from, to);
        _providerOrder.Clear();
        _providerOrder.AddRange(reordered);
        ShowProviderOrder(to);
        Publish();
    }

    private void UpdateReadouts()
    {
        DirectionColorRows.IsEnabled = DirectionColorCheck.IsChecked == true;
        UpdateStartupCheck.IsEnabled = UpdateNeverRadio.IsChecked != true;

        // Per-core bars take the place of the aggregate gauge, so its style no longer applies.
        var aggregateCpu = CoresCheck.IsChecked != true;
        CpuNumberRadio.IsEnabled = aggregateCpu;
        CpuBarRadio.IsEnabled = aggregateCpu;

        OpacityText.Text = string.Create(CultureInfo.InvariantCulture, $"{Math.Round(OpacitySlider.Value)}%");
        CustomSizeText.Text = Math.Round(CustomSizeSlider.Value).ToString(CultureInfo.InvariantCulture);
        OutlineWidthText.Text = OutlineWidthSlider.Value.ToString("0.#", CultureInfo.InvariantCulture);
        LabelSizeText.Text = Math.Round(LabelSizeSlider.Value).ToString(CultureInfo.InvariantCulture);
        CaptionSizeText.Text = Math.Round(CaptionSizeSlider.Value).ToString(CultureInfo.InvariantCulture);
        FontSizeText.Text = Math.Round(FontSizeSlider.Value).ToString(CultureInfo.InvariantCulture);
        CircleText.Text = Math.Round(CircleSlider.Value).ToString(CultureInfo.InvariantCulture);
        RefreshText.Text = string.Create(CultureInfo.InvariantCulture, $"{Math.Round(RefreshSlider.Value)}s");
        MarginText.Text = Math.Round(MarginSlider.Value).ToString(CultureInfo.InvariantCulture);

        TextColorSwatch.Background = SwatchFor(TextColorBox.Text, JkMonSettings.DefaultTextColor);
        CustomColorSwatch.Background = SwatchFor(CustomColorBox.Text, JkMonSettings.DefaultCustomTextColor);
        BackColorSwatch.Background = SwatchFor(BackColorBox.Text, JkMonSettings.DefaultBackgroundColor);
        NetInColorSwatch.Background = SwatchFor(NetInColorBox.Text, JkMonSettings.DefaultNetworkInColor);
        NetOutColorSwatch.Background = SwatchFor(NetOutColorBox.Text, JkMonSettings.DefaultNetworkOutColor);
        DiskReadColorSwatch.Background = SwatchFor(DiskReadColorBox.Text, JkMonSettings.DefaultDiskReadColor);
        DiskWriteColorSwatch.Background = SwatchFor(DiskWriteColorBox.Text, JkMonSettings.DefaultDiskWriteColor);
        OutlineColorSwatch.Background = SwatchFor(OutlineColorBox.Text, JkMonSettings.DefaultGaugeOutlineColor);
        CpuGaugeColorSwatch.Background = SwatchFor(CpuGaugeColorBox.Text, JkMonSettings.DefaultCpuGaugeColor);
        MemoryGaugeColorSwatch.Background = SwatchFor(MemoryGaugeColorBox.Text, JkMonSettings.DefaultMemoryGaugeColor);
    }

    private static Brush SwatchFor(string value, string fallback)
    {
        var color = HexColor.ParseOrDefault(value, HexColor.ParseOrDefault(fallback, new HexColor(255, 0, 0, 0)));
        var brush = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }
}
