namespace JKMon.Core.Presentation;

/// <summary>
/// Where each throughput row changes colour, in bytes per second. Network and storage get their own pair because a
/// rate that is busy for a link is unremarkable for an SSD.
/// </summary>
public readonly record struct ActivityThresholds(
    double NetworkFirst,
    double NetworkSecond,
    double DiskFirst,
    double DiskSecond)
{
    public static ActivityThresholds Default { get; } = new(
        NetworkFirst: 1024d * 1024,
        NetworkSecond: 10d * 1024 * 1024,
        DiskFirst: 5d * 1024 * 1024,
        DiskSecond: 50d * 1024 * 1024);
}
