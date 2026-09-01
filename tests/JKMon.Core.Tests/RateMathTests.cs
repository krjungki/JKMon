using JKMon.Core.Metrics;

namespace JKMon.Core.Tests;

public class RateMathTests
{
    private static MetricSample Sample(
        double seconds,
        ulong idle = 0,
        ulong kernel = 0,
        ulong user = 0,
        ulong total = 0,
        ulong avail = 0,
        ulong rx = 0,
        ulong tx = 0) =>
        new(DateTimeOffset.UnixEpoch.AddSeconds(seconds), idle, kernel, user, total, avail, rx, tx, 0, 0);

    [Fact]
    public void CpuPercent_IsZero_WhenAllTimeIsIdle()
    {
        var a = Sample(0, idle: 0, kernel: 0, user: 0);
        var b = Sample(1, idle: 100, kernel: 100, user: 0);

        Assert.Equal(0d, RateMath.CpuPercent(a, b), 3);
    }

    [Fact]
    public void CpuPercent_CountsKernelAndUserMinusIdle()
    {
        var a = Sample(0);
        var b = Sample(1, idle: 25, kernel: 75, user: 25);

        Assert.Equal(75d, RateMath.CpuPercent(a, b), 3);
    }

    [Fact]
    public void CpuPercent_ReturnsZero_WhenCountersDoNotAdvance()
    {
        var a = Sample(0, idle: 10, kernel: 10, user: 10);
        var b = Sample(1, idle: 10, kernel: 10, user: 10);

        Assert.Equal(0d, RateMath.CpuPercent(a, b));
    }

    [Fact]
    public void CpuPercent_TreatsCounterResetAsNoProgress()
    {
        var a = Sample(0, idle: 500, kernel: 900, user: 100);
        var b = Sample(1, idle: 5, kernel: 9, user: 1);

        var value = RateMath.CpuPercent(a, b);

        Assert.InRange(value, 0d, 100d);
    }

    [Fact]
    public void MemoryPercent_UsesTotalMinusAvailable()
    {
        var sample = Sample(0, total: 1000, avail: 250);

        Assert.Equal(75d, RateMath.MemoryPercent(sample), 3);
    }

    [Fact]
    public void MemoryPercent_ReturnsZero_WhenTotalIsUnknown()
    {
        Assert.Equal(0d, RateMath.MemoryPercent(Sample(0, total: 0, avail: 0)));
    }

    [Theory]
    [InlineData(1, 2048)]
    [InlineData(2, 1024)]
    [InlineData(4, 512)]
    public void BytesPerSecond_ScalesWithElapsedTime(double seconds, double expected)
    {
        var value = RateMath.BytesPerSecond(0, 2048, TimeSpan.FromSeconds(seconds));

        Assert.Equal(expected, value, 3);
    }

    [Fact]
    public void BytesPerSecond_ReturnsZero_WhenElapsedIsNotPositive()
    {
        Assert.Equal(0d, RateMath.BytesPerSecond(0, 1024, TimeSpan.Zero));
    }

    [Fact]
    public void Compose_ProducesRatesForBothDirections()
    {
        var a = Sample(0, rx: 1000, tx: 2000);
        var b = Sample(2, idle: 0, kernel: 100, user: 0, total: 100, avail: 50, rx: 3048, tx: 4048);

        var snapshot = RateMath.Compose(a, b);

        Assert.Equal(1024d, snapshot.NetworkInBytesPerSecond, 3);
        Assert.Equal(1024d, snapshot.NetworkOutBytesPerSecond, 3);
        Assert.Equal(50d, snapshot.MemoryPercent, 3);
    }
}
