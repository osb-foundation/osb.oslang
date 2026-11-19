using System.Collections.Generic;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// MATH namespace functions.
/// All math operations delegated to IMathProvider.
/// </summary>
public static class MathNamespace
{
    public static bool Call(string method, IReadOnlyList<Value> args, SourceLocation location, ExecutionContext ctx,
        IMathProvider math, ITimeProvider time, IRandomProvider random, IUtf8Provider utf8, out Value result)
    {
        result = method switch
        {
            "PI" => Value.Number(math.PI),
            "E" => Value.Number(math.E),
            "SQRT" => OneArg(args, x => math.Sqrt(x), ctx),
            "ABS" => OneArg(args, x => math.Abs(x), ctx),
            "POW" => TwoArgs(args, (x, y) => math.Pow(x, y), ctx),
            "FLOOR" => OneArg(args, x => math.Floor(x), ctx),
            "CEIL" => OneArg(args, x => math.Ceil(x), ctx),
            "ROUND" => OneArg(args, x => math.Round(x), ctx), // banker's rounding
            "TRUNC" => OneArg(args, x => math.Trunc(x), ctx),
            "SIN" => OneArg(args, x => math.Sin(x), ctx),
            "COS" => OneArg(args, x => math.Cos(x), ctx),
            "TAN" => OneArg(args, x => math.Tan(x), ctx),
            "ASIN" => OneArg(args, x => math.Asin(x), ctx),
            "ACOS" => OneArg(args, x => math.Acos(x), ctx),
            "ATAN" => OneArg(args, x => math.Atan(x), ctx),
            "LOG" => OneArg(args, x => math.Log(x), ctx),
            "LOG10" => OneArg(args, x => math.Log10(x), ctx),
            "EXP" => OneArg(args, x => math.Exp(x), ctx),
            "SIGN" => OneArg(args, x => math.Sign(x), ctx),
            "MIN" => TwoArgs(args, (x, y) => math.Min(x, y), ctx),
            "MAX" => TwoArgs(args, (x, y) => math.Max(x, y), ctx),
            "CLAMP" => ThreeArgs(args, (x, min, max) => math.Clamp(x, min, max), ctx),
            "MOD" => TwoArgs(args, (x, y) => x % y, ctx),
            "RANDOM" => Random(args, ctx, random),
            _ => Value.NullValue,
        };
        return result.Kind != ValueKind.Null;
    }

    private static Value OneArg(IReadOnlyList<Value> args, System.Func<double, double> fn, ExecutionContext ctx)
    {
        if (args.Count != 1 || args[0].Kind != ValueKind.Number) return Value.NullValue;
        return Value.Number(fn(args[0].AsNumber));
    }

    private static Value TwoArgs(IReadOnlyList<Value> args, System.Func<double, double, double> fn, ExecutionContext ctx)
    {
        if (args.Count != 2 || args[0].Kind != ValueKind.Number || args[1].Kind != ValueKind.Number) return Value.NullValue;
        return Value.Number(fn(args[0].AsNumber, args[1].AsNumber));
    }

    private static Value ThreeArgs(IReadOnlyList<Value> args, System.Func<double, double, double, double> fn, ExecutionContext ctx)
    {
        if (args.Count != 3 || args[0].Kind != ValueKind.Number || args[1].Kind != ValueKind.Number || args[2].Kind != ValueKind.Number) return Value.NullValue;
        return Value.Number(fn(args[0].AsNumber, args[1].AsNumber, args[2].AsNumber));
    }

    private static Value Random(IReadOnlyList<Value> args, ExecutionContext ctx, IRandomProvider random)
    {
        if (args.Count == 0) return Value.Number(random.NextDouble());
        if (args.Count == 1 && args[0].Kind == ValueKind.Number) return Value.Number(random.Next((int)args[0].AsNumber));
        if (args.Count == 2 && args[0].Kind == ValueKind.Number && args[1].Kind == ValueKind.Number) return Value.Number(random.Next((int)args[0].AsNumber, (int)args[1].AsNumber));
        return Value.NullValue;
    }
}