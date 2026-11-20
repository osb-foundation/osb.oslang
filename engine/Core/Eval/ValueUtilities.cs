using Osb.Engine.Core.Values;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// Value utility functions (truthiness, equality, display formatting).
/// </summary>
internal static class ValueUtilities
{
    public static bool IsTruthy(Value value) => value switch
    {
        { Kind: ValueKind.Null } => false,
        { Kind: ValueKind.Boolean, AsBoolean: var b } => b,
        { Kind: ValueKind.Number, AsNumber: var n } => n != 0,
        { Kind: ValueKind.String } => value.AsString.CodePointCount > 0,
        { Kind: ValueKind.Array } => true,
        { Kind: ValueKind.Object } => true,
        { Kind: ValueKind.Enum } => true,
        { Kind: ValueKind.EnumSet } => true,
        _ => false,
    };

    public static bool ValuesEqual(Value a, Value b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            ValueKind.Null => true,
            ValueKind.Boolean => a.AsBoolean == b.AsBoolean,
            ValueKind.Number => a.AsNumber == b.AsNumber,
            ValueKind.String => a.AsString.ToString() == b.AsString.ToString(),
            ValueKind.Array => a.AsArray == b.AsArray,
            ValueKind.Object => a.AsObject == b.AsObject,
            ValueKind.Enum => a.AsEnum == b.AsEnum,
            ValueKind.EnumSet => a.AsEnumSet == b.AsEnumSet,
            _ => false,
        };
    }

    public static string ToDisplayString(Interpreter interpreter, Value value) => value.Kind switch
    {
        ValueKind.Null => "NULL",
        ValueKind.String => value.AsString.ToString(),
        ValueKind.Number => value.ToString(),
        ValueKind.Boolean => value.AsBoolean ? "TRUE" : "FALSE",
        ValueKind.Array => throw new System.InvalidOperationException("Cannot convert ARRAY to STRING"),
        ValueKind.Object => value.AsObject switch
        {
            HeapObjectInstance inst => inst.ClassName,
            HeapClass cls => cls.Name,
            _ => value.AsObject?.GetType().Name ?? "NULL",
        },
        ValueKind.Enum => FormatEnum(interpreter, value),
        ValueKind.EnumSet => $"ENUMSET[{value.AsEnumSet.Count}]",
        _ => value.ToString(),
    };

    private static string FormatEnum(Interpreter interpreter, Value value)
    {
        var (typeIndex, memberIndex) = value.AsEnum;
        if (typeIndex >= 0 && typeIndex < interpreter._enumNames.Count)
        {
            var enumName = interpreter._enumNames[typeIndex];
            if (interpreter._enums.TryGetValue(enumName, out var enumDecl) && memberIndex >= 0 && memberIndex < enumDecl.Members.Count)
            {
                return $"{enumName.ToUpperInvariant()}.{enumDecl.Members[memberIndex].Name}";
            }
        }
        return $"ENUM#{typeIndex}.{memberIndex}";
    }
}