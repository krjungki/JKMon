namespace JKMon.Core.Presentation;

/// <summary>
/// How busy a throughput row is. <see cref="Idle"/> is its own step rather than the bottom of the scale, because
/// "nothing is moving" is the one state a glance has to be able to tell apart without reading the numbers.
/// </summary>
public enum ActivityLevel
{
    Idle,
    Normal,
    Elevated,
    High
}

public static class ActivityLevelMath
{
    /// <summary>
    /// <paramref name="bytesPerSecond"/> is the combined rate of both directions, so a row that is only reading
    /// still lights up. Thresholds out of order are tolerated by treating the larger one as the upper step.
    /// </summary>
    public static ActivityLevel Of(double bytesPerSecond, double firstThreshold, double secondThreshold)
    {
        if (!double.IsFinite(bytesPerSecond) || bytesPerSecond <= 0)
        {
            return ActivityLevel.Idle;
        }

        var lower = Math.Min(firstThreshold, secondThreshold);
        var upper = Math.Max(firstThreshold, secondThreshold);

        if (upper > 0 && bytesPerSecond >= upper)
        {
            return ActivityLevel.High;
        }

        return lower > 0 && bytesPerSecond >= lower ? ActivityLevel.Elevated : ActivityLevel.Normal;
    }
}
