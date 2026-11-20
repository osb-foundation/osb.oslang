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
        ValueKind.Number => NumberFormatting.FormatNumber(AsNumber),
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
}

#endregion