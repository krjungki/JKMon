using JKMon.Core.Metrics;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;
using JKMon.Core.Sync;

namespace JKMon.Core;

/// <summary>
/// Drives the two independent cadences: fast metric refresh and a slower provider poll. Provider results are
/// cached between polls so a slow or unreachable provider never delays the metric line.
/// </summary>
public sealed class MonitorEngine : IDisposable
{
    private readonly Func<MetricsSnapshot> _readMetrics;
    private readonly IReadOnlyList<ISyncProvider> _providers;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _pollGate = new(1, 1);

    private IReadOnlyList<SyncProviderSnapshot> _cachedProviders = [];
    private DateTimeOffset _lastPoll = DateTimeOffset.MinValue;

    public MonitorEngine(
        Func<MetricsSnapshot> readMetrics,
        IReadOnlyList<ISyncProvider> providers,
        TimeProvider? timeProvider = null)
    {
        _readMetrics = readMetrics;
        _providers = providers;
        _time = timeProvider ?? TimeProvider.System;
    }

    public JkMonSettings Settings { get; set; } = new();

    public IReadOnlyList<SyncProviderSnapshot> CachedProviders => _cachedProviders;

    public bool IsProviderPollDue(DateTimeOffset now) =>
        now - _lastPoll >= TimeSpan.FromSeconds(Settings.Normalized().ProviderPollSeconds);

    public async Task<OverlayModel> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var metrics = _readMetrics();
        var now = _time.GetUtcNow();

        if (IsProviderPollDue(now) && _pollGate.Wait(0))
        {
            try
            {
                _cachedProviders = await PollProvidersAsync(cancellationToken).ConfigureAwait(false);
                _lastPoll = now;
            }
            finally
            {
                _pollGate.Release();
            }
        }

        return OverlayModelBuilder.Build(metrics, _cachedProviders, Settings.Normalized().ProviderOrder);
    }

    private async Task<IReadOnlyList<SyncProviderSnapshot>> PollProvidersAsync(CancellationToken cancellationToken)
    {
        var results = new List<SyncProviderSnapshot>(_providers.Count);
        foreach (var provider in _providers)
        {
            try
            {
                results.Add(await provider.GetSnapshotAsync(cancellationToken).ConfigureAwait(false));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failing provider must not remove the other circles.
                results.Add(new SyncProviderSnapshot(
                    provider.ProviderId, provider.Initial, SyncState.Unknown, "provider failed"));
            }
        }

        return results;
    }

    public void Dispose() => _pollGate.Dispose();
}
