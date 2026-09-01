namespace JKMon.Core.Update;

public enum UpdateCheckFrequency
{
    Never,
    Daily,
    Weekly
}

/// <summary>Decides when an automatic check may run, so the app never polls GitHub more than the user asked for.</summary>
public static class UpdateSchedule
{
    public static TimeSpan IntervalOf(UpdateCheckFrequency frequency) => frequency switch
    {
        UpdateCheckFrequency.Daily => TimeSpan.FromDays(1),
        UpdateCheckFrequency.Weekly => TimeSpan.FromDays(7),
        _ => TimeSpan.Zero
    };

    /// <summary>
    /// <paramref name="lastCheckUtc"/> of default means the app has never checked. A start-up check still obeys the
    /// interval so restarting the app repeatedly cannot turn into a request loop.
    /// </summary>
    public static bool IsDue(
        UpdateCheckFrequency frequency,
        DateTimeOffset lastCheckUtc,
        DateTimeOffset nowUtc,
        bool atStartup,
        bool checkAtStartup)
    {
        if (frequency == UpdateCheckFrequency.Never)
        {
            return false;
        }

        if (atStartup && !checkAtStartup)
        {
            return false;
        }

        if (lastCheckUtc == default)
        {
            return true;
        }

        // A clock that moved backwards would otherwise postpone checks indefinitely.
        return nowUtc < lastCheckUtc || nowUtc - lastCheckUtc >= IntervalOf(frequency);
    }
}
