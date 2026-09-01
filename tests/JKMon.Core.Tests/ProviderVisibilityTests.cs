using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class ProviderVisibilityTests
{
    private sealed class StoppedOneDrive : OneDriveActivityProbe
    {
        public override bool IsRunning() => false;

        public override long TotalTransferBytes() => 0;
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("service is not listening");
    }

    private static SyncthingEndpoint Endpoint => new()
    {
        BaseAddress = new Uri("http://127.0.0.1:8384/"),
        ApiKey = "unused"
    };

    [Fact]
    public void OneDrive_IsAbsent_WhenTheAppIsNotRunning()
    {
        var provider = new OneDriveSyncProvider(new StoppedOneDrive());

        var snapshot = provider.GetSnapshot();

        Assert.Equal(SyncState.Absent, snapshot.State);
        Assert.False(snapshot.IsVisible);
    }

    [Fact]
    public async Task Syncthing_IsAbsent_WhenNoLocalConfigurationExists()
    {
        using var provider = new SyncthingSyncProvider(() => null);

        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(SyncState.Absent, snapshot.State);
        Assert.False(snapshot.IsVisible);
    }

    [Fact]
    public async Task Syncthing_IsAbsent_WhenTheServiceIsNotListening()
    {
        using var http = new HttpClient(new OfflineHandler());
        using var provider = new SyncthingSyncProvider(() => Endpoint, http);

        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(SyncState.Absent, snapshot.State);
        Assert.False(snapshot.IsVisible);
    }

    [Fact]
    public void AbsentProviders_ContributeNoIndicator()
    {
        var providers = new[]
        {
            SyncProviderSnapshot.Absent("onedrive", 'O'),
            SyncProviderSnapshot.Absent("syncthing", 'S')
        };

        var model = JKMon.Core.Presentation.OverlayModelBuilder.Build(
            new JKMon.Core.Metrics.MetricsSnapshot(0, 0, 0, 0, 0, 0), providers);

        Assert.Empty(model.Circles);
    }
}
