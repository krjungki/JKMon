using JKMon.Core.Metrics;
using JKMon.Core.Presentation;
using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class OverlayModelBuilderTests
{
    private static readonly MetricsSnapshot Metrics = new(25.4, 60.2, 2048, 1024, 4096, 512);

    [Theory]
    [InlineData(SyncState.UpToDate, CircleColor.Green)]
    [InlineData(SyncState.Synchronizing, CircleColor.Red)]
    [InlineData(SyncState.Error, CircleColor.Red)]
    [InlineData(SyncState.Unknown, CircleColor.Gray)]
    [InlineData(SyncState.Absent, CircleColor.Gray)]
    public void ToColor_FollowsThePlanColourRule(SyncState state, CircleColor expected)
    {
        Assert.Equal(expected, OverlayModelBuilder.ToColor(state));
    }

    [Fact]
    public void Build_FormatsEveryMetricField()
    {
        var model = OverlayModelBuilder.Build(Metrics, []);

        Assert.Equal("25%", model.Cpu);
        Assert.Equal("60%", model.Memory);
        Assert.Equal("2 KiB/s", model.NetworkIn);
        Assert.Equal("1 KiB/s", model.NetworkOut);
        Assert.Equal("4 KiB/s", model.DiskRead);
        Assert.Equal("512 B/s", model.DiskWrite);
    }

    [Fact]
    public void Build_KeepsTheRawPercentagesForTheGauges()
    {
        var model = OverlayModelBuilder.Build(Metrics, []);

        Assert.Equal(25.4, model.CpuPercent);
        Assert.Equal(60.2, model.MemoryPercent);
    }

    [Fact]
    public void Build_PassesCorePercentagesThrough()
    {
        var metrics = Metrics with { CorePercents = new[] { 10d, 90d } };

        var model = OverlayModelBuilder.Build(metrics, []);

        Assert.Equal([10d, 90d], model.CorePercents);
    }

    [Fact]
    public void Build_YieldsNoCoresWhenTheSnapshotHasNone()
    {
        Assert.Empty(OverlayModelBuilder.Build(Metrics, []).CorePercents);
    }

    [Fact]
    public void Build_OmitsCirclesForProvidersThatAreNotRunning()
    {
        var providers = new[]
        {
            SyncProviderSnapshot.Absent("onedrive", 'O'),
            new SyncProviderSnapshot("syncthing", 'S', SyncState.UpToDate, "all folders up to date")
        };

        var model = OverlayModelBuilder.Build(Metrics, providers);

        var circle = Assert.Single(model.Circles);
        Assert.Equal('S', circle.Initial);
        Assert.Equal(CircleColor.Green, circle.Color);
    }

    [Fact]
    public void Build_PreservesProviderOrderAndGrowsWithMoreProviders()
    {
        var providers = new[]
        {
            new SyncProviderSnapshot("onedrive", 'O', SyncState.Synchronizing, "1 of 2 sync root(s) syncing"),
            new SyncProviderSnapshot("syncthing", 'S', SyncState.UpToDate, "all folders up to date"),
            new SyncProviderSnapshot("future", 'X', SyncState.Unknown, "state unavailable")
        };

        var model = OverlayModelBuilder.Build(Metrics, providers);

        Assert.Equal(3, model.Circles.Count);
        Assert.Equal(['O', 'S', 'X'], model.Circles.Select(c => c.Initial));
        Assert.Equal(
            [CircleColor.Red, CircleColor.Green, CircleColor.Gray],
            model.Circles.Select(c => c.Color));
    }

    [Fact]
    public void Build_PutsProviderDetailInTheTooltip()
    {
        var providers = new[]
        {
            new SyncProviderSnapshot("onedrive", 'O', SyncState.UpToDate, "2 sync root(s) up to date")
        };

        var circle = Assert.Single(OverlayModelBuilder.Build(Metrics, providers).Circles);

        Assert.Contains("2 sync root(s) up to date", circle.Tooltip, StringComparison.Ordinal);
    }
}
