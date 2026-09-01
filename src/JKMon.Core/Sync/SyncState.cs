namespace JKMon.Core.Sync;

public enum SyncState
{
    /// <summary>Provider is not running or not registered, so no circle is shown.</summary>
    Absent,

    /// <summary>Provider is running but its state could not be determined.</summary>
    Unknown,

    Synchronizing,

    UpToDate,

    Error
}
