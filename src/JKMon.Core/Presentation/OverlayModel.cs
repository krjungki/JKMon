using JKMon.Core.Sync;

namespace JKMon.Core.Presentation;

/// <summary>The three circle colours the plan allows. Red covers both active sync and error conditions.</summary>
public enum CircleColor
{
    Gray,
    Red,
    Green
}

public readonly record struct SyncCircle(string ProviderId, char Initial, CircleColor Color, string Tooltip);

/// <summary>Everything the overlay draws for one refresh, with no WPF types so it stays testable.</summary>
public sealed record OverlayModel
{
    public required string Cpu { get; init; }

    public required string Memory { get; init; }

    /// <summary>The same readings as <see cref="Cpu"/> and <see cref="Memory"/>, kept numeric for the gauges.</summary>
    public required double CpuPercent { get; init; }

    public required double MemoryPercent { get; init; }

    public required IReadOnlyList<double> CorePercents { get; init; }

    public required string NetworkIn { get; init; }

    public required string NetworkOut { get; init; }

    public required string DiskRead { get; init; }

    public required string DiskWrite { get; init; }

    /// <summary>Busyness of each throughput row, from the two directions combined.</summary>
    public required ActivityLevel NetworkLevel { get; init; }

    public required ActivityLevel DiskLevel { get; init; }

    public required IReadOnlyList<SyncCircle> Circles { get; init; }

    public static OverlayModel Empty { get; } = new()
    {
        Cpu = "0%",
        Memory = "0%",
        CpuPercent = 0,
        MemoryPercent = 0,
        CorePercents = [],
        NetworkIn = "0 B/s",
        NetworkOut = "0 B/s",
        DiskRead = "0 B/s",
        DiskWrite = "0 B/s",
        NetworkLevel = ActivityLevel.Idle,
        DiskLevel = ActivityLevel.Idle,
        Circles = []
    };
}
