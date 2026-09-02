using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class ProviderOrderViewTests
{
    private static readonly string[] All =
        [SyncProviderCatalog.OneDrive, SyncProviderCatalog.Syncthing, SyncProviderCatalog.GlobalSecureAccess];

    [Fact]
    public void Visible_KeepsOnlyProvidersThatArePresent()
    {
        var visible = ProviderOrderView.Visible(All, [SyncProviderCatalog.OneDrive]);

        Assert.Equal([SyncProviderCatalog.OneDrive], visible);
    }

    [Fact]
    public void Visible_PreservesTheStoredOrderRatherThanThePresentOrder()
    {
        var visible = ProviderOrderView.Visible(
            All, [SyncProviderCatalog.GlobalSecureAccess, SyncProviderCatalog.OneDrive]);

        Assert.Equal([SyncProviderCatalog.OneDrive, SyncProviderCatalog.GlobalSecureAccess], visible);
    }

    [Fact]
    public void Visible_IsEmptyWhenNothingIsRunning()
    {
        Assert.Empty(ProviderOrderView.Visible(All, []));
    }

    [Fact]
    public void Move_SwapsTwoVisibleProviders()
    {
        var moved = ProviderOrderView.Move(All, All, 0, 1);

        Assert.Equal(
            [SyncProviderCatalog.Syncthing, SyncProviderCatalog.OneDrive, SyncProviderCatalog.GlobalSecureAccess],
            moved);
    }

    /// <summary>The absent provider sits between the two being swapped, so a naive reinsert would displace it.</summary>
    [Fact]
    public void Move_LeavesAnAbsentProviderInItsStoredSlot()
    {
        var present = new[] { SyncProviderCatalog.OneDrive, SyncProviderCatalog.GlobalSecureAccess };

        var moved = ProviderOrderView.Move(All, present, 0, 1);

        Assert.Equal(
            [SyncProviderCatalog.GlobalSecureAccess, SyncProviderCatalog.Syncthing, SyncProviderCatalog.OneDrive],
            moved);
        Assert.Equal(SyncProviderCatalog.Syncthing, moved[1]);
    }

    [Fact]
    public void Move_IgnoresPositionsOutsideTheVisibleList()
    {
        var present = new[] { SyncProviderCatalog.OneDrive };

        Assert.Equal(All, ProviderOrderView.Move(All, present, 0, 1));
        Assert.Equal(All, ProviderOrderView.Move(All, present, -1, 0));
    }

    [Fact]
    public void Move_RoundTripsBackToTheOriginalOrder()
    {
        var once = ProviderOrderView.Move(All, All, 0, 2);
        var back = ProviderOrderView.Move(once, All, 0, 2);

        Assert.Equal(All, back);
    }
}
