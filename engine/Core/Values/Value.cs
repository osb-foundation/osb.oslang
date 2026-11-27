namespace Osb.Engine.Core.Values;

using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Math;

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
/// Layout is kept ≤ 24 bytes to avoid freestanding ILC out-parameter copy defect.
/// Enum type/member indices are packed into _payload (high/low 32 bits).
/// </summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly partial struct Value
{
    // Reference payload for heap objects (offset 0)
    internal readonly object _heapRef;

    // Boolean/Number/Enum payload (offset 8)
    internal readonly ulong _payload;

    // Kind tag (offset 16) - within 24-byte limit for freestanding NativeAOT
    internal readonly ValueKind _kind;

    internal Value(ValueKind kind, ulong payload = 0, object heapRef = null)
    {
        _heapRef = heapRef;
        _payload = payload;
        _kind = kind;
    }

    public ValueKind Kind => _kind;
    public bool IsNull => _kind == ValueKind.Null;
    public bool IsImmediate => _kind == ValueKind.Boolean || _kind == ValueKind.Number;

    // Factory methods
    public static Value NullValue => new(ValueKind.Null);
    public static Value Boolean(bool value) => new(ValueKind.Boolean, value ? 1ul : 0ul);
    public static Value Number(double value) => new(ValueKind.Number, BitConverter.DoubleToUInt64Bits(value));
    public static Value String(HeapString str) => new(ValueKind.String, heapRef: str);
    public static Value Array(HeapArray arr) => new(ValueKind.Array, heapRef: arr);
    public static Value Object(HeapObject obj) => new(ValueKind.Object, heapRef: obj);
    internal static Value Make(ValueKind kind, ulong payload, object heapRef) => new Value(kind, payload, heapRef);
    public static Value Enum(int typeIndex, int memberIndex) => new(ValueKind.Enum, ((ulong)(uint)typeIndex << 32) | (uint)memberIndex);
    public static Value EnumSet(HeapEnumSet set) => new(ValueKind.EnumSet, heapRef: set);
    public static Value Closure(HeapClosure closure) => new(ValueKind.Closure, heapRef: closure);
    public static Value KeyEvent(HeapKeyEvent evt) => new(ValueKind.KeyEvent, heapRef: evt);
    public static Value NativeFunction(NativeFunction fn) => new(ValueKind.NativeFunction, heapRef: new HeapNativeFunction(fn));

    // Accessors
    public bool AsBoolean => _kind == ValueKind.Boolean ? _payload != 0 : false;
    public double AsNumber => _kind == ValueKind.Number ? BitConverter.UInt64BitsToDouble(_payload) : double.NaN;
    public HeapString AsString => _kind == ValueKind.String ? (HeapString)_heapRef : null!;
    public HeapArray AsArray => _kind == ValueKind.Array ? (HeapArray)_heapRef : null!;
    public HeapObject AsObject => _kind == ValueKind.Object ? (HeapObject)_heapRef : null!;
    public (int TypeIndex, int MemberIndex) AsEnum => _kind == ValueKind.Enum ? ((int)(_payload >> 32), (int)(_payload & 0xFFFFFFFFu)) : (-1, -1);
    public HeapEnumSet AsEnumSet => _kind == ValueKind.EnumSet ? (HeapEnumSet)_heapRef : null!;
    public HeapClosure AsClosure => _kind == ValueKind.Closure ? (HeapClosure)_heapRef : null!;
    public HeapKeyEvent AsKeyEvent => _kind == ValueKind.KeyEvent ? (HeapKeyEvent)_heapRef : null!;
    public NativeFunction AsNativeFunction => _kind == ValueKind.NativeFunction ? ((HeapNativeFunction)_heapRef).Function : null!;
    public HeapNativeFunction AsNativeFunctionObj => _kind == ValueKind.NativeFunction ? (HeapNativeFunction)_heapRef : null!;

    #region Explicit (non-ambiguous) accessors

    public bool TryAsNumber(out double value)
    {
        if (_kind == ValueKind.Number) { value = BitConverter.UInt64BitsToDouble(_payload); return true; }
        value = 0; return false;
    }

    public bool TryAsBoolean(out bool value)
    {
        if (_kind == ValueKind.Boolean) { value = _payload != 0; return true; }
        value = false; return false;
    }

    public bool TryAsString(out HeapString value)
    {
        if (_kind == ValueKind.String) { value = (HeapString)_heapRef; return true; }
        value = null!; return false;
    }

    public bool TryAsArray(out HeapArray value)
    {
        if (_kind == ValueKind.Array) { value = (HeapArray)_heapRef; return true; }
        value = null!; return false;
    }

    public bool TryAsObject(out HeapObject value)
    {
        if (_kind == ValueKind.Object) { value = (HeapObject)_heapRef; return true; }
        value = null!; return false;
    }

    public bool TryAsEnum(out (int TypeIndex, int MemberIndex) value)
    {
        if (_kind == ValueKind.Enum) { value = ((int)(_payload >> 32), (int)(_payload & 0xFFFFFFFFu)); return true; }
        value = (-1, -1); return false;
    }

    public bool TryAsEnumSet(out HeapEnumSet value)
    {
        if (_kind == ValueKind.EnumSet) { value = (HeapEnumSet)_heapRef; return true; }
        value = null!; return false;
    }

    public bool TryAsClosure(out HeapClosure value)
    {
        if (_kind == ValueKind.Closure) { value = (HeapClosure)_heapRef; return true; }
        value = null!; return false;
    }

    public bool TryAsKeyEvent(out HeapKeyEvent value)
    {
        if (_kind == ValueKind.KeyEvent) { value = (HeapKeyEvent)_heapRef; return true; }
        value = null!; return false;
    }

    public bool TryAsNativeFunction(out NativeFunction value)
    {
        if (_kind == ValueKind.NativeFunction) { value = ((HeapNativeFunction)_heapRef).Function; return true; }
        value = null!; return false;
    }

    #endregion

    public override string ToString() => _kind switch
    {
        ValueKind.Null => "NULL",
        ValueKind.Boolean => AsBoolean ? "TRUE" : "FALSE",
        ValueKind.Number => AsNumber.ToString(),
        ValueKind.String => _heapRef != null ? AsString.ToString() : "NULL",
        _ => "NULL",
    };
}

#endregion