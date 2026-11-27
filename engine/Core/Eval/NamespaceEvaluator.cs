using System.Collections.Generic;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// Namespace expression evaluation (e.g., MATH.PI, DATE.NOW).
/// </summary>
internal static class NamespaceEvaluator
{
    public static Value EvaluateNamespace(Interpreter interpreter, Lang.NamespaceExpr expr, ExecutionContext ctx,
        IMathProvider math, ITimeProvider time, IRandomProvider random, IUtf8Provider utf8)
    {
        return Value.NativeFunction((args, loc) =>
        {
            if (StandardLibrary.TryCallNamespace(expr.NamespaceName, "CALL", args, loc, ctx, math, time, random, utf8, out var result))
                return result;
            ctx.Throw($"Unknown namespace call: {expr.NamespaceName}", loc);
            return Value.NullValue;
        });
    }

    public static Value EvaluateNamespaceCall(Interpreter interpreter, Lang.NamespaceCallExpr expr, ExecutionContext ctx,
        IMathProvider math, ITimeProvider time, IRandomProvider random, IUtf8Provider utf8)
    {
        var args = new List<Value>();
        var callArgs = expr.Args;
        for (int i = 0; i < CollectionIteration.Count(callArgs); i++)
            args.Add(interpreter.Evaluate(CollectionIteration.Get(callArgs, i)));

        if (StandardLibrary.TryCallNamespace(expr.NamespaceName, expr.NamespaceName, args, expr.Location, ctx, math, time, random, utf8, out var result))
            return result;

        ctx.Throw($"Unknown namespace function: {expr.NamespaceName}", expr.Location);
        return Value.NullValue;
    }
}