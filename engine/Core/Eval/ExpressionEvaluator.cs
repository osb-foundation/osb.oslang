using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// Expression evaluation logic.
/// </summary>
internal sealed class ExpressionEvaluator
{
    private readonly Interpreter _interpreter;
    private readonly ExecutionContext _ctx;
    private readonly IMathProvider _math;
    private readonly ITimeProvider _time;
    private readonly IRandomProvider _random;
    private readonly IUtf8Provider _utf8;
    private readonly StringMap<Lang.FunctionDecl> _functions;
    private readonly StringMap<HeapClass> _classes;
    private readonly StringMap<Lang.EnumDecl> _enums;
    private readonly UsingList _usings;
    private readonly StringMap<NativeFunction> _extensions;
    private readonly StringMap<int> _enumTypeIndices;
    private readonly StringList _enumNames;

    public ExpressionEvaluator(Interpreter interpreter)
    {
        _interpreter = interpreter;
        _ctx = interpreter._ctx;
        _math = interpreter._math;
        _time = interpreter._time;
        _random = interpreter._random;
        _utf8 = interpreter._utf8;
        _functions = interpreter._functions;
        _classes = interpreter._classes;
        _enums = interpreter._enums;
        _usings = interpreter._usings;
        _extensions = interpreter._extensions;
        _enumTypeIndices = interpreter._enumTypeIndices;
        _enumNames = interpreter._enumNames;
    }

    public Value Evaluate(Lang.Expr expr)
    {
        if (_ctx.CurrentFlow != ControlFlow.Normal) return Value.NullValue;

        return expr switch
        {
            Lang.NumberLiteralExpr n => Value.Number(n.Value),
            Lang.StringLiteralExpr s => Value.String(_ctx.Heap.AllocString(s.Value)),
            Lang.DateLiteralExpr d => Value.Number(d.DaysSinceEpoch),
            Lang.TimeLiteralExpr t => Value.Number(t.MillisSinceMidnight),
            Lang.BooleanLiteralExpr b => Value.Boolean(b.Value),
            Lang.NullLiteralExpr => Value.NullValue,
            Lang.ArrayLiteralExpr a => EvaluateArray(a),
            Lang.IdentifierExpr id => ResolveIdentifier(id),
            Lang.CallExpr c => EvaluateCall(c),
            Lang.MethodCallExpr mc => EvaluateMethodCall(mc),
            Lang.MemberAccessExpr ma => EvaluateMemberAccess(ma),
            Lang.IndexExpr ix => EvaluateIndex(ix),
            Lang.NewExpr ne => EvaluateNew(ne),
            Lang.MeExpr => _ctx.CurrentFrame.This,
            Lang.BaseExpr => _ctx.CurrentFrame.Base,
            Lang.UnaryExpr u => EvaluateUnary(u),
            Lang.BinaryExpr b => EvaluateBinary(b),
            Lang.NamespaceExpr ns => NamespaceEvaluator.EvaluateNamespace(_interpreter, ns, _ctx, _math, _time, _random, _utf8),
            Lang.NamespaceCallExpr nsc => NamespaceEvaluator.EvaluateNamespaceCall(_interpreter, nsc, _ctx, _math, _time, _random, _utf8),
            Lang.PostfixExpr p => EvaluatePostfix(p),
            _ => throw new System.InvalidOperationException($"Unknown expression type: {expr.GetType().Name}"),
        };
    }

    private Value EvaluateArray(Lang.ArrayLiteralExpr expr)
    {
        var elements = expr.Elements;
        var arr = _ctx.Heap.AllocArray(CollectionIteration.Count(elements));
        for (int i = 0; i < CollectionIteration.Count(elements); i++)
        {
            arr.Items[i] = Evaluate(CollectionIteration.Get(elements, i));
        }
        return Value.Array(arr);
    }

    private Value ResolveIdentifier(Lang.IdentifierExpr id)
    {
        // Use the return-buffer getter (GetValue) rather than TryGetValue(out Value):
        // freestanding NativeAOT ILC corrupts an out Value when the call
        // immediately follows the indexer (get_Item). GetValue returns the Value
        // through the caller's return buffer, which round-trips correctly.
        Value local = _ctx.CurrentFrame.Locals.GetValue(id.Name, out bool localFound);
        if (localFound)
            return local;

        Value global = TryResolveGlobal(id.Name, out bool globalFound);
        if (globalFound)
            return global;

        if (_functions.TryGetValue(id.Name, out var fn))
        {
            return Value.Closure(_ctx.Heap.AllocClosure(fn, []));
        }

        if (_classes.TryGetValue(id.Name, out var cls))
        {
            return Value.Object(cls);
        }

        _ctx.Throw($"Undefined variable '{id.Name}'", id.Location);
        return Value.NullValue;
    }

    private Value TryResolveGlobal(string name, out bool found)
    {
        // Return-buffer getter (no out Value) to avoid the freestanding ILC
        // out-parameter struct-copy defect.
        return _interpreter._globals.GetValue(name, out found);
    }

    private Value EvaluateCall(Lang.CallExpr expr)
    {
        var args = new ValueArray();
        var callArgs = expr.Args;
        for (int ai = 0; ai < CollectionIteration.Count(callArgs); ai++)
        {
            args.Add(Evaluate(CollectionIteration.Get(callArgs, ai)));
            if (_ctx.CurrentFlow != ControlFlow.Normal) return Value.NullValue;
        }

        if (StandardLibrary.TryCall(expr.Name, args, expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
            return result;

        if (_extensions.TryGetValue(expr.Name, out var extFn))
            return extFn(args, expr.Location);

        if (_functions.TryGetValue(expr.Name, out var fn))
            return _interpreter.CallFunction(fn, args, expr.Location);

        _ctx.Throw($"Unknown function '{expr.Name}'", expr.Location);
        return Value.NullValue;
    }

    private Value EvaluateMethodCall(Lang.MethodCallExpr expr)
    {
        var obj = Evaluate(expr.Object);
        if (_ctx.CurrentFlow != ControlFlow.Normal) return Value.NullValue;
        var args = new ValueArray();
        var callArgs = expr.Args;
        for (int ai = 0; ai < CollectionIteration.Count(callArgs); ai++)
        {
            args.Add(Evaluate(CollectionIteration.Get(callArgs, ai)));
            if (_ctx.CurrentFlow != ControlFlow.Normal) return Value.NullValue;
        }

        if (expr.Object is Lang.NamespaceExpr ns)
        {
            if (StandardLibrary.TryCallNamespace(ns.NamespaceName, expr.MethodName, args, expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
                return result;
        }

        if (expr.Object is Lang.MemberAccessExpr ma && ma.Object is Lang.NamespaceExpr os && os.NamespaceName == "OSL")
        {
            var fullNs = "OSL." + ma.MemberName;
            if (StandardLibrary.TryCallOslNamespace(fullNs, expr.MethodName, args, expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
                return result;
        }

        if (obj.Kind == ValueKind.Array)
        {
            return ArrayMethods.Call(obj.AsArray, expr.MethodName, args, expr.Location, _ctx, _utf8);
        }

        if (obj.Kind == ValueKind.String)
        {
            return StringMethods.Call(obj.AsString, expr.MethodName, args, expr.Location, _ctx, _utf8);
        }

        if (obj.Kind == ValueKind.Object && obj.AsObject is HeapClass cls)
        {
            if (StandardLibrary.TryCallNamespace(cls.Name, expr.MethodName, args, expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
                return result;
        }

        if (obj.Kind == ValueKind.Object && obj.AsObject is HeapObjectInstance instance)
        {
            var method = instance.ClassDefinition.FindMethod(expr.MethodName);
            if (method != null)
            {
                return _interpreter.CallMethod(instance, method, args, expr.Location);
            }
        }

        _ctx.Throw($"Method '{expr.MethodName}' not found on type '{obj.Kind}'", expr.Location);
        return Value.NullValue;
    }

    private Value EvaluateMemberAccess(Lang.MemberAccessExpr expr)
    {
        if (expr.Object is Lang.NamespaceExpr ns)
        {
            if (StandardLibrary.TryCallNamespace(ns.NamespaceName, expr.MemberName, [], expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
                return result;
        }

        if (expr.Object is Lang.IdentifierExpr enumId)
        {
            if (_enums.TryGetValue(enumId.Name, out var enumDecl))
            {
                var members = enumDecl.Members;
                for (int i = 0; i < CollectionIteration.Count(members); i++)
                {
                    var member = CollectionIteration.Get(members, i);
                    if (member.Name.Equals(expr.MemberName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (member.Value is Lang.NumberLiteralExpr numExpr)
                            return Value.Number(numExpr.Value);
                        int typeIndex = GetOrCreateEnumTypeIndex(enumId.Name);
                        return Value.Enum(typeIndex, i);
                    }
                }
            }
        }

        var obj = Evaluate(expr.Object);

        if (obj.Kind == ValueKind.Object && obj.AsObject is HeapObjectInstance instance)
        {
            int fieldIndex = -1;
            var names = instance.ClassDefinition.FieldNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].Equals(expr.MemberName, StringComparison.OrdinalIgnoreCase))
                {
                    fieldIndex = i;
                    break;
                }
            }

            if (fieldIndex >= 0 && fieldIndex < instance.Fields.Length)
                return instance.Fields[fieldIndex];
        }

        if (obj.Kind == ValueKind.KeyEvent)
        {
            var evt = obj.AsKeyEvent;
            var name = expr.MemberName.ToUpperInvariant();
            if (name == "KEY") return Value.Number(evt.KeyCode);
            if (name == "CHAR") return Value.String(_ctx.Heap.AllocString(evt.Character.ToString()));
            if (name == "CTRL") return Value.Boolean(evt.Ctrl);
            if (name == "ALT") return Value.Boolean(evt.Alt);
            if (name == "SHIFT") return Value.Boolean(evt.Shift);
        }

        _ctx.Throw($"Member '{expr.MemberName}' not found", expr.Location);
        return Value.NullValue;
    }

    private int GetOrCreateEnumTypeIndex(string enumName)
    {
        if (!_enumTypeIndices.TryGetValue(enumName, out var index))
        {
            index = _enumTypeIndices.Count;
            _enumTypeIndices[enumName] = index;
            _enumNames.Add(enumName);
        }
        return index;
    }

    private Value EvaluateIndex(Lang.IndexExpr expr)
    {
        var arr = Evaluate(expr.Array);
        var idx = Evaluate(expr.Index);

        if (arr.Kind == ValueKind.String)
        {
            if (idx.Kind != ValueKind.Number)
            {
                _ctx.Throw("String index must be a number", expr.Location);
                return Value.NullValue;
            }

            int strIndex = (int)idx.AsNumber;
            var str = arr.AsString;
            if (strIndex < 0 || strIndex >= str.CodePointCount)
            {
                _ctx.Throw("String index out of bounds", expr.Location);
                return Value.NullValue;
            }

            var codePointStr = str.GetCodePoint(strIndex);
            return Value.String(_ctx.Heap.AllocString(codePointStr.ToString()));
        }

        if (arr.Kind != ValueKind.Array)
        {
            _ctx.Throw("Index operator only supported on arrays and strings", expr.Location);
            return Value.NullValue;
        }

        if (idx.Kind != ValueKind.Number)
        {
            _ctx.Throw("Array index must be a number", expr.Location);
            return Value.NullValue;
        }

        int arrIndex = (int)idx.AsNumber;
        if (arrIndex < 0 || arrIndex >= arr.AsArray.Length)
        {
            _ctx.Throw("Array index out of bounds", expr.Location);
            return Value.NullValue;
        }

        return arr.AsArray.Items[arrIndex];
    }

    private Value EvaluateNew(Lang.NewExpr expr)
    {
        if (!_classes.TryGetValue(expr.ClassName, out var cls))
        {
            _ctx.Throw($"Class '{expr.ClassName}' not found", expr.Location);
            return Value.NullValue;
        }

        var args = new ValueArray();
        var callArgs = expr.Args;
        for (int ai = 0; ai < CollectionIteration.Count(callArgs); ai++)
        {
            args.Add(Evaluate(CollectionIteration.Get(callArgs, ai)));
            if (_ctx.CurrentFlow != ControlFlow.Normal) return Value.NullValue;
        }

        var ctor = cls.FindMethod("CONSTRUCTOR");
        var instance = _ctx.Heap.AllocObject(cls);

        if (ctor != null)
        {
            _interpreter.CallMethod(instance, ctor, args, expr.Location);
        }

        return Value.Object(instance);
    }

    private Value EvaluateUnary(Lang.UnaryExpr expr)
    {
        var operand = Evaluate(expr.Operand);
        return expr.Operator switch
        {
            "-" when operand.Kind == ValueKind.Number => Value.Number(-operand.AsNumber),
            "NOT" => Value.Boolean(!Interpreter.IsTruthy(operand)),
            _ => operand,
        };
    }

    private Value EvaluateBinary(Lang.BinaryExpr expr)
    {
        var left = Evaluate(expr.Left);
        if (_ctx.CurrentFlow != ControlFlow.Normal) return Value.NullValue;
        var right = Evaluate(expr.Right);
        if (_ctx.CurrentFlow != ControlFlow.Normal) return Value.NullValue;

        if (expr.Operator == "+" && left.Kind == ValueKind.String && right.Kind == ValueKind.String)
        {
            string combined = left.AsString.ToString() + right.AsString.ToString();
            return Value.String(_ctx.Heap.AllocString(combined));
        }

        if (left.Kind == ValueKind.Number && right.Kind == ValueKind.Number)
        {
            double l = left.AsNumber, r = right.AsNumber;
            if (expr.Operator == "/")
            {
                if (r == 0)
                {
                    _ctx.Throw("Division by zero.", expr.Location);
                    return Value.NullValue;
                }
                return Value.Number(l / r);
            }
            return expr.Operator switch
            {
                "+" => Value.Number(l + r),
                "-" => Value.Number(l - r),
                "*" => Value.Number(l * r),
                "%" => Value.Number(l % r),
                "**" => Value.Number(_math.Pow(l, r)),
                "=" => Value.Boolean(l == r),
                "<>" => Value.Boolean(l != r),
                "<" => Value.Boolean(l < r),
                ">" => Value.Boolean(l > r),
                "<=" => Value.Boolean(l <= r),
                ">=" => Value.Boolean(l >= r),
                _ => Value.NullValue,
            };
        }

        if (expr.Operator == "=" || expr.Operator == "<>")
        {
            bool eq = ValueUtilities.ValuesEqual(left, right);
            return Value.Boolean(expr.Operator == "=" ? eq : !eq);
        }

        _ctx.Throw($"Binary operator '{expr.Operator}' not supported for types {left.Kind} and {right.Kind}", expr.Location);
        return Value.NullValue;
    }

    private Value EvaluatePostfix(Lang.PostfixExpr expr)
    {
        var target = expr.Operand;
        if (target is Lang.IdentifierExpr id)
        {
            Value current = _ctx.CurrentFrame.Locals.GetValue(id.Name, out bool currentFound);
            if (!currentFound)
            {
                _ctx.Throw($"Undefined variable '{id.Name}'", expr.Location);
                return Value.NullValue;
            }

            if (current.Kind != ValueKind.Number)
            {
                _ctx.Throw("Increment/decrement only supported on numbers", expr.Location);
                return Value.NullValue;
            }

            double newVal = expr.Operator == "++" ? current.AsNumber + 1 : current.AsNumber - 1;
            _ctx.CurrentFrame.Locals[id.Name] = Value.Number(newVal);
            return Value.Number(newVal);
        }

        _ctx.Throw("Increment/decrement only supported on variables", expr.Location);
        return Value.NullValue;
    }
}