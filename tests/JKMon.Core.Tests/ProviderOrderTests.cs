using JKMon.Core.Metrics;
using JKMon.Core.Presentation;
using JKMon.Core.Settings;
using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class ProviderOrderTests
{
    private static readonly MetricsSnapshot Metrics = new(25.4, 60.2, 2048, 1024, 4096, 512);

    private static SyncProviderSnapshot Up(string id, char initial) =>
        new(id, initial, SyncState.UpToDate, "settled");

    private static readonly SyncProviderSnapshot[] AllThree =
    [
        Up(SyncProviderCatalog.OneDrive, 'O'),
        Up(SyncProviderCatalog.Syncthing, 'S'),
        Up(SyncProviderCatalog.GlobalSecureAccess, 'G')
    ];

    [Fact]
    public void Normalize_FillsInTheDefaultOrderWhenNothingIsStored()
    {
        Assert.Equal(SyncProviderCatalog.DefaultOrder, SyncProviderCatalog.Normalize(null));
    }

    [Fact]
    public void Normalize_AppendsProvidersTheStoredOrderPredates()
    {
        var order = SyncProviderCatalog.Normalize([SyncProviderCatalog.Syncthing]);

        Assert.Equal(SyncProviderCatalog.Syncthing, order[0]);
        Assert.Contains(SyncProviderCatalog.OneDrive, order);
        Assert.Contains(SyncProviderCatalog.GlobalSecureAccess, order);
        Assert.Equal(SyncProviderCatalog.DefaultOrder.Count, order.Count);
    }

    [Fact]
    public void Normalize_DropsUnknownAndDuplicateIds()
    {
        var order = SyncProviderCatalog.Normalize(
            [SyncProviderCatalog.GlobalSecureAccess, "nonsense", SyncProviderCatalog.GlobalSecureAccess, "  "]);

        Assert.Equal(SyncProviderCatalog.GlobalSecureAccess, order[0]);
        Assert.Equal(SyncProviderCatalog.DefaultOrder.Count, order.Count);
        Assert.DoesNotContain("nonsense", order);
    }

    [Fact]
    public void Settings_NormalizeTheStoredOrder()
    {
        var settings = new JkMonSettings { ProviderOrder = ["nonsense"] }.Normalized();

        Assert.Equal(SyncProviderCatalog.DefaultOrder, settings.ProviderOrder);
    }

    [Fact]
    public void Build_UsesTheConfiguredOrder()
    {
        var model = OverlayModelBuilder.Build(
            Metrics,
            AllThree,
            [SyncProviderCatalog.GlobalSecureAccess, SyncProviderCatalog.Syncthing, SyncProviderCatalog.OneDrive]);

        Assert.Equal(['G', 'S', 'O'], model.Circles.Select(c => c.Initial));
    }

    [Fact]
    public void Build_KeepsTheProviderSequenceWhenNoOrderIsGiven()
    {
        var model = OverlayModelBuilder.Build(Metrics, AllThree);

        Assert.Equal(['O', 'S', 'G'], model.Circles.Select(c => c.Initial));
    }

    [Fact]
    public void Build_SortsUnlistedProvidersLast()
    {
        SyncProviderSnapshot[] providers = [Up("future", 'F'), Up(SyncProviderCatalog.Syncthing, 'S')];

        var model = OverlayModelBuilder.Build(Metrics, providers, SyncProviderCatalog.DefaultOrder);

        Assert.Equal(['S', 'F'], model.Circles.Select(c => c.Initial));
    }

    [Fact]
    public void Build_TooltipNamesTheProviderInFullRatherThanItsId()
    {
        var model = OverlayModelBuilder.Build(Metrics, [Up(SyncProviderCatalog.GlobalSecureAccess, 'G')]);

        Assert.Equal("Global Secure Access: settled", model.Circles[0].Tooltip);
    }
}
