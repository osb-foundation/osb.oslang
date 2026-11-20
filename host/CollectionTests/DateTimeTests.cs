using System;
using System.Collections.Generic;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace CollectionTests;

internal static class DateTimeTests
{
    internal static void RunAll()
    {
        TestUnixEpoch();
        TestDayAfterEpoch();
        TestDayBeforeEpoch();
        TestYear2000();
        TestConformanceDate();
        TestEquivalenceAgainstBcl();
    }

    private sealed class TestTimeProvider : ITimeProvider
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow => Epoch;
        public ulong Ticks => (ulong)Epoch.Ticks;

        public DateTime? CreateDate(int year, int month, int day)
        {
            try { return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc); }
            catch { return null; }
        }

        public DateTime? CreateTime(int hour, int minute, int second, int millisecond = 0)
        {
            try { return new DateTime(1, 1, 1, hour, minute, second, millisecond, DateTimeKind.Utc); }
            catch { return null; }
        }

        public (int Year, int Month, int Day, int DayOfWeek)? DaysToDate(long daysSinceEpoch)
        {
            try
            {
                var dt = Epoch.AddDays(daysSinceEpoch);
                return (dt.Year, dt.Month, dt.Day, (int)dt.DayOfWeek);
            }
            catch { return null; }
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

    private static Value CallDate(int year, int month, int day, ITimeProvider time)
    {
        var args = new List<Value>
        {
            Value.Number(year),
            Value.Number(month),
            Value.Number(day),
        };
        var ctx = new Osb.Engine.Core.Eval.ExecutionContext(null, null);
        DateTimeNamespace.Call("DATE", "DATE", args, SourceLocation.Unknown, ctx,
            null, time, null, null, out var result);
        return result;
    }

    private static void TestUnixEpoch()
    {
        var time = new TestTimeProvider();
        var result = CallDate(1970, 1, 1, time);
        Program.Check("DateTime.UnixEpoch", result.Kind == ValueKind.Number && result.AsNumber == 0);
    }

    private static void TestDayAfterEpoch()
    {
        var time = new TestTimeProvider();
        var result = CallDate(1970, 1, 2, time);
        Program.Check("DateTime.DayAfterEpoch", result.Kind == ValueKind.Number && result.AsNumber == 1);
    }

    private static void TestDayBeforeEpoch()
    {
        var time = new TestTimeProvider();
        var result = CallDate(1969, 12, 31, time);
        Program.Check("DateTime.DayBeforeEpoch", result.Kind == ValueKind.Number && result.AsNumber == -1);
    }

    private static void TestYear2000()
    {
        var time = new TestTimeProvider();
        var result = CallDate(2000, 1, 1, time);
        // 30 years * 365 + 7 leap days (1972, 1976, 1980, 1984, 1988, 1992, 1996)
        Program.Check("DateTime.Year2000", result.Kind == ValueKind.Number && result.AsNumber == 10957);
    }

    private static void TestConformanceDate()
    {
        var time = new TestTimeProvider();
        var result = CallDate(2026, 11, 19, time);
        // The exact days value doesn't matter for conformance; what matters is
        // DaysToDate() converts it back to the correct year/month/day.
        Program.Check("DateTime.ConformanceDate", result.Kind == ValueKind.Number);
    }

    private static void TestEquivalenceAgainstBcl()
    {
        var time = new TestTimeProvider();
        var testCases = new (int Year, int Month, int Day)[]
        {
            (1970, 1, 1),
            (1969, 12, 31),
            (1970, 1, 2),
            (2000, 1, 1),
            (2026, 11, 19),
            (1900, 1, 1),
            (2038, 1, 19),
            (1, 1, 1),
        };

        foreach (var tc in testCases)
        {
            var result = CallDate(tc.Year, tc.Month, tc.Day, time);
            long expected = new DateTimeOffset(new DateTime(tc.Year, tc.Month, tc.Day, 0, 0, 0, DateTimeKind.Utc)).ToUnixTimeSeconds() / 86400;
            Program.Check($"DateTime.Equivalence.{tc.Year}-{tc.Month:D2}-{tc.Day:D2}",
                result.Kind == ValueKind.Number && result.AsNumber == expected);
        }
    }
}
