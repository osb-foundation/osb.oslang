namespace Osb.Engine.Core.Values;

using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;

/// <summary>
/// Runtime type tags for the tagged value system.
/// </summary>
public enum ValueKind : byte
{
    Null = 0,
    Boolean,
    Number,
    String,
    Array,
    Object,
    Enum,
    EnumSet,
    Closure,
    KeyEvent,
    NativeFunction,
}

#region Value Struct (Tagged Union)

/// <summary>
/// Tagged value - struct with kind tag and payload union.
/// Note: For freestanding NativeAOT target, a separate implementation without BCL
/// dependencies would be needed (using raw pointers and manual memory management).
/// </summary>
public readonly partial struct Value
{
    private readonly ValueKind _kind;

    // Boolean/Number payload (8 bytes)
    private readonly ulong _payload;

    // Reference payload for heap objects
    private readonly object _heapRef;

    // Additional payload for enum (type index + member index)
    private readonly int _auxInt1;
    private readonly int _auxInt2;

    private Value(ValueKind kind, ulong payload = 0, object heapRef = null, int aux1 = 0, int aux2 = 0)
    {
        _kind = kind;
        _payload = payload;
        _heapRef = heapRef;
        _auxInt1 = aux1;
        _auxInt2 = aux2;
    }

    public ValueKind Kind => _kind;
    public bool IsNull => _kind == ValueKind.Null;
    public bool IsImmediate => _kind is ValueKind.Boolean or ValueKind.Number;

    // Factory methods
    public static Value NullValue => new(ValueKind.Null);
    public static Value Boolean(bool value) => new(ValueKind.Boolean, value ? 1ul : 0ul);
    public static Value Number(double value) => new(ValueKind.Number, BitConverter.DoubleToUInt64Bits(value));
    public static Value String(HeapString str) => new(ValueKind.String, heapRef: str);
    public static Value Array(HeapArray arr) => new(ValueKind.Array, heapRef: arr);
    public static Value Object(HeapObject obj) => new(ValueKind.Object, heapRef: obj);
    public static Value Enum(int typeIndex, int memberIndex) => new(ValueKind.Enum, aux1: typeIndex, aux2: memberIndex);
    public static Value EnumSet(HeapEnumSet set) => new(ValueKind.EnumSet, heapRef: set);
    public static Value Closure(HeapClosure closure) => new(ValueKind.Closure, heapRef: closure);
    public static Value KeyEvent(HeapKeyEvent evt) => new(ValueKind.KeyEvent, heapRef: evt);
    public static Value NativeFunction(NativeFunction fn) => new(ValueKind.NativeFunction, heapRef: new HeapNativeFunction(fn));

    // Accessors
    public bool AsBoolean => _kind == ValueKind.Boolean && _payload != 0;
    public double AsNumber => _kind == ValueKind.Number ? BitConverter.UInt64BitsToDouble(_payload) : double.NaN;
    public HeapString AsString => _kind == ValueKind.String ? (HeapString)_heapRef : null!;
    public HeapArray AsArray => _kind == ValueKind.Array ? (HeapArray)_heapRef : null!;
    public HeapObject AsObject => _kind == ValueKind.Object ? (HeapObject)_heapRef : null!;
    public (int TypeIndex, int MemberIndex) AsEnum => _kind == ValueKind.Enum ? (_auxInt1, _auxInt2) : (-1, -1);
    public HeapEnumSet AsEnumSet => _kind == ValueKind.EnumSet ? (HeapEnumSet)_heapRef : null!;
    public HeapClosure AsClosure => _kind == ValueKind.Closure ? (HeapClosure)_heapRef : null!;
    public HeapKeyEvent AsKeyEvent => _kind == ValueKind.KeyEvent ? (HeapKeyEvent)_heapRef : null!;
    public NativeFunction AsNativeFunction => _kind == ValueKind.NativeFunction ? ((HeapNativeFunction)_heapRef).Function : null!;
    public HeapNativeFunction AsNativeFunctionObj => _kind == ValueKind.NativeFunction ? (HeapNativeFunction)_heapRef : null!;

    public override string ToString() => _kind switch
    {
        ValueKind.Null => "NULL",
        ValueKind.Boolean => AsBoolean ? "TRUE" : "FALSE",
        ValueKind.Number => FormatNumber(AsNumber),
        ValueKind.String => AsString.ToString(),
        ValueKind.Array => $"ARRAY[{AsArray.Length}]",
        ValueKind.Object => AsObject switch
        {
            HeapObjectInstance inst => $"OBJECT:{inst.ClassName}",
            HeapClass cls => $"CLASS:{cls.Name}",
            _ => $"OBJECT:{AsObject?.GetType().Name ?? "NULL"}",
        },
        ValueKind.Enum => $"ENUM#{AsEnum.TypeIndex}.{AsEnum.MemberIndex}",
        ValueKind.EnumSet => $"ENUMSET[{AsEnumSet.Count}]",
        ValueKind.Closure => "CLOSURE",
        ValueKind.KeyEvent => "KEYEVENT",
        ValueKind.NativeFunction => "NATIVE_FN",
        _ => "UNKNOWN",
    };

    private static string FormatNumber(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";

        // Handle negative zero
        if (value == 0 && BitConverter.DoubleToUInt64Bits(value) == 0x8000000000000000)
            return "-0";

        // Match reference: integers without decimal, fixed precision for fractions
        // Use bit operations instead of Math.Floor/Abs for freestanding compatibility
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        bool isNegative = (bits & 0x8000000000000000) != 0;
        double absValue = isNegative ? -value : value;

        // Check if integer-valued (no fractional part) and within safe integer range
        // For doubles, integers up to 2^53 can be represented exactly
        if (absValue < 9007199254740992.0) // 2^53
        {
            double truncated = (long)absValue;
            if (absValue == truncated)
            {
                // Integer value - format without decimal point
                return FormatInteger((long)value);
            }
        }

        // Fractional value - use G15 equivalent formatting
        return FormatDouble(value);
    }

    private static string FormatInteger(long value)
    {
        if (value == 0) return "0";
        
        bool negative = value < 0;
        ulong abs = negative ? (ulong)(-value) : (ulong)value;
        
        // Count digits
        int digits = 0;
        ulong temp = abs;
        while (temp > 0)
        {
            temp /= 10;
            digits++;
        }
        
        char[] chars = new char[digits + (negative ? 1 : 0)];
        int pos = chars.Length - 1;
        
        while (abs > 0)
        {
            chars[pos--] = (char)('0' + (abs % 10));
            abs /= 10;
        }
        
        if (negative) chars[0] = '-';
        
        return new string(chars);
    }

    private static string FormatDouble(double value)
    {
        // G15-style formatting: 15 significant digits, scientific notation for very large/small
        // This is a simplified but deterministic implementation
        
        bool negative = value < 0;
        double absValue = negative ? -value : value;
        
        // Special cases handled above (NaN, Infinity, zero)
        
        // Determine if we should use scientific notation
        // G15 uses scientific for: abs >= 1e15 or abs < 1e-4 (and not zero)
        bool useScientific = absValue >= 1e15 || (absValue > 0 && absValue < 1e-4);
        
        if (useScientific)
        {
            return FormatScientific(value, negative, absValue);
        }
        else
        {
            return FormatFixed(value, negative, absValue);
        }
    }

    private static string FormatScientific(double value, bool negative, double absValue)
    {
        // Format as d.ddddddddddddddE+xxx (15 significant digits)
        // Find exponent
        int exponent = 0;
        double mantissa = absValue;
        
        if (mantissa >= 1.0)
        {
            while (mantissa >= 10.0)
            {
                mantissa /= 10.0;
                exponent++;
            }
        }
        else if (mantissa > 0)
        {
            while (mantissa < 1.0)
            {
                mantissa *= 10.0;
                exponent--;
            }
        }
        
        // Round to 15 significant digits
        // We'll format as 1 digit before decimal + 14 after = 15 significant digits
        double rounded = MathRound(mantissa * 1e14) / 1e14;
        
        // Handle rounding that pushes mantissa to 10.0
        if (rounded >= 10.0)
        {
            rounded /= 10.0;
            exponent++;
        }
        
        // Format mantissa
        long intPart = (long)rounded;
        double fracPart = rounded - intPart;
        
        // Get 14 fractional digits
        long fracDigits = (long)MathRound(fracPart * 1e14);
        
        // Build string: [-]d.ddddddddddddddE[+/-]xxx
        var sb = new System.Text.StringBuilder();
        if (negative) sb.Append('-');
        sb.Append(intPart);
        sb.Append('.');
        
        // Pad fractional part to 14 digits
        string fracStr = fracDigits.ToString();
        sb.Append('0', 14 - fracStr.Length);
        sb.Append(fracStr);
        
        sb.Append('E');
        sb.Append(exponent >= 0 ? '+' : '-');
        int absExp = exponent >= 0 ? exponent : -exponent;
        if (absExp >= 100)
        {
            sb.Append(absExp);
        }
        else if (absExp >= 10)
        {
            sb.Append('0');
            sb.Append(absExp);
        }
        else
        {
            sb.Append("00");
            sb.Append(absExp);
        }
        
        return sb.ToString();
    }

    private static string FormatFixed(double value, bool negative, double absValue)
    {
        // Format as fixed-point with up to 15 significant digits, with proper rounding
        // G15 style: show all integer digits, then fractional digits up to 15 significant digits total
        
        // Round to 15 significant digits using decimal for precision
        decimal rounded = RoundToSignificantDigitsDecimal(absValue, 15);
        
        // Extract integer and fractional parts from decimal
        long intPart = (long)rounded;
        decimal fracPart = rounded - intPart;
        
        var sb = new System.Text.StringBuilder();
        if (negative) sb.Append('-');
        sb.Append(FormatUnsignedInteger((ulong)intPart));
        
        if (fracPart > 0)
        {
            sb.Append('.');
            // Generate fractional digits from decimal (up to 14)
            for (int i = 0; i < 14; i++)
            {
                fracPart *= 10;
                int digit = (int)fracPart;
                sb.Append((char)('0' + digit));
                fracPart -= digit;
                if (fracPart == 0) break;
            }
        }
        
        return sb.ToString();
    }

    // Round a positive double to the specified number of significant digits, returns decimal
    private static decimal RoundToSignificantDigitsDecimal(double value, int digits)
    {
        if (value == 0) return 0;
        
        // Find exponent such that value * 10^-exponent is in [1, 10)
        int exponent = 0;
        double temp = value;
        if (temp >= 1.0)
        {
            while (temp >= 10.0)
            {
                temp /= 10.0;
                exponent++;
            }
        }
        else
        {
            while (temp < 1.0)
            {
                temp *= 10.0;
                exponent--;
            }
        }
        
        // Scale using decimal for precision
        int scaleExp = digits - 1 - exponent;
        decimal scale = 1.0m;
        if (scaleExp > 0)
        {
            for (int i = 0; i < scaleExp; i++)
                scale *= 10.0m;
        }
        else if (scaleExp < 0)
        {
            for (int i = 0; i < -scaleExp; i++)
                scale /= 10.0m;
        }
        
        decimal dValue = (decimal)value;
        decimal scaled = dValue * scale;
        decimal rounded = Math.Floor(scaled + 0.5m);
        return rounded / scale;
    }

    private static int CountDigits(ulong value)
    {
        if (value == 0) return 1;
        int count = 0;
        while (value > 0)
        {
            value /= 10;
            count++;
        }
        return count;
    }

    private static string FormatUnsignedInteger(ulong value)
    {
        if (value == 0) return "0";
        
        int digits = 0;
        ulong temp = value;
        while (temp > 0)
        {
            temp /= 10;
            digits++;
        }
        
        char[] chars = new char[digits];
        int pos = digits - 1;
        
        while (value > 0)
        {
            chars[pos--] = (char)('0' + (value % 10));
            value /= 10;
        }
        
        return new string(chars);
    }

    // Simple rounding helper - avoids System.Math.Round
    private static double MathRound(double x)
    {
        if (x >= 0)
            return (long)(x + 0.5);
        return (long)(x - 0.5);
    }
}

#endregion

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
        var provider = utf8Provider ?? new DefaultUtf8Provider();
        // Validate and store as UTF-8
        Utf8Bytes = provider.GetBytes(text ?? string.Empty);

        // Build code-point offset table (SQ-001: code-point indexing)
        // Each code point's starting byte offset
        var offsets = new List<int>();
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
        var provider = new DefaultUtf8Provider();
        return provider.GetString(Utf8Bytes);
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
        FieldCount = fieldNames.Length + (baseClass?.FieldCount ?? 0);
    }

    public Lang.MethodDecl? FindMethod(string name)
    {
        for (int i = 0; i < Methods.Length; i++)
        {
            if (Methods[i].Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return Methods[i];
        }
        return BaseClass?.FindMethod(name);
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
    public readonly int KeyCode;     // KEYCODE enum member index
    public readonly char Character;  // printable character or '\0'
    public readonly bool Ctrl;
    public readonly bool Alt;
    public readonly bool Shift;

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