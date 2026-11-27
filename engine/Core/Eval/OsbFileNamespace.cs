using System.Collections.Generic;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Heap;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// OSL.FILE namespace calls. Delegates platform services to IHostApi.
/// </summary>
public static class OsbFileNamespace
{
    public static bool Call(string method, IReadOnlyList<Value> args, SourceLocation location, ExecutionContext ctx, out Value result)
    {
        var m = method.ToUpperInvariant();
        result = m switch
        {
            "EXISTS" => Exists(args, ctx),
            "READLINES" => ReadLines(args, ctx),
            "WRITELINES" => WriteLines(args, ctx),
            _ => Value.NullValue,
        };
        return result.Kind != ValueKind.Null;
    }

    private static Value Exists(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1 || args[0].Kind != ValueKind.String) return Value.NullValue;
        bool exists = ctx.Host.FileExists(args[0].AsString.ToString());
        return Value.Boolean(exists);
    }

    private static Value ReadLines(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1 || args[0].Kind != ValueKind.String) return Value.NullValue;
        var lines = ctx.Host.FileReadLines(args[0].AsString.ToString());
        var arr = ctx.Heap.AllocArray(lines.Length);
        for (int i = 0; i < lines.Length; i++)
            arr.Items[i] = Value.String(ctx.Heap.AllocString(lines[i]));
        return Value.Array(arr);
    }

    private static Value WriteLines(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 2 || args[0].Kind != ValueKind.String || args[1].Kind != ValueKind.Array)
            return Value.NullValue;
        var path = args[0].AsString.ToString();
        var arr = args[1].AsArray;
        var lines = new string[arr.Length];
        for (int i = 0; i < arr.Length; i++)
            lines[i] = arr.Items[i].Kind == ValueKind.String ? arr.Items[i].AsString.ToString() : string.Empty;
        ctx.Host.FileWriteLines(path, lines);
        return Value.Boolean(true);
    }
}
