using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

// File-level aliases beat the GDI+ imports that UseWindowsForms brings in.
using Color = System.Windows.Media.Color;
using Orientation = System.Windows.Controls.Orientation;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace JKMon.App;

/// <summary>Shared chrome for every gauge so a style change never desynchronises colours or metrics.</summary>
internal sealed record GaugeChrome(
    Brush Fill,
    Brush Outline,
    FontFamily Font,
    FontWeight Weight,
    double Height,
    Effect? Shadow)
{
    /// <summary>Outline width. Core bars override it because a full-thickness outline would hide their fill.</summary>
    internal double Thickness { get; init; } = Math.Max(1d, Math.Round(Height / 18d));

    /// <summary>The unfilled part of a gauge, dark enough to read the fill against any wallpaper.</summary>
    internal Brush Track { get; } = Freeze(new SolidColorBrush(Color.FromArgb(0x66, 0x08, 0x0B, 0x10)));

    internal static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}

/// <summary>A percentage readout the overlay can swap between text, bar and pie presentations.</summary>
internal interface IGauge
{
    FrameworkElement Element { get; }

    void Set(double percent, string text);
}

/// <summary>The percentage on its own: no label, sized to fill the panel height.</summary>
internal sealed class NumberGauge : IGauge
{
    private readonly TextBlock _text;

    internal NumberGauge(GaugeChrome chrome, double fontSize, double width)
    {
        _text = new TextBlock
        {
            FontFamily = chrome.Font,
            FontWeight = FontWeights.Bold,
            FontSize = fontSize,
            Foreground = chrome.Fill,
            Effect = chrome.Shadow,
            Width = width,
            // Left aligned so the digits start at a fixed x that a caption above can line up with. Right alignment
            // would move the first digit whenever the value changed width.
            TextAlignment = TextAlignment.Left,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public FrameworkElement Element => _text;

    public void Set(double percent, string text) => _text.Text = text;
}

/// <summary>A vertical bar that fills from the bottom, outlined in its own colour.</summary>
internal sealed class BarGauge : IGauge
{
    private readonly Border _frame;
    private readonly Rectangle _fill;
    private readonly double _inner;

    internal BarGauge(GaugeChrome chrome, double width)
    {
        var thickness = chrome.Thickness;
        _inner = Math.Max(0d, chrome.Height - thickness * 2);

        _fill = new Rectangle
        {
            Fill = chrome.Fill,
            Height = 0,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        _frame = new Border
        {
            Width = width,
            Height = chrome.Height,
            Background = chrome.Track,
            BorderBrush = chrome.Outline,
            BorderThickness = new Thickness(thickness),
            Effect = chrome.Shadow,
            Child = _fill
        };
    }

    public FrameworkElement Element => _frame;

    public void Set(double percent, string text) => _fill.Height = _inner * Fraction(percent);

    internal static double Fraction(double percent) =>
        double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) / 100d : 0d;
}

/// <summary>A pie that sweeps clockwise from twelve o'clock.</summary>
internal sealed class PieGauge : IGauge
{
    private readonly Grid _host;
    private readonly Path _slice;
    private readonly double _radius;
    private readonly Point _centre;

    internal PieGauge(GaugeChrome chrome)
    {
        var size = chrome.Height;
        var thickness = chrome.Thickness;
        _radius = Math.Max(0d, size / 2d - thickness);
        _centre = new Point(size / 2d, size / 2d);

        _slice = new Path { Fill = chrome.Fill };

        _host = new Grid
        {
            Width = size,
            Height = size,
            Effect = chrome.Shadow
        };

        _host.Children.Add(new Ellipse { Fill = chrome.Track });
        _host.Children.Add(_slice);
        _host.Children.Add(new Ellipse
        {
            Stroke = chrome.Outline,
            StrokeThickness = thickness
        });
    }

    public FrameworkElement Element => _host;

    public void Set(double percent, string text)
    {
        var fraction = BarGauge.Fraction(percent);
        if (fraction <= 0 || _radius <= 0)
        {
            _slice.Data = null;
            return;
        }

        // A full sweep would put the arc back on its start point, which draws nothing.
        var angle = Math.Min(fraction, 0.9999) * 2 * Math.PI;
        var start = new Point(_centre.X, _centre.Y - _radius);
        var end = new Point(
            _centre.X + _radius * Math.Sin(angle),
            _centre.Y - _radius * Math.Cos(angle));

        var figure = new PathFigure { StartPoint = _centre, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment(start, false));
        figure.Segments.Add(new ArcSegment(
            end,
            new Size(_radius, _radius),
            0,
            angle > Math.PI,
            SweepDirection.Clockwise,
            false));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        _slice.Data = GaugeChrome.Freeze(geometry);
    }
}

/// <summary>
/// Puts a small percentage above a graphical gauge. The unit is left off because the shape already says it is a
/// percentage, and the digits have to stay narrow enough not to widen the column.
/// </summary>
internal sealed class LabelledGauge : IGauge
{
    private readonly StackPanel _host;
    private readonly TextBlock _value;
    private readonly IGauge _inner;

    internal LabelledGauge(IGauge inner, GaugeChrome chrome, double fontSize)
    {
        _inner = inner;

        _value = new TextBlock
        {
            FontFamily = chrome.Font,
            FontWeight = FontWeights.Bold,
            FontSize = fontSize,
            Foreground = chrome.Fill,
            Effect = chrome.Shadow,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap
        };

        _host = new StackPanel { Orientation = Orientation.Vertical };
        _host.Children.Add(_value);
        _host.Children.Add(inner.Element);
    }

    public FrameworkElement Element => _host;

    public void Set(double percent, string text)
    {
        _value.Text = Rounded(percent);
        _inner.Set(percent, text);
    }

    private static string Rounded(double percent) => double.IsFinite(percent)
        ? ((int)Math.Round(Math.Clamp(percent, 0, 100))).ToString(CultureInfo.InvariantCulture)
        : "0";
}

/// <summary>Names the metric above a gauge whose shape does not already identify it.</summary>
internal sealed class CaptionedGauge : IGauge
{
    private readonly StackPanel _host;
    private readonly IGauge _inner;

    internal CaptionedGauge(IGauge inner, GaugeChrome chrome, string caption, double fontSize, Brush foreground)
    {
        _inner = inner;

        var text = new TextBlock
        {
            Text = caption,
            FontFamily = chrome.Font,
            FontWeight = FontWeights.Bold,
            FontSize = fontSize,
            Foreground = foreground,
            Effect = chrome.Shadow,
            TextAlignment = TextAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Left,
            TextWrapping = TextWrapping.NoWrap
        };

        // Both edges start at the same x, which is the only way the caption tracks the value below it.
        inner.Element.HorizontalAlignment = HorizontalAlignment.Left;

        _host = new StackPanel { Orientation = Orientation.Vertical };
        _host.Children.Add(text);
        _host.Children.Add(inner.Element);
    }

    public FrameworkElement Element => _host;

    public void Set(double percent, string text) => _inner.Set(percent, text);
}

/// <summary>One slim bar per logical processor.</summary>
internal sealed class CoreBarStrip
{
    private readonly StackPanel _host;
    private readonly GaugeChrome _chrome;
    private readonly double _width;
    private readonly List<BarGauge> _bars = [];

    internal CoreBarStrip(StackPanel host, GaugeChrome chrome, double width)
    {
        _host = host;
        // A full-thickness outline would leave no room for the fill in a bar this narrow.
        _chrome = chrome with { Thickness = Math.Min(1d, chrome.Thickness) };
        _width = width;
        _host.Children.Clear();
    }

    internal void Set(IReadOnlyList<double> percents)
    {
        if (_bars.Count != percents.Count)
        {
            Rebuild(percents.Count);
        }

        for (var i = 0; i < percents.Count; i++)
        {
            _bars[i].Set(percents[i], string.Empty);
        }
    }

    private void Rebuild(int count)
    {
        _host.Children.Clear();
        _bars.Clear();

        for (var i = 0; i < count; i++)
        {
            var bar = new BarGauge(_chrome, _width);
            bar.Element.Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0);
            _bars.Add(bar);
            _host.Children.Add(bar.Element);
        }
    }
}
