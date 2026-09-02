using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using JKMon.App.Interop;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;
using JKMon.Core.Sync;
using JKMon.Core.Update;

namespace JKMon.App;

/// <summary>
/// The settings window. It is WinForms rather than WPF for the same reason the overlay is: a WPF window loads the
/// display driver for the lifetime of the process, which blocks GPU switching on hybrid-graphics laptops.
/// </summary>
internal sealed class SettingsForm : Form
{
    private const string AutomaticMonitor = "Automatic (follow window)";

    // The palette arrives with the theme, so controls are created bare and coloured by Skin once it is known.
    private readonly ThemeChrome _chrome;
    private readonly Color Surface;
    private readonly Color SecondarySurface;
    private readonly Color Field;
    private readonly Color Hairline;
    private readonly Color Ink;
    private readonly Color Muted;
    private readonly Color Accent;

    private readonly List<string> _monitorDevices = [];
    private readonly List<string> _providerOrder = [];

    /// <summary>Empty until the first refresh reports which clients are running, which hides the list rather than
    /// showing providers the machine does not have.</summary>
    private List<string> _presentProviders = [];

    private int[] _customColors = [];
    private DateTimeOffset _lastUpdateCheckUtc;
    private bool _loading;

    private TableLayoutPanel _columns = null!;
    private FlowLayoutPanel _left = null!;
    private FlowLayoutPanel _right = null!;

    private readonly TextBox _customText = Input(JkMonSettings.MaxCustomTextLength);
    private readonly ComboBox _customFont = Picker();
    private readonly Slider _customSize = new(9, 72, 1);
    private readonly TextBox _customColor = Input(9);
    private readonly ChromeButton _customColorSwatch = Swatch();
    private readonly RadioButton _captionLeft = new ThemedRadioButton { Text = "Left", AutoSize = true };
    private readonly RadioButton _captionCenter = new ThemedRadioButton { Text = "Center", AutoSize = true };
    private readonly RadioButton _captionRight = new ThemedRadioButton { Text = "Right", AutoSize = true };
    private readonly CheckBox _captionShadow = new ThemedCheckBox { Text = "Shadow", AutoSize = true };

    private readonly TextBox _textColor = Input(9);
    private readonly ChromeButton _textColorSwatch = Swatch();
    private readonly TextBox _backColor = Input(9);
    private readonly ChromeButton _backColorSwatch = Swatch();

    private readonly RadioButton _cpuNumber = new ThemedRadioButton { Text = "Number", AutoSize = true };
    private readonly RadioButton _cpuBar = new ThemedRadioButton { Text = "Bar", AutoSize = true };
    private readonly RadioButton _memoryNumber = new ThemedRadioButton { Text = "Number", AutoSize = true };
    private readonly RadioButton _memoryBar = new ThemedRadioButton { Text = "Bar", AutoSize = true };
    private readonly RadioButton _memoryPie = new ThemedRadioButton { Text = "Pie", AutoSize = true };
    private readonly CheckBox _cores = new ThemedCheckBox { Text = "Show every core", AutoSize = true };
    private readonly TextBox _outlineColor = Input(9);
    private readonly ChromeButton _outlineColorSwatch = Swatch();
    private readonly Slider _outlineWidth = new(0, 6, 0.5);
    private readonly Slider _labelSize = new(6, 32, 1);
    private readonly Slider _captionSize = new(0, 32, 1);
    private readonly TextBox _cpuGaugeColor = Input(9);
    private readonly ChromeButton _cpuGaugeColorSwatch = Swatch();
    private readonly TextBox _memoryGaugeColor = Input(9);
    private readonly ChromeButton _memoryGaugeColorSwatch = Swatch();

    private readonly ListBox _order = new()
    {
        BorderStyle = BorderStyle.FixedSingle
    };

    private readonly Label _noProviders = new()
    {
        Text = "동기화 클라이언트를 찾지 못했습니다.",
        AutoSize = true
    };

    private ChromeButton _orderUp = null!;
    private ChromeButton _orderDown = null!;

    private readonly CheckBox _activityBars = new ThemedCheckBox { Text = "Show activity bars", AutoSize = true };
    private readonly TextBox _activityIdleColor = Input(9);
    private readonly ChromeButton _activityIdleSwatch = Swatch();
    private readonly TextBox _activityNormalColor = Input(9);
    private readonly ChromeButton _activityNormalSwatch = Swatch();
    private readonly TextBox _activityElevatedColor = Input(9);
    private readonly ChromeButton _activityElevatedSwatch = Swatch();
    private readonly TextBox _activityHighColor = Input(9);
    private readonly ChromeButton _activityHighSwatch = Swatch();
    private readonly TextBox _netFirst = Input(12);
    private readonly TextBox _netSecond = Input(12);
    private readonly TextBox _diskFirst = Input(12);
    private readonly TextBox _diskSecond = Input(12);
    private readonly List<Control> _activityRows = [];
    private readonly ToolTip _tips = new() { AutoPopDelay = 12000 };

    private readonly Slider _opacity = new(0, 100, 5);
    private readonly ComboBox _font = Picker();
    private readonly Slider _fontSize = new(9, 32, 1);
    private readonly Slider _circle = new(16, 64, 2);
    private readonly CheckBox _bold = new ThemedCheckBox { Text = "Bold", AutoSize = true };
    private readonly CheckBox _shadow = new ThemedCheckBox { Text = "Shadow", AutoSize = true };

    private readonly Slider _refresh = new(1, 10, 1, "s");
    private readonly Slider _margin = new(0, 120, 2);
    private readonly ComboBox _monitor = Picker();
    private readonly RadioButton _bottomLeft = new ThemedRadioButton { Text = "Left", AutoSize = true };
    private readonly RadioButton _bottomCenter = new ThemedRadioButton { Text = "Center", AutoSize = true };
    private readonly RadioButton _bottomRight = new ThemedRadioButton { Text = "Right", AutoSize = true };
    private readonly RadioButton _desktop = new ThemedRadioButton { Text = "Pin to desktop", AutoSize = true };
    private readonly RadioButton _top = new ThemedRadioButton { Text = "Always on top", AutoSize = true };
    private readonly CheckBox _startup = new ThemedCheckBox { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _hideOnHover = new ThemedCheckBox { Text = "Hide when the pointer is over it", AutoSize = true };
    private readonly CheckBox _fullscreen = new ThemedCheckBox { Text = "Pause while a full-screen app is in front", AutoSize = true };
    private readonly RadioButton _updateNever = new ThemedRadioButton { Text = "Never", AutoSize = true };
    private readonly RadioButton _updateDaily = new ThemedRadioButton { Text = "Daily", AutoSize = true };
    private readonly RadioButton _updateWeekly = new ThemedRadioButton { Text = "Weekly", AutoSize = true };
    private readonly CheckBox _updateStartup = new ThemedCheckBox { Text = "Also check at startup", AutoSize = true };

    private readonly RadioButton _themeLight = new ThemedRadioButton { Text = "Light", AutoSize = true };
    private readonly RadioButton _themeDark = new ThemedRadioButton { Text = "Dark", AutoSize = true };

    private readonly RadioButton _stripeNone = new ThemedRadioButton { Text = "None", AutoSize = true };
    private readonly RadioButton _stripeSolid = new ThemedRadioButton { Text = "Solid", AutoSize = true };
    private readonly RadioButton _stripeTricolour = new ThemedRadioButton { Text = "Tricolour", AutoSize = true };
    private readonly TextBox _stripeFirst = Input(9);
    private readonly ChromeButton _stripeFirstSwatch = Swatch();
    private readonly TextBox _stripeSecond = Input(9);
    private readonly ChromeButton _stripeSecondSwatch = Swatch();
    private readonly TextBox _stripeThird = Input(9);
    private readonly ChromeButton _stripeThirdSwatch = Swatch();
    private readonly List<Control> _stripeColourRows = [];
    private readonly StripeBand _stripeBand = new() { Dock = DockStyle.Top, Height = 4 };

    private readonly ThemePresetStore _presets = new();
    private readonly ComboBox _savedThemes = Picker();
    private ChromeButton _loadTheme = null!;
    private ChromeButton _deleteTheme = null!;

    private AppTheme _theme;

    internal SettingsForm(JkMonSettings settings)
    {
        _chrome = ThemeCatalog.ChromeFor(settings.Normalized().Theme);
        Surface = ColorOf(_chrome.Surface);
        SecondarySurface = ColorOf(_chrome.SecondarySurface);
        Field = ColorOf(_chrome.Field);
        Hairline = ColorOf(_chrome.Hairline);
        Ink = ColorOf(_chrome.Ink);
        Muted = ColorOf(_chrome.Muted);
        Accent = ColorOf(_chrome.Accent);

        Text = "JKMon";
        Icon = AppIcon.Value;
        BackColor = Surface;
        ForeColor = Ink;
        Font = new Font(_chrome.BodyFont, _chrome.BodyFontSize);
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        MinimizeBox = false;
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(1020, 820);
        MinimumSize = new Size(760, 520);

        BuildLayout();
        Skin();

        // Swatches and lists carry no text of their own, so they are sized from the font to follow DPI scaling.
        var unit = Font.Height;
        foreach (var swatch in Swatches)
        {
            swatch.Size = new Size(unit * 2, unit + 6);
        }

        _order.Height = unit * 5;
        _order.Width = unit * 12;

        // Field widths follow the font too: a fixed pixel width clips the last hex digit once the type scales.
        foreach (var box in Fields)
        {
            var sample = box.MaxLength switch
            {
                <= 9 => "#FFFFFFFF",
                <= 12 => "1048576000",
                _ => new string('n', 26)
            };

            box.Width = TextRenderer.MeasureText(sample, box.Font).Width + 8;
        }

        foreach (var picker in Pickers)
        {
            if (picker.Parent is Panel host)
            {
                host.Size = new Size(unit * 13, picker.PreferredHeight + 2);
            }
        }

        foreach (var button in Descendants(this).OfType<ChromeButton>())
        {
            button.Size = button.Measured();
        }

        foreach (var slider in Descendants(this).OfType<Slider>())
        {
            slider.Rescale(Muted);
        }

        var families = FontFamily.Families
            .Select(f => f.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _font.Items.AddRange(families);
        _customFont.Items.AddRange(families);

        LoadMonitors();
        LoadFrom(settings);
        ShowSavedThemes(null);
        Wire();
        SizeToContent();
    }

    /// <summary>Colours every control the theme owns. Controls are created bare so this is the only place palette lands.</summary>
    private void Skin()
    {
        foreach (var control in Descendants(this))
        {
            switch (control)
            {
                case TextBox box:
                    box.BackColor = Field;
                    box.ForeColor = Ink;
                    break;

                case ComboBox picker:
                    picker.BackColor = Field;
                    picker.ForeColor = Ink;
                    break;

                case ListBox list:
                    list.BackColor = Field;
                    list.ForeColor = Ink;
                    break;

                case ThemedLabel label:
                    label.ForeColor = Ink;
                    label.MutedColor = Muted;
                    break;

                case ThemedCheckBox check:
                    check.ForeColor = Ink;
                    check.MutedColor = Muted;
                    break;

                case ThemedRadioButton radio:
                    radio.ForeColor = Ink;
                    radio.MutedColor = Muted;
                    break;
            }
        }

        foreach (var swatch in Swatches)
        {
            swatch.Radius = _chrome.PillButtons ? 8 : 0;
            swatch.BorderColor = Hairline;
            swatch.MutedColor = Muted;
        }

        _noProviders.ForeColor = Muted;
    }

    private static Color ColorOf(string value)
    {
        var color = HexColor.ParseOrDefault(value, new HexColor(255, 0, 0, 0));
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>
    /// Opens at the width the two columns actually need. The column widths depend on the font, so they are only
    /// known once the controls exist; a fixed default width left a horizontal scrollbar on some scalings.
    /// </summary>
    private void SizeToContent()
    {
        var work = Screen.FromControl(this).WorkingArea;
        var content = _left.PreferredSize.Width + _left.Margin.Horizontal
            + _right.PreferredSize.Width + _right.Margin.Horizontal
            + _columns.Padding.Horizontal;

        // The column stack is taller than any screen, so the vertical scrollbar is always there and takes width.
        var chrome = Width - ClientSize.Width + SystemInformation.VerticalScrollBarWidth;

        Size = new Size(
            Math.Min(content + chrome, work.Width),
            Math.Min(Height, work.Height));

        if (Left < work.Left || Top < work.Top || Right > work.Right || Bottom > work.Bottom)
        {
            Location = new Point(
                work.Left + Math.Max(0, (work.Width - Width) / 2),
                work.Top + Math.Max(0, (work.Height - Height) / 2));
        }
    }

    /// <summary>Raised on every change so the overlay previews the design immediately.</summary>
    internal event Action<JkMonSettings>? SettingsChanged;

    /// <summary>Raised once the user has confirmed the restart a theme switch needs.</summary>
    internal event Action<AppTheme>? ThemeChangeRequested;

    /// <summary>Raised when a saved theme brings the other palette with it and the window has to be rebuilt.</summary>
    internal event Action<JkMonSettings>? ThemeLoadRequested;

    private void BuildLayout()
    {
        var left = Column();
        var right = Column();
        _left = left;
        _right = right;

        Heading(left, "Theme");
        Row(left, "Palette", Group(_themeLight, _themeDark));
        Row(left, "Accent stripe", Group(_stripeNone, _stripeSolid, _stripeTricolour));
        _stripeColourRows.Add(Row(left, "Stripe colour 1", ColorRow(_stripeFirst, _stripeFirstSwatch)));
        _stripeColourRows.Add(Row(left, "Stripe colour 2", ColorRow(_stripeSecond, _stripeSecondSwatch)));
        _stripeColourRows.Add(Row(left, "Stripe colour 3", ColorRow(_stripeThird, _stripeThirdSwatch)));

        _loadTheme = Action("Load", primary: false);
        _deleteTheme = Action("Delete", primary: false);
        var savedRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        savedRow.Controls.Add(Bordered(_savedThemes));
        savedRow.Controls.Add(_loadTheme);
        savedRow.Controls.Add(_deleteTheme);
        Row(left, "Saved themes", savedRow);

        Heading(left, "Custom text");
        Row(left, "Caption", _customText);
        Row(left, "Caption font", Bordered(_customFont));
        Row(left, "Caption size", _customSize);
        Row(left, "Caption colour", ColorRow(_customColor, _customColorSwatch));
        Row(left, "Caption align", Group(_captionLeft, _captionCenter, _captionRight));
        Row(left, string.Empty, _captionShadow);

        Heading(left, "Appearance");
        Row(left, "Text colour", ColorRow(_textColor, _textColorSwatch));
        Row(left, "Background", ColorRow(_backColor, _backColorSwatch));

        Heading(left, "Gauges");
        Row(left, "CPU", Group(_cpuNumber, _cpuBar));
        Row(left, "Memory", Group(_memoryNumber, _memoryBar, _memoryPie));
        Row(left, string.Empty, _cores);
        Row(left, "Outline", ColorRow(_outlineColor, _outlineColorSwatch));
        Row(left, "Outline width", _outlineWidth);
        Row(left, "Label size", _labelSize);
        Row(left, "Caption size", _captionSize);
        Row(left, "CPU usage", ColorRow(_cpuGaugeColor, _cpuGaugeColorSwatch));
        Row(left, "Memory usage", ColorRow(_memoryGaugeColor, _memoryGaugeColorSwatch));

        Heading(left, "Indicators");
        _orderUp = Action("\u25B2", primary: false);
        _orderDown = Action("\u25BC", primary: false);
        var orderButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            Margin = new Padding(6, 0, 0, 0)
        };
        orderButtons.Controls.Add(_orderUp);
        orderButtons.Controls.Add(_orderDown);

        var orderRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
        orderRow.Controls.Add(_order);
        orderRow.Controls.Add(_noProviders);
        orderRow.Controls.Add(orderButtons);
        Row(left, "Icon order", orderRow);

        Heading(right, "Activity bars");
        Row(right, string.Empty, _activityBars);
        _activityRows.Add(Row(right, "Idle", ColorRow(_activityIdleColor, _activityIdleSwatch)));
        _activityRows.Add(Row(right, "Normal", ColorRow(_activityNormalColor, _activityNormalSwatch)));
        _activityRows.Add(Row(right, "Elevated", ColorRow(_activityElevatedColor, _activityElevatedSwatch)));
        _activityRows.Add(Row(right, "High", ColorRow(_activityHighColor, _activityHighSwatch)));
        _activityRows.Add(Row(right, "Net KiB/s", Thresholds(_netFirst, _netSecond)));
        _activityRows.Add(Row(right, "Disk KiB/s", Thresholds(_diskFirst, _diskSecond)));

        Heading(right, "Typography");
        Row(right, "Background opacity", _opacity);
        Row(right, "Font", Bordered(_font));
        Row(right, "Font size", _fontSize);
        Row(right, "Indicator size", _circle);
        Row(right, string.Empty, Group(_bold, _shadow));

        Heading(right, "Behaviour");
        Row(right, "Refresh", _refresh);
        Row(right, "Edge margin", _margin);
        Row(right, "Monitor", Bordered(_monitor));
        Row(right, "Position", Group(_bottomLeft, _bottomCenter, _bottomRight));
        Row(right, "Window layer", Group(_desktop, _top));
        Row(right, string.Empty, _startup);
        Row(right, string.Empty, _hideOnHover);
        Row(right, string.Empty, _fullscreen);
        Row(right, "Update check", Group(_updateNever, _updateDaily, _updateWeekly));
        Row(right, string.Empty, _updateStartup);

        var columns = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 1,
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(16, 12, 16, 12)
        };
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        columns.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        columns.Controls.Add(left, 0, 0);
        columns.Controls.Add(right, 1, 0);
        _columns = columns;

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = Font.Height * 3,
            BackColor = Surface,
            Padding = new Padding(16, 10, 16, 10)
        };

        var save = Action("Theme save", primary: false);
        var reset = Action("Reset", primary: false);
        var close = Action("Close", primary: true);
        save.Click += (_, _) => SaveTheme();
        reset.Click += (_, _) =>
        {
            LoadFrom(new JkMonSettings());
            Publish();
        };

        close.Click += (_, _) => Close();

        var dataRoot = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Muted,
            Text = AppPaths.IsPortable
                ? $"Portable. Settings and log live in {AppPaths.DataRoot}"
                : $"The app folder is read-only, so settings and log live in {AppPaths.DataRoot}"
        };

        dataRoot.MouseHover += (_, _) => new ToolTip().SetToolTip(dataRoot, AppPaths.DataRoot);

        var actions = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Right,
            Margin = Padding.Empty
        };
        actions.Controls.Add(close);
        actions.Controls.Add(reset);
        actions.Controls.Add(save);

        // Docked edges are laid out before the filling label, so the label takes whatever is left.
        footer.Controls.Add(dataRoot);
        footer.Controls.Add(actions);

        Controls.Add(columns);
        Controls.Add(footer);
        Controls.Add(_stripeBand);
    }

    private static FlowLayoutPanel Column() => new()
    {
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Dock = DockStyle.Top,
        Margin = new Padding(0, 0, 40, 0)
    };

    /// <summary>Section eyebrow. One theme sets it in caps with tracking; the other leaves it as written.</summary>
    private void Heading(FlowLayoutPanel column, string text)
    {
        var caps = _chrome.UppercaseHeadings;
        var label = new ChromeLabel
        {
            Text = caps ? text.ToUpperInvariant() : text,
            Tracking = caps ? 1.5f : 0f,
            ForeColor = caps ? Ink : Accent,
            Font = new Font(_chrome.DisplayFont, caps ? 10f : 11f, FontStyle.Bold),
            Margin = new Padding(0, 28, 0, caps ? 4 : 10)
        };

        label.Size = label.Measured();
        column.Controls.Add(label);

        if (!caps)
        {
            return;
        }

        column.Controls.Add(new Panel
        {
            Height = 1,
            Width = label.Width * 3,
            BackColor = Hairline,
            Margin = new Padding(0, 0, 0, 12)
        });
    }

    /// <summary>
    /// Rows go into an auto-sizing two column table. Fixed label widths were clipping longer captions once the
    /// form scaled for DPI, and a table also keeps the labels in a section aligned without measuring anything.
    /// </summary>
    private Control Row(FlowLayoutPanel column, string label, Control control)
    {
        var table = column.Controls.Count > 0 && column.Controls[^1] is TableLayoutPanel existing
            ? existing
            : NewTable(column);

        var caption = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 6, 14, 4),
            ForeColor = Ink
        };

        control.Margin = new Padding(0, 2, 0, 4);
        control.Anchor = AnchorStyles.Left;

        table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(caption, 0, table.RowCount - 1);
        table.Controls.Add(control, 1, table.RowCount - 1);
        return control;
    }

    private static TableLayoutPanel NewTable(FlowLayoutPanel column)
    {
        var table = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 0,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        column.Controls.Add(table);
        return table;
    }

    private static FlowLayoutPanel Group(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        foreach (var control in controls)
        {
            control.Margin = new Padding(0, 3, 12, 0);
            panel.Controls.Add(control);
        }

        return panel;
    }

    private static FlowLayoutPanel ColorRow(TextBox box, Button swatch)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        panel.Controls.Add(box);
        swatch.Margin = new Padding(6, 0, 0, 0);
        panel.Controls.Add(swatch);
        return panel;
    }

    /// <summary>
    /// The two numbers are the points where the bar changes colour, so each is captioned with the level it
    /// starts. Without that they read as an unexplained pair.
    /// </summary>
    private FlowLayoutPanel Thresholds(TextBox elevated, TextBox high)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        panel.Controls.Add(Caption("Elevated at"));
        elevated.Margin = new Padding(0, 0, 4, 0);
        panel.Controls.Add(elevated);
        panel.Controls.Add(Caption("High at", leading: 14));
        high.Margin = new Padding(0, 0, 4, 0);
        panel.Controls.Add(high);

        const string rule = "읽기와 쓰기를 합친 전송량이 기준입니다.\n"
            + "전송이 없으면 Idle, 첫 값보다 작으면 Normal,\n"
            + "첫 값 이상이면 Elevated, 둘째 값 이상이면 High 색으로 바뀝니다.";

        _tips.SetToolTip(elevated, rule);
        _tips.SetToolTip(high, rule);
        return panel;
    }

    private ThemedLabel Caption(string text, int leading = 0) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Muted,
        MutedColor = Muted,
        Margin = new Padding(leading, 6, 6, 0)
    };

    private static TextBox Input(int maxLength) => new()
    {
        MaxLength = maxLength,
        BorderStyle = BorderStyle.FixedSingle
    };

    private TextBox[] Fields =>
    [
        _customText, _customColor, _textColor, _backColor, _outlineColor, _cpuGaugeColor, _memoryGaugeColor,
        _activityIdleColor, _activityNormalColor, _activityElevatedColor, _activityHighColor,
        _netFirst, _netSecond, _diskFirst, _diskSecond,
        _stripeFirst, _stripeSecond, _stripeThird
    ];

    private ComboBox[] Pickers => [_customFont, _font, _monitor, _savedThemes];

    /// <summary>
    /// A flat combo draws no border of its own and reads as a label rather than something to click. The system
    /// style would draw one but ignores the dark palette, so the border is a one pixel frame behind the control.
    /// </summary>
    private Panel Bordered(Control inner)
    {
        var host = new Panel
        {
            BackColor = Hairline,
            Padding = new Padding(1),
            Margin = Padding.Empty
        };

        inner.Dock = DockStyle.Fill;
        host.Controls.Add(inner);
        return host;
    }

    /// <summary>Drawn flat inside a bordered host: a borderless combo reads as a label rather than a control.</summary>
    private static ComboBox Picker() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat
    };

    private static ChromeButton Swatch() => new()
    {
        Text = string.Empty
    };

    private ChromeButton[] Swatches =>
    [
        _customColorSwatch, _textColorSwatch, _backColorSwatch, _outlineColorSwatch, _cpuGaugeColorSwatch,
        _memoryGaugeColorSwatch, _activityIdleSwatch, _activityNormalSwatch, _activityElevatedSwatch,
        _activityHighSwatch, _stripeFirstSwatch, _stripeSecondSwatch, _stripeThirdSwatch
    ];

    /// <summary>
    /// One button shape covers both themes: a full pill for the rounded theme, a sharp outlined rectangle with a
    /// letter-spaced uppercase label for the other.
    /// </summary>
    private ChromeButton Action(string text, bool primary)
    {
        var pill = _chrome.PillButtons;
        return new ChromeButton
        {
            Text = pill ? text : text.ToUpperInvariant(),
            Tracking = pill ? 0f : 1.5f,
            Radius = pill ? ChromeButton.PillRadius : 0,
            ForeColor = primary && pill ? ColorOf("#FFFFFF") : primary ? Ink : pill ? Accent : Ink,
            BackColor = primary && pill ? Accent : Surface,
            BorderColor = pill && primary ? Color.Transparent : primary ? Ink : pill ? Accent : Hairline,
            MutedColor = Muted,
            Margin = new Padding(8, 0, 0, 0)
        };
    }

    /// <summary>The first entry means "whichever monitor the overlay is on", so its device name is empty.</summary>
    private void LoadMonitors()
    {
        _monitorDevices.Add(string.Empty);
        _monitor.Items.Add(AutomaticMonitor);

        foreach (var monitor in MonitorCatalog.All())
        {
            _monitor.Items.Add(monitor.Label);
            _monitorDevices.Add(monitor.DeviceName);
        }
    }

    private void Wire()
    {
        foreach (var box in (TextBox[])
                 [
                     _customText, _customColor, _textColor, _backColor, _outlineColor, _cpuGaugeColor,
                     _memoryGaugeColor, _activityIdleColor, _activityNormalColor, _activityElevatedColor,
                     _activityHighColor, _netFirst, _netSecond, _diskFirst, _diskSecond,
                     _stripeFirst, _stripeSecond, _stripeThird
                 ])
        {
            box.TextChanged += (_, _) => Publish();
        }

        foreach (var toggle in (RadioButton[])[_stripeNone, _stripeSolid, _stripeTricolour])
        {
            toggle.CheckedChanged += (_, _) => Publish();
        }

        _themeLight.Click += (_, _) => RequestTheme(AppTheme.Light);
        _themeDark.Click += (_, _) => RequestTheme(AppTheme.Dark);
        _loadTheme.Click += (_, _) => LoadTheme();
        _deleteTheme.Click += (_, _) => DeleteTheme();

        foreach (var picker in (ComboBox[])[_customFont, _font, _monitor])
        {
            picker.SelectedIndexChanged += (_, _) => Publish();
        }
        foreach (var slider in (Slider[])
                 [_customSize, _outlineWidth, _labelSize, _captionSize, _opacity, _fontSize, _circle, _refresh, _margin])
        {
            slider.ValueChanged += Publish;
        }

        foreach (var toggle in (ButtonBase[])
                 [
                     _captionLeft, _captionCenter, _captionRight, _captionShadow, _cpuNumber, _cpuBar,
                     _memoryNumber, _memoryBar, _memoryPie, _cores, _activityBars, _bold, _shadow,
                     _bottomLeft, _bottomCenter, _bottomRight, _desktop, _top, _startup, _hideOnHover,
                     _fullscreen, _updateNever, _updateDaily, _updateWeekly, _updateStartup
                 ])
        {
            switch (toggle)
            {
                case RadioButton radio:
                    radio.CheckedChanged += (_, _) => Publish();
                    break;
                case CheckBox check:
                    check.CheckedChanged += (_, _) => Publish();
                    break;
            }
        }

        _orderUp.Click += (_, _) => MoveProvider(-1);
        _orderDown.Click += (_, _) => MoveProvider(1);

        WirePicker(_textColorSwatch, _textColor, JkMonSettings.DefaultTextColor);
        WirePicker(_customColorSwatch, _customColor, JkMonSettings.DefaultCustomTextColor);
        WirePicker(_backColorSwatch, _backColor, JkMonSettings.DefaultBackgroundColor);
        WirePicker(_outlineColorSwatch, _outlineColor, JkMonSettings.DefaultGaugeOutlineColor);
        WirePicker(_cpuGaugeColorSwatch, _cpuGaugeColor, JkMonSettings.DefaultCpuGaugeColor);
        WirePicker(_memoryGaugeColorSwatch, _memoryGaugeColor, JkMonSettings.DefaultMemoryGaugeColor);
        WirePicker(_activityIdleSwatch, _activityIdleColor, JkMonSettings.DefaultActivityIdleColor);
        WirePicker(_activityNormalSwatch, _activityNormalColor, JkMonSettings.DefaultActivityNormalColor);
        WirePicker(_activityElevatedSwatch, _activityElevatedColor, JkMonSettings.DefaultActivityElevatedColor);
        WirePicker(_activityHighSwatch, _activityHighColor, JkMonSettings.DefaultActivityHighColor);
        WirePicker(_stripeFirstSwatch, _stripeFirst, JkMonSettings.DefaultAccentStripeFirstColor);
        WirePicker(_stripeSecondSwatch, _stripeSecond, JkMonSettings.DefaultAccentStripeSecondColor);
        WirePicker(_stripeThirdSwatch, _stripeThird, JkMonSettings.DefaultAccentStripeThirdColor);
    }

    /// <summary>
    /// A theme replaces every colour and typeface, and the window measures itself from the font it was built
    /// with, so the switch happens on a fresh process rather than by re-skinning what is already on screen.
    /// </summary>
    private void RequestTheme(AppTheme theme)
    {
        if (_loading || theme == _theme)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            "테마를 바꾸면 색과 글꼴이 모두 새 테마의 값으로 바뀌고 앱이 다시 시작됩니다.\n" +
            "캐션, 위치, 크기, 임계값 같은 설정은 그대로 유지됩니다.\n\n계속할까요?",
            "JKMon",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Question);

        if (answer != DialogResult.OK)
        {
            _loading = true;
            _themeLight.Checked = _theme == AppTheme.Light;
            _themeDark.Checked = _theme == AppTheme.Dark;
            _loading = false;
            return;
        }

        ThemeChangeRequested?.Invoke(theme);
    }

    private void ShowSavedThemes(string? select)
    {
        var names = _presets.All().Select(p => p.Name).ToArray();

        _savedThemes.BeginUpdate();
        _savedThemes.Items.Clear();
        _savedThemes.Items.AddRange(names);
        _savedThemes.EndUpdate();

        if (names.Length > 0)
        {
            var index = select is null ? 0 : Array.FindIndex(names, n => n == select);
            _savedThemes.SelectedIndex = index < 0 ? 0 : index;
        }

        _loadTheme.Enabled = names.Length > 0;
        _deleteTheme.Enabled = names.Length > 0;
    }

    private void SaveTheme()
    {
        var suggested = _savedThemes.SelectedItem as string ?? string.Empty;
        var name = NamePrompt.Ask(
            this, _chrome, "JKMon", "이 테마를 어떤 이름으로 저장할까요?", suggested);

        if (name is null)
        {
            return;
        }

        if (_presets.All().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            && MessageBox.Show(
                this,
                $"\"{name}\" 이름의 테마가 이미 있습니다. 덮어쓸까요?",
                "JKMon",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question) != DialogResult.OK)
        {
            return;
        }

        _presets.Save(ThemePreset.From(Current(), name));
        ShowSavedThemes(name);
    }

    /// <summary>
    /// A preset can carry the other palette, and the window is built around the one it opened with, so loading
    /// across themes restarts the same way the theme radios do.
    /// </summary>
    private void LoadTheme()
    {
        if (_savedThemes.SelectedItem is not string name)
        {
            return;
        }

        var preset = _presets.All().FirstOrDefault(p => p.Name == name);
        if (preset is null)
        {
            return;
        }

        var applied = preset.ApplyTo(Current());
        if (preset.Theme != _theme)
        {
            ThemeLoadRequested?.Invoke(applied);
            return;
        }

        LoadFrom(applied);
        SettingsChanged?.Invoke(applied);
    }

    private void DeleteTheme()
    {
        if (_savedThemes.SelectedItem is not string name)
        {
            return;
        }

        if (MessageBox.Show(
                this,
                $"저장된 테마 \"{name}\"을 삭제할까요?",
                "JKMon",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning) != DialogResult.OK)
        {
            return;
        }

        _presets.Delete(name);
        ShowSavedThemes(null);
    }

    /// <summary>Writing the pick back into the box reuses its TextChanged handler, so the overlay updates itself.</summary>
    private void WirePicker(Button swatch, TextBox box, string fallback) => swatch.Click += (_, _) =>
    {
        var current = HexColor.ParseOrDefault(box.Text, HexColor.ParseOrDefault(fallback, new HexColor(255, 0, 0, 0)));

        using var dialog = new ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            CustomColors = _customColors,
            Color = Color.FromArgb(current.R, current.G, current.B)
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        // Keep the palette the user builds up for the rest of the session.
        _customColors = dialog.CustomColors;
        box.Text = new HexColor(255, dialog.Color.R, dialog.Color.G, dialog.Color.B).ToHex();
    };

    private void LoadFrom(JkMonSettings settings)
    {
        _loading = true;
        try
        {
            var normalized = settings.Normalized();
            _theme = normalized.Theme;
            _themeLight.Checked = normalized.Theme == AppTheme.Light;
            _themeDark.Checked = normalized.Theme == AppTheme.Dark;
            _stripeNone.Checked = normalized.AccentStripe == AccentStripeMode.None;
            _stripeSolid.Checked = normalized.AccentStripe == AccentStripeMode.Solid;
            _stripeTricolour.Checked = normalized.AccentStripe == AccentStripeMode.Tricolour;
            _stripeFirst.Text = normalized.AccentStripeFirstColor;
            _stripeSecond.Text = normalized.AccentStripeSecondColor;
            _stripeThird.Text = normalized.AccentStripeThirdColor;
            _customText.Text = normalized.CustomText;
            _customFont.SelectedItem = normalized.CustomTextFontFamily;
            _customSize.Value = normalized.CustomTextFontSize;
            _customColor.Text = normalized.CustomTextColor;
            _captionLeft.Checked = normalized.CustomTextAlignment == CaptionAlignment.Left;
            _captionCenter.Checked = normalized.CustomTextAlignment == CaptionAlignment.Center;
            _captionRight.Checked = normalized.CustomTextAlignment == CaptionAlignment.Right;
            _captionShadow.Checked = normalized.CustomTextShadow;
            _activityBars.Checked = normalized.ShowActivityBars;
            _activityIdleColor.Text = normalized.ActivityIdleColor;
            _activityNormalColor.Text = normalized.ActivityNormalColor;
            _activityElevatedColor.Text = normalized.ActivityElevatedColor;
            _activityHighColor.Text = normalized.ActivityHighColor;
            _netFirst.Text = normalized.NetworkFirstThresholdKib.ToString("0.#", CultureInfo.InvariantCulture);
            _netSecond.Text = normalized.NetworkSecondThresholdKib.ToString("0.#", CultureInfo.InvariantCulture);
            _diskFirst.Text = normalized.DiskFirstThresholdKib.ToString("0.#", CultureInfo.InvariantCulture);
            _diskSecond.Text = normalized.DiskSecondThresholdKib.ToString("0.#", CultureInfo.InvariantCulture);
            _textColor.Text = normalized.TextColor;
            _backColor.Text = normalized.BackgroundColor;
            _cpuNumber.Checked = normalized.CpuGauge == CpuGaugeStyle.Number;
            _cpuBar.Checked = normalized.CpuGauge == CpuGaugeStyle.Bar;
            _memoryNumber.Checked = normalized.MemoryGauge == MemoryGaugeStyle.Number;
            _memoryBar.Checked = normalized.MemoryGauge == MemoryGaugeStyle.Bar;
            _memoryPie.Checked = normalized.MemoryGauge == MemoryGaugeStyle.Pie;
            _cores.Checked = normalized.ShowIndividualCores;
            _outlineColor.Text = normalized.GaugeOutlineColor;
            _outlineWidth.Value = normalized.GaugeOutlineThickness;
            _labelSize.Value = normalized.GaugeLabelFontSize;
            _captionSize.Value = normalized.GaugeCaptionFontSize;
            _cpuGaugeColor.Text = normalized.CpuGaugeColor;
            _memoryGaugeColor.Text = normalized.MemoryGaugeColor;
            _opacity.Value = normalized.BackgroundOpacityPercent;
            _font.SelectedItem = normalized.FontFamily;
            _fontSize.Value = normalized.FontSize;
            _circle.Value = normalized.CircleDiameter;
            _bold.Checked = normalized.BoldText;
            _shadow.Checked = normalized.TextShadow;
            _refresh.Value = normalized.RefreshSeconds;
            _margin.Value = normalized.MarginPixels;
            _desktop.Checked = normalized.Layer == WindowLayer.Desktop;
            _top.Checked = normalized.Layer == WindowLayer.AlwaysOnTop;
            _bottomLeft.Checked = normalized.Position == OverlayPosition.BottomLeft;
            _bottomCenter.Checked = normalized.Position == OverlayPosition.BottomCenter;
            _bottomRight.Checked = normalized.Position == OverlayPosition.BottomRight;

            var monitorIndex = _monitorDevices.IndexOf(normalized.MonitorDeviceName);
            _monitor.SelectedIndex = monitorIndex >= 0 ? monitorIndex : 0;

            _startup.Checked = normalized.StartWithWindows;
            _hideOnHover.Checked = normalized.HideWhenPointerOver;
            _fullscreen.Checked = normalized.PauseWhenFullscreen;
            _updateNever.Checked = normalized.UpdateCheck == UpdateCheckFrequency.Never;
            _updateDaily.Checked = normalized.UpdateCheck == UpdateCheckFrequency.Daily;
            _updateWeekly.Checked = normalized.UpdateCheck == UpdateCheckFrequency.Weekly;
            _updateStartup.Checked = normalized.CheckUpdatesOnStartup;
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
        Theme = _theme,
        AccentStripe = _stripeSolid.Checked ? AccentStripeMode.Solid
            : _stripeTricolour.Checked ? AccentStripeMode.Tricolour
            : AccentStripeMode.None,
        AccentStripeFirstColor = _stripeFirst.Text,
        AccentStripeSecondColor = _stripeSecond.Text,
        AccentStripeThirdColor = _stripeThird.Text,
        CustomText = _customText.Text,
        CustomTextFontFamily = _customFont.SelectedItem as string ?? JkMonSettings.DefaultFontFamily,
        CustomTextFontSize = Math.Round(_customSize.Value),
        CustomTextColor = _customColor.Text,
        CustomTextAlignment = _captionLeft.Checked ? CaptionAlignment.Left
            : _captionRight.Checked ? CaptionAlignment.Right
            : CaptionAlignment.Center,
        TextColor = _textColor.Text,
        BackgroundColor = _backColor.Text,
        CpuGauge = _cpuBar.Checked ? CpuGaugeStyle.Bar : CpuGaugeStyle.Number,
        MemoryGauge = _memoryBar.Checked ? MemoryGaugeStyle.Bar
            : _memoryPie.Checked ? MemoryGaugeStyle.Pie
            : MemoryGaugeStyle.Number,
        ShowIndividualCores = _cores.Checked,
        GaugeOutlineColor = _outlineColor.Text,
        GaugeOutlineThickness = _outlineWidth.Value,
        GaugeLabelFontSize = Math.Round(_labelSize.Value),
        GaugeCaptionFontSize = Math.Round(_captionSize.Value),
        CpuGaugeColor = _cpuGaugeColor.Text,
        MemoryGaugeColor = _memoryGaugeColor.Text,
        BackgroundOpacityPercent = (int)Math.Round(_opacity.Value),
        FontFamily = _font.SelectedItem as string ?? JkMonSettings.DefaultFontFamily,
        FontSize = Math.Round(_fontSize.Value),
        CircleDiameter = (int)Math.Round(_circle.Value),
        BoldText = _bold.Checked,
        TextShadow = _shadow.Checked,
        CustomTextShadow = _captionShadow.Checked,
        ShowActivityBars = _activityBars.Checked,
        ActivityIdleColor = _activityIdleColor.Text,
        ActivityNormalColor = _activityNormalColor.Text,
        ActivityElevatedColor = _activityElevatedColor.Text,
        ActivityHighColor = _activityHighColor.Text,
        NetworkFirstThresholdKib = ParsedThreshold(_netFirst, 1024),
        NetworkSecondThresholdKib = ParsedThreshold(_netSecond, 10 * 1024),
        DiskFirstThresholdKib = ParsedThreshold(_diskFirst, 5 * 1024),
        DiskSecondThresholdKib = ParsedThreshold(_diskSecond, 50 * 1024),
        RefreshSeconds = (int)Math.Round(_refresh.Value),
        MarginPixels = (int)Math.Round(_margin.Value),
        Layer = _top.Checked ? WindowLayer.AlwaysOnTop : WindowLayer.Desktop,
        Position = _bottomLeft.Checked ? OverlayPosition.BottomLeft
            : _bottomCenter.Checked ? OverlayPosition.BottomCenter
            : OverlayPosition.BottomRight,
        MonitorDeviceName = _monitor.SelectedIndex > 0 && _monitor.SelectedIndex < _monitorDevices.Count
            ? _monitorDevices[_monitor.SelectedIndex]
            : string.Empty,
        StartWithWindows = _startup.Checked,
        HideWhenPointerOver = _hideOnHover.Checked,
        PauseWhenFullscreen = _fullscreen.Checked,
        ProviderOrder = _providerOrder.ToList(),
        UpdateCheck = _updateDaily.Checked ? UpdateCheckFrequency.Daily
            : _updateWeekly.Checked ? UpdateCheckFrequency.Weekly
            : UpdateCheckFrequency.Never,
        CheckUpdatesOnStartup = _updateStartup.Checked,
        LastUpdateCheckUtc = _lastUpdateCheckUtc
    }.Normalized();

    private static double ParsedThreshold(TextBox box, double fallback) =>
        double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    private void ShowProviderOrder(int selected)
    {
        var visible = ProviderOrderView.Visible(_providerOrder, _presentProviders);

        _order.BeginUpdate();
        _order.Items.Clear();
        foreach (var id in visible)
        {
            _order.Items.Add(SyncProviderCatalog.DisplayName(id));
        }

        _order.EndUpdate();

        var any = visible.Count > 0;
        if (any)
        {
            _order.SelectedIndex = Math.Clamp(selected, 0, visible.Count - 1);
        }

        _order.Visible = any;
        _noProviders.Visible = !any;
        _orderUp.Enabled = visible.Count > 1;
        _orderDown.Enabled = visible.Count > 1;
    }

    /// <summary>The set can change while the window is open, so a client started meanwhile shows up without a reopen.</summary>
    internal void SetPresentProviders(IReadOnlyList<string> providerIds)
    {
        if (_presentProviders.SequenceEqual(providerIds, StringComparer.Ordinal))
        {
            return;
        }

        _presentProviders = [.. providerIds];
        ShowProviderOrder(_order.SelectedIndex);
    }

    /// <summary>Reordering is a list edit, so the selection follows the moved entry rather than the position.</summary>
    private void MoveProvider(int offset)
    {
        var from = _order.SelectedIndex;
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
        _updateStartup.Enabled = !_updateNever.Checked;
        foreach (var row in _activityRows)
        {
            row.Enabled = _activityBars.Checked;
        }

        // Per-core bars take the place of the aggregate gauge, so its style no longer applies.
        var aggregateCpu = !_cores.Checked;
        _cpuNumber.Enabled = aggregateCpu;
        _cpuBar.Enabled = aggregateCpu;

        _opacity.Suffix = "%";

        // Only the tricolour mode needs the second and third colours.
        var stripe = _stripeSolid.Checked || _stripeTricolour.Checked;
        for (var i = 0; i < _stripeColourRows.Count; i++)
        {
            _stripeColourRows[i].Enabled = i == 0 ? stripe : _stripeTricolour.Checked;
        }

        _stripeBand.Colours = StripeColours();
        _stripeBand.Height = _stripeBand.Colours.Length == 0 ? 0 : 4;
        _stripeBand.Invalidate();

        PaintSwatch(_stripeFirstSwatch, _stripeFirst.Text, JkMonSettings.DefaultAccentStripeFirstColor);
        PaintSwatch(_stripeSecondSwatch, _stripeSecond.Text, JkMonSettings.DefaultAccentStripeSecondColor);
        PaintSwatch(_stripeThirdSwatch, _stripeThird.Text, JkMonSettings.DefaultAccentStripeThirdColor);
        PaintSwatch(_textColorSwatch, _textColor.Text, JkMonSettings.DefaultTextColor);
        PaintSwatch(_customColorSwatch, _customColor.Text, JkMonSettings.DefaultCustomTextColor);
        PaintSwatch(_backColorSwatch, _backColor.Text, JkMonSettings.DefaultBackgroundColor);
        PaintSwatch(_activityIdleSwatch, _activityIdleColor.Text, JkMonSettings.DefaultActivityIdleColor);
        PaintSwatch(_activityNormalSwatch, _activityNormalColor.Text, JkMonSettings.DefaultActivityNormalColor);
        PaintSwatch(_activityElevatedSwatch, _activityElevatedColor.Text, JkMonSettings.DefaultActivityElevatedColor);
        PaintSwatch(_activityHighSwatch, _activityHighColor.Text, JkMonSettings.DefaultActivityHighColor);
        PaintSwatch(_outlineColorSwatch, _outlineColor.Text, JkMonSettings.DefaultGaugeOutlineColor);
        PaintSwatch(_cpuGaugeColorSwatch, _cpuGaugeColor.Text, JkMonSettings.DefaultCpuGaugeColor);
        PaintSwatch(_memoryGaugeColorSwatch, _memoryGaugeColor.Text, JkMonSettings.DefaultMemoryGaugeColor);
    }

    private static void PaintSwatch(Button swatch, string value, string fallback)
    {
        var color = HexColor.ParseOrDefault(value, HexColor.ParseOrDefault(fallback, new HexColor(255, 0, 0, 0)));
        swatch.BackColor = Color.FromArgb(color.R, color.G, color.B);
    }

    private Color[] StripeColours()
    {
        if (_stripeSolid.Checked)
        {
            return [ColorOf(_stripeFirst.Text)];
        }

        if (!_stripeTricolour.Checked)
        {
            return [];
        }

        return [ColorOf(_stripeFirst.Text), ColorOf(_stripeSecond.Text), ColorOf(_stripeThird.Text)];
    }

    /// <summary>
    /// A track bar with a readout. TrackBar only carries integers, so fractional steps are stored multiplied by
    /// their step count and converted back on the way out.
    /// </summary>
    private sealed class Slider : FlowLayoutPanel
    {
        private readonly TrackBar _bar;
        private readonly Label _readout;
        private readonly double _step;

        internal Slider(double minimum, double maximum, double step, string suffix = "")
        {
            _step = step;
            Suffix = suffix;
            AutoSize = true;
            WrapContents = false;
            Margin = Padding.Empty;

            _bar = new TrackBar
            {
                Minimum = (int)Math.Round(minimum / step),
                Maximum = (int)Math.Round(maximum / step),
                TickStyle = TickStyle.None,
                // TrackBar ignores Height unless auto sizing is off, and its default is tall enough to push the
                // row label out of line with the track.
                AutoSize = false,
                Width = 168,
                Height = 30,
                Margin = new Padding(0, 0, 6, 0)
            };

            _readout = new Label
            {
                AutoSize = false,
                Width = 44,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 0)
            };

            _bar.ValueChanged += (_, _) =>
            {
                ShowValue();
                ValueChanged?.Invoke();
            };

            Controls.Add(_bar);
            Controls.Add(_readout);
            ShowValue();
        }

        internal event Action? ValueChanged;

        /// <summary>The readout exists before the form font and palette are known, so it is finished here.</summary>
        internal void Rescale(Color muted)
        {
            var unit = Font.Height;
            _bar.Height = unit + 8;
            _bar.Width = unit * 8;

            _readout.Font = Font;
            _readout.ForeColor = muted;
            _readout.AutoSize = false;
            _readout.Size = new Size(unit * 3, unit + 8);
            _readout.TextAlign = ContentAlignment.MiddleLeft;
            _readout.Margin = Padding.Empty;
            ShowValue();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string Suffix { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal double Value
        {
            get => _bar.Value * _step;
            set => _bar.Value = (int)Math.Clamp(Math.Round(value / _step), _bar.Minimum, _bar.Maximum);
        }

        private void ShowValue() =>
            _readout.Text = Value.ToString("0.#", CultureInfo.InvariantCulture) + Suffix;
    }

    private static readonly Bitmap MeasureSurface = new(1, 1);

    /// <summary>GenericTypographic reports a zero width for spaces, which closes up the gaps in tracked text.</summary>
    private static readonly StringFormat Typographic = new(StringFormat.GenericTypographic)
    {
        FormatFlags = StringFormat.GenericTypographic.FormatFlags | StringFormatFlags.MeasureTrailingSpaces
    };

    private static float TrackedWidth(string text, Font font, float tracking)
    {
        using var g = Graphics.FromImage(MeasureSurface);
        if (tracking <= 0)
        {
            return g.MeasureString(text, font, PointF.Empty, Typographic).Width;
        }

        var width = 0f;
        foreach (var ch in text)
        {
            width += g.MeasureString(ch.ToString(), font, PointF.Empty, Typographic).Width;
        }

        return width + tracking * Math.Max(0, text.Length - 1);
    }

    private static void DrawTracked(Graphics g, string text, Font font, Brush brush, float x, float y, float tracking)
    {
        if (tracking <= 0)
        {
            g.DrawString(text, font, brush, x, y, Typographic);
            return;
        }

        foreach (var ch in text)
        {
            var glyph = ch.ToString();
            g.DrawString(glyph, font, brush, x, y, Typographic);
            x += g.MeasureString(glyph, font, PointF.Empty, Typographic).Width + tracking;
        }
    }

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

    /// <summary>
    /// WinForms cannot round a button or letter-space a label, and both themes need one of those. Drawing covers
    /// the pill, the sharp outlined rectangle and the plain swatch from a single control.
    /// </summary>
    private sealed class ChromeButton : Button
    {
        internal const int PillRadius = -1;

        private bool _pressed;

        internal ChromeButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(
                ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
                true);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal int Radius { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal float Tracking { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Color BorderColor { get; set; } = Color.Transparent;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Color MutedColor { get; set; } = Color.Gray;

        internal Size Measured() => new(
            (int)Math.Ceiling(TrackedWidth(Text, Font, Tracking)) + Font.Height * 3,
            Font.Height * 2);

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _pressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent?.BackColor ?? BackColor);

            // Pressed inverts: the outline fills and the label drops out.
            var fill = _pressed && BorderColor != Color.Transparent ? BorderColor : BackColor;
            var ink = _pressed && BorderColor != Color.Transparent ? BackColor : ForeColor;

            var bounds = new RectangleF(0, 0, Width - 1, Height - 1);
            var radius = Radius == PillRadius ? bounds.Height / 2 : Radius;

            using (var shape = RoundedRect(bounds, radius))
            {
                using var background = new SolidBrush(Enabled ? fill : BackColor);
                g.FillPath(background, shape);

                if (BorderColor != Color.Transparent)
                {
                    using var pen = new Pen(Enabled ? BorderColor : MutedColor, 1);
                    g.DrawPath(pen, shape);
                }
            }

            if (Text.Length == 0)
            {
                return;
            }

            var width = TrackedWidth(Text, Font, Tracking);
            using var brush = new SolidBrush(Enabled ? ink : MutedColor);
            DrawTracked(g, Text, Font, brush, (Width - width) / 2, (Height - Font.Height) / 2f, Tracking);
        }
    }

    private sealed class ChromeLabel : Label
    {
        internal ChromeLabel()
        {
            AutoSize = false;
            SetStyle(
                ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer,
                true);
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal float Tracking { get; set; }

        internal Size Measured() => new(
            (int)Math.Ceiling(TrackedWidth(Text, Font, Tracking)) + 2,
            Font.Height + 2);

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using var brush = new SolidBrush(ForeColor);
            DrawTracked(e.Graphics, Text, Font, brush, 1, 1, Tracking);
        }
    }

    /// <summary>
    /// WinForms derives disabled text from the control's own BackColor, which on a dark canvas paints dark on
    /// dark and the label disappears. These three redraw it so a disabled row stays readable in either theme.
    /// </summary>
    private static void DrawDisabledText(Graphics g, string text, Font font, Rectangle bounds, Color muted) =>
        TextRenderer.DrawText(g, text, font, bounds, muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

    private sealed class ThemedLabel : Label
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Color MutedColor { get; set; } = Color.Gray;

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Enabled)
            {
                base.OnPaint(e);
                return;
            }

            e.Graphics.Clear(Parent?.BackColor ?? BackColor);
            DrawDisabledText(e.Graphics, Text, Font, ClientRectangle, MutedColor);
        }
    }

    private sealed class ThemedCheckBox : CheckBox
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Color MutedColor { get; set; } = Color.Gray;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Enabled)
            {
                return;
            }

            var glyph = CheckBoxRenderer.GetGlyphSize(
                e.Graphics, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedDisabled);

            DrawDisabledText(
                e.Graphics, Text, Font,
                new Rectangle(glyph.Width + 3, 0, Math.Max(0, Width - glyph.Width - 3), Height), MutedColor);
        }
    }

    private sealed class ThemedRadioButton : RadioButton
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Color MutedColor { get; set; } = Color.Gray;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Enabled)
            {
                return;
            }

            var glyph = RadioButtonRenderer.GetGlyphSize(
                e.Graphics, System.Windows.Forms.VisualStyles.RadioButtonState.UncheckedDisabled);

            DrawDisabledText(
                e.Graphics, Text, Font,
                new Rectangle(glyph.Width + 3, 0, Math.Max(0, Width - glyph.Width - 3), Height), MutedColor);
        }
    }

    /// <summary>Three colour bands, or one, along the top edge. Off unless the user turns it on.</summary>
    private sealed class StripeBand : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal Color[] Colours { get; set; } = [];

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Colours.Length == 0)
            {
                return;
            }

            var segment = Width / (float)Colours.Length;
            for (var i = 0; i < Colours.Length; i++)
            {
                using var brush = new SolidBrush(Colours[i]);
                var width = i == Colours.Length - 1 ? Width - segment * i : segment;
                e.Graphics.FillRectangle(brush, segment * i, 0, width, Height);
            }
        }
    }
}
