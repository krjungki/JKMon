namespace JKMon.Core.Sync;

/// <summary>
/// Turns a monotonic byte counter into an on/off activity signal. A hold window keeps the signal asserted across
/// the short idle gaps that appear between transfer bursts.
/// </summary>
public sealed class ActivityGate
{
    private readonly long _thresholdBytesPerSecond;
    private readonly TimeSpan _hold;

    private long _lastTotal = -1;
    private DateTimeOffset _lastSampled;
    private DateTimeOffset _lastActive = DateTimeOffset.MinValue;

    public ActivityGate(long thresholdBytesPerSecond, TimeSpan hold)
    {
        _thresholdBytesPerSecond = thresholdBytesPerSecond;
        _hold = hold;
    }

    public double LastRateBytesPerSecond { get; private set; }

    public bool Update(long totalBytes, DateTimeOffset now)
    {
        if (_lastTotal < 0 || now <= _lastSampled)
        {
            _lastTotal = totalBytes;
            _lastSampled = now;
            return IsWithinHold(now);
        }

        var elapsed = (now - _lastSampled).TotalSeconds;
        var delta = totalBytes >= _lastTotal ? totalBytes - _lastTotal : 0;

        _lastTotal = totalBytes;
        _lastSampled = now;
        LastRateBytesPerSecond = delta / elapsed;

        if (LastRateBytesPerSecond >= _thresholdBytesPerSecond)
        {
            _lastActive = now;
            return true;
        }

        return IsWithinHold(now);
    }

    private bool IsWithinHold(DateTimeOffset now) =>
        _lastActive != DateTimeOffset.MinValue && now - _lastActive <= _hold;
}
