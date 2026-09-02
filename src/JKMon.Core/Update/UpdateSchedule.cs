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
    /// <summary>How long to wait before retrying after a check could not reach the server.</summary>
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromHours(1);

    public static TimeSpan IntervalOf(UpdateCheckFrequency frequency) => frequency switch
    {
        UpdateCheckFrequency.Daily => TimeSpan.FromDays(1),
        UpdateCheckFrequency.Weekly => TimeSpan.FromDays(7),
        _ => TimeSpan.Zero
    };

    /// <summary>
    /// One decision for every caller, so launching the app cannot sneak past the user's start-up preference.
    ///
    /// <paramref name="checkAtStartup"/> off means a launch must never trigger a check, so the wait is measured
    /// from whichever is later, the last check or this run's start. The app then only checks after a full interval
    /// of actually running. With the switch on, the wait is measured from the last check alone, so a launch after a
    /// long gap checks immediately.
    ///
    /// <paramref name="lastCheckUtc"/> of default means the app has never checked.
    /// </summary>
    public static bool IsDue(
        UpdateCheckFrequency frequency,
        DateTimeOffset lastCheckUtc,
        DateTimeOffset appStartedUtc,
        DateTimeOffset nowUtc,
        bool checkAtStartup)
    {
        if (frequency == UpdateCheckFrequency.Never)
        {
            return false;
        }

        if (checkAtStartup && lastCheckUtc == default)
        {
            return true;
        }

        var since = checkAtStartup
            ? lastCheckUtc
            : Later(lastCheckUtc, appStartedUtc);

        if (since == default)
        {
            return true;
        }

        // A clock that moved backwards would otherwise postpone checks indefinitely.
        return nowUtc < since || nowUtc - since >= IntervalOf(frequency);
    }

    private static DateTimeOffset Later(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
