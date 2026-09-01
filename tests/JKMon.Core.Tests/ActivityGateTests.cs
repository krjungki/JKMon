using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

public class ActivityGateTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static ActivityGate Gate() => new(64 * 1024, TimeSpan.FromSeconds(6));

    [Fact]
    public void FirstSampleOnlyEstablishesABaseline()
    {
        var gate = Gate();

        Assert.False(gate.Update(1_000_000, Start));
    }

    [Fact]
    public void ReportsActive_WhenRateExceedsThreshold()
    {
        var gate = Gate();
        gate.Update(0, Start);

        Assert.True(gate.Update(10 * 1024 * 1024, Start.AddSeconds(1)));
    }

    [Fact]
    public void ReportsIdle_WhenRateStaysBelowThreshold()
    {
        var gate = Gate();
        gate.Update(0, Start);

        Assert.False(gate.Update(2 * 1024, Start.AddSeconds(1)));
    }

    [Fact]
    public void HoldsActive_AcrossShortGapsBetweenBursts()
    {
        var gate = Gate();
        gate.Update(0, Start);
        gate.Update(10 * 1024 * 1024, Start.AddSeconds(1));

        // A three second lull inside a transfer must not flip the circle back to green.
        Assert.True(gate.Update(10 * 1024 * 1024, Start.AddSeconds(4)));
    }

    [Fact]
    public void ReleasesActive_AfterTheHoldWindowExpires()
    {
        var gate = Gate();
        gate.Update(0, Start);
        gate.Update(10 * 1024 * 1024, Start.AddSeconds(1));

        Assert.False(gate.Update(10 * 1024 * 1024, Start.AddSeconds(8)));
    }

    [Fact]
    public void TreatsCounterResetAsNoTransfer()
    {
        var gate = Gate();
        gate.Update(50_000_000, Start);

        Assert.False(gate.Update(10, Start.AddSeconds(1)));
    }

    [Fact]
    public void ExposesTheMeasuredRate()
    {
        var gate = Gate();
        gate.Update(0, Start);
        gate.Update(2 * 1024 * 1024, Start.AddSeconds(2));

        Assert.Equal(1024 * 1024, gate.LastRateBytesPerSecond, 0);
    }

    [Fact]
    public void IgnoresNonAdvancingTimestamps()
    {
        var gate = Gate();
        gate.Update(0, Start);

        Assert.False(gate.Update(10 * 1024 * 1024, Start));
    }
}
