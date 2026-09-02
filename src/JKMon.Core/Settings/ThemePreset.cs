using System.Runtime.Versioning;
using System.Text.Json;

namespace JKMon.Core.Settings;

/// <summary>
/// A named snapshot of how the overlay looks. It carries appearance only: what the readings are, where they sit
/// and how often they refresh belong to the user, not to a look, so loading a preset never disturbs them.
/// </summary>
public sealed record ThemePreset
{
    public string Name { get; init; } = string.Empty;

    public AppTheme Theme { get; init; } = AppTheme.Dark;

    public string TextColor { get; init; } = JkMonSettings.DefaultTextColor;

    public string BackgroundColor { get; init; } = JkMonSettings.DefaultBackgroundColor;

    public string CustomTextColor { get; init; } = JkMonSettings.DefaultCustomTextColor;

    public string GaugeOutlineColor { get; init; } = JkMonSettings.DefaultGaugeOutlineColor;

    public string CpuGaugeColor { get; init; } = JkMonSettings.DefaultCpuGaugeColor;

    public string MemoryGaugeColor { get; init; } = JkMonSettings.DefaultMemoryGaugeColor;

    public string ActivityIdleColor { get; init; } = JkMonSettings.DefaultActivityIdleColor;

    public string ActivityNormalColor { get; init; } = JkMonSettings.DefaultActivityNormalColor;

    public string ActivityElevatedColor { get; init; } = JkMonSettings.DefaultActivityElevatedColor;

    public string ActivityHighColor { get; init; } = JkMonSettings.DefaultActivityHighColor;

    public int BackgroundOpacityPercent { get; init; } = 100;

    public double GaugeOutlineThickness { get; init; } = 1;

    public double GaugeLabelFontSize { get; init; } = 9;

    public double GaugeCaptionFontSize { get; init; } = 9;

    public bool TextShadow { get; init; }

    public bool CustomTextShadow { get; init; }

    public string FontFamily { get; init; } = JkMonSettings.DefaultFontFamily;

    public double FontSize { get; init; } = 13;

    public string CustomTextFontFamily { get; init; } = JkMonSettings.DefaultFontFamily;

    public double CustomTextFontSize { get; init; } = 16;

    public int CircleDiameter { get; init; } = 26;

    public AccentStripeMode AccentStripe { get; init; } = AccentStripeMode.None;

    public string AccentStripeFirstColor { get; init; } = JkMonSettings.DefaultAccentStripeFirstColor;

    public string AccentStripeSecondColor { get; init; } = JkMonSettings.DefaultAccentStripeSecondColor;

    public string AccentStripeThirdColor { get; init; } = JkMonSettings.DefaultAccentStripeThirdColor;

    public const int MaxNameLength = 40;

    public static ThemePreset From(JkMonSettings settings, string name)
    {
        var source = settings.Normalized();
        return new ThemePreset
        {
            Name = CleanName(name),
            Theme = source.Theme,
            TextColor = source.TextColor,
            BackgroundColor = source.BackgroundColor,
            CustomTextColor = source.CustomTextColor,
            GaugeOutlineColor = source.GaugeOutlineColor,
            CpuGaugeColor = source.CpuGaugeColor,
            MemoryGaugeColor = source.MemoryGaugeColor,
            ActivityIdleColor = source.ActivityIdleColor,
            ActivityNormalColor = source.ActivityNormalColor,
            ActivityElevatedColor = source.ActivityElevatedColor,
            ActivityHighColor = source.ActivityHighColor,
            BackgroundOpacityPercent = source.BackgroundOpacityPercent,
            GaugeOutlineThickness = source.GaugeOutlineThickness,
            GaugeLabelFontSize = source.GaugeLabelFontSize,
            GaugeCaptionFontSize = source.GaugeCaptionFontSize,
            TextShadow = source.TextShadow,
            CustomTextShadow = source.CustomTextShadow,
            FontFamily = source.FontFamily,
            FontSize = source.FontSize,
            CustomTextFontFamily = source.CustomTextFontFamily,
            CustomTextFontSize = source.CustomTextFontSize,
            CircleDiameter = source.CircleDiameter,
            AccentStripe = source.AccentStripe,
            AccentStripeFirstColor = source.AccentStripeFirstColor,
            AccentStripeSecondColor = source.AccentStripeSecondColor,
            AccentStripeThirdColor = source.AccentStripeThirdColor
        };
    }

    /// <summary>Writes the look over the given settings and leaves every other choice untouched.</summary>
    public JkMonSettings ApplyTo(JkMonSettings settings) => (settings with
    {
        Theme = Theme,
        TextColor = TextColor,
        BackgroundColor = BackgroundColor,
        CustomTextColor = CustomTextColor,
        GaugeOutlineColor = GaugeOutlineColor,
        CpuGaugeColor = CpuGaugeColor,
        MemoryGaugeColor = MemoryGaugeColor,
        ActivityIdleColor = ActivityIdleColor,
        ActivityNormalColor = ActivityNormalColor,
        ActivityElevatedColor = ActivityElevatedColor,
        ActivityHighColor = ActivityHighColor,
        BackgroundOpacityPercent = BackgroundOpacityPercent,
        GaugeOutlineThickness = GaugeOutlineThickness,
        GaugeLabelFontSize = GaugeLabelFontSize,
        GaugeCaptionFontSize = GaugeCaptionFontSize,
        TextShadow = TextShadow,
        CustomTextShadow = CustomTextShadow,
        FontFamily = FontFamily,
        FontSize = FontSize,
        CustomTextFontFamily = CustomTextFontFamily,
        CustomTextFontSize = CustomTextFontSize,
        CircleDiameter = CircleDiameter,
        AccentStripe = AccentStripe,
        AccentStripeFirstColor = AccentStripeFirstColor,
        AccentStripeSecondColor = AccentStripeSecondColor,
        AccentStripeThirdColor = AccentStripeThirdColor
    }).Normalized();

    /// <summary>A name is a list entry and a file value, so control characters and padding are stripped.</summary>
    public static string CleanName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var single = new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        return single.Length > MaxNameLength ? single[..MaxNameLength].TrimEnd() : single;
    }
}

/// <summary>Keeps saved looks beside the settings file so a portable copy carries them too.</summary>
[SupportedOSPlatform("windows")]
public sealed class ThemePresetStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public ThemePresetStore(string? path = null) =>
        _path = path ?? Path.Combine(AppPaths.DataRoot, "themes.json");

    public string FilePath => _path;

    public IReadOnlyList<ThemePreset> All()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            var loaded = JsonSerializer.Deserialize<List<ThemePreset>>(File.ReadAllText(_path), Options);
            return loaded is null
                ? []
                : [.. loaded.Where(p => !string.IsNullOrWhiteSpace(p.Name)).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>Saving under an existing name replaces it, which is what "save" means for a named slot.</summary>
    public void Save(ThemePreset preset)
    {
        var name = ThemePreset.CleanName(preset.Name);
        if (name.Length == 0)
        {
            return;
        }

        var kept = All().Where(p => !string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        Write([.. kept, preset with { Name = name }]);
    }

    public void Delete(string name)
    {
        var clean = ThemePreset.CleanName(name);
        Write([.. All().Where(p => !string.Equals(p.Name, clean, StringComparison.OrdinalIgnoreCase))]);
    }

    private void Write(IReadOnlyList<ThemePreset> presets)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var ordered = presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        File.WriteAllText(_path, JsonSerializer.Serialize(ordered, Options));
    }
}
