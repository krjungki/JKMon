using JKMon.Core;
using JKMon.Core.Metrics;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;
using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class MonitorEngineTests
{
    private sealed class StubProvider(string id, char initial, Func<SyncProviderSnapshot> factory) : ISyncProvider
    {
        internal int Calls { get; private set; }

        public string ProviderId => id;

        public char Initial => initial;

        public Task<SyncProviderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(factory());
        }
    }

    private sealed class ThrowingProvider : ISyncProvider
    {
        public string ProviderId => "bad";

        public char Initial => 'B';

        public Task<SyncProviderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }

    private static MetricsSnapshot Metrics() => new(10, 20, 0, 0, 0, 0);

    [Fact]
    public async Task RefreshAsync_PollsProvidersOnFirstRefresh()
    {
        var provider = new StubProvider("syncthing", 'S',
            () => new SyncProviderSnapshot("syncthing", 'S', SyncState.UpToDate, "ok"));
        using var engine = new MonitorEngine(Metrics, [provider]);

        var model = await engine.RefreshAsync();

        Assert.Equal(1, provider.Calls);
        Assert.Single(model.Circles);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotRepollBeforeTheProviderInterval()
    {
        var time = new FakeTimeProvider();
        var provider = new StubProvider("syncthing", 'S',
            () => new SyncProviderSnapshot("syncthing", 'S', SyncState.UpToDate, "ok"));
        using var engine = new MonitorEngine(Metrics, [provider], time);

        await engine.RefreshAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        await engine.RefreshAsync();

        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task RefreshAsync_RepollsAfterTheProviderInterval()
    {
        var time = new FakeTimeProvider();
        var provider = new StubProvider("syncthing", 'S',
            () => new SyncProviderSnapshot("syncthing", 'S', SyncState.UpToDate, "ok"));
        using var engine = new MonitorEngine(Metrics, [provider], time);

        await engine.RefreshAsync();
        time.Advance(TimeSpan.FromSeconds(3));
        await engine.RefreshAsync();

        Assert.Equal(2, provider.Calls);
    }

    [Fact]
    public async Task RefreshAsync_KeepsCachedCirclesBetweenPolls()
    {
        var time = new FakeTimeProvider();
        var provider = new StubProvider("syncthing", 'S',
            () => new SyncProviderSnapshot("syncthing", 'S', SyncState.UpToDate, "ok"));
        using var engine = new MonitorEngine(Metrics, [provider], time);

        await engine.RefreshAsync();
        time.Advance(TimeSpan.FromSeconds(1));
        var second = await engine.RefreshAsync();

        Assert.Single(second.Circles);
        Assert.Equal(CircleColor.Green, second.Circles[0].Color);
    }

    [Fact]
    public async Task RefreshAsync_IsolatesAFailingProvider()
    {
        var healthy = new StubProvider("syncthing", 'S',
            () => new SyncProviderSnapshot("syncthing", 'S', SyncState.UpToDate, "ok"));
        using var engine = new MonitorEngine(Metrics, [new ThrowingProvider(), healthy]);

        var model = await engine.RefreshAsync();

        Assert.Equal(2, model.Circles.Count);
        Assert.Equal(CircleColor.Gray, model.Circles.Single(c => c.ProviderId == "bad").Color);
        Assert.Equal(CircleColor.Green, model.Circles.Single(c => c.ProviderId == "syncthing").Color);
    }

    [Fact]
    public void IsProviderPollDue_HonoursTheNormalizedMinimum()
    {
        using var engine = new MonitorEngine(Metrics, [])
        {
            Settings = new JkMonSettings { ProviderPollSeconds = 1 }
        };

        Assert.True(engine.IsProviderPollDue(DateTimeOffset.UnixEpoch));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public override DateTimeOffset GetUtcNow() => _now;

        internal void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
