using System.Collections.Generic;
namespace Osb.Engine.Core.Values;

using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;

#region Heap Objects

/// <summary>
/// Base class for all heap-allocated objects.
/// </summary>
public abstract class HeapObject
{
    public bool Marked; // GC mark bit
    public abstract ValueKind Kind { get; }
}

/// <summary>
/// Heap-allocated string. Immutable, length-prefixed UTF-8 (SQ-001).
/// </summary>
public sealed class HeapString : HeapObject
{
    public readonly byte[] Utf8Bytes; // UTF-8 encoded
    public readonly int[] CodePointOffsets; // byte offset of each code point (for O(1) code-point indexing)

    public int ByteLength => Utf8Bytes.Length;
    public int CodePointCount => CodePointOffsets.Length;

    public override ValueKind Kind => ValueKind.String;

    public HeapString(string text, IUtf8Provider? utf8Provider = null)
    {
        // Freestanding interface dispatch is broken (dispatch cells are not
        // initialized by the runtime), so calling GetBytes through the
        // IUtf8Provider interface reference yields a null/garbage target.
        // When no provider is supplied, use the concrete FreestandingUtf8Provider
        // and call its GetBytes directly (no interface dispatch). When a provider
        // is supplied, honor it (the host passes a provider whose generated
        // direct-call fast path resolves without the broken dispatch cell).
        string source = text ?? string.Empty;
        Utf8Bytes = utf8Provider != null
            ? utf8Provider.GetBytes(source)
            : new FreestandingUtf8Provider().GetBytes(source);

        // Build code-point offset table (SQ-001: code-point indexing)
        // Each code point's starting byte offset
        var offsets = new IntList();
        for (int i = 0; i < Utf8Bytes.Length;)
        {
            offsets.Add(i);
            int cpLen = GetUtf8CodePointLength(Utf8Bytes[i]);
            i += cpLen;
        }
        CodePointOffsets = offsets.ToArray();
    }

    public HeapString(byte[] utf8Bytes, int[] codePointOffsets)
    {
        Utf8Bytes = utf8Bytes;
        CodePointOffsets = codePointOffsets;
    }

    /// <summary>
    /// Gets a substring by code-point range [start, length).
    /// Returns a new HeapString (immutable).
    /// </summary>
    public HeapString SubstringByCodePoints(int start, int length)
    {
        if (start < 0 || start >= CodePointCount || length < 0 || start + length > CodePointCount)
            throw new System.ArgumentOutOfRangeException();

        int byteStart = CodePointOffsets[start];
        int byteEnd = (start + length < CodePointCount) ? CodePointOffsets[start + length] : ByteLength;
        int byteLen = byteEnd - byteStart;

        var newBytes = new byte[byteLen];
        System.Array.Copy(Utf8Bytes, byteStart, newBytes, 0, byteLen);

        // Build new offset table
        var newOffsets = new int[length];
        for (int i = 0; i < length; i++)
            newOffsets[i] = CodePointOffsets[start + i] - byteStart;

        return new HeapString(newBytes, newOffsets);
    }

    /// <summary>
    /// Gets the code point at the given index as a new HeapString (length 1).
    /// </summary>
    public HeapString GetCodePoint(int index)
    {
        if (index < 0 || index >= CodePointCount)
            throw new System.ArgumentOutOfRangeException();
        return SubstringByCodePoints(index, 1);
    }

    /// <summary>
    /// Finds the code-point index of the first occurrence of a substring.
    /// </summary>
    public int IndexOf(HeapString needle)
    {
        // Simple byte search, then convert to code-point index
        int byteIndex = IndexOfBytes(needle.Utf8Bytes);
        if (byteIndex < 0) return -1;
        // Find which code point contains this byte
        for (int i = 0; i < CodePointCount; i++)
        {
            int cpStart = CodePointOffsets[i];
            int cpEnd = (i + 1 < CodePointCount) ? CodePointOffsets[i + 1] : ByteLength;
            if (byteIndex >= cpStart && byteIndex < cpEnd)
                return i;
        }
        return -1;
    }

    private int IndexOfBytes(byte[] needle)
    {
        if (needle.Length == 0) return 0;
        if (needle.Length > ByteLength) return -1;

        for (int i = 0; i <= ByteLength - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (Utf8Bytes[i + j] != needle[j]) { match = false; break; }
            }
            if (match) return i;
        }
        return -1;
    }

    public override string ToString()
    {
        // Concrete call avoids the broken freestanding interface dispatch.
        return new FreestandingUtf8Provider().GetString(Utf8Bytes);
    }

    private static int GetUtf8CodePointLength(byte firstByte)
    {
        if ((firstByte & 0x80) == 0) return 1;          // ASCII
        if ((firstByte & 0xE0) == 0xC0) return 2;       // 2-byte
        if ((firstByte & 0xF0) == 0xE0) return 3;       // 3-byte
        if ((firstByte & 0xF8) == 0xF0) return 4;       // 4-byte
        return 1; // Invalid - treat as 1 byte
    }
}

/// <summary>
/// Heap-allocated array.
/// </summary>
public sealed class HeapArray : HeapObject
{
    public Value[] Items;

    public int Length => Items.Length;

    public override ValueKind Kind => ValueKind.Array;

    public HeapArray(int capacity) => Items = new Value[capacity];
    public HeapArray(Value[] items) => Items = items;
}

/// <summary>
/// Heap-allocated object (class instance).
/// </summary>
public sealed class HeapObjectInstance : HeapObject
{
    public readonly HeapClass ClassDefinition;
    public readonly Value[] Fields; // instance fields by index

    public string ClassName => ClassDefinition.Name;

    public override ValueKind Kind => ValueKind.Object;

    public HeapObjectInstance(HeapClass classDef)
    {
        ClassDefinition = classDef;
        Fields = new Value[classDef.FieldCount];
    }
}

/// <summary>
/// Class definition (stored on heap for reflection/GC).
/// </summary>
public sealed class HeapClass : HeapObject
{
    public readonly string Name;
    public readonly HeapClass? BaseClass;
    public readonly string[] FieldNames;
    public readonly Lang.MethodDecl[] Methods;
    public readonly int FieldCount;

    public override ValueKind Kind => ValueKind.Object;

    public HeapClass(string name, HeapClass? baseClass, string[] fieldNames, Lang.MethodDecl[] methods)
    {
        Name = name;
        BaseClass = baseClass;
        FieldNames = fieldNames;
        Methods = methods;
        FieldCount = fieldNames.Length + (baseClass == null ? 0 : baseClass.FieldCount);
    }

    public Lang.MethodDecl? FindMethod(string name)
    {
        for (int i = 0; i < Methods.Length; i++)
        {
            if (Methods[i].Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return Methods[i];
        }
        return BaseClass == null ? null : BaseClass.FindMethod(name);
    }
}

/// <summary>
/// Enum value.
/// </summary>
public sealed class HeapEnumSet : HeapObject
{
    public readonly string EnumTypeName;
    public readonly Value[] Values; // each is Value.Enum(typeIndex, memberIndex)

    public int Count => Values.Length;

    public override ValueKind Kind => ValueKind.EnumSet;

    public HeapEnumSet(string enumTypeName, Value[] values)
    {
        EnumTypeName = enumTypeName;
        Values = values;
    }
}

/// <summary>
/// Closure (function value + captured frame).
/// </summary>
public sealed class HeapClosure : HeapObject
{
    public readonly Lang.FunctionDecl Function;
    public readonly Value[] Captured; // captured variables

    public override ValueKind Kind => ValueKind.Closure;

    public HeapClosure(Lang.FunctionDecl function, Value[] captured)
    {
        Function = function;
        Captured = captured;
    }
}

/// <summary>
/// Key event (from OSL.CONSOLE).
/// </summary>
public sealed class HeapKeyEvent : HeapObject
{
    public int KeyCode;     // KEYCODE enum member index
    public char Character;  // printable character or '\0'
    public bool Ctrl;
    public bool Alt;
    public bool Shift;

    public override ValueKind Kind => ValueKind.KeyEvent;

    public HeapKeyEvent(int keyCode, char character, bool ctrl, bool alt, bool shift)
    {
        KeyCode = keyCode;
        Character = character;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
    }
}

/// <summary>
/// Native function wrapper for heap storage.
/// </summary>
public sealed class HeapNativeFunction : HeapObject
{
    public readonly NativeFunction Function;

    public override ValueKind Kind => ValueKind.NativeFunction;

    public HeapNativeFunction(NativeFunction function)
    {
        Function = function;
    }
}

/// <summary>
/// Native function (host API callable from OSLANG).
/// </summary>
public delegate Value NativeFunction(IReadOnlyList<Value> args, SourceLocation location);

#endregion