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
}