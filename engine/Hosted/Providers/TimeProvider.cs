using Osb.Engine.Core.Providers;

namespace Osb.Engine.Hosted.Providers;

/// <summary>
/// Hosted BCL implementation of ITimeProvider.
/// </summary>
public sealed class TimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;

    public ulong Ticks => (ulong)DateTime.UtcNow.Ticks;

    public DateTime? CreateDate(int year, int month, int day)
    {
        try
        {
            return new DateTime(year, month, day);
        }
        catch
        {
            return null;
        }
    }

    public DateTime? CreateTime(int hour, int minute, int second, int millisecond = 0)
    {
        try
        {
            return new DateTime(1, 1, 1, hour, minute, second, millisecond);
        }
        catch
        {
            return null;
        }
    }

    public (int Year, int Month, int Day, int DayOfWeek)? DaysToDate(long daysSinceEpoch)
    {
        try
        {
            var dt = DateTimeOffset.FromUnixTimeSeconds(daysSinceEpoch * 86400).DateTime;
            return (dt.Year, dt.Month, dt.Day, (int)dt.DayOfWeek);
        }
        catch
        {
            return null;
        }
    }

    public (int Hour, int Minute, int Second)? MillisToTime(long millisSinceMidnight)
    {
        if (millisSinceMidnight < 0 || millisSinceMidnight >= 86400000) return null;
        int hour = (int)(millisSinceMidnight / 3600000);
        int minute = (int)((millisSinceMidnight % 3600000) / 60000);
        int second = (int)((millisSinceMidnight % 60000) / 1000);
        return (hour, minute, second);
    }
}