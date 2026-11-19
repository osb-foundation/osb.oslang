using System.Collections.Generic;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// Standard library implementation (host-independent).
/// All platform services are injected via providers.
/// </summary>
public static class StandardLibrary
{
    public static bool TryCall(string name, IReadOnlyList<Value> args, SourceLocation location, ExecutionContext ctx,
        IMathProvider math, ITimeProvider time, IRandomProvider random, IUtf8Provider utf8, out Value result)
    {
        var upper = name.ToUpperInvariant();

        // String functions
        if (upper == "STR")
        {
            result = args.Count == 1 ? Value.String(ctx.Heap.AllocString(args[0].ToString())) : Value.NullValue;
            return true;
        }
        if (upper == "NUMBER")
        {
            result = args.Count == 1 && args[0].Kind == ValueKind.Number ? args[0] : Value.Number(0);
            return true;
        }
        if (upper == "BOOL")
        {
            result = Value.Boolean(args.Count > 0 && Interpreter.IsTruthy(args[0]));
            return true;
        }
        if (upper == "COUNT")
        {
            result = args.Count == 1 ? Count(args[0], utf8) : Value.Number(0);
            return true;
        }
        if (upper == "TYPEOF")
        {
            result = args.Count == 1 ? Value.String(ctx.Heap.AllocString(args[0].Kind.ToString())) : Value.NullValue;
            return true;
        }
        if (upper == "LEFT")
        {
            result = StringOp(args, (s, n) => Value.String(ctx.Heap.AllocString(s.SubstringByCodePoints(0, (int)n).ToString())), ctx, utf8);
            return true;
        }
        if (upper == "RIGHT")
        {
            result = StringOp(args, (s, n) => Value.String(ctx.Heap.AllocString(s.SubstringByCodePoints(s.CodePointCount - (int)n, (int)n).ToString())), ctx, utf8);
            return true;
        }
        if (upper == "TOUPPER") // SQ-002: ASCII-only
        {
            result = StringOp(args, s => Value.String(ctx.Heap.AllocString(AsciiToUpper(s, utf8))), ctx, utf8);
            return true;
        }
        if (upper == "TOLOWER") // SQ-002: ASCII-only
        {
            result = StringOp(args, s => Value.String(ctx.Heap.AllocString(AsciiToLower(s, utf8))), ctx, utf8);
            return true;
        }
        if (upper == "TRIM")
        {
            result = StringOp(args, s => Value.String(ctx.Heap.AllocString(s.ToString().Trim())), ctx, utf8);
            return true;
        }

        result = Value.NullValue;
        return false;
    }

    public static bool TryCallNamespace(string ns, string method, IReadOnlyList<Value> args, SourceLocation location, ExecutionContext ctx,
        IMathProvider math, ITimeProvider time, IRandomProvider random, IUtf8Provider utf8, out Value result)
    {
        var upperNs = ns.ToUpperInvariant();
        var upperMethod = method.ToUpperInvariant();

        if (upperNs == "MATH")
        {
            return MathNamespace.Call(upperMethod, args, location, ctx, math, time, random, utf8, out result);
        }
        if (upperNs == "DATE" || upperNs == "TIME")
        {
            return DateTimeNamespace.Call(upperNs, upperMethod, args, location, ctx, math, time, random, utf8, out result);
        }

        result = Value.NullValue;
        return false;
    }

    private static Value Count(Value value, IUtf8Provider utf8) => value.Kind switch
    {
        ValueKind.String => Value.Number(value.AsString.CodePointCount), // SQ-001: code points
        ValueKind.Array => Value.Number(value.AsArray.Length),
        ValueKind.Null => Value.Number(0),
        _ => throw new System.InvalidOperationException($"COUNT not supported for {value.Kind}"),
    };

    private static Value StringOp(IReadOnlyList<Value> args, Func<HeapString, Value> op, ExecutionContext ctx, IUtf8Provider utf8)
    {
        if (args.Count < 1 || args[0].Kind != ValueKind.String)
            return Value.NullValue;
        return op(args[0].AsString);
    }

    private static Value StringOp(IReadOnlyList<Value> args, Func<HeapString, double, Value> op, ExecutionContext ctx, IUtf8Provider utf8)
    {
        if (args.Count < 2 || args[0].Kind != ValueKind.String || args[1].Kind != ValueKind.Number)
            return Value.NullValue;
        return op(args[0].AsString, args[1].AsNumber);
    }

    private static string AsciiToUpper(HeapString s, IUtf8Provider utf8)
    {
        // SQ-002: ASCII-only - operate on UTF-8 bytes directly
        var bytes = s.Utf8Bytes;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b >= 0x61 && b <= 0x7A) // 'a'-'z'
                bytes[i] = (byte)(b - 0x20);
        }
        return utf8.GetString(bytes);
    }

    private static string AsciiToLower(HeapString s, IUtf8Provider utf8)
    {
        // SQ-002: ASCII-only - operate on UTF-8 bytes directly
        var bytes = s.Utf8Bytes;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b >= 0x41 && b <= 0x5A) // 'A'-'Z'
                bytes[i] = (byte)(b + 0x20);
        }
        return utf8.GetString(bytes);
    }
}