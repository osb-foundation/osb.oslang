using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Host;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// I/O statement execution (PRINT, SHOW, INPUT).
/// </summary>
internal static class IOStatements
{
    public static void ExecutePrint(Interpreter interpreter, Lang.PrintStmt stmt, ExecutionContext ctx)
    {
        var sb = new StringBuffer();
        foreach (var expr in stmt.Expressions)
        {
            var val = interpreter.Evaluate(expr);
            sb.Append(interpreter.ToDisplayString(val));
        }
        ctx.Host.Write(sb.ToString());
        ctx.Host.Write("\n");
    }

    public static void ExecuteShow(Interpreter interpreter, Lang.ShowStmt stmt, ExecutionContext ctx)
    {
        var sb = new StringBuffer();
        foreach (var expr in stmt.Expressions)
        {
            var val = interpreter.Evaluate(expr);
            sb.Append(interpreter.ToDisplayString(val));
        }
        ctx.Host.Write(sb.ToString());
    }

    public static void ExecuteInput(Lang.InputStmt stmt, ExecutionContext ctx)
    {
        var line = ctx.Host.ReadLine() ?? string.Empty;
        var value = Value.String(ctx.Heap.AllocString(line));
        ctx.CurrentFrame.Locals[stmt.VariableName] = value;
    }
}