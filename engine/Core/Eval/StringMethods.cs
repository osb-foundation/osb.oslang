using System.Collections.Generic;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// String method implementations.
/// </summary>
public static class StringMethods
{
    public static Value Call(HeapString str, string methodName, IReadOnlyList<Value> args, SourceLocation location, ExecutionContext ctx, IUtf8Provider utf8)
    {
        return methodName.ToUpperInvariant() switch
        {
            "TOUPPER" => StringToUpper(str, ctx, utf8),
            "TOLOWER" => StringToLower(str, ctx, utf8),
            "LENGTH" or "COUNT" => Value.Number(str.CodePointCount),
            "SUBSTRING" or "SUBSTR" => StringSubstring(str, args, ctx, utf8),
            "LEFT" => StringLeft(str, args, ctx, utf8),
            "RIGHT" => StringRight(str, args, ctx, utf8),
            "REPEAT" => StringRepeat(str, args, ctx),
            "INSERT" => StringInsert(str, args, ctx),
            "REMOVE" => StringRemove(str, args, ctx),
            "INDEXOF" => StringIndexOf(str, args, ctx, utf8),
            "TRIM" => StringTrim(str, ctx, utf8),
            _ => Value.NullValue,
        };
    }

    private static Value StringToUpper(HeapString str, ExecutionContext ctx, IUtf8Provider utf8)
    {
        // SQ-002: ASCII-only case folding - operate on UTF-8 bytes
        var bytes = new byte[str.ByteLength];
        System.Array.Copy(str.Utf8Bytes, bytes, str.ByteLength);
        for (int i = 0; i < str.ByteLength; i++)
        {
            byte b = bytes[i];
            if (b >= 0x61 && b <= 0x7A) // 'a'-'z'
                bytes[i] = (byte)(b - 0x20);
        }
        return Value.String(ctx.Heap.AllocString(utf8.GetString(bytes)));
    }

    private static Value StringToLower(HeapString str, ExecutionContext ctx, IUtf8Provider utf8)
    {
        // SQ-002: ASCII-only case folding - operate on UTF-8 bytes
        var bytes = new byte[str.ByteLength];
        System.Array.Copy(str.Utf8Bytes, bytes, str.ByteLength);
        for (int i = 0; i < str.ByteLength; i++)
        {
            byte b = bytes[i];
            if (b >= 0x41 && b <= 0x5A) // 'A'-'Z'
                bytes[i] = (byte)(b + 0x20);
        }
        return Value.String(ctx.Heap.AllocString(utf8.GetString(bytes)));
    }

    private static Value StringSubstring(HeapString str, IReadOnlyList<Value> args, ExecutionContext ctx, IUtf8Provider utf8)
    {
        if (args.Count < 1 || args[0].Kind != ValueKind.Number) return Value.NullValue;
        int start = (int)args[0].AsNumber;
        int length = args.Count > 1 && args[1].Kind == ValueKind.Number ? (int)args[1].AsNumber : str.CodePointCount - start;
        if (start < 0 || start >= str.CodePointCount || length < 0 || start + length > str.CodePointCount)
            return Value.NullValue;
        var subStr = str.SubstringByCodePoints(start, length);
        return Value.String(ctx.Heap.AllocString(subStr.ToString()));
    }

    private static Value StringIndexOf(HeapString str, IReadOnlyList<Value> args, ExecutionContext ctx, IUtf8Provider utf8)
    {
        if (args.Count != 1 || args[0].Kind != ValueKind.String) return Value.Number(-1);
        var needle = args[0].AsString;
        int index = str.IndexOf(needle);
        return Value.Number(index);
    }

    private static Value StringTrim(HeapString str, ExecutionContext ctx, IUtf8Provider utf8)
    {
        // Trim ASCII whitespace from both ends
        int start = 0;
        int end = str.CodePointCount - 1;
        while (start < str.CodePointCount && IsAsciiWhitespace(str.GetCodePoint(start).Utf8Bytes[0])) start++;
        while (end >= start && IsAsciiWhitespace(str.GetCodePoint(end).Utf8Bytes[0])) end--;
        int length = end - start + 1;
        if (length <= 0) return Value.String(ctx.Heap.AllocString(""));
        var subStr = str.SubstringByCodePoints(start, length);
        return Value.String(ctx.Heap.AllocString(subStr.ToString()));
    }

    private static bool IsAsciiWhitespace(byte b) => b == 0x20 || b == 0x09 || b == 0x0A || b == 0x0D || b == 0x0B || b == 0x0C;

    private static Value StringLeft(HeapString str, IReadOnlyList<Value> args, ExecutionContext ctx, IUtf8Provider utf8)
    {
        if (args.Count != 1 || args[0].Kind != ValueKind.Number) return Value.NullValue;
        int n = (int)args[0].AsNumber;
        if (n < 0 || n > str.CodePointCount) return Value.NullValue;
        var sub = str.SubstringByCodePoints(0, n);
        return Value.String(ctx.Heap.AllocString(sub.ToString()));
    }

    private static Value StringRight(HeapString str, IReadOnlyList<Value> args, ExecutionContext ctx, IUtf8Provider utf8)
    {
        if (args.Count != 1 || args[0].Kind != ValueKind.Number) return Value.NullValue;
        int n = (int)args[0].AsNumber;
        if (n < 0 || n > str.CodePointCount) return Value.NullValue;
        var sub = str.SubstringByCodePoints(str.CodePointCount - n, n);
        return Value.String(ctx.Heap.AllocString(sub.ToString()));
    }

    private static Value StringRepeat(HeapString str, IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1 || args[0].Kind != ValueKind.Number) return Value.NullValue;
        int n = (int)args[0].AsNumber;
        if (n < 0) return Value.NullValue;
        var text = str.ToString();
        string result = "";
        for (int i = 0; i < n; i++) result += text;
        return Value.String(ctx.Heap.AllocString(result));
    }

    private static Value StringInsert(HeapString str, IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 2 || args[0].Kind != ValueKind.Number || args[1].Kind != ValueKind.String) return Value.NullValue;
        int col = (int)args[0].AsNumber;
        var ins = args[1].AsString.ToString();
        if (col < 0 || col > str.CodePointCount) return Value.NullValue;
        var before = str.SubstringByCodePoints(0, col).ToString();
        var after = str.SubstringByCodePoints(col, str.CodePointCount - col).ToString();
        return Value.String(ctx.Heap.AllocString(before + ins + after));
    }

    private static Value StringRemove(HeapString str, IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 2 || args[0].Kind != ValueKind.Number || args[1].Kind != ValueKind.Number) return Value.NullValue;
        int start = (int)args[0].AsNumber;
        int count = (int)args[1].AsNumber;
        if (start < 0 || count < 0 || start + count > str.CodePointCount) return Value.NullValue;
        var before = str.SubstringByCodePoints(0, start).ToString();
        var after = str.SubstringByCodePoints(start + count, str.CodePointCount - start - count).ToString();
        return Value.String(ctx.Heap.AllocString(before + after));
    }
}