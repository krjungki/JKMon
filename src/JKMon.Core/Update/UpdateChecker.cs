using System.Text.Json;
using System.Text.Json.Serialization;

namespace JKMon.Core.Update;

/// <summary>What a release offers, resolved from the version file published beside the artifacts.</summary>
public readonly record struct UpdateInfo(ReleaseVersion Version, Uri PackageUrl, Uri ChecksumUrl, string PackageName);

/// <summary>
/// Reads the version file from the project's own release permalink. The permalink is used rather than the REST API
/// because unauthenticated API calls are limited to 60 per hour per address, and colleagues behind one egress
/// address would share that budget.
/// </summary>
public sealed class UpdateChecker
{
    public const string DefaultRepository = "krjungki/JKMon";

    private readonly HttpClient _http;
    private readonly string _repository;

    public UpdateChecker(HttpClient http, string? repository = null)
    {
        _http = http;
        _repository = repository ?? DefaultRepository;
    }

    public Uri LatestDownloadBase => new($"https://github.com/{_repository}/releases/latest/download/");

    public Uri ReleasesPage => new($"https://github.com/{_repository}/releases/latest");

    /// <summary>Returns null when the release cannot be read; a failed check is never treated as "up to date".</summary>
    public async Task<UpdateInfo?> TryGetLatestAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http
                .GetAsync(new Uri(LatestDownloadBase, "version.json"), cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var payload = JsonSerializer.Deserialize<VersionPayload>(json);

            if (payload is null || !ReleaseVersion.TryParse(payload.Version, out var version))
            {
                return null;
            }

            var package = $"JKMon-{version}-win-x64.zip";
            return new UpdateInfo(
                version,
                new Uri(LatestDownloadBase, package),
                new Uri(LatestDownloadBase, "SHA256SUMS.txt"),
                package);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            return null;
        }
    }

    private sealed record VersionPayload
    {
        [JsonPropertyName("version")]
        public string? Version { get; init; }
    }
}
