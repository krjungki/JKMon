using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class GlobalSecureAccessProviderTests
{
    private static GlobalSecureAccessSyncProvider Provider(bool running, GlobalSecureAccessStatus? status) =>
        new(() => running, () => status);

    private static GlobalSecureAccessStatus Status(int eventId) =>
        new(eventId, GlobalSecureAccessStatusMapper.Describe(eventId), DateTimeOffset.UnixEpoch);

    [Fact]
    public void Snapshot_IsAbsentWhenTheClientIsNotRunning()
    {
        var snapshot = Provider(running: false, Status(GlobalSecureAccessStatusMapper.Connected)).GetSnapshot();

        Assert.Equal(SyncState.Absent, snapshot.State);
        Assert.False(snapshot.IsVisible);
    }

    [Fact]
    public void Snapshot_IsUnknownWhenTheLogHasNoStatusYet()
    {
        var snapshot = Provider(running: true, null).GetSnapshot();

        Assert.Equal(SyncState.Unknown, snapshot.State);
        Assert.Equal("no status reported yet", snapshot.Detail);
    }

    [Fact]
    public void Snapshot_IsGreenWhenAllChannelsAreConnected()
    {
        var snapshot = Provider(running: true, Status(GlobalSecureAccessStatusMapper.Connected)).GetSnapshot();

        Assert.Equal(SyncState.UpToDate, snapshot.State);
        Assert.Equal("connected to all channels", snapshot.Detail);
    }

    [Theory]
    [InlineData(GlobalSecureAccessStatusMapper.Disconnected)]
    [InlineData(GlobalSecureAccessStatusMapper.SomeChannelsDisconnected)]
    [InlineData(GlobalSecureAccessStatusMapper.ClientDisabled)]
    [InlineData(GlobalSecureAccessStatusMapper.NoNetwork)]
    [InlineData(GlobalSecureAccessStatusMapper.NoInternet)]
    [InlineData(GlobalSecureAccessStatusMapper.EmptyPolicy)]
    [InlineData(GlobalSecureAccessStatusMapper.PolicyMissing)]
    [InlineData(GlobalSecureAccessStatusMapper.PolicySchemaMismatch)]
    [InlineData(GlobalSecureAccessStatusMapper.Offboarded)]
    [InlineData(GlobalSecureAccessStatusMapper.BreakGlass)]
    public void Snapshot_TreatsEveryNonConnectedStatusAsAProblem(int eventId)
    {
        Assert.Equal(SyncState.Error, Provider(running: true, Status(eventId)).GetSnapshot().State);
    }

    [Fact]
    public void EnabledOnItsOwnIsNotAConnection()
    {
        var snapshot = Provider(running: true, Status(GlobalSecureAccessStatusMapper.ClientEnabled)).GetSnapshot();

        Assert.Equal(SyncState.Unknown, snapshot.State);
    }

    [Fact]
    public void UnknownEventIdsDoNotClaimAState()
    {
        Assert.Equal(SyncState.Unknown, GlobalSecureAccessStatusMapper.ToSyncState(1));
        Assert.Equal("state unavailable", GlobalSecureAccessStatusMapper.Describe(1));
    }

    [Fact]
    public void StatusEventIdsCoverEveryMappedCode()
    {
        Assert.Equal(12, GlobalSecureAccessStatusMapper.StatusEventIds.Count);
        Assert.Contains(GlobalSecureAccessStatusMapper.Connected, GlobalSecureAccessStatusMapper.StatusEventIds);
        Assert.All(
            GlobalSecureAccessStatusMapper.StatusEventIds,
            id => Assert.NotEqual("state unavailable", GlobalSecureAccessStatusMapper.Describe(id)));
    }

    [Fact]
    public void ProviderIdentityMatchesTheCatalog()
    {
        var provider = Provider(running: true, null);

        Assert.Equal(SyncProviderCatalog.GlobalSecureAccess, provider.ProviderId);
        Assert.Equal('G', provider.Initial);
    }
}
