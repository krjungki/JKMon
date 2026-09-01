namespace JKMon.Core.Sync;

/// <summary>A pollable sync provider. Implementations must never throw; they report failures as state instead.</summary>
public interface ISyncProvider
{
    string ProviderId { get; }

    char Initial { get; }

    Task<SyncProviderSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);
}
