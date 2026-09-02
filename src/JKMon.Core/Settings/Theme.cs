namespace JKMon.Core.Settings;

/// <summary>
/// Picking a theme replaces the design values and leaves everything the user chose. Names stay descriptive of the
/// surface rather than of any product the palettes were derived from.
/// </summary>
public enum AppTheme
{
    Light,
    Dark
}

/// <summary>An optional colour band along the top edge of the overlay panel and the settings window.</summary>
public enum AccentStripeMode
{
    None,
    Solid,
    Tricolour
}

/// <summary>Chrome for the settings window. Held as plain data so the palette stays testable without a UI.</summary>
public sealed record ThemeChrome(
    string Surface,
    string SecondarySurface,
    string Field,
    string Hairline,
    string Ink,
    string Muted,
    string Accent,
    string DisplayFont,
    string BodyFont,
    float BodyFontSize,
    bool PillButtons,
    bool UppercaseHeadings,
    bool SystemComboBorder);

/// <summary>The parts of the overlay's look that are not already user-editable settings.</summary>
public sealed record ThemeOverlay(
    float PanelPaddingX,
    float PanelPaddingY,
    float PanelCorner,
    string Track,
    string StatusOk,
    string StatusBusy,
    string StatusUnknown,
    bool UppercaseLabels,
    bool RoundedBars);

public static class ThemeCatalog
{
    private static readonly ThemeChrome LightChrome = new(
        Surface: "#FFFFFF",
        SecondarySurface: "#F5F5F7",
        Field: "#FFFFFF",
        Hairline: "#E0E0E0",
        Ink: "#1D1D1F",
        Muted: "#7A7A7A",
        Accent: "#0066CC",
        DisplayFont: "Segoe UI Variable Display",
        BodyFont: "Segoe UI Variable Text",
        BodyFontSize: 10f,
        PillButtons: true,
        UppercaseHeadings: false,
        SystemComboBorder: true);

    private static readonly ThemeChrome DarkChrome = new(
        Surface: "#000000",
        SecondarySurface: "#0D0D0D",
        Field: "#1A1A1A",
        Hairline: "#3C3C3C",
        Ink: "#FFFFFF",
        Muted: "#7E7E7E",
        Accent: "#FFFFFF",
        DisplayFont: "Segoe UI",
        BodyFont: "Segoe UI Light",
        BodyFontSize: 10f,
        PillButtons: false,
        UppercaseHeadings: true,
        SystemComboBorder: false);

    /// <summary>
    /// The light theme names the settings window, not the overlay. The overlay stays on a dark panel in both
    /// themes because it sits over the wallpaper, where light text on a dark card is what stays readable.
    /// </summary>
    private static readonly ThemeOverlay LightOverlay = new(
        PanelPaddingX: 24,
        PanelPaddingY: 14,
        PanelCorner: 18,
        Track: "#66080B10",
        StatusOk: "#30D158",
        StatusBusy: "#FF453A",
        StatusUnknown: "#7A7A7A",
        UppercaseLabels: false,
        RoundedBars: true);

    private static readonly ThemeOverlay DarkOverlay = new(
        PanelPaddingX: 24,
        PanelPaddingY: 16,
        PanelCorner: 0,
        Track: "#FF1A1A1A",
        StatusOk: "#0FA336",
        StatusBusy: "#E22718",
        StatusUnknown: "#7E7E7E",
        UppercaseLabels: true,
        RoundedBars: false);

    public static ThemeChrome ChromeFor(AppTheme theme) => theme == AppTheme.Light ? LightChrome : DarkChrome;

    public static ThemeOverlay OverlayFor(AppTheme theme) => theme == AppTheme.Light ? LightOverlay : DarkOverlay;

    /// <summary>
    /// Replaces every value the theme owns and keeps every value the user chose. Placement, sizes, thresholds,
    /// the caption text and the accent stripe are all user decisions, so a theme change never touches them.
    /// </summary>
    public static JkMonSettings Apply(JkMonSettings settings, AppTheme theme) => theme == AppTheme.Light
        ? settings with
        {
            Theme = theme,
            TextColor = "#F5F5F7",
            BackgroundColor = "#272729",
            CustomTextColor = "#FFFFFF",
            GaugeOutlineColor = "#7A7A7A",
            CpuGaugeColor = "#2997FF",
            MemoryGaugeColor = "#F5F5F7",
            ActivityIdleColor = "#7A7A7A",
            ActivityNormalColor = "#2997FF",
            ActivityElevatedColor = "#FFFFFF",
            ActivityHighColor = "#FF453A",
            BackgroundOpacityPercent = 80,
            GaugeOutlineThickness = 1,
            TextShadow = false,
            CustomTextShadow = false,
            FontFamily = "Segoe UI Variable Display",
            CustomTextFontFamily = "Segoe UI Variable Display"
        }
        : settings with
        {
            Theme = theme,
            TextColor = JkMonSettings.DefaultTextColor,
            BackgroundColor = JkMonSettings.DefaultBackgroundColor,
            CustomTextColor = JkMonSettings.DefaultCustomTextColor,
            GaugeOutlineColor = JkMonSettings.DefaultGaugeOutlineColor,
            CpuGaugeColor = JkMonSettings.DefaultCpuGaugeColor,
            MemoryGaugeColor = JkMonSettings.DefaultMemoryGaugeColor,
            ActivityIdleColor = JkMonSettings.DefaultActivityIdleColor,
            ActivityNormalColor = JkMonSettings.DefaultActivityNormalColor,
            ActivityElevatedColor = JkMonSettings.DefaultActivityElevatedColor,
            ActivityHighColor = JkMonSettings.DefaultActivityHighColor,
            BackgroundOpacityPercent = 100,
            GaugeOutlineThickness = 1,
            TextShadow = false,
            CustomTextShadow = false,
            FontFamily = JkMonSettings.DefaultFontFamily,
            CustomTextFontFamily = JkMonSettings.DefaultFontFamily
        };
}
