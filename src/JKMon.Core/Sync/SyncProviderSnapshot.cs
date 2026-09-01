namespace JKMon.Core.Sync;

/// <summary>One status circle. <paramref name="Initial"/> is the letter drawn inside it.</summary>
public readonly record struct SyncProviderSnapshot(
    string ProviderId,
    char Initial,
    SyncState State,
    string Detail)
{
    public bool IsVisible => State != SyncState.Absent;

    public static SyncProviderSnapshot Absent(string providerId, char initial) =>
        new(providerId, initial, SyncState.Absent, "not running");
}
