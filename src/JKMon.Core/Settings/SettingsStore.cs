using System.Runtime.Versioning;
using System.Text.Json;

namespace JKMon.Core.Settings;

/// <summary>Persists settings in the portable data root so a copied folder carries its own configuration.</summary>
[SupportedOSPlatform("windows")]
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsStore(string? path = null) => _path = path ?? AppPaths.SettingsFile;

    public string FilePath => _path;

    public JkMonSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new JkMonSettings();
            }

            var json = File.ReadAllText(_path);
            var loaded = JsonSerializer.Deserialize<JkMonSettings>(json, Options);
            return (loaded ?? new JkMonSettings()).Normalized();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new JkMonSettings();
        }
    }

    public void Save(JkMonSettings settings)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(settings.Normalized(), Options));
    }
}
