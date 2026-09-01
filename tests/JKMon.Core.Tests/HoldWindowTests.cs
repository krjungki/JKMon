using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class HoldWindowTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    [Fact]
    public void IsInactive_BeforeAnythingIsMarked()
    {
        Assert.False(new HoldWindow(TimeSpan.FromSeconds(5)).IsActive(Start));
    }

    [Fact]
    public void StaysActive_WithinTheWindow()
    {
        var window = new HoldWindow(TimeSpan.FromSeconds(5));
        window.Mark(Start);

        Assert.True(window.IsActive(Start.AddSeconds(4)));
    }

    [Fact]
    public void StaysActive_AtTheBoundary()
    {
        var window = new HoldWindow(TimeSpan.FromSeconds(5));
        window.Mark(Start);

        Assert.True(window.IsActive(Start.AddSeconds(5)));
    }

    [Fact]
    public void Expires_AfterTheWindow()
    {
        var window = new HoldWindow(TimeSpan.FromSeconds(5));
        window.Mark(Start);

        Assert.False(window.IsActive(Start.AddSeconds(6)));
    }

    [Fact]
    public void ReMarking_ExtendsTheWindow()
    {
        var window = new HoldWindow(TimeSpan.FromSeconds(5));
        window.Mark(Start);
        window.Mark(Start.AddSeconds(4));

        Assert.True(window.IsActive(Start.AddSeconds(8)));
    }
}

public class SyncthingEventFilterTests
{
    [Theory]
    [InlineData("ItemStarted")]
    [InlineData("ItemFinished")]
    [InlineData("LocalIndexUpdated")]
    [InlineData("RemoteIndexUpdated")]
    [InlineData("DownloadProgress")]
    [InlineData("RemoteDownloadProgress")]
    [InlineData("FolderScanProgress")]
    public void RecognisesTransferAndScanEvents(string type)
    {
        Assert.True(SyncthingEventFilter.IndicatesActivity(type, null));
    }

    [Theory]
    [InlineData("ConfigSaved")]
    [InlineData("DeviceConnected")]
    [InlineData("FolderSummary")]
    [InlineData("Ping")]
    public void IgnoresEventsThatDoNotMeanTransfer(string type)
    {
        Assert.False(SyncthingEventFilter.IndicatesActivity(type, null));
    }

    [Theory]
    [InlineData("syncing", true)]
    [InlineData("scanning", true)]
    [InlineData("sync-preparing", true)]
    [InlineData("idle", false)]
    public void StateChangedDependsOnTheTargetState(string to, bool expected)
    {
        Assert.Equal(expected, SyncthingEventFilter.IndicatesActivity("StateChanged", to));
    }

    [Fact]
    public void StateChangedWithoutATargetIsNotActivity()
    {
        Assert.False(SyncthingEventFilter.IndicatesActivity("StateChanged", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MissingTypeIsNotActivity(string? type)
    {
        Assert.False(SyncthingEventFilter.IndicatesActivity(type, "syncing"));
    }
}
