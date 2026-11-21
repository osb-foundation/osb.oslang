using System;
using System.Collections.Generic;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// DATE/TIME namespace functions (OSLANG 0.5).
/// All time operations delegated to ITimeProvider.
/// </summary>
public static class DateTimeNamespace
{
    public static bool Call(string ns, string method, IReadOnlyList<Value> args, SourceLocation location, ExecutionContext ctx,
        IMathProvider math, ITimeProvider time, IRandomProvider random, IUtf8Provider utf8, out Value result)
    {
        var upperNs = ns.ToUpperInvariant();
        var upperMethod = method.ToUpperInvariant();

        // Constructor-style calls: DATE(year, month, day) or TIME(hour, minute, second)
        if (upperMethod == upperNs)
        {
            result = upperNs switch
            {
                "DATE" => DateConstructor(args, ctx, time),
                "TIME" => TimeConstructor(args, ctx, time),
                _ => Value.NullValue,
            };
            return result.Kind != ValueKind.Null;
        }

        // Method calls: DATE.YEAR(d), TIME.HOUR(t), etc.
        result = upperMethod switch
        {
            "NOW" => Value.Number(time.Ticks),
            "YEAR" => DateComponent(args, d => d.Year, ctx, time),
            "MONTH" => DateComponent(args, d => d.Month, ctx, time),
            "DAY" => DateComponent(args, d => d.Day, ctx, time),
            "DAYOFWEEK" => DateComponent(args, d => d.DayOfWeek, ctx, time),
            "HOUR" => TimeComponent(args, m => m.Hour, ctx, time),
            "MINUTE" => TimeComponent(args, m => m.Minute, ctx, time),
            "SECOND" => TimeComponent(args, m => m.Second, ctx, time),
            _ => Value.NullValue,
        };
        return result.Kind != ValueKind.Null;
    }

    private static Value DateConstructor(IReadOnlyList<Value> args, ExecutionContext ctx, ITimeProvider time)
    {
        if (args.Count != 3) return Value.NullValue;
        if (args[0].Kind != ValueKind.Number || args[1].Kind != ValueKind.Number || args[2].Kind != ValueKind.Number)
            return Value.NullValue;

        int year = (int)args[0].AsNumber;
        int month = (int)args[1].AsNumber;
        int day = (int)args[2].AsNumber;

        var dt = time.CreateDate(year, month, day);
        if (!dt.HasValue) return Value.NullValue;

        // Convert to days since Unix epoch (1970-01-01).
        // .NET DateTime ticks are 100-nanosecond intervals since 0001-01-01.
        // Unix epoch (1970-01-01) = 621355968000000000 ticks from 0001-01-01.
        // Integer division truncates toward zero at each step, matching
        // DateTimeOffset.ToUnixTimeSeconds() / 86400 semantics exactly.
        const long UnixEpochTicks = 621355968000000000L;
        const long TicksPerSecond = 10000000L;
        const long SecondsPerDay = 86400L;

        long ticks = dt.Value.Ticks;
        long unixSeconds = (ticks - UnixEpochTicks) / TicksPerSecond;
        long daysSinceEpoch = unixSeconds / SecondsPerDay;
        return Value.Number(daysSinceEpoch);
    }

    private static Value TimeConstructor(IReadOnlyList<Value> args, ExecutionContext ctx, ITimeProvider time)
    {
        if (args.Count < 2 || args.Count > 3) return Value.NullValue;
        if (args[0].Kind != ValueKind.Number || args[1].Kind != ValueKind.Number)
            return Value.NullValue;

        int hour = (int)args[0].AsNumber;
        int minute = (int)args[1].AsNumber;
        int second = args.Count == 3 && args[2].Kind == ValueKind.Number ? (int)args[2].AsNumber : 0;

        var dt = time.CreateTime(hour, minute, second);
        if (!dt.HasValue) return Value.NullValue;

        long millis = ((long)hour * 3600 + minute * 60 + second) * 1000;
        return Value.Number(millis);
    }

    private static Value DateComponent(IReadOnlyList<Value> args, Func<(int Year, int Month, int Day, int DayOfWeek), int> selector, ExecutionContext ctx, ITimeProvider time)
    {
        if (args.Count < 1 || args[0].Kind != ValueKind.Number) return Value.NullValue;
        // days since epoch -> DateTime components
        var date = time.DaysToDate((long)args[0].AsNumber);
        if (!date.HasValue) return Value.NullValue;
        return Value.Number(selector(date.Value));
    }

    private static Value TimeComponent(IReadOnlyList<Value> args, Func<(int Hour, int Minute, int Second), int> selector, ExecutionContext ctx, ITimeProvider time)
    {
        if (args.Count < 1 || args[0].Kind != ValueKind.Number) return Value.NullValue;
        long millis = (long)args[0].AsNumber; // milliseconds since midnight
        var tm = time.MillisToTime(millis);
        if (!tm.HasValue) return Value.NullValue;
        return Value.Number(selector(tm.Value));
    }
}