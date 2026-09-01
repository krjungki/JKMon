namespace JKMon.Core.Metrics;

/// <summary>Display-ready metric values derived from two consecutive samples.</summary>
public readonly record struct MetricsSnapshot(
    double CpuPercent,
    double MemoryPercent,
    double NetworkInBytesPerSecond,
    double NetworkOutBytesPerSecond,
    double DiskReadBytesPerSecond,
    double DiskWriteBytesPerSecond,
    IReadOnlyList<double>? CorePercents = null)
{
    /// <summary>Never null, because the default struct value leaves the list unset.</summary>
    public IReadOnlyList<double> Cores => CorePercents ?? [];

    public static MetricsSnapshot Empty => new(0, 0, 0, 0, 0, 0);
}
