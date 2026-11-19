using System.Collections.Generic;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// Array method implementations.
/// </summary>
public static class ArrayMethods
{
    public static Value Call(HeapArray array, string methodName, IReadOnlyList<Value> args, SourceLocation location, ExecutionContext ctx, IUtf8Provider utf8)
    {
        return methodName.ToUpperInvariant() switch
        {
            "COUNT" => Value.Number(array.Length),
            "FIRST" => array.Length > 0 ? array.Items[0] : Value.NullValue,
            "LAST" => array.Length > 0 ? array.Items[array.Length - 1] : Value.NullValue,
            "PUSH" => ArrayPush(array, args, ctx),
            "POP" => ArrayPop(array, ctx),
            "CLEAR" => ArrayClear(array, ctx),
            "CONTAINS" => ArrayContains(array, args, ctx),
            "REMOVEAT" => ArrayRemoveAt(array, args, ctx),
            "INSERTAT" => ArrayInsertAt(array, args, ctx),
            _ => Value.NullValue,
        };
    }

    private static Value ArrayPush(HeapArray array, IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1) return Value.NullValue;
        var newItems = new Value[array.Length + 1];
        System.Array.Copy(array.Items, newItems, array.Length);
        newItems[array.Length] = args[0];
        array.Items = newItems;
        return Value.Number(array.Length);
    }

    private static Value ArrayPop(HeapArray array, ExecutionContext ctx)
    {
        if (array.Length == 0) return Value.NullValue;
        var last = array.Items[array.Length - 1];
        var newItems = new Value[array.Length - 1];
        System.Array.Copy(array.Items, newItems, array.Length - 1);
        array.Items = newItems;
        return last;
    }

    private static Value ArrayClear(HeapArray array, ExecutionContext ctx)
    {
        array.Items = [];
        return Value.Number(0);
    }

    private static Value ArrayContains(HeapArray array, IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1) return Value.Boolean(false);
        var search = args[0];
        foreach (var item in array.Items)
        {
            if (ValuesEqual(item, search)) return Value.Boolean(true);
        }
        return Value.Boolean(false);
    }

    private static Value ArrayRemoveAt(HeapArray array, IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1 || args[0].Kind != ValueKind.Number) return Value.NullValue;
        int index = (int)args[0].AsNumber;
        if (index < 0 || index >= array.Length) return Value.NullValue;
        var newItems = new Value[array.Length - 1];
        System.Array.Copy(array.Items, 0, newItems, 0, index);
        System.Array.Copy(array.Items, index + 1, newItems, index, array.Length - index - 1);
        array.Items = newItems;
        return Value.Number(array.Length);
    }

    private static Value ArrayInsertAt(HeapArray array, IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 2 || args[0].Kind != ValueKind.Number) return Value.NullValue;
        int index = (int)args[0].AsNumber;
        if (index < 0 || index > array.Length) return Value.NullValue;
        var newItems = new Value[array.Length + 1];
        System.Array.Copy(array.Items, 0, newItems, 0, index);
        newItems[index] = args[1];
        System.Array.Copy(array.Items, index, newItems, index + 1, array.Length - index);
        array.Items = newItems;
        return Value.Number(array.Length);
    }

    private static bool ValuesEqual(Value a, Value b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            ValueKind.Null => true,
            ValueKind.Boolean => a.AsBoolean == b.AsBoolean,
            ValueKind.Number => a.AsNumber == b.AsNumber,
            ValueKind.String => a.AsString.ToString() == b.AsString.ToString(),
            _ => a.ToString() == b.ToString(),
        };
    }
}