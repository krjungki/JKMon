using JKMon.Core.Metrics;
using JKMon.Core.Sync;

namespace JKMon.Core.Presentation;

public static class OverlayModelBuilder
{
    public static CircleColor ToColor(SyncState state) => state switch
    {
        SyncState.UpToDate => CircleColor.Green,
        SyncState.Synchronizing or SyncState.Error => CircleColor.Red,
        _ => CircleColor.Gray
    };

    public static OverlayModel Build(
        in MetricsSnapshot metrics,
        IEnumerable<SyncProviderSnapshot> providers,
        IReadOnlyList<string>? order = null,
        ActivityThresholds? thresholds = null)
    {
        var limits = thresholds ?? ActivityThresholds.Default;
        var visible = providers.Where(provider => provider.IsVisible);

        // OrderBy is stable, so a provider the order does not mention keeps its position among the others.
        var ordered = order is null
            ? visible
            : visible.OrderBy(provider => SyncProviderCatalog.RankOf(order, provider.ProviderId));

        var circles = new List<SyncCircle>();
        foreach (var provider in ordered)
        {
            circles.Add(new SyncCircle(
                provider.ProviderId,
                provider.Initial,
                ToColor(provider.State),
                $"{SyncProviderCatalog.DisplayName(provider.ProviderId)}: {provider.Detail}"));
        }

        return new OverlayModel
        {
            Cpu = ByteRateFormatter.FormatPercent(metrics.CpuPercent),
            Memory = ByteRateFormatter.FormatPercent(metrics.MemoryPercent),
            CpuPercent = metrics.CpuPercent,
            MemoryPercent = metrics.MemoryPercent,
            CorePercents = metrics.Cores,
            NetworkIn = ByteRateFormatter.Format(metrics.NetworkInBytesPerSecond),
            NetworkOut = ByteRateFormatter.Format(metrics.NetworkOutBytesPerSecond),
            DiskRead = ByteRateFormatter.Format(metrics.DiskReadBytesPerSecond),
            DiskWrite = ByteRateFormatter.Format(metrics.DiskWriteBytesPerSecond),
            NetworkLevel = ActivityLevelMath.Of(
                metrics.NetworkInBytesPerSecond + metrics.NetworkOutBytesPerSecond,
                limits.NetworkFirst,
                limits.NetworkSecond),
            DiskLevel = ActivityLevelMath.Of(
                metrics.DiskReadBytesPerSecond + metrics.DiskWriteBytesPerSecond,
                limits.DiskFirst,
                limits.DiskSecond),
            Circles = circles
        };
    }
}
