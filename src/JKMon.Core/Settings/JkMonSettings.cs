using System.Text.Json.Serialization;
using JKMon.Core.Presentation;
using JKMon.Core.Sync;
using JKMon.Core.Update;

namespace JKMon.Core.Settings;

public enum WindowLayer
{
    /// <summary>Pinned to the desktop and covered by other windows.</summary>
    Desktop,

    AlwaysOnTop
}

public sealed record JkMonSettings
{
    public const int MinRefreshSeconds = 1;
    public const int MaxRefreshSeconds = 10;
    public const int MinProviderPollSeconds = 2;

    public const double MinFontSize = 9;
    public const double MaxFontSize = 32;
    public const int MinCircleDiameter = 16;
    public const int MaxCircleDiameter = 64;

    public const double MinCustomTextFontSize = 9;
    public const double MaxCustomTextFontSize = 72;

    public const double MinGaugeOutlineThickness = 0;
    public const double MaxGaugeOutlineThickness = 6;

    public const double MinGaugeLabelFontSize = 6;
    public const double MaxGaugeLabelFontSize = 32;

    public const double MinGaugeCaptionFontSize = 0;
    public const double MaxGaugeCaptionFontSize = 32;

    /// <summary>A caption, not a paragraph. The cap also stops a corrupted file from stretching the overlay off screen.</summary>
    public const int MaxCustomTextLength = 64;

    public const string DefaultTextColor = "#35FF6A";
    public const string DefaultBackgroundColor = "#101418";
    public const string DefaultFontFamily = "Segoe UI";
    public const string DefaultCustomTextColor = "#E6EAF0";

    public const string DefaultGaugeOutlineColor = "#2F81F7";
    public const string DefaultCpuGaugeColor = "#35FF6A";
    public const string DefaultMemoryGaugeColor = "#C77DFF";

    public const string DefaultActivityIdleColor = "#6B7280";
    public const string DefaultActivityNormalColor = "#35FF6A";
    public const string DefaultActivityElevatedColor = "#FFC857";
    public const string DefaultActivityHighColor = "#FF5B5B";

    /// <summary>Thresholds are edited in KiB/s because bytes per second is an awkward number to type.</summary>
    public const double MinActivityThresholdKib = 1;
    public const double MaxActivityThresholdKib = 1024d * 1024;

    public int RefreshSeconds { get; init; } = 2;

    public int ProviderPollSeconds { get; init; } = 3;

    public WindowLayer Layer { get; init; } = WindowLayer.Desktop;

    public OverlayPosition Position { get; init; } = OverlayPosition.BottomRight;

    /// <summary>Empty means follow whichever monitor currently hosts the window.</summary>
    public string MonitorDeviceName { get; init; } = string.Empty;

    public int MarginPixels { get; init; } = 8;

    public bool StartWithWindows { get; init; }

    /// <summary>Fades the overlay out while the pointer is over it so it never hides what is underneath.</summary>
    public bool HideWhenPointerOver { get; init; }

    public string TextColor { get; init; } = DefaultTextColor;

    public string BackgroundColor { get; init; } = DefaultBackgroundColor;

    public CpuGaugeStyle CpuGauge { get; init; } = CpuGaugeStyle.Number;

    public MemoryGaugeStyle MemoryGauge { get; init; } = MemoryGaugeStyle.Number;

    /// <summary>Drawn around the bars and the pie so the gauge outline stays visible over any wallpaper.</summary>
    public string GaugeOutlineColor { get; init; } = DefaultGaugeOutlineColor;

    public string CpuGaugeColor { get; init; } = DefaultCpuGaugeColor;

    public string MemoryGaugeColor { get; init; } = DefaultMemoryGaugeColor;

    /// <summary>Outline width in pixels. 0 removes the outline entirely.</summary>
    public double GaugeOutlineThickness { get; init; } = 2;

    /// <summary>Size of the percentage drawn above the bar and pie gauges.</summary>
    public double GaugeLabelFontSize { get; init; } = 9;

    /// <summary>Size of the CPU and Memory names above the numeric gauges. 0 hides them.</summary>
    public double GaugeCaptionFontSize { get; init; } = 9;

    /// <summary>Adds one small bar per logical processor beside the CPU gauge.</summary>
    public bool ShowIndividualCores { get; init; }

    /// <summary>Names the network and storage columns and shows how busy each one is.</summary>
    public bool ShowActivityBars { get; init; } = true;

    public string ActivityIdleColor { get; init; } = DefaultActivityIdleColor;

    public string ActivityNormalColor { get; init; } = DefaultActivityNormalColor;

    public string ActivityElevatedColor { get; init; } = DefaultActivityElevatedColor;

    public string ActivityHighColor { get; init; } = DefaultActivityHighColor;

    /// <summary>Combined in and out rate, in KiB/s, at which the bar leaves its normal colour.</summary>
    public double NetworkFirstThresholdKib { get; init; } = 1024;

    public double NetworkSecondThresholdKib { get; init; } = 10 * 1024;

    public double DiskFirstThresholdKib { get; init; } = 5 * 1024;

    public double DiskSecondThresholdKib { get; init; } = 50 * 1024;

    [JsonIgnore]
    public ActivityThresholds ActivityThresholds => new(
        NetworkFirstThresholdKib * 1024,
        NetworkSecondThresholdKib * 1024,
        DiskFirstThresholdKib * 1024,
        DiskSecondThresholdKib * 1024);

    /// <summary>0 renders the panel fully transparent, 100 fully opaque.</summary>
    public int BackgroundOpacityPercent { get; init; } = 45;

    public string FontFamily { get; init; } = DefaultFontFamily;

    public double FontSize { get; init; } = 13;

    public bool BoldText { get; init; } = true;

    public bool TextShadow { get; init; } = true;

    public int CircleDiameter { get; init; } = 26;

    /// <summary>Free caption drawn above the metric row. Empty hides the row entirely.</summary>
    public string CustomText { get; init; } = string.Empty;

    public string CustomTextFontFamily { get; init; } = DefaultFontFamily;

    public double CustomTextFontSize { get; init; } = 16;

    public string CustomTextColor { get; init; } = DefaultCustomTextColor;

    public CaptionAlignment CustomTextAlignment { get; init; } = CaptionAlignment.Center;

    /// <summary>The caption sits outside the panel background, so it carries its own shadow setting.</summary>
    public bool CustomTextShadow { get; init; } = true;

    /// <summary>Left-to-right order of the status circles. Providers missing from the list are appended.</summary>
    public IReadOnlyList<string> ProviderOrder { get; init; } = SyncProviderCatalog.DefaultOrder;

    /// <summary>How often the app may contact GitHub on its own. `Never` disables every automatic check.</summary>
    public UpdateCheckFrequency UpdateCheck { get; init; } = UpdateCheckFrequency.Never;

    public bool CheckUpdatesOnStartup { get; init; }

    /// <summary>Default means no check has run yet.</summary>
    public DateTimeOffset LastUpdateCheckUtc { get; init; }

    [JsonIgnore]
    public bool HasCustomText => CustomText.Length > 0;

    /// <summary>Values arriving from disk are untrusted, so every field is forced back into its supported range.</summary>
    public JkMonSettings Normalized() => this with
    {
        RefreshSeconds = Clamp(RefreshSeconds, MinRefreshSeconds, MaxRefreshSeconds, 2),
        ProviderPollSeconds = Clamp(ProviderPollSeconds, MinProviderPollSeconds, 600, 3),
        MarginPixels = Clamp(MarginPixels, 0, 400, 8),
        Layer = Enum.IsDefined(Layer) ? Layer : WindowLayer.Desktop,
        Position = Enum.IsDefined(Position) ? Position : OverlayPosition.BottomRight,
        MonitorDeviceName = string.IsNullOrWhiteSpace(MonitorDeviceName) ? string.Empty : MonitorDeviceName.Trim(),
        TextColor = ValidColor(TextColor, DefaultTextColor),
        BackgroundColor = ValidColor(BackgroundColor, DefaultBackgroundColor),
        CpuGauge = Enum.IsDefined(CpuGauge) ? CpuGauge : CpuGaugeStyle.Number,
        MemoryGauge = Enum.IsDefined(MemoryGauge) ? MemoryGauge : MemoryGaugeStyle.Number,
        GaugeOutlineColor = ValidColor(GaugeOutlineColor, DefaultGaugeOutlineColor),
        CpuGaugeColor = ValidColor(CpuGaugeColor, DefaultCpuGaugeColor),
        MemoryGaugeColor = ValidColor(MemoryGaugeColor, DefaultMemoryGaugeColor),
        GaugeOutlineThickness = ClampDouble(
            GaugeOutlineThickness, MinGaugeOutlineThickness, MaxGaugeOutlineThickness, 2),
        GaugeLabelFontSize = ClampDouble(
            GaugeLabelFontSize, MinGaugeLabelFontSize, MaxGaugeLabelFontSize, 9),
        GaugeCaptionFontSize = ClampDouble(
            GaugeCaptionFontSize, MinGaugeCaptionFontSize, MaxGaugeCaptionFontSize, 9),
        ActivityIdleColor = ValidColor(ActivityIdleColor, DefaultActivityIdleColor),
        ActivityNormalColor = ValidColor(ActivityNormalColor, DefaultActivityNormalColor),
        ActivityElevatedColor = ValidColor(ActivityElevatedColor, DefaultActivityElevatedColor),
        ActivityHighColor = ValidColor(ActivityHighColor, DefaultActivityHighColor),
        NetworkFirstThresholdKib = ClampDouble(
            NetworkFirstThresholdKib, MinActivityThresholdKib, MaxActivityThresholdKib, 1024),
        NetworkSecondThresholdKib = ClampDouble(
            NetworkSecondThresholdKib, MinActivityThresholdKib, MaxActivityThresholdKib, 10 * 1024),
        DiskFirstThresholdKib = ClampDouble(
            DiskFirstThresholdKib, MinActivityThresholdKib, MaxActivityThresholdKib, 5 * 1024),
        DiskSecondThresholdKib = ClampDouble(
            DiskSecondThresholdKib, MinActivityThresholdKib, MaxActivityThresholdKib, 50 * 1024),
        BackgroundOpacityPercent = Clamp(BackgroundOpacityPercent, 0, 100, 45),
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? DefaultFontFamily : FontFamily.Trim(),
        FontSize = ClampDouble(FontSize, MinFontSize, MaxFontSize, 13),
        CircleDiameter = Clamp(CircleDiameter, MinCircleDiameter, MaxCircleDiameter, 26),
        CustomText = Caption(CustomText),
        CustomTextFontFamily = string.IsNullOrWhiteSpace(CustomTextFontFamily)
            ? DefaultFontFamily
            : CustomTextFontFamily.Trim(),
        CustomTextFontSize = ClampDouble(CustomTextFontSize, MinCustomTextFontSize, MaxCustomTextFontSize, 16),
        CustomTextColor = ValidColor(CustomTextColor, DefaultCustomTextColor),
        CustomTextAlignment = Enum.IsDefined(CustomTextAlignment) ? CustomTextAlignment : CaptionAlignment.Center,
        ProviderOrder = SyncProviderCatalog.Normalize(ProviderOrder),
        UpdateCheck = Enum.IsDefined(UpdateCheck) ? UpdateCheck : UpdateCheckFrequency.Never
    };

    /// <summary>Line breaks would silently change the overlay's height, so the caption is collapsed to one line.</summary>
    private static string Caption(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var single = new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        return single.Length > MaxCustomTextLength ? single[..MaxCustomTextLength].TrimEnd() : single;
    }

    private static string ValidColor(string? value, string fallback) =>
        HexColor.TryParse(value, out var parsed) ? parsed.ToHex() : fallback;

    private static double ClampDouble(double value, double min, double max, double fallback)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return fallback;
        }

        return value < min ? min : value > max ? max : value;
    }

    private static int Clamp(int value, int min, int max, int fallback)
    {
        if (value < min || value > max)
        {
            return value < min ? min : value > max ? max : fallback;
        }

        return value;
    }
}
