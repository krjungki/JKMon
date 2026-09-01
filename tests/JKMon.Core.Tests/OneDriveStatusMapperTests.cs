using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class OneDriveStatusMapperTests
{
    [Theory]
    [InlineData(CloudProviderStatus.Idle, SyncState.UpToDate)]
    [InlineData(CloudProviderStatus.SyncIncremental, SyncState.Synchronizing)]
    [InlineData(CloudProviderStatus.SyncFull, SyncState.Synchronizing)]
    [InlineData(CloudProviderStatus.PopulateContent, SyncState.Synchronizing)]
    [InlineData(CloudProviderStatus.PopulateMetadata, SyncState.Synchronizing)]
    [InlineData(CloudProviderStatus.PopulateNamespace, SyncState.Synchronizing)]
    [InlineData(CloudProviderStatus.Error, SyncState.Error)]
    [InlineData(CloudProviderStatus.Terminated, SyncState.Error)]
    [InlineData(CloudProviderStatus.Disconnected, SyncState.Unknown)]
    [InlineData(CloudProviderStatus.ConnectivityLost, SyncState.Unknown)]
    public void ToSyncState_MapsDocumentedValues(CloudProviderStatus status, SyncState expected)
    {
        Assert.Equal(expected, OneDriveStatusMapper.ToSyncState(status));
    }

    [Fact]
    public void ToSyncState_TreatsUndocumentedValueAsUnknown()
    {
        Assert.Equal(SyncState.Unknown, OneDriveStatusMapper.ToSyncState((CloudProviderStatus)0x1234u));
    }

    [Fact]
    public void Aggregate_ReturnsAbsent_WhenNoSyncRootsRegistered()
    {
        Assert.Equal(SyncState.Absent, OneDriveStatusMapper.Aggregate([]));
    }

    [Fact]
    public void Aggregate_IsGreen_OnlyWhenEveryRootIsUpToDate()
    {
        Assert.Equal(SyncState.UpToDate, OneDriveStatusMapper.Aggregate([SyncState.UpToDate, SyncState.UpToDate]));
    }

    [Fact]
    public void Aggregate_IsRed_WhenAnyRootIsSyncing()
    {
        Assert.Equal(
            SyncState.Synchronizing,
            OneDriveStatusMapper.Aggregate([SyncState.UpToDate, SyncState.Synchronizing, SyncState.Unknown]));
    }

    [Fact]
    public void Aggregate_IsUnknown_WhenARootCannotBeRead()
    {
        Assert.Equal(SyncState.Unknown, OneDriveStatusMapper.Aggregate([SyncState.UpToDate, SyncState.Unknown]));
    }

    [Fact]
    public void Aggregate_PrefersError_OverOtherStates()
    {
        Assert.Equal(
            SyncState.Error,
            OneDriveStatusMapper.Aggregate([SyncState.UpToDate, SyncState.Synchronizing, SyncState.Error]));
    }

    [Fact]
    public void Aggregate_ScalesWithAdditionalRoots()
    {
        var roots = Enumerable.Repeat(SyncState.UpToDate, 5).ToList();
        Assert.Equal(SyncState.UpToDate, OneDriveStatusMapper.Aggregate(roots));

        roots.Add(SyncState.Synchronizing);
        Assert.Equal(SyncState.Synchronizing, OneDriveStatusMapper.Aggregate(roots));
    }
}
