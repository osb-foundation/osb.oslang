using System.Collections.Generic;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Host;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// OSLANG interpreter.
/// Evaluates AST nodes using explicit frame stack (no C# exceptions for control flow).
/// Platform services (math, time, random, UTF-8) are injected via provider interfaces.
/// </summary>
public sealed class Interpreter
{
    private readonly ExecutionContext _ctx;
    private readonly Dictionary<string, Lang.FunctionDecl> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HeapClass> _classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Lang.EnumDecl> _enums = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Lang.UsingDecl> _usings = new();
    private readonly Dictionary<string, NativeFunction> _extensions = new(StringComparer.OrdinalIgnoreCase);

    private readonly IMathProvider _math;
    private readonly ITimeProvider _time;
    private readonly IRandomProvider _random;
    private readonly IUtf8Provider _utf8;

    private readonly Dictionary<string, int> _enumTypeIndices = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _enumNames = new();

    public Interpreter(EngineHeap heap, IHostApi host, IMathProvider math, ITimeProvider time, IRandomProvider random, IUtf8Provider utf8)
    {
        _ctx = new ExecutionContext(heap, host);
        _math = math;
        _time = time;
        _random = random;
        _utf8 = utf8;
    }

    public void RegisterFunction(Lang.FunctionDecl fn) => _functions[fn.Name] = fn;
    public void RegisterClass(HeapClass cls) => _classes[cls.Name] = cls;
    public void RegisterEnum(Lang.EnumDecl en) => _enums[en.Name] = en;
    public void RegisterUsing(Lang.UsingDecl u) => _usings.Add(u);
    public void RegisterExtension(string name, NativeFunction fn) => _extensions[name] = fn;

    /// <summary>
    /// Executes a program starting from MAIN().
    /// </summary>
    public Value Execute(Lang.Program program)
    {
        // Register all declarations
        foreach (var fn in program.Functions) RegisterFunction(fn);
        foreach (var cls in program.Classes) RegisterClass(CreateHeapClass(cls));
        foreach (var en in program.Enums) RegisterEnum(en);
        foreach (var u in program.Usings) RegisterUsing(u);

        // Find MAIN
        if (!_functions.TryGetValue("MAIN", out var mainFn))
        {
            _ctx.Throw("No MAIN function found", SourceLocation.Unknown);
            return Value.NullValue;
        }

        // Call MAIN
        return CallFunction(mainFn, [], SourceLocation.Unknown);
    }

    private HeapClass CreateHeapClass(Lang.ClassDecl decl)
    {
        var baseClass = decl.BaseClasses.Count > 0 && _classes.TryGetValue(decl.BaseClasses[0], out var baseCls)
            ? baseCls : null;

        var fieldNames = new List<string>();
        var methods = new List<Lang.MethodDecl>();

        foreach (var member in decl.Members)
        {
            switch (member)
            {
                case Lang.PropertyDecl prop:
                    fieldNames.Add(prop.Name);
                    break;
                case Lang.MethodDecl method:
                    methods.Add(method);
                    break;
                case Lang.ConstructorDecl ctor:
                    // Convert constructor to a method named CONSTRUCTOR
                    methods.Add(new Lang.MethodDecl(
                        ctor.Visibility,
                        "CONSTRUCTOR",
                        ctor.Parameters,
                        ctor.Body,
                        ctor.Location
                    ));
                    break;
            }
        }

        var heapClass = _ctx.Heap.AllocClass(decl.Name, baseClass, fieldNames.ToArray(), methods.ToArray());
        return heapClass;
    }

    private Value CallFunction(Lang.FunctionDecl fn, IReadOnlyList<Value> args, SourceLocation location)
    {
        // Check arity
        if (args.Count != fn.Parameters.Count)
        {
            _ctx.Throw($"Function '{fn.Name}' expects {fn.Parameters.Count} arguments, got {args.Count}", location);
            return Value.NullValue;
        }

        var frame = new Frame(fn);
        // Bind parameters
        for (int i = 0; i < fn.Parameters.Count; i++)
        {
            frame.Locals[fn.Parameters[i].Name] = args[i];
        }

        _ctx.PushFrame(frame);
        _ctx.CurrentFlow = ControlFlow.Normal;

        foreach (var stmt in fn.Body)
        {
            ExecuteStatement(stmt);
            if (_ctx.CurrentFlow != ControlFlow.Normal)
                break;
        }

        var retVal = _ctx.ReturnValue;
        _ctx.ReturnValue = Value.NullValue;
        _ctx.PopFrame();
        _ctx.CurrentFlow = ControlFlow.Normal;

        return retVal;
    }

    private Value CallMethod(HeapObjectInstance instance, Lang.MethodDecl method, IReadOnlyList<Value> args, SourceLocation location)
    {
        if (args.Count != method.Parameters.Count)
        {
            _ctx.Throw($"Method '{method.Name}' expects {method.Parameters.Count} arguments, got {args.Count}", location);
            return Value.NullValue;
        }

        // Create a function decl from the method for the frame
        var fn = new Lang.FunctionDecl(method.Name, method.Parameters, method.Body, location);

        var frame = new Frame(fn, Value.Object(instance), Value.NullValue);
        for (int i = 0; i < method.Parameters.Count; i++)
        {
            frame.Locals[method.Parameters[i].Name] = args[i];
        }

        _ctx.PushFrame(frame);
        _ctx.CurrentFlow = ControlFlow.Normal;

        foreach (var stmt in method.Body)
        {
            ExecuteStatement(stmt);
            if (_ctx.CurrentFlow != ControlFlow.Normal)
                break;
        }

        var retVal = _ctx.ReturnValue;
        _ctx.ReturnValue = Value.NullValue;
        _ctx.PopFrame();
        _ctx.CurrentFlow = ControlFlow.Normal;

        return retVal;
    }

    private void ExecuteStatement(Lang.Stmt stmt)
    {
        if (_ctx.CurrentFlow != ControlFlow.Normal) return;

        switch (stmt)
        {
            case Lang.VarDeclStmt v:
                _ctx.CurrentFrame.Locals[v.Name] = Value.NullValue;
                break;

            case Lang.AssignStmt a:
                var value = Evaluate(a.Value);
                AssignToTarget(a.Target, value);
                break;

            case Lang.PrintStmt p:
                ExecutePrint(p);
                break;

            case Lang.ShowStmt s:
                ExecuteShow(s);
                break;

            case Lang.InputStmt i:
                ExecuteInput(i);
                break;

            case Lang.IfStmt iff:
                ExecuteIf(iff);
                break;

            case Lang.ForStmt f:
                ExecuteFor(f);
                break;

            case Lang.WhileStmt w:
                ExecuteWhile(w);
                break;

            case Lang.DoWhileStmt dw:
                ExecuteDoWhile(dw);
                break;

            case Lang.BreakStmt:
                _ctx.CurrentFlow = ControlFlow.Break;
                break;

            case Lang.ContinueStmt:
                _ctx.CurrentFlow = ControlFlow.Continue;
                break;

            case Lang.ReturnStmt r:
                _ctx.ReturnValue = r.Value != null ? Evaluate(r.Value) : Value.NullValue;
                _ctx.CurrentFlow = ControlFlow.Return;
                break;

            case Lang.TryCatchStmt t:
                ExecuteTryCatch(t);
                break;

            case Lang.SwitchStmt sw:
                ExecuteSwitch(sw);
                break;

            case Lang.ClearStmt:
                _ctx.Host.Clear();
                break;

            case Lang.BaseCallStmt b:
                ExecuteBaseCall(b);
                break;

            case Lang.GlobalDeclStmt g:
                var gval = Evaluate(g.Value);
                // Store in globals (simple list for now)
                while (_ctx.Globals.Count <= _usings.Count) _ctx.Globals.Add(Value.NullValue);
                _ctx.Globals.Add(gval);
                break;

            case Lang.ExpressionStmt e:
                Evaluate(e.Expression);
                break;

            case Lang.BlockStmt b:
                foreach (var s in b.Statements)
                {
                    ExecuteStatement(s);
                    if (_ctx.CurrentFlow != ControlFlow.Normal) break;
                }
                break;
        }
    }

    private void ExecutePrint(Lang.PrintStmt stmt)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var expr in stmt.Expressions)
        {
            var val = Evaluate(expr);
            sb.Append(ToDisplayString(val));
        }
        _ctx.Host.Write(sb.ToString());
        _ctx.Host.Write("\n");
    }

    private void ExecuteShow(Lang.ShowStmt stmt)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var expr in stmt.Expressions)
        {
            var val = Evaluate(expr);
            sb.Append(ToDisplayString(val));
        }
        _ctx.Host.Write(sb.ToString());
    }

    private void ExecuteInput(Lang.InputStmt stmt)
    {
        var line = _ctx.Host.ReadLine() ?? string.Empty;
        var value = Value.String(_ctx.Heap.AllocString(line));
        _ctx.CurrentFrame.Locals[stmt.VariableName] = value;
    }

    private void ExecuteIf(Lang.IfStmt stmt)
    {
        var cond = Evaluate(stmt.Condition);
        if (IsTruthy(cond))
        {
            foreach (var s in stmt.ThenBody)
            {
                ExecuteStatement(s);
                if (_ctx.CurrentFlow != ControlFlow.Normal) return;
            }
        }
        else
        {
            bool matched = false;
            foreach (var elif in stmt.ElifClauses)
            {
                var elifCond = Evaluate(elif.Condition);
                if (IsTruthy(elifCond))
                {
                    foreach (var s in elif.Body)
                    {
                        ExecuteStatement(s);
                        if (_ctx.CurrentFlow != ControlFlow.Normal) return;
                    }
                    matched = true;
                    break;
                }
            }

            if (!matched && stmt.ElseBody != null)
            {
                foreach (var s in stmt.ElseBody)
                {
                    ExecuteStatement(s);
                    if (_ctx.CurrentFlow != ControlFlow.Normal) return;
                }
            }
        }
    }

    private void ExecuteFor(Lang.ForStmt stmt)
    {
        var fromVal = Evaluate(stmt.From);
        var toVal = Evaluate(stmt.To);
        var stepVal = stmt.Step != null ? Evaluate(stmt.Step) : Value.Number(1);

        if (fromVal.Kind != ValueKind.Number || toVal.Kind != ValueKind.Number || stepVal.Kind != ValueKind.Number)
        {
            _ctx.Throw("FOR loop bounds must be numbers", stmt.Location);
            return;
        }

        double from = fromVal.AsNumber;
        double to = toVal.AsNumber;
        double step = stepVal.AsNumber;

        if (step == 0)
        {
            _ctx.Throw("FOR loop STEP cannot be zero", stmt.Location);
            return;
        }

        _ctx.CurrentFrame.Locals[stmt.Variable] = Value.Number(from);
        _ctx.LoopDepth++;

        while (true)
        {
            var current = _ctx.CurrentFrame.Locals[stmt.Variable].AsNumber;
            bool continueLoop = step > 0 ? current <= to : current >= to;

            if (!continueLoop) break;

            foreach (var s in stmt.Body)
            {
                ExecuteStatement(s);
                if (_ctx.CurrentFlow == ControlFlow.Break) { _ctx.CurrentFlow = ControlFlow.Normal; goto ExitLoop; }
                if (_ctx.CurrentFlow == ControlFlow.Continue) { _ctx.CurrentFlow = ControlFlow.Normal; break; }
                if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitLoop;
            }

            _ctx.CurrentFrame.Locals[stmt.Variable] = Value.Number(current + step);
        }

    ExitLoop:
        _ctx.LoopDepth--;
    }

    private void ExecuteWhile(Lang.WhileStmt stmt)
    {
        _ctx.LoopDepth++;
        while (true)
        {
            var cond = Evaluate(stmt.Condition);
            if (!IsTruthy(cond)) break;

            foreach (var s in stmt.Body)
            {
                ExecuteStatement(s);
                if (_ctx.CurrentFlow == ControlFlow.Break) { _ctx.CurrentFlow = ControlFlow.Normal; goto ExitLoop; }
                if (_ctx.CurrentFlow == ControlFlow.Continue) { _ctx.CurrentFlow = ControlFlow.Normal; break; }
                if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitLoop;
            }
        }
    ExitLoop:
        _ctx.LoopDepth--;
    }

    private void ExecuteDoWhile(Lang.DoWhileStmt stmt)
    {
        _ctx.LoopDepth++;
        while (true)
        {
            foreach (var s in stmt.Body)
            {
                ExecuteStatement(s);
                if (_ctx.CurrentFlow == ControlFlow.Break) { _ctx.CurrentFlow = ControlFlow.Normal; goto ExitLoop; }
                if (_ctx.CurrentFlow == ControlFlow.Continue) { _ctx.CurrentFlow = ControlFlow.Normal; break; }
                if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitLoop;
            }

            var cond = Evaluate(stmt.Condition);
            if (!IsTruthy(cond)) break;
        }
    ExitLoop:
        _ctx.LoopDepth--;
    }

    private void ExecuteTryCatch(Lang.TryCatchStmt stmt)
    {
        int prevTryFrame = _ctx.CurrentFrame.TryCatchFrameIndex;
        _ctx.CurrentFrame.TryCatchFrameIndex = _ctx.Frames.Count - 1;
        _ctx.CurrentFrame.InTry = true;

        foreach (var s in stmt.TryBody)
        {
            ExecuteStatement(s);
            if (_ctx.CurrentFlow != ControlFlow.Normal) break;
        }

        if (_ctx.CurrentFlow == ControlFlow.Exception)
        {
            // Bind error variable
            _ctx.CurrentFrame.Locals[stmt.ErrorVariable] = _ctx.CurrentException;
            _ctx.CurrentFlow = ControlFlow.Normal;
            _ctx.CurrentException = Value.NullValue;

            foreach (var s in stmt.CatchBody)
            {
                ExecuteStatement(s);
                if (_ctx.CurrentFlow != ControlFlow.Normal) break;
            }
        }

        _ctx.CurrentFrame.TryCatchFrameIndex = prevTryFrame;
        _ctx.CurrentFrame.InTry = false;
    }

    private void ExecuteSwitch(Lang.SwitchStmt stmt)
    {
        var value = Evaluate(stmt.Value);
        bool matched = false;
        _ctx.InSwitch = true;

        foreach (var case_ in stmt.Cases)
        {
            foreach (var caseVal in case_.Values)
            {
                var cv = Evaluate(caseVal);
                if (ValuesEqual(value, cv))
                {
                    foreach (var s in case_.Body)
                    {
                        ExecuteStatement(s);
                        if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitSwitch;
                    }
                    matched = true;
                    goto ExitSwitch;
                }
            }
        }

        if (!matched && stmt.DefaultBody != null)
        {
            foreach (var s in stmt.DefaultBody)
            {
                ExecuteStatement(s);
                if (_ctx.CurrentFlow != ControlFlow.Normal) goto ExitSwitch;
            }
        }

    ExitSwitch:
        _ctx.InSwitch = false;
    }

    private void ExecuteBaseCall(Lang.BaseCallStmt stmt)
    {
        var frame = _ctx.CurrentFrame;
        if (frame.Base.IsNull)
        {
            _ctx.Throw("BASE() called outside constructor", stmt.Location);
            return;
        }

        // In a real implementation, this would call the base class constructor
        // For now, just evaluate args
        foreach (var arg in stmt.Args)
        {
            Evaluate(arg);
        }
    }

    private Value Evaluate(Lang.Expr expr)
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
            Lang.NamespaceExpr ns => EvaluateNamespace(ns),
            Lang.NamespaceCallExpr nsc => EvaluateNamespaceCall(nsc),
            Lang.PostfixExpr p => EvaluatePostfix(p),
            _ => throw new System.InvalidOperationException($"Unknown expression type: {expr.GetType().Name}"),
        };
    }

    private Value EvaluateArray(Lang.ArrayLiteralExpr expr)
    {
        var arr = _ctx.Heap.AllocArray(expr.Elements.Count);
        for (int i = 0; i < expr.Elements.Count; i++)
        {
            arr.Items[i] = Evaluate(expr.Elements[i]);
        }
        return Value.Array(arr);
    }

    private Value ResolveIdentifier(Lang.IdentifierExpr id)
    {
        // Check locals first
        if (_ctx.CurrentFrame.Locals.TryGetValue(id.Name, out var local))
            return local;

        // Check globals
        if (TryResolveGlobal(id.Name, out var global))
            return global;

        // Check functions (function values)
        if (_functions.TryGetValue(id.Name, out var fn))
        {
            // Return a closure/func value
            return Value.Closure(_ctx.Heap.AllocClosure(fn, []));
        }

        // Check classes
        if (_classes.TryGetValue(id.Name, out var cls))
        {
            return Value.Object(cls); // class object itself
        }

        _ctx.Throw($"Undefined variable '{id.Name}'", id.Location);
        return Value.NullValue;
    }

    private bool TryResolveGlobal(string name, out Value value)
    {
        // Simplified global lookup
        value = Value.NullValue;
        return false;
    }

    private Value EvaluateCall(Lang.CallExpr expr)
    {
        var args = new List<Value>();
        foreach (var arg in expr.Args)
            args.Add(Evaluate(arg));

        // Check builtin functions
        if (StandardLibrary.TryCall(expr.Name, args, expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
            return result;

        // Check extensions
        if (_extensions.TryGetValue(expr.Name, out var extFn))
            return extFn(args, expr.Location);

        // Check user functions
        if (_functions.TryGetValue(expr.Name, out var fn))
            return CallFunction(fn, args, expr.Location);

        _ctx.Throw($"Unknown function '{expr.Name}'", expr.Location);
        return Value.NullValue;
    }

    private Value EvaluateMethodCall(Lang.MethodCallExpr expr)
    {
        var obj = Evaluate(expr.Object);
        var args = new List<Value>();
        foreach (var arg in expr.Args)
            args.Add(Evaluate(arg));

        // Namespace method calls (e.g., MATH.SQRT(16), DATE.YEAR(d))
        if (expr.Object is Lang.NamespaceExpr ns)
        {
            if (StandardLibrary.TryCallNamespace(ns.NamespaceName, expr.MethodName, args, expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
                return result;
        }

        // Array method calls
        if (obj.Kind == ValueKind.Array)
        {
            return ArrayMethods.Call(obj.AsArray, expr.MethodName, args, expr.Location, _ctx, _utf8);
        }

        // String method calls
        if (obj.Kind == ValueKind.String)
        {
            return StringMethods.Call(obj.AsString, expr.MethodName, args, expr.Location, _ctx, _utf8);
        }

        // Class/namespace object method calls
        if (obj.Kind == ValueKind.Object && obj.AsObject is HeapClass cls)
        {
            if (StandardLibrary.TryCallNamespace(cls.Name, expr.MethodName, args, expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
                return result;
        }

        // Object method calls
        if (obj.Kind == ValueKind.Object && obj.AsObject is HeapObjectInstance instance)
        {
            var method = instance.ClassDefinition.FindMethod(expr.MethodName);
            if (method != null)
            {
                return CallMethod(instance, method, args, expr.Location);
            }
        }

        _ctx.Throw($"Method '{expr.MethodName}' not found on type '{obj.Kind}'", expr.Location);
        return Value.NullValue;
    }

    private Value EvaluateMemberAccess(Lang.MemberAccessExpr expr)
    {
        // Handle namespace member access (e.g., MATH.PI, DATE.YEAR)
        if (expr.Object is Lang.NamespaceExpr ns)
        {
            if (StandardLibrary.TryCallNamespace(ns.NamespaceName, expr.MemberName, [], expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
                return result;
        }

        // Handle enum member access (e.g., Color.RED)
        if (expr.Object is Lang.IdentifierExpr enumId)
        {
            if (_enums.TryGetValue(enumId.Name, out var enumDecl))
            {
                // Find member index
                for (int i = 0; i < enumDecl.Members.Count; i++)
                {
                    if (enumDecl.Members[i].Name.Equals(expr.MemberName, StringComparison.OrdinalIgnoreCase))
                    {
                        // Get or create enum type index
                        int typeIndex = GetOrCreateEnumTypeIndex(enumId.Name);
                        return Value.Enum(typeIndex, i);
                    }
                }
            }
        }

        var obj = Evaluate(expr.Object);

        if (obj.Kind == ValueKind.Object && obj.AsObject is HeapObjectInstance instance)
        {
            // Find field index
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

        // String indexing (SQ-001: code-point indexing)
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

            // Return a new HeapString with a single code point (SQ-001)
            var codePointStr = str.GetCodePoint(strIndex);
            return Value.String(_ctx.Heap.AllocString(codePointStr.ToString()));
        }

        // Array indexing
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

        var args = new List<Value>();
        foreach (var arg in expr.Args)
            args.Add(Evaluate(arg));

        // Find constructor
        var ctor = cls.FindMethod("CONSTRUCTOR");
        var instance = _ctx.Heap.AllocObject(cls);

        if (ctor != null)
        {
            CallMethod(instance, ctor, args, expr.Location);
        }

        return Value.Object(instance);
    }

    private Value EvaluateUnary(Lang.UnaryExpr expr)
    {
        var operand = Evaluate(expr.Operand);
        return expr.Operator switch
        {
            "-" when operand.Kind == ValueKind.Number => Value.Number(-operand.AsNumber),
            "NOT" => Value.Boolean(!IsTruthy(operand)),
            _ => operand,
        };
    }

    private Value EvaluateBinary(Lang.BinaryExpr expr)
    {
        var left = Evaluate(expr.Left);
        var right = Evaluate(expr.Right);

        // String concatenation
        if (expr.Operator == "+" && left.Kind == ValueKind.String && right.Kind == ValueKind.String)
        {
            string combined = left.AsString.ToString() + right.AsString.ToString();
            return Value.String(_ctx.Heap.AllocString(combined));
        }

        // Numeric operations
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

        // Equality for other types
        if (expr.Operator == "=" || expr.Operator == "<>")
        {
            bool eq = ValuesEqual(left, right);
            return Value.Boolean(expr.Operator == "=" ? eq : !eq);
        }

        _ctx.Throw($"Binary operator '{expr.Operator}' not supported for types {left.Kind} and {right.Kind}", expr.Location);
        return Value.NullValue;
    }

    private Value EvaluateNamespace(Lang.NamespaceExpr expr)
    {
        // Return a namespace marker object
        return Value.NativeFunction((args, loc) =>
        {
            if (StandardLibrary.TryCallNamespace(expr.NamespaceName, "CALL", args, loc, _ctx, _math, _time, _random, _utf8, out var result))
                return result;
            _ctx.Throw($"Unknown namespace call: {expr.NamespaceName}", loc);
            return Value.NullValue;
        });
    }

    private Value EvaluateNamespaceCall(Lang.NamespaceCallExpr expr)
    {
        var args = new List<Value>();
        foreach (var arg in expr.Args)
            args.Add(Evaluate(arg));

        if (StandardLibrary.TryCallNamespace(expr.NamespaceName, expr.NamespaceName, args, expr.Location, _ctx, _math, _time, _random, _utf8, out var result))
            return result;

        _ctx.Throw($"Unknown namespace function: {expr.NamespaceName}", expr.Location);
        return Value.NullValue;
    }

    private Value EvaluatePostfix(Lang.PostfixExpr expr)
    {
        // ++/-- on variables
        var target = expr.Operand;
        if (target is Lang.IdentifierExpr id)
        {
            if (!_ctx.CurrentFrame.Locals.TryGetValue(id.Name, out var current))
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

    private void AssignToTarget(Lang.AssignTarget target, Value value)
    {
        switch (target)
        {
            case Lang.VariableTarget vt:
                _ctx.CurrentFrame.Locals[vt.Name] = value;
                break;
            case Lang.IndexTarget it:
                var arr = Evaluate(it.Array);
                var idx = Evaluate(it.Index);
                if (arr.Kind == ValueKind.Array && idx.Kind == ValueKind.Number)
                {
                    int i = (int)idx.AsNumber;
                    if (i >= 0 && i < arr.AsArray.Length)
                        arr.AsArray.Items[i] = value;
                }
                break;
            case Lang.MemberTarget mt:
                var obj = Evaluate(mt.Object);
                if (obj.Kind == ValueKind.Object && obj.AsObject is HeapObjectInstance instance)
                {
                    int fieldIndex = -1;
                    var names = instance.ClassDefinition.FieldNames;
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (names[i].Equals(mt.MemberName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            fieldIndex = i;
                            break;
                        }
                    }
                    if (fieldIndex >= 0 && fieldIndex < instance.Fields.Length)
                        instance.Fields[fieldIndex] = value;
                }
                break;
        }
    }

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

    private static bool ValuesEqual(Value a, Value b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            ValueKind.Null => true,
            ValueKind.Boolean => a.AsBoolean == b.AsBoolean,
            ValueKind.Number => a.AsNumber == b.AsNumber,
            ValueKind.String => a.AsString.ToString() == b.AsString.ToString(),
            ValueKind.Array => a.AsArray == b.AsArray, // reference equality
            ValueKind.Object => a.AsObject == b.AsObject,
            ValueKind.Enum => a.AsEnum == b.AsEnum,
            ValueKind.EnumSet => a.AsEnumSet == b.AsEnumSet,
            _ => false,
        };
    }

    private string ToDisplayString(Value value) => value.Kind switch
    {
        ValueKind.Null => "NULL",
        ValueKind.String => value.AsString.ToString(),
        ValueKind.Number => value.ToString(), // uses FormatNumber
        ValueKind.Boolean => value.AsBoolean ? "TRUE" : "FALSE",
        ValueKind.Array => throw new System.InvalidOperationException("Cannot convert ARRAY to STRING"),
        ValueKind.Object => value.AsObject switch
        {
            HeapObjectInstance inst => inst.ClassName,
            HeapClass cls => cls.Name,
            _ => value.AsObject?.GetType().Name ?? "NULL",
        },
        ValueKind.Enum => FormatEnum(value),
        ValueKind.EnumSet => $"ENUMSET[{value.AsEnumSet.Count}]",
        _ => value.ToString(),
    };

    private string FormatEnum(Value value)
    {
        var (typeIndex, memberIndex) = value.AsEnum;
        if (typeIndex >= 0 && typeIndex < _enumNames.Count)
        {
            var enumName = _enumNames[typeIndex];
            if (_enums.TryGetValue(enumName, out var enumDecl) && memberIndex >= 0 && memberIndex < enumDecl.Members.Count)
            {
                return $"{enumName.ToUpperInvariant()}.{enumDecl.Members[memberIndex].Name}";
            }
        }
        return $"ENUM#{typeIndex}.{memberIndex}";
    }
}