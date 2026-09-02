using System.Drawing.Drawing2D;
using System.Drawing.Text;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;

namespace JKMon.App.Rendering;

/// <summary>
/// Draws the whole overlay with GDI+. This replaces what used to be a WPF visual tree: any WPF window creates a
/// Direct3D 9 device and keeps the display driver loaded, which stops a hybrid-graphics laptop from switching GPUs.
/// GDI touches no adapter at all.
/// </summary>
/// <remarks>
/// WPF sized everything in device independent pixels and scaled for DPI on its own. Here every size is multiplied
/// by <see cref="_scale"/> instead, so the overlay stays the same physical size on a scaled display.
/// </remarks>
internal sealed class OverlayRenderer : IDisposable
{
    private float PanelPaddingX => _theme.PanelPaddingX;

    private float PanelPaddingY => _theme.PanelPaddingY;

    private float PanelCorner => _theme.PanelCorner;

    /// <summary>Widest strings each formatter can produce; the value columns are sized to hold them.</summary>
    private static readonly string[] RateWidthSamples =
        ["1023 B/s", "9.9 KiB/s", "1023 KiB/s", "1023 MiB/s", "1023 GiB/s", "1023 TiB/s"];

    private const string UpArrow = "\u25B2";
    private const string DownArrow = "\u25BC";

    private const float StripeHeight = 4;

    private ThemeOverlay _theme = ThemeCatalog.OverlayFor(AppTheme.Dark);
    private Color _track;
    private Color _statusOk;
    private Color _statusBusy;
    private Color _statusUnknown;
    private Color[] _stripe = [];

    private readonly Bitmap _scratch = new(1, 1);
    private readonly Graphics _measure;

    private JkMonSettings _settings = new();
    private float _scale = 1;

    private Font _valueFont = null!;
    private Font _customTextFont = null!;
    private Font _activityLabelFont = null!;
    private Font _gaugeNumberFont = null!;
    private Font _gaugeLabelFont = null!;
    private Font _gaugeCaptionFont = null!;
    private Font _circleFont = null!;

    private Color _backgroundColor;
    private Color _textColor;
    private Color _cpuColor;
    private Color _memoryColor;
    private Color _outlineColor;
    private Color _customTextColor;

    private float _rowHeight;
    private float _rateWidth;
    private float _arrowWidth;
    private float _arrowGap;
    private float _columnGap;
    private float _gaugeHeight;
    private float _gaugeNumberWidth;
    private float _barWidth;
    private float _coreBarWidth;
    private float _outlineThickness;
    private float _circleDiameter;

    private Size _size;
    private OverlayModel? _model;

    internal OverlayRenderer()
    {
        _measure = Graphics.FromImage(_scratch);
        _measure.TextRenderingHint = TextRenderingHint.AntiAlias;
        ApplySettings(new JkMonSettings(), 1);
    }

    internal Size Size => _size;

    internal void ApplySettings(JkMonSettings settings, float scale)
    {
        _settings = settings.Normalized();
        _scale = scale <= 0 ? 1 : scale;
        _theme = ThemeCatalog.OverlayFor(_settings.Theme);

        _track = ColorOf(_theme.Track, "#66080B10");
        _statusOk = ColorOf(_theme.StatusOk, "#2ED160");
        _statusBusy = ColorOf(_theme.StatusBusy, "#E83B3B");
        _statusUnknown = ColorOf(_theme.StatusUnknown, "#9AA3AE");
        _stripe = StripeColors();

        DisposeFonts();

        var style = _settings.BoldText ? FontStyle.Bold : FontStyle.Regular;
        _valueFont = CreateFont(_settings.FontFamily, _settings.FontSize, style);
        _customTextFont = CreateFont(_settings.CustomTextFontFamily, _settings.CustomTextFontSize, style);

        var activitySize = Math.Max(JkMonSettings.MinGaugeLabelFontSize, Math.Round(_settings.FontSize * 0.72));
        _activityLabelFont = CreateFont(_settings.FontFamily, activitySize, FontStyle.Bold);

        _backgroundColor = Blend(_settings.BackgroundColor, JkMonSettings.DefaultBackgroundColor,
            (byte)Math.Round(_settings.BackgroundOpacityPercent * 255d / 100d));
        _textColor = ColorOf(_settings.TextColor, JkMonSettings.DefaultTextColor);
        _cpuColor = ColorOf(_settings.CpuGaugeColor, JkMonSettings.DefaultCpuGaugeColor);
        _memoryColor = ColorOf(_settings.MemoryGaugeColor, JkMonSettings.DefaultMemoryGaugeColor);
        _outlineColor = ColorOf(_settings.GaugeOutlineColor, JkMonSettings.DefaultGaugeOutlineColor);
        _customTextColor = ColorOf(_settings.CustomTextColor, JkMonSettings.DefaultCustomTextColor);

        _rowHeight = MeasureText("100%", _valueFont).Height;
        _rateWidth = (float)Math.Ceiling(RateWidthSamples.Max(s => MeasureText(s, _valueFont).Width));
        _arrowWidth = (float)Math.Ceiling(Math.Max(
            MeasureText(UpArrow, _valueFont).Width, MeasureText(DownArrow, _valueFont).Width));
        _arrowGap = (float)Math.Ceiling(Math.Ceiling(_settings.FontSize * 0.4) * 0.5) * _scale;
        _columnGap = (float)Math.Round(_settings.FontSize * 0.85) * _scale;

        _gaugeHeight = (float)Math.Ceiling(_rowHeight * 2);
        _outlineThickness = (float)_settings.GaugeOutlineThickness * _scale;
        _barWidth = (float)Math.Ceiling(_gaugeHeight * 0.5);
        _coreBarWidth = (float)Math.Max(5d, Math.Round(_settings.FontSize * 0.55)) * _scale;
        _circleDiameter = _settings.CircleDiameter * _scale;

        var numberSize = Math.Max(JkMonSettings.MinFontSize, Math.Round(_gaugeHeight / _scale * 0.62));
        _gaugeNumberFont = CreateFont(_settings.FontFamily, numberSize, FontStyle.Bold);
        _gaugeNumberWidth = (float)Math.Ceiling(MeasureText("100%", _gaugeNumberFont).Width);
        _gaugeLabelFont = CreateFont(_settings.FontFamily, _settings.GaugeLabelFontSize, FontStyle.Bold);
        _gaugeCaptionFont = CreateFont(_settings.FontFamily, _settings.GaugeCaptionFontSize, FontStyle.Bold);
        _circleFont = CreateFont(_settings.FontFamily, Math.Max(9, _settings.CircleDiameter * 0.5), FontStyle.Bold);
    }

    /// <summary>Recomputes the overlay size for the given readings. Circles are the only part that changes width.</summary>
    internal Size Layout(OverlayModel model)
    {
        _model = model;

        var content = ContentSize(model);
        var panel = new SizeF(content.Width + PanelPaddingX * 2 * _scale, content.Height + PanelPaddingY * 2 * _scale);

        var width = panel.Width;
        var height = panel.Height;

        if (_settings.HasCustomText)
        {
            var caption = MeasureText(_settings.CustomText, _customTextFont);
            width = Math.Max(width, caption.Width + PanelPaddingX * 2 * _scale);
            height += caption.Height + (float)Math.Round(_settings.CustomTextFontSize * 0.2) * _scale;
        }

        _size = new Size((int)Math.Ceiling(width), (int)Math.Ceiling(height));
        return _size;
    }

    internal void Paint(Graphics g)
    {
        if (_model is not { } model)
        {
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(Color.Transparent);

        var top = 0f;
        if (_settings.HasCustomText)
        {
            var caption = MeasureText(_settings.CustomText, _customTextFont);
            var inner = _size.Width - PanelPaddingX * 2 * _scale;
            var x = _settings.CustomTextAlignment switch
            {
                CaptionAlignment.Left => PanelPaddingX * _scale,
                CaptionAlignment.Right => PanelPaddingX * _scale + inner - caption.Width,
                _ => PanelPaddingX * _scale + (inner - caption.Width) / 2
            };

            DrawText(g, _settings.CustomText, _customTextFont, _customTextColor, x, top, _settings.CustomTextShadow);
            top += caption.Height + (float)Math.Round(_settings.CustomTextFontSize * 0.2) * _scale;
        }

        var content = ContentSize(model);
        var panel = new RectangleF(
            0, top,
            content.Width + PanelPaddingX * 2 * _scale,
            content.Height + PanelPaddingY * 2 * _scale);

        using (var background = new SolidBrush(_backgroundColor))
        using (var shape = RoundedRect(panel, PanelCorner * _scale))
        {
            g.FillPath(background, shape);
        }

        PaintAccentStripe(g, panel);
        PaintContent(g, model, panel.X + PanelPaddingX * _scale, panel.Y + PanelPaddingY * _scale, content.Height);
    }

    /// <summary>An optional band across the top edge of the panel. Off unless the user turns it on.</summary>
    private void PaintAccentStripe(Graphics g, RectangleF panel)
    {
        if (_stripe.Length == 0)
        {
            return;
        }

        var height = StripeHeight * _scale;
        var segment = panel.Width / _stripe.Length;

        for (var i = 0; i < _stripe.Length; i++)
        {
            using var brush = new SolidBrush(_stripe[i]);
            var width = i == _stripe.Length - 1 ? panel.Right - (panel.X + segment * i) : segment;
            g.FillRectangle(brush, panel.X + segment * i, panel.Y, width, height);
        }
    }

    private Color[] StripeColors() => _settings.AccentStripe switch
    {
        AccentStripeMode.Solid =>
            [ColorOf(_settings.AccentStripeFirstColor, JkMonSettings.DefaultAccentStripeFirstColor)],
        AccentStripeMode.Tricolour =>
        [
            ColorOf(_settings.AccentStripeFirstColor, JkMonSettings.DefaultAccentStripeFirstColor),
            ColorOf(_settings.AccentStripeSecondColor, JkMonSettings.DefaultAccentStripeSecondColor),
            ColorOf(_settings.AccentStripeThirdColor, JkMonSettings.DefaultAccentStripeThirdColor)
        ],
        _ => []
    };

    private SizeF ContentSize(OverlayModel model)
    {
        var cpu = CpuColumnSize(model);
        var readings = ReadingColumnSize();
        var circles = CirclesSize(model);

        var width = cpu.Width + _columnGap + readings.Width + _columnGap + readings.Width;
        if (circles.Width > 0)
        {
            width += (float)Math.Round(_settings.FontSize * 1.1) * _scale + circles.Width;
        }

        var height = Math.Max(Math.Max(cpu.Height, readings.Height), circles.Height);
        return new SizeF(width, height);
    }

    private SizeF CpuColumnSize(OverlayModel model)
    {
        var left = _settings.ShowIndividualCores
            ? new SizeF(CoreStripWidth(model.CorePercents.Count), _gaugeHeight)
            : GaugeSize(CpuGaugeKind());

        var memory = GaugeSize(MemoryGaugeKind());
        return new SizeF(left.Width + _columnGap + memory.Width, Math.Max(left.Height, memory.Height));
    }

    private float CoreStripWidth(int cores) =>
        cores <= 0 ? 0 : cores * _coreBarWidth + (cores - 1) * 2 * _scale;

    private enum GaugeKind { Number, Bar, Pie }

    private GaugeKind CpuGaugeKind() => _settings.CpuGauge == CpuGaugeStyle.Bar ? GaugeKind.Bar : GaugeKind.Number;

    private GaugeKind MemoryGaugeKind() => _settings.MemoryGauge switch
    {
        MemoryGaugeStyle.Bar => GaugeKind.Bar,
        MemoryGaugeStyle.Pie => GaugeKind.Pie,
        _ => GaugeKind.Number
    };

    /// <summary>A number gauge carries a caption above it; the graphical ones carry a percentage.</summary>
    private SizeF GaugeSize(GaugeKind kind)
    {
        if (kind == GaugeKind.Number)
        {
            var widest = Label("Memory");
            var caption = _settings.GaugeCaptionFontSize > 0
                ? MeasureText(widest, _gaugeCaptionFont).Height
                : 0;

            return new SizeF(
                Math.Max(_gaugeNumberWidth, MeasureText(widest, _gaugeCaptionFont).Width),
                caption + MeasureText("100%", _gaugeNumberFont).Height);
        }

        var label = MeasureText("100", _gaugeLabelFont);
        var body = kind == GaugeKind.Pie ? _gaugeHeight : _barWidth;
        return new SizeF(Math.Max(body, label.Width), label.Height + _gaugeHeight);
    }

    private SizeF ReadingColumnSize()
    {
        var row = _rateWidth + _arrowGap + _arrowWidth;
        var height = _rowHeight * 2;
        if (_settings.ShowActivityBars)
        {
            height += 3 * _scale + Math.Max(3 * _scale, MeasureText(Label("Disk"), _activityLabelFont).Height);
        }

        return new SizeF(row, height);
    }

    private SizeF CirclesSize(OverlayModel model)
    {
        if (model.Circles.Count == 0)
        {
            return SizeF.Empty;
        }

        var barHeight = Math.Max(3 * _scale, (float)Math.Round(_circleDiameter * 0.2));
        var gap = Math.Max(2 * _scale, (float)Math.Round(_circleDiameter * 0.1));
        var width = model.Circles.Count * (_circleDiameter + 6 * _scale) - 6 * _scale;
        return new SizeF(width, _circleDiameter + gap + barHeight);
    }

    private void PaintContent(Graphics g, OverlayModel model, float x, float y, float height)
    {
        var cpuSize = CpuColumnSize(model);
        PaintCpuColumn(g, model, x, y + (height - cpuSize.Height) / 2);
        x += cpuSize.Width + _columnGap;

        var readings = ReadingColumnSize();
        PaintReadingColumn(g, x, y + (height - readings.Height) / 2, Label("Net"),
            model.NetworkOut, model.NetworkIn, model.NetworkLevel);
        x += readings.Width + _columnGap;

        PaintReadingColumn(g, x, y + (height - readings.Height) / 2, Label("Disk"),
            model.DiskRead, model.DiskWrite, model.DiskLevel);
        x += readings.Width;

        if (model.Circles.Count == 0)
        {
            return;
        }

        x += (float)Math.Round(_settings.FontSize * 1.1) * _scale;
        var circles = CirclesSize(model);
        var circleTop = y + (height - circles.Height) / 2;
        foreach (var circle in model.Circles)
        {
            PaintIndicator(g, circle, x, circleTop);
            x += _circleDiameter + 6 * _scale;
        }
    }

    private void PaintCpuColumn(Graphics g, OverlayModel model, float x, float y)
    {
        var size = CpuColumnSize(model);

        if (_settings.ShowIndividualCores)
        {
            var stripWidth = CoreStripWidth(model.CorePercents.Count);
            var barX = x;
            foreach (var percent in model.CorePercents)
            {
                PaintBar(g, barX, y + size.Height - _gaugeHeight, _coreBarWidth, _gaugeHeight, percent,
                    _cpuColor, Math.Min(1 * _scale, _outlineThickness));
                barX += _coreBarWidth + 2 * _scale;
            }

            x += stripWidth;
        }
        else
        {
            var cpu = GaugeSize(CpuGaugeKind());
            PaintGauge(g, CpuGaugeKind(), x, y + (size.Height - cpu.Height) / 2, model.CpuPercent, model.Cpu,
                _cpuColor, Label("CPU"));
            x += cpu.Width;
        }

        var memory = GaugeSize(MemoryGaugeKind());
        PaintGauge(g, MemoryGaugeKind(), x + _columnGap, y + (size.Height - memory.Height) / 2,
            model.MemoryPercent, model.Memory, _memoryColor, Label("Memory"));
    }

    private void PaintGauge(
        Graphics g, GaugeKind kind, float x, float y, double percent, string text, Color fill, string caption)
    {
        var size = GaugeSize(kind);

        if (kind == GaugeKind.Number)
        {
            if (_settings.GaugeCaptionFontSize > 0)
            {
                DrawText(g, caption, _gaugeCaptionFont, _textColor, x, y, _settings.TextShadow);
                y += MeasureText(caption, _gaugeCaptionFont).Height;
            }

            DrawText(g, text, _gaugeNumberFont, fill, x, y, _settings.TextShadow);
            return;
        }

        var label = Rounded(percent);
        var labelSize = MeasureText(label, _gaugeLabelFont);
        DrawText(g, label, _gaugeLabelFont, fill, x + (size.Width - labelSize.Width) / 2, y, _settings.TextShadow);
        y += labelSize.Height;

        var bodyX = x + (size.Width - (kind == GaugeKind.Pie ? _gaugeHeight : _barWidth)) / 2;
        if (kind == GaugeKind.Pie)
        {
            PaintPie(g, bodyX, y, percent, fill);
        }
        else
        {
            PaintBar(g, bodyX, y, _barWidth, _gaugeHeight, percent, fill, _outlineThickness);
        }
    }

    /// <summary>The unfilled part of a gauge, dark enough to read the fill against any wallpaper.</summary>
    private Color Track => _track;

    private void PaintBar(Graphics g, float x, float y, float width, float height, double percent, Color fill, float thickness)
    {
        var frame = new RectangleF(x, y, width, height);
        using (var track = new SolidBrush(Track))
        {
            g.FillRectangle(track, frame);
        }

        var inner = Math.Max(0f, height - thickness * 2);
        var filled = inner * (float)Fraction(percent);
        if (filled > 0)
        {
            using var brush = new SolidBrush(fill);
            g.FillRectangle(brush, x + thickness, y + thickness + (inner - filled), width - thickness * 2, filled);
        }

        if (thickness > 0)
        {
            using var pen = new Pen(_outlineColor, thickness);
            g.DrawRectangle(pen, x + thickness / 2, y + thickness / 2, width - thickness, height - thickness);
        }
    }

    private void PaintPie(Graphics g, float x, float y, double percent, Color fill)
    {
        var box = new RectangleF(x, y, _gaugeHeight, _gaugeHeight);
        using (var track = new SolidBrush(Track))
        {
            g.FillEllipse(track, box);
        }

        var fraction = Fraction(percent);
        if (fraction > 0)
        {
            var inset = _outlineThickness;
            var slice = new RectangleF(x + inset, y + inset, _gaugeHeight - inset * 2, _gaugeHeight - inset * 2);
            using var brush = new SolidBrush(fill);
            // Sweeps clockwise from twelve o'clock. A full turn would draw nothing, so it stops just short.
            g.FillPie(brush, slice, -90, (float)(Math.Min(fraction, 0.9999) * 360));
        }

        if (_outlineThickness > 0)
        {
            using var pen = new Pen(_outlineColor, _outlineThickness);
            g.DrawEllipse(pen, x + _outlineThickness / 2, y + _outlineThickness / 2,
                _gaugeHeight - _outlineThickness, _gaugeHeight - _outlineThickness);
        }
    }

    private void PaintReadingColumn(
        Graphics g, float x, float y, string name, string first, string second, ActivityLevel level)
    {
        var color = ColorOf(ColorFor(level), JkMonSettings.DefaultActivityIdleColor);

        DrawRateRow(g, x, y, first, UpArrow, color);
        DrawRateRow(g, x, y + _rowHeight, second, DownArrow, color);

        if (!_settings.ShowActivityBars)
        {
            return;
        }

        var labelSize = MeasureText(name, _activityLabelFont);
        var rowY = y + _rowHeight * 2 + 3 * _scale;
        var rowHeight = Math.Max(3 * _scale, labelSize.Height);
        var barWidth = _rateWidth + _arrowGap + _arrowWidth - labelSize.Width - 5 * _scale;
        var barHeight = 3 * _scale;

        if (barWidth > 0)
        {
            using var brush = new SolidBrush(color);
            var box = new RectangleF(x, rowY + (rowHeight - barHeight) / 2, barWidth, barHeight);
            using var shape = RoundedRect(box, _theme.RoundedBars ? 2 * _scale : 0);
            g.FillPath(brush, shape);
        }

        DrawText(g, name, _activityLabelFont, _textColor,
            x + Math.Max(0, barWidth) + 5 * _scale, rowY + (rowHeight - labelSize.Height) / 2, _settings.TextShadow);
    }

    private void DrawRateRow(Graphics g, float x, float y, string value, string arrow, Color color)
    {
        var size = MeasureText(value, _valueFont);
        DrawText(g, value, _valueFont, color, x + _rateWidth - size.Width, y, _settings.TextShadow);
        DrawText(g, arrow, _valueFont, color, x + _rateWidth + _arrowGap, y, _settings.TextShadow);
    }

    private void PaintIndicator(Graphics g, SyncCircle circle, float x, float y)
    {
        var icon = ProviderIconResolver.Resolve(circle.ProviderId, (int)Math.Round(_circleDiameter));
        if (icon is null)
        {
            PaintCircle(g, circle, x, y);
        }
        else
        {
            g.DrawImage(icon, new RectangleF(x, y, _circleDiameter, _circleDiameter));
        }

        var barHeight = Math.Max(3 * _scale, (float)Math.Round(_circleDiameter * 0.2));
        var gap = Math.Max(2 * _scale, (float)Math.Round(_circleDiameter * 0.1));
        PaintStatusBar(g, circle.Color, x, y + _circleDiameter + gap, _circleDiameter, barHeight);
    }

    private void PaintStatusBar(Graphics g, CircleColor color, float x, float y, float width, float height)
    {
        var baseColor = ColorFor(color);
        var box = new RectangleF(x, y, width, height);

        using (var shape = RoundedRect(box, _theme.RoundedBars ? height / 2 : 0))
        using (var fill = new LinearGradientBrush(
            new RectangleF(x, y - 1, width, height + 2), Shift(baseColor, 0.4), Shift(baseColor, -0.25), 90f))
        {
            g.FillPath(fill, shape);
            using var pen = new Pen(Shift(baseColor, -0.5), Math.Max(0.5f, width / 40f));
            g.DrawPath(pen, shape);
        }
    }

    /// <summary>A four stop radial fill and a soft gloss sell the raised look without needing a bitmap.</summary>
    private void PaintCircle(Graphics g, SyncCircle circle, float x, float y)
    {
        var baseColor = ColorFor(circle.Color);
        var box = new RectangleF(x, y, _circleDiameter, _circleDiameter);

        using (var path = new GraphicsPath())
        {
            path.AddEllipse(box);
            using var fill = new PathGradientBrush(path)
            {
                CenterPoint = new PointF(x + _circleDiameter * 0.35f, y + _circleDiameter * 0.30f),
                CenterColor = Shift(baseColor, 0.55),
                SurroundColors = [Shift(baseColor, -0.45)]
            };

            // PathGradientBrush positions run from the boundary (0) inwards, the reverse of the WPF stops.
            fill.InterpolationColors = new ColorBlend
            {
                Colors = [Shift(baseColor, -0.45), Shift(baseColor, -0.28), Shift(baseColor, 0.12), Shift(baseColor, 0.55)],
                Positions = [0f, 0.08f, 0.55f, 1f]
            };

            g.FillPath(fill, path);
        }

        using (var pen = new Pen(Shift(baseColor, -0.55), Math.Max(1f, _circleDiameter / 26f)))
        {
            g.DrawEllipse(pen, box);
        }

        var glossBox = new RectangleF(
            x + _circleDiameter * 0.19f, y + _circleDiameter * 0.08f,
            _circleDiameter * 0.62f, _circleDiameter * 0.40f);

        using (var glossPath = new GraphicsPath())
        {
            glossPath.AddEllipse(glossBox);
            using var gloss = new PathGradientBrush(glossPath)
            {
                CenterColor = Color.FromArgb(0x8C, 255, 255, 255),
                SurroundColors = [Color.FromArgb(0, 255, 255, 255)]
            };

            g.FillPath(gloss, glossPath);
        }

        var initial = circle.Initial.ToString();
        var size = MeasureText(initial, _circleFont);
        DrawText(g, initial, _circleFont,
            Luminance(baseColor) > 0.6 ? Color.FromArgb(0x10, 0x14, 0x18) : Color.White,
            x + (_circleDiameter - size.Width) / 2, y + (_circleDiameter - size.Height) / 2, shadow: true);
    }

    /// <summary>
    /// GDI+ has no blur, so the shadow is a single offset copy. At these sizes it reads the same as the blurred
    /// WPF effect it replaces and costs one extra draw.
    /// </summary>
    private void DrawText(Graphics g, string text, Font font, Color color, float x, float y, bool shadow)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (shadow)
        {
            using var dark = new SolidBrush(Color.FromArgb(0xD9, 0, 0, 0));
            g.DrawString(text, font, dark, x + _scale, y + _scale, StringFormat.GenericTypographic);
        }

        using var brush = new SolidBrush(color);
        g.DrawString(text, font, brush, x, y, StringFormat.GenericTypographic);
    }

    /// <summary>One theme sets its labels in caps; the strings are otherwise identical.</summary>
    private string Label(string text) => _theme.UppercaseLabels ? text.ToUpperInvariant() : text;

    private SizeF MeasureText(string text, Font font) => string.IsNullOrEmpty(text)
        ? SizeF.Empty
        : _measure.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic);

    private Font CreateFont(string family, double size, FontStyle style)
    {
        var points = (float)Math.Max(1, size) * _scale;
        try
        {
            return new Font(family, points, style, GraphicsUnit.Pixel);
        }
        catch (Exception)
        {
            return new Font(JkMonSettings.DefaultFontFamily, points, style, GraphicsUnit.Pixel);
        }
    }

    private string ColorFor(ActivityLevel level) => level switch
    {
        ActivityLevel.Normal => _settings.ActivityNormalColor,
        ActivityLevel.Elevated => _settings.ActivityElevatedColor,
        ActivityLevel.High => _settings.ActivityHighColor,
        _ => _settings.ActivityIdleColor
    };

    private Color ColorFor(CircleColor color) => color switch
    {
        CircleColor.Green => _statusOk,
        CircleColor.Red => _statusBusy,
        _ => _statusUnknown
    };

    private static Color ColorOf(string value, string fallback)
    {
        var color = HexColor.ParseOrDefault(value, HexColor.ParseOrDefault(fallback, new HexColor(255, 255, 255, 255)));
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    private static Color Blend(string value, string fallback, byte alpha)
    {
        var color = ColorOf(value, fallback);
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    /// <summary>Positive amounts move toward white, negative toward black.</summary>
    private static Color Shift(Color color, double amount)
    {
        static byte Channel(byte value, double amount) => amount >= 0
            ? (byte)Math.Clamp(value + (255 - value) * amount, 0, 255)
            : (byte)Math.Clamp(value * (1 + amount), 0, 255);

        return Color.FromArgb(Channel(color.R, amount), Channel(color.G, amount), Channel(color.B, amount));
    }

    private static double Luminance(Color color) =>
        (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255d;

    private static double Fraction(double percent) =>
        double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) / 100d : 0d;

    private static string Rounded(double percent) => double.IsFinite(percent)
        ? ((int)Math.Round(Math.Clamp(percent, 0, 100))).ToString(System.Globalization.CultureInfo.InvariantCulture)
        : "0";

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void DisposeFonts()
    {
        _valueFont?.Dispose();
        _customTextFont?.Dispose();
        _activityLabelFont?.Dispose();
        _gaugeNumberFont?.Dispose();
        _gaugeLabelFont?.Dispose();
        _gaugeCaptionFont?.Dispose();
        _circleFont?.Dispose();
    }

    public void Dispose()
    {
        DisposeFonts();
        _measure.Dispose();
        _scratch.Dispose();
    }
}
