using JKMon.Core.Sync;

namespace JKMon.Core.Tests;

/// <summary>
/// The gate has to reject the short bursts an idle client makes while still catching a real transfer. These
/// cases are built from a 10 minute idle measurement of OneDrive, so they fail if the rule is loosened again.
/// </summary>
public class ActivityGateSustainTests
{
    private const long Threshold = 16 * 1024;
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private static ActivityGate Gate(int samplesToAssert = 3) =>
        new(Threshold, TimeSpan.FromSeconds(15), samplesToAssert);

    /// <summary>Feeds per-sample rates in bytes per second at the app's three second cadence.</summary>
    private static List<bool> Run(ActivityGate gate, params double[] ratesBytesPerSecond)
    {
        var results = new List<bool>();
        long total = 0;
        var at = Start;

        gate.Update(total, at);
        foreach (var rate in ratesBytesPerSecond)
        {
            at = at.AddSeconds(3);
            total += (long)(rate * 3);
            results.Add(gate.Update(total, at));
        }

        return results;
    }

    /// <summary>The exact burst measured on an idle client: two samples, peaking at 988 KiB/s.</summary>
    [Fact]
    public void RejectsTheMeasuredIdleBurst()
    {
        var results = Run(Gate(), 102.3 * 1024, 988.0 * 1024, 0, 0, 0);

        Assert.All(results, active => Assert.False(active));
    }

    [Fact]
    public void TheSameBurstWouldHaveTrippedTheOldSingleSampleRule()
    {
        var results = Run(Gate(samplesToAssert: 1), 102.3 * 1024, 988.0 * 1024);

        Assert.True(results[0]);
    }

    [Fact]
    public void AssertsOnceActivityIsSustained()
    {
        var results = Run(Gate(), 5 * 1024 * 1024, 5 * 1024 * 1024, 5 * 1024 * 1024);

        Assert.Equal([false, false, true], results);
    }

    [Fact]
    public void KeepsTheSignalThroughALullOnceAsserted()
    {
        var gate = Gate();

        // Three samples to assert, then a quiet sample well inside the fifteen second hold.
        var results = Run(gate, 5d * 1024 * 1024, 5d * 1024 * 1024, 5d * 1024 * 1024, 0, 0);

        Assert.True(results[2]);
        Assert.True(results[3]);
        Assert.True(results[4]);
    }

    [Fact]
    public void ReleasesAfterTheHoldExpires()
    {
        var gate = Gate();
        long total = 0;

        gate.Update(total, Start);
        for (var i = 1; i <= 3; i++)
        {
            total += 5L * 1024 * 1024 * 3;
            Assert.Equal(i == 3, gate.Update(total, Start.AddSeconds(i * 3)));
        }

        // The counter stops advancing, and this sample lands past the fifteen second hold.
        var quiet = gate.Update(total, Start.AddSeconds(9 + 18));

        Assert.False(quiet);
    }

    [Fact]
    public void ARateBelowTheThresholdBreaksTheRun()
    {
        var results = Run(Gate(), 5d * 1024 * 1024, 5d * 1024 * 1024, 1024, 5d * 1024 * 1024, 5d * 1024 * 1024);

        Assert.All(results, active => Assert.False(active));
    }

    [Fact]
    public void CountsConsecutiveSamplesForDiagnostics()
    {
        var gate = Gate();
        Run(gate, 5d * 1024 * 1024, 5d * 1024 * 1024);

        Assert.Equal(2, gate.ConsecutiveSamples);
    }
}
