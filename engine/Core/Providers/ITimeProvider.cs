using System;
using Osb.Engine.Core.Providers;

/// <summary>
/// Time provider. Implemented by hosted BCL or freestanding platform.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Current UTC date/time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// High-resolution ticks (typically 100ns intervals since epoch).
    /// </summary>
    ulong Ticks { get; }

    /// <summary>
    /// Creates a DateTime from year, month, day.
    /// Returns null if invalid.
    /// </summary>
    DateTime? CreateDate(int year, int month, int day);

    /// <summary>
    /// Creates a DateTime from hour, minute, second, millisecond.
    /// Returns null if invalid.
    /// </summary>
    DateTime? CreateTime(int hour, int minute, int second, int millisecond = 0);

    /// <summary>
    /// Converts days since Unix epoch to year, month, day, dayOfWeek.
    /// Returns null if invalid.
    /// </summary>
    (int Year, int Month, int Day, int DayOfWeek)? DaysToDate(long daysSinceEpoch);

    /// <summary>
    /// Converts milliseconds since midnight to hour, minute, second.
    /// Returns null if invalid.
    /// </summary>
    (int Hour, int Minute, int Second)? MillisToTime(long millisSinceMidnight);
}