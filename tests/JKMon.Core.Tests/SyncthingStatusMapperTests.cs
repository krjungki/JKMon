using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class SyncthingStatusMapperTests
{
    private static SyncthingFolderStatus Folder(string state, long need = 0, long pullErrors = 0, long localChanges = 0) =>
        new("test-folder", state, need, pullErrors, localChanges);

    [Fact]
    public void Folder_IsGreen_OnlyWhenIdleAndNothingOutstanding()
    {
        Assert.Equal(SyncState.UpToDate, SyncthingStatusMapper.ToSyncState(Folder("idle")));
    }

    /// <summary>Every state the daemon can report other than idle means the folder is not up to date.</summary>
    [Theory]
    [InlineData("scanning")]
    [InlineData("scan-waiting")]
    [InlineData("syncing")]
    [InlineData("sync-waiting")]
    [InlineData("sync-preparing")]
    [InlineData("cleaning")]
    [InlineData("cleaning-waiting")]
    [InlineData("paused")]
    [InlineData("stopped")]
    [InlineData("unshared")]
    public void Folder_IsRed_ForEveryNonIdleState(string state)
    {
        Assert.Equal(SyncState.Synchronizing, SyncthingStatusMapper.ToSyncState(Folder(state)));
    }

    [Fact]
    public void Folder_IsRed_WhenIdleButOutOfSync()
    {
        Assert.Equal(SyncState.Synchronizing, SyncthingStatusMapper.ToSyncState(Folder("idle", need: 3)));
    }

    [Fact]
    public void Folder_IsRed_WhenIdleWithUnsentLocalChanges()
    {
        Assert.Equal(SyncState.Synchronizing, SyncthingStatusMapper.ToSyncState(Folder("idle", localChanges: 2)));
    }

    [Fact]
    public void Folder_IsError_ForPullErrorsOrTheErrorState()
    {
        Assert.Equal(SyncState.Error, SyncthingStatusMapper.ToSyncState(Folder("idle", pullErrors: 1)));
        Assert.Equal(SyncState.Error, SyncthingStatusMapper.ToSyncState(Folder("error")));
    }

    [Fact]
    public void Folder_IgnoresCasingOfTheStateName()
    {
        Assert.Equal(SyncState.UpToDate, SyncthingStatusMapper.ToSyncState(Folder("Idle")));
    }

    /// <summary>A missing state must not invent a red circle; the counters still decide.</summary>
    [Fact]
    public void Folder_FallsBackToCounters_WhenTheStateIsAbsent()
    {
        Assert.Equal(SyncState.UpToDate, SyncthingStatusMapper.ToSyncState(Folder(string.Empty)));
        Assert.Equal(SyncState.Synchronizing, SyncthingStatusMapper.ToSyncState(Folder(string.Empty, need: 1)));
    }

    [Fact]
    public void AggregateFolders_IsGreen_OnlyWhenEveryFolderIsClean()
    {
        Assert.Equal(SyncState.UpToDate, SyncthingStatusMapper.AggregateFolders([Folder("idle"), Folder("idle")]));
        Assert.Equal(SyncState.Synchronizing, SyncthingStatusMapper.AggregateFolders([Folder("idle"), Folder("scanning")]));
    }

    [Fact]
    public void AggregateFolders_PrefersTheMoreSeriousCondition()
    {
        var state = SyncthingStatusMapper.AggregateFolders([Folder("scanning"), Folder("idle", pullErrors: 2)]);

        Assert.Equal(SyncState.Error, state);
    }

    [Fact]
    public void AggregateFolders_IsGreen_WhenThereAreNoFolders()
    {
        Assert.Equal(SyncState.UpToDate, SyncthingStatusMapper.AggregateFolders([]));
    }

    [Fact]
    public void DescribeFolders_NamesOnlyTheUnfinishedFolders()
    {
        var detail = SyncthingStatusMapper.DescribeFolders(
            [new SyncthingFolderStatus("Photos", "idle", 0, 0, 0), new SyncthingFolderStatus("Docs", "scanning", 0, 0, 0)]);

        Assert.Equal("Docs scanning", detail);
    }

    [Fact]
    public void DescribeFolders_ReportsAllClear_WhenEveryFolderIsIdle()
    {
        Assert.Equal("all folders up to date", SyncthingStatusMapper.DescribeFolders([Folder("idle")]));
    }

    [Fact]
    public void ToSyncState_IsGreen_WhenNothingIsOutstanding()
    {
        var completion = new SyncthingCompletion(100d, 0, 0, 0);

        Assert.Equal(SyncState.UpToDate, SyncthingStatusMapper.ToSyncState(completion));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 0, 1)]
    public void ToSyncState_IsRed_WhenAnyNeedCounterIsPositive(long bytes, long items, long deletes)
    {
        var completion = new SyncthingCompletion(100d, bytes, items, deletes);

        Assert.Equal(SyncState.Synchronizing, SyncthingStatusMapper.ToSyncState(completion));
    }

    [Fact]
    public void ToSyncState_IsRed_WhenCompletionIsBelowThreshold()
    {
        var completion = new SyncthingCompletion(99.5d, 0, 0, 0);

        Assert.Equal(SyncState.Synchronizing, SyncthingStatusMapper.ToSyncState(completion));
    }

    [Fact]
    public void Describe_DoesNotLeakCountsForUnknownState()
    {
        var text = SyncthingStatusMapper.Describe(new SyncthingCompletion(0, 0, 0, 0), SyncState.Unknown);

        Assert.Equal("state unavailable", text);
    }

    private static SyncthingCompletion Idle => new(100d, 0, 0, 0);

    [Fact]
    public void Aggregate_IsGreen_WhenLocalAndRemotesAreSettled()
    {
        Assert.Equal(SyncState.UpToDate, SyncthingStatusMapper.Aggregate(Idle, [Idle, Idle]));
    }

    [Fact]
    public void Aggregate_IsGreen_WhenNoDevicesAreConnected()
    {
        Assert.Equal(SyncState.UpToDate, SyncthingStatusMapper.Aggregate(Idle, []));
    }

    // Observed live: editing a local file leaves the local completion at 100 while the peer still needs the data.
    [Fact]
    public void Aggregate_DetectsOutgoingWork_WhenOnlyARemoteIsBehind()
    {
        var remote = new SyncthingCompletion(99.9961d, 2081821, 90, 0);

        Assert.Equal(SyncState.Synchronizing, SyncthingStatusMapper.Aggregate(Idle, [remote]));
    }

    [Fact]
    public void Aggregate_DetectsIncomingWork_WhenOnlyLocalIsBehind()
    {
        var local = new SyncthingCompletion(99.5d, 4096, 2, 0);

        Assert.Equal(SyncState.Synchronizing, SyncthingStatusMapper.Aggregate(local, [Idle]));
    }

    [Fact]
    public void Describe_ReportsOutgoingWork()
    {
        var remote = new SyncthingCompletion(99.9961d, 2081821, 90, 0);

        var text = SyncthingStatusMapper.Describe(Idle, [remote]);

        Assert.Contains("sending 90 item(s) to 1 device(s)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_ReportsBothDirections()
    {
        var local = new SyncthingCompletion(99.5d, 4096, 2, 0);
        var remote = new SyncthingCompletion(99.9d, 1024, 5, 0);

        var text = SyncthingStatusMapper.Describe(local, [remote]);

        Assert.Contains("receiving 2 item(s)", text, StringComparison.Ordinal);
        Assert.Contains("sending 5 item(s) to 1 device(s)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_IsIdle_WhenNothingIsPending()
    {
        Assert.Equal("all folders up to date", SyncthingStatusMapper.Describe(Idle, [Idle]));
    }

    [Fact]
    public void Describe_CountsOnlyTheRemotesThatAreBehind()
    {
        var behind = new SyncthingCompletion(99d, 10, 3, 0);

        var text = SyncthingStatusMapper.Describe(Idle, [Idle, behind, Idle]);

        Assert.Contains("to 1 device(s)", text, StringComparison.Ordinal);
    }
}
