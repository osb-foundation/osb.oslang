using System.Collections.Generic;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Heap;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// OSL.CONSOLE namespace calls. Delegates platform services to IHostApi.
/// No delegates are used (freestanding NativeAOT cannot compile them).
/// </summary>
public static class OsbConsoleNamespace
{
    // Reusable KeyEvent to avoid per-GETKEY heap allocation in freestanding
    // NativeAOT (no GC, ~32KB heap). Safe because Key is a local variable
    // immediately consumed by HandleKey — no cross-iteration references.
    private static HeapKeyEvent _keyEvent = new(0, '\0', false, false, false);

    public static bool Call(string method, IReadOnlyList<Value> args, SourceLocation location, ExecutionContext ctx, out Value result)
    {
        var m = method.ToUpperInvariant();
        switch (m)
        {
            case "ENTER":
                ctx.Host.ConsoleEnter();
                result = Value.Boolean(true);
                return true;
            case "ALTERNATE":
                if (args.Count != 1 || args[0].Kind != ValueKind.Boolean) { result = Value.NullValue; return false; }
                ctx.Host.ConsoleAlternate(args[0].AsBoolean);
                result = Value.Boolean(true);
                return true;
            case "HIDECURSOR":
                ctx.Host.ConsoleHideCursor();
                result = Value.Boolean(true);
                return true;
            case "SHOWCURSOR":
                ctx.Host.ConsoleShowCursor();
                result = Value.Boolean(true);
                return true;
            case "EXIT":
                ctx.Host.ConsoleExit();
                result = Value.Boolean(true);
                return true;
            case "GETKEY":
                result = GetKey(ctx);
                return true;
            case "SIZE":
                result = Size(ctx);
                return true;
            case "BEGINFRAME":
                ctx.Host.ConsoleBeginFrame();
                result = Value.Boolean(true);
                return true;
            case "ENDFRAME":
                ctx.Host.ConsoleEndFrame();
                result = Value.Boolean(true);
                return true;
            case "WRITE":
                result = Write(args, ctx);
                return result.Kind != ValueKind.Null;
            case "SETCURSOR":
                result = SetCursor(args, ctx);
                return result.Kind != ValueKind.Null;
            case "SHOW":
                result = Show(args, ctx);
                return result.Kind != ValueKind.Null;
            case "INPUT":
                result = Input(ctx);
                return true;
            default:
                result = Value.NullValue;
                return false;
        }
    }

    private static Value GetKey(ExecutionContext ctx)
    {
        ctx.Host.ConsoleGetKey(out int keyCode, out char character, out bool ctrl);
        _keyEvent.KeyCode = keyCode;
        _keyEvent.Character = character;
        _keyEvent.Ctrl = ctrl;
        return Value.KeyEvent(_keyEvent);
    }

    private static Value Size(ExecutionContext ctx)
    {
        ctx.Host.ConsoleSize(out int width, out int height);
        var sizeClass = new HeapClass("Size", null, new[] { "WIDTH", "HEIGHT" }, new Lang.MethodDecl[0]);
        var instance = ctx.Heap.AllocObject(sizeClass);
        instance.Fields[0] = Value.Number(width);
        instance.Fields[1] = Value.Number(height);
        return Value.Object(instance);
    }

    private static Value Write(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 3 || args[0].Kind != ValueKind.Number || args[1].Kind != ValueKind.Number || args[2].Kind != ValueKind.String)
            return Value.NullValue;
        ctx.Host.ConsoleWrite((int)args[0].AsNumber, (int)args[1].AsNumber, args[2].AsString.ToString());
        return Value.Boolean(true);
    }

    private static Value SetCursor(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 2 || args[0].Kind != ValueKind.Number || args[1].Kind != ValueKind.Number)
            return Value.NullValue;
        ctx.Host.ConsoleSetCursor((int)args[0].AsNumber, (int)args[1].AsNumber);
        return Value.Boolean(true);
    }

    private static Value Show(IReadOnlyList<Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1 || args[0].Kind != ValueKind.String) return Value.NullValue;
        ctx.Host.ConsoleShow(args[0].AsString.ToString());
        return Value.Boolean(true);
    }

    private static Value Input(ExecutionContext ctx)
    {
        var text = ctx.Host.ConsoleInput();
        return Value.String(ctx.Heap.AllocString(text));
    }
}
