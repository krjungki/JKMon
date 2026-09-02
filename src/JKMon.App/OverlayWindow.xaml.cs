using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using JKMon.App.Interop;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;

// File-level aliases beat the WinForms and GDI+ imports that UseWindowsForms brings in.
using Color = System.Windows.Media.Color;
using Image = System.Windows.Controls.Image;
using Orientation = System.Windows.Controls.Orientation;

namespace JKMon.App;

public partial class OverlayWindow : Window
{
    private static readonly HexColor Green = new(255, 0x2E, 0xD1, 0x60);
    private static readonly HexColor Red = new(255, 0xE8, 0x3B, 0x3B);
    private static readonly HexColor Gray = new(255, 0x9A, 0xA3, 0xAE);

    /// <summary>Widest strings each formatter can produce; the value columns are sized to hold them.</summary>
    private static readonly string[] PercentWidthSamples = ["100%"];

    private static readonly string[] RateWidthSamples =
        ["1023 B/s", "9.9 KiB/s", "1023 KiB/s", "1023 MiB/s", "1023 GiB/s", "1023 TiB/s"];

    private JkMonSettings _settings = new();
    private IReadOnlyList<SyncCircle> _lastCircles = [];
    private IGauge? _cpuGauge;
    private IGauge? _memoryGauge;
    private CoreBarStrip? _coreBars;
    private DispatcherTimer? _pointerWatch;

    /// <summary>Height of one text row, which sets how tall the gauges are drawn.</summary>
    private double _rowHeight = 16;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private TextBlock[] RateValues => [NetInValue, NetOutValue, DiskReadValue, DiskWriteValue];

    private TextBlock[] MetricArrows => [NetInArrow, NetOutArrow, DiskReadArrow, DiskWriteArrow];

    public void ApplySettings(JkMonSettings settings)
    {
        _settings = settings.Normalized();

        var background = HexColor.ParseOrDefault(_settings.BackgroundColor, new HexColor(255, 0x10, 0x14, 0x18));
        var alpha = (byte)Math.Round(_settings.BackgroundOpacityPercent * 255d / 100d);
        Panel.Background = Frozen(new SolidColorBrush(
            Color.FromArgb(alpha, background.R, background.G, background.B)));

        var family = ResolveFont(_settings.FontFamily);
        var weight = _settings.BoldText ? FontWeights.SemiBold : FontWeights.Normal;
        var shadow = _settings.TextShadow
            ? Frozen(new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.85 })
            : null;

        foreach (var block in RateValues.Concat(MetricArrows))
        {
            block.FontFamily = family;
            block.FontSize = _settings.FontSize;
            block.FontWeight = weight;
            block.TextWrapping = TextWrapping.NoWrap;
            block.Effect = shadow;
        }

        // Each direction owns one colour that covers both its reading and its arrow.
        var netIn = BrushFor(_settings.EffectiveNetworkInColor, JkMonSettings.DefaultNetworkInColor);
        var netOut = BrushFor(_settings.EffectiveNetworkOutColor, JkMonSettings.DefaultNetworkOutColor);
        var diskRead = BrushFor(_settings.EffectiveDiskReadColor, JkMonSettings.DefaultDiskReadColor);
        var diskWrite = BrushFor(_settings.EffectiveDiskWriteColor, JkMonSettings.DefaultDiskWriteColor);

        NetInValue.Foreground = netIn;
        NetInArrow.Foreground = netIn;
        NetOutValue.Foreground = netOut;
        NetOutArrow.Foreground = netOut;
        DiskReadValue.Foreground = diskRead;
        DiskReadArrow.Foreground = diskRead;
        DiskWriteValue.Foreground = diskWrite;
        DiskWriteArrow.Foreground = diskWrite;

        ApplyColumnWidths(family, weight);
        RebuildGauges(family, shadow);
        ApplyCustomText(shadow);

        // Column spacing tracks the font so the layout stays balanced at any size.
        var gap = Math.Round(_settings.FontSize * 0.85);
        NetColumn.Margin = new Thickness(gap, 0, 0, 0);
        DiskColumn.Margin = new Thickness(gap, 0, 0, 0);
        Circles.Margin = new Thickness(Math.Round(_settings.FontSize * 1.1), 0, 0, 0);

        _lastCircles = [];
        var hwnd = OverlayWindowInterop.GetHandle(this);
        OverlayWindowInterop.ApplyLayer(hwnd, _settings.Layer);
        ResizeAndReposition();
        ApplyPointerWatch();
    }

    /// <summary>
    /// The overlay is click-through, so it never receives mouse messages and the pointer has to be polled. The
    /// interval is short enough to feel immediate and the call itself costs almost nothing.
    /// </summary>
    private void ApplyPointerWatch()
    {
        if (!_settings.HideWhenPointerOver)
        {
            _pointerWatch?.Stop();
            Panel.Opacity = 1;
            return;
        }

        if (_pointerWatch is null)
        {
            _pointerWatch = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };

            _pointerWatch.Tick += (_, _) => UpdatePointerConcealment();
        }

        _pointerWatch.Start();
        UpdatePointerConcealment();
    }

    private void UpdatePointerConcealment()
    {
        var cursor = OverlayWindowInterop.CursorPosition();
        if (cursor is null)
        {
            return;
        }

        var bounds = OverlayWindowInterop.GetBounds(OverlayWindowInterop.GetHandle(this));
        var conceal = HoverGate.ShouldConceal(_settings.HideWhenPointerOver, bounds, cursor.Value.X, cursor.Value.Y);

        // Opacity rather than visibility: a collapsed panel would shrink the window and let the pointer fall outside.
        Panel.Opacity = conceal ? 0 : 1;
    }

    public void Update(OverlayModel model)
    {
        _cpuGauge?.Set(model.CpuPercent, model.Cpu);
        _memoryGauge?.Set(model.MemoryPercent, model.Memory);
        CpuHost.ToolTip = $"CPU {model.Cpu}";
        CoreBars.ToolTip = $"CPU {model.Cpu} across {model.CorePercents.Count} cores";
        MemoryHost.ToolTip = $"Memory {model.Memory}";

        if (_settings.ShowIndividualCores)
        {
            _coreBars?.Set(model.CorePercents);
        }

        NetInValue.Text = model.NetworkIn;
        NetOutValue.Text = model.NetworkOut;
        DiskReadValue.Text = model.DiskRead;
        DiskWriteValue.Text = model.DiskWrite;

        // Circles change far less often than the metric text, so only rebuild them when they actually differ.
        if (!_lastCircles.SequenceEqual(model.Circles))
        {
            _lastCircles = model.Circles;
            Circles.Children.Clear();
            foreach (var circle in model.Circles)
            {
                Circles.Children.Add(CreateIndicator(circle));
            }
        }

        // Resizing in the same pass as the content update avoids a one frame lag that clips the circles.
        ResizeAndReposition();
    }

    /// <summary>
    /// Each value column reserves room for the widest reading its formatter can produce, so the overlay keeps a
    /// constant size no matter what the current values are.
    /// </summary>
    private void ApplyColumnWidths(FontFamily family, FontWeight weight)
    {
        double Measure(string text) => Measured(text, family, weight, _settings.FontSize).WidthIncludingTrailingWhitespace;

        _rowHeight = Measured("100%", family, weight, _settings.FontSize).Height;

        var rateWidth = Math.Ceiling(RateWidthSamples.Max(Measure));
        var arrowWidth = Math.Ceiling(MetricArrows.Max(arrow => Measure(arrow.Text)));
        var gap = Math.Ceiling(_settings.FontSize * 0.4);

        foreach (var value in RateValues)
        {
            value.Width = rateWidth;
        }

        foreach (var arrow in MetricArrows)
        {
            arrow.Width = arrowWidth;
            arrow.Margin = new Thickness(Math.Ceiling(gap * 0.5), 0, 0, 0);
        }
    }

    /// <summary>The caption row is optional, so an empty setting collapses it rather than leaving blank padding.</summary>
    private void ApplyCustomText(Effect? shadow)
    {
        if (!_settings.HasCustomText)
        {
            CustomTextBlock.Visibility = Visibility.Collapsed;
            return;
        }

        CustomTextBlock.Visibility = Visibility.Visible;
        CustomTextBlock.Text = _settings.CustomText;
        CustomTextBlock.FontFamily = ResolveFont(_settings.CustomTextFontFamily);
        CustomTextBlock.FontSize = _settings.CustomTextFontSize;
        CustomTextBlock.FontWeight = _settings.BoldText ? FontWeights.SemiBold : FontWeights.Normal;
        CustomTextBlock.Foreground = BrushFor(_settings.CustomTextColor, JkMonSettings.DefaultCustomTextColor);
        CustomTextBlock.Effect = shadow;
        CustomTextBlock.Margin = new Thickness(0, 0, 0, Math.Round(_settings.CustomTextFontSize * 0.3));
        CustomTextBlock.HorizontalAlignment = _settings.CustomTextAlignment switch
        {
            CaptionAlignment.Left => HorizontalAlignment.Left,
            CaptionAlignment.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center
        };
    }

    /// <summary>
    /// The gauges are rebuilt only when the settings change, so a refresh just pushes new percentages into them.
    /// Their height matches the two text rows beside them, which keeps the panel a constant height in every style.
    /// </summary>
    private void RebuildGauges(FontFamily family, Effect? shadow)
    {
        var height = Math.Ceiling(_rowHeight * 2);
        var outline = BrushFor(_settings.GaugeOutlineColor, JkMonSettings.DefaultGaugeOutlineColor);
        var gap = Math.Round(_settings.FontSize * 0.85);

        var cpu = new GaugeChrome(
            BrushFor(_settings.CpuGaugeColor, JkMonSettings.DefaultCpuGaugeColor), outline, family,
            FontWeights.Bold, height, shadow)
        {
            Thickness = _settings.GaugeOutlineThickness
        };

        var memory = cpu with { Fill = BrushFor(_settings.MemoryGaugeColor, JkMonSettings.DefaultMemoryGaugeColor) };

        var numberSize = Math.Max(JkMonSettings.MinFontSize, Math.Round(height * 0.62));
        var numberWidth = Math.Ceiling(
            PercentWidthSamples.Max(s => Measured(s, family, FontWeights.Bold, numberSize).WidthIncludingTrailingWhitespace));
        var barWidth = Math.Ceiling(height * 0.5);
        var labelSize = _settings.GaugeLabelFontSize;
        var captionSize = _settings.GaugeCaptionFontSize;

        _cpuGauge = _settings.CpuGauge == CpuGaugeStyle.Bar
            ? new LabelledGauge(new BarGauge(cpu, barWidth), cpu, labelSize)
            : Captioned(new NumberGauge(cpu, numberSize, numberWidth), cpu, "CPU", captionSize);
        CpuHost.Content = _cpuGauge.Element;

        _memoryGauge = _settings.MemoryGauge switch
        {
            MemoryGaugeStyle.Bar => new LabelledGauge(new BarGauge(memory, barWidth), memory, labelSize),
            MemoryGaugeStyle.Pie => new LabelledGauge(new PieGauge(memory), memory, labelSize),
            _ => Captioned(new NumberGauge(memory, numberSize, numberWidth), memory, "Memory", captionSize)
        };
        MemoryHost.Content = _memoryGauge.Element;
        MemoryHost.Margin = new Thickness(gap, 0, 0, 0);

        // The per-core bars stand in for the aggregate gauge, so only one of the two is ever shown.
        CpuHost.Visibility = _settings.ShowIndividualCores ? Visibility.Collapsed : Visibility.Visible;
        CoreBars.Visibility = _settings.ShowIndividualCores ? Visibility.Visible : Visibility.Collapsed;
        _coreBars = new CoreBarStrip(CoreBars, cpu, Math.Max(5d, Math.Round(_settings.FontSize * 0.55)));
    }

    private static IGauge Captioned(IGauge inner, GaugeChrome chrome, string caption, double fontSize) =>
        fontSize > 0 ? new CaptionedGauge(inner, chrome, caption, fontSize) : inner;

    private FormattedText Measured(string text, FontFamily family, FontWeight weight, double size)
    {
        var pixelsPerDip = PresentationSource.FromVisual(this) is null
            ? 1d
            : VisualTreeHelper.GetDpi(this).PixelsPerDip;

        return new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal),
            size,
            System.Windows.Media.Brushes.Black,
            pixelsPerDip);
    }

    private static SolidColorBrush BrushFor(string value, string fallback)
    {
        var color = HexColor.ParseOrDefault(value, HexColor.ParseOrDefault(fallback, new HexColor(255, 255, 255, 255)));
        return Frozen(new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)));
    }

    /// <summary>The provider icon sits above a bar that carries the sync colour.</summary>
    private UIElement CreateIndicator(SyncCircle circle)
    {
        var size = _settings.CircleDiameter;
        var stack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = circle.Tooltip
        };

        var icon = ProviderIconResolver.Resolve(circle.ProviderId, size);
        if (icon is null)
        {
            stack.Children.Add(CreateCircle(circle));
        }
        else
        {
            stack.Children.Add(new Image
            {
                Source = icon,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                Effect = Frozen(new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = Math.Max(3, size / 5d),
                    ShadowDepth = Math.Max(1, size / 20d),
                    Direction = 270,
                    Opacity = 0.55
                })
            });
        }

        stack.Children.Add(CreateStatusBar(circle.Color, size));
        return stack;
    }

    private static UIElement CreateStatusBar(CircleColor color, double width)
    {
        var height = Math.Max(3d, Math.Round(width * 0.2));
        var baseColor = ColorFor(color);

        var fill = new LinearGradientBrush(Shift(baseColor, 0.4), Shift(baseColor, -0.25), 90);
        fill.Freeze();

        return new Border
        {
            Width = width,
            Height = height,
            Margin = new Thickness(0, Math.Max(2d, Math.Round(width * 0.1)), 0, 0),
            CornerRadius = new CornerRadius(height / 2d),
            Background = fill,
            BorderBrush = Frozen(new SolidColorBrush(Shift(baseColor, -0.5))),
            BorderThickness = new Thickness(Math.Max(0.5, width / 40d)),
            Effect = Frozen(new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 3,
                ShadowDepth = 1,
                Direction = 270,
                Opacity = 0.5
            })
        };
    }

    private UIElement CreateCircle(SyncCircle circle)
    {
        var baseColor = ColorFor(circle.Color);
        var diameter = _settings.CircleDiameter;

        var host = new Grid
        {
            Width = diameter,
            Height = diameter,
            ToolTip = circle.Tooltip
        };

        var fill = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.35, 0.30),
            Center = new Point(0.42, 0.38),
            RadiusX = 0.85,
            RadiusY = 0.85
        };
        fill.GradientStops.Add(new GradientStop(Shift(baseColor, 0.55), 0.0));
        fill.GradientStops.Add(new GradientStop(Shift(baseColor, 0.12), 0.45));
        fill.GradientStops.Add(new GradientStop(Shift(baseColor, -0.28), 0.92));
        fill.GradientStops.Add(new GradientStop(Shift(baseColor, -0.45), 1.0));
        fill.Freeze();

        host.Children.Add(new Ellipse
        {
            Fill = fill,
            Stroke = Frozen(new SolidColorBrush(Shift(baseColor, -0.55))),
            StrokeThickness = Math.Max(1, diameter / 26d),
            Effect = Frozen(new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = Math.Max(3, diameter / 4d),
                ShadowDepth = Math.Max(1, diameter / 18d),
                Direction = 270,
                Opacity = 0.6
            })
        });

        // A soft highlight sells the raised look without needing a bitmap.
        var gloss = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5
        };
        gloss.GradientStops.Add(new GradientStop(Color.FromArgb(0x8C, 255, 255, 255), 0.0));
        gloss.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 255, 255, 255), 1.0));
        gloss.Freeze();

        host.Children.Add(new Ellipse
        {
            Fill = gloss,
            Width = diameter * 0.62,
            Height = diameter * 0.40,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, diameter * 0.08, 0, 0),
            IsHitTestVisible = false
        });

        host.Children.Add(new TextBlock
        {
            Text = circle.Initial.ToString(),
            Foreground = Frozen(new SolidColorBrush(
                Luminance(baseColor) > 0.6 ? Color.FromRgb(0x10, 0x14, 0x18) : Colors.White)),
            FontFamily = ResolveFont(_settings.FontFamily),
            FontSize = Math.Max(9, diameter * 0.5),
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Effect = Frozen(new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 3,
                ShadowDepth = 1,
                Opacity = 0.55
            })
        });

        return host;
    }

    private static Color ColorFor(CircleColor color)
    {
        var hex = color switch
        {
            CircleColor.Green => Green,
            CircleColor.Red => Red,
            _ => Gray
        };

        return Color.FromRgb(hex.R, hex.G, hex.B);
    }

    /// <summary>Positive amounts move toward white, negative toward black.</summary>
    private static Color Shift(Color color, double amount)
    {
        static byte Blend(byte channel, double amount) => amount >= 0
            ? (byte)Math.Clamp(channel + (255 - channel) * amount, 0, 255)
            : (byte)Math.Clamp(channel * (1 + amount), 0, 255);

        return Color.FromRgb(Blend(color.R, amount), Blend(color.G, amount), Blend(color.B, amount));
    }

    private static double Luminance(Color color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255d;

    private static FontFamily ResolveFont(string name)
    {
        try
        {
            return new FontFamily(name);
        }
        catch (Exception)
        {
            return new FontFamily(JkMonSettings.DefaultFontFamily);
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = OverlayWindowInterop.GetHandle(this);
        OverlayWindowInterop.ApplyOverlayStyles(hwnd);
        OverlayWindowInterop.ApplyLayer(hwnd, _settings.Layer);

        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        ResizeAndReposition();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case OverlayWindowInterop.WmDpiChanged:
            case OverlayWindowInterop.WmDisplayChange:
            case OverlayWindowInterop.WmSettingChange:
                // The taskbar can move or change size, which redefines the work area.
                Dispatcher.BeginInvoke(ResizeAndReposition);
                break;

            case OverlayWindowInterop.WmWindowPosChanging:
                if (_settings.Layer == WindowLayer.Desktop)
                {
                    OverlayWindowInterop.PinToBottom(lParam);
                }

                break;
        }

        return IntPtr.Zero;
    }

    /// <summary>SizeToContent is unreliable for this layered transparent window, so the size is measured explicitly.</summary>
    private void ResizeAndReposition()
    {
        if (PresentationSource.FromVisual(this) is null)
        {
            return;
        }

        // Visuals are created in code, so force layout before trusting DesiredSize.
        Panel.UpdateLayout();
        Panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = Panel.DesiredSize;
        if (desired.Width > 0 && desired.Height > 0)
        {
            Width = Math.Ceiling(desired.Width);
            Height = Math.Ceiling(desired.Height);
        }

        Reposition();
    }

    private void Reposition()
    {
        var hwnd = OverlayWindowInterop.GetHandle(this);
        var (width, height) = OverlayWindowInterop.GetSize(hwnd);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var workArea = OverlayPlacement.WorkAreaFor(this, _settings.MonitorDeviceName);
        var (x, y) = PlacementMath.Bottom(workArea, width, height, _settings.MarginPixels, _settings.Position);
        OverlayWindowInterop.MoveTo(hwnd, x, y);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}



