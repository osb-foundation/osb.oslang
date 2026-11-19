using System.Collections.Generic;
using System.Text;
using Osb.Engine.Lang;

namespace Osb.Engine.Eval;

/// <summary>
    /// OSLANG interpreter.
    /// Evaluates AST nodes using explicit frame stack (no C# exceptions for control flow).
    /// </summary>
    public sealed class Interpreter
    {
        private readonly ExecutionContext _ctx;
        private readonly Dictionary<string, Lang.FunctionDecl> _functions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Values.HeapClass> _classes = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Lang.EnumDecl> _enums = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Lang.UsingDecl> _usings = new();
        private readonly Dictionary<string, Values.NativeFunction> _extensions = new(StringComparer.OrdinalIgnoreCase);

        public Interpreter(Heap.EngineHeap heap, Host.IHostApi host)
        {
            _ctx = new ExecutionContext(heap, host);
        }

        public void RegisterFunction(Lang.FunctionDecl fn) => _functions[fn.Name] = fn;
        public void RegisterClass(Values.HeapClass cls) => _classes[cls.Name] = cls;
        public void RegisterEnum(Lang.EnumDecl en) => _enums[en.Name] = en;
        public void RegisterUsing(Lang.UsingDecl u) => _usings.Add(u);
        public void RegisterExtension(string name, Values.NativeFunction fn) => _extensions[name] = fn;

        /// <summary>
        /// Executes a program starting from MAIN().
        /// </summary>
    public Values.Value Execute(Lang.Program program)
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
            return Values.Value.NullValue;
        }

        // Call MAIN
        return CallFunction(mainFn, [], SourceLocation.Unknown);
    }

    private Values.HeapClass CreateHeapClass(Lang.ClassDecl decl)
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

    private Values.Value CallFunction(Lang.FunctionDecl fn, IReadOnlyList<Values.Value> args, SourceLocation location)
    {
        // Check arity
        if (args.Count != fn.Parameters.Count)
        {
            _ctx.Throw($"Function '{fn.Name}' expects {fn.Parameters.Count} arguments, got {args.Count}", location);
            return Values.Value.NullValue;
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
        _ctx.ReturnValue = Values.Value.NullValue;
        _ctx.PopFrame();
        _ctx.CurrentFlow = ControlFlow.Normal;

        return retVal;
    }

    private Values.Value CallMethod(Values.HeapObjectInstance instance, Lang.MethodDecl method, IReadOnlyList<Values.Value> args, SourceLocation location)
    {
        if (args.Count != method.Parameters.Count)
        {
            _ctx.Throw($"Method '{method.Name}' expects {method.Parameters.Count} arguments, got {args.Count}", location);
            return Values.Value.NullValue;
        }

        // Create a function decl from the method for the frame
        var fn = new Lang.FunctionDecl(method.Name, method.Parameters, method.Body, location);
        
        var frame = new Frame(fn, Values.Value.Object(instance), Values.Value.NullValue);
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
        _ctx.ReturnValue = Values.Value.NullValue;
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
                _ctx.CurrentFrame.Locals[v.Name] = Values.Value.NullValue;
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
                _ctx.ReturnValue = r.Value != null ? Evaluate(r.Value) : Values.Value.NullValue;
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
                while (_ctx.Globals.Count <= _usings.Count) _ctx.Globals.Add(Values.Value.NullValue);
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
        var sb = new StringBuilder();
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
        var sb = new StringBuilder();
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
        var value = Values.Value.String(_ctx.Heap.AllocString(line));
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
        var stepVal = stmt.Step != null ? Evaluate(stmt.Step) : Values.Value.Number(1);

        if (fromVal.Kind != Values.ValueKind.Number || toVal.Kind != Values.ValueKind.Number || stepVal.Kind != Values.ValueKind.Number)
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

        _ctx.CurrentFrame.Locals[stmt.Variable] = Values.Value.Number(from);
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

            _ctx.CurrentFrame.Locals[stmt.Variable] = Values.Value.Number(current + step);
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
            _ctx.CurrentException = Values.Value.NullValue;

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

    private Values.Value Evaluate(Lang.Expr expr)
    {
        if (_ctx.CurrentFlow != ControlFlow.Normal) return Values.Value.NullValue;

        return expr switch
        {
            Lang.NumberLiteralExpr n => Values.Value.Number(n.Value),
            Lang.StringLiteralExpr s => Values.Value.String(_ctx.Heap.AllocString(s.Value)),
            Lang.DateLiteralExpr d => Values.Value.Number(d.DaysSinceEpoch),
            Lang.TimeLiteralExpr t => Values.Value.Number(t.MillisSinceMidnight),
            Lang.BooleanLiteralExpr b => Values.Value.Boolean(b.Value),
            Lang.NullLiteralExpr => Values.Value.NullValue,
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

    private Values.Value EvaluateArray(Lang.ArrayLiteralExpr expr)
    {
        var arr = _ctx.Heap.AllocArray(expr.Elements.Count);
        for (int i = 0; i < expr.Elements.Count; i++)
        {
            arr.Items[i] = Evaluate(expr.Elements[i]);
        }
        return Values.Value.Array(arr);
    }

    private Values.Value ResolveIdentifier(Lang.IdentifierExpr id)
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
            return Values.Value.Closure(_ctx.Heap.AllocClosure(fn, []));
        }

        // Check classes
        if (_classes.TryGetValue(id.Name, out var cls))
        {
            return Values.Value.Object(cls); // class object itself
        }

        _ctx.Throw($"Undefined variable '{id.Name}'", id.Location);
        return Values.Value.NullValue;
    }

    private bool TryResolveGlobal(string name, out Values.Value value)
    {
        // Simplified global lookup
        value = Values.Value.NullValue;
        return false;
    }

    private Values.Value EvaluateCall(Lang.CallExpr expr)
    {
        var args = new List<Values.Value>();
        foreach (var arg in expr.Args)
            args.Add(Evaluate(arg));

        // Check builtin functions
        if (StandardLibrary.TryCall(expr.Name, args, expr.Location, _ctx, out var result))
            return result;

        // Check extensions
        if (_extensions.TryGetValue(expr.Name, out var extFn))
            return extFn(args, expr.Location);

        // Check user functions
        if (_functions.TryGetValue(expr.Name, out var fn))
            return CallFunction(fn, args, expr.Location);

        _ctx.Throw($"Unknown function '{expr.Name}'", expr.Location);
        return Values.Value.NullValue;
    }

    private Values.Value EvaluateMethodCall(Lang.MethodCallExpr expr)
    {
        var obj = Evaluate(expr.Object);
        var args = new List<Values.Value>();
        foreach (var arg in expr.Args)
            args.Add(Evaluate(arg));

        // Namespace method calls (e.g., MATH.SQRT(16), DATE.YEAR(d))
        if (expr.Object is Lang.NamespaceExpr ns)
        {
            if (StandardLibrary.TryCallNamespace(ns.NamespaceName, expr.MethodName, args, expr.Location, _ctx, out var result))
                return result;
        }

        // Array method calls
        if (obj.Kind == Values.ValueKind.Array)
        {
            return ArrayMethods.Call(obj.AsArray, expr.MethodName, args, expr.Location, _ctx);
        }

        // String method calls
        if (obj.Kind == Values.ValueKind.String)
        {
            return StringMethods.Call(obj.AsString, expr.MethodName, args, expr.Location, _ctx);
        }

        // Class/namespace object method calls
        if (obj.Kind == Values.ValueKind.Object && obj.AsObject is Values.HeapClass cls)
        {
            if (StandardLibrary.TryCallNamespace(cls.Name, expr.MethodName, args, expr.Location, _ctx, out var result))
                return result;
        }

        // Object method calls
        if (obj.Kind == Values.ValueKind.Object && obj.AsObject is Values.HeapObjectInstance instance)
        {
            var method = instance.ClassDefinition.FindMethod(expr.MethodName);
            if (method != null)
            {
                return CallMethod(instance, method, args, expr.Location);
            }
        }

        _ctx.Throw($"Method '{expr.MethodName}' not found on type '{obj.Kind}'", expr.Location);
        return Values.Value.NullValue;
    }

    private Values.Value EvaluateMemberAccess(Lang.MemberAccessExpr expr)
    {
        // Handle namespace member access (e.g., MATH.PI, DATE.YEAR)
        if (expr.Object is Lang.NamespaceExpr ns)
        {
            if (StandardLibrary.TryCallNamespace(ns.NamespaceName, expr.MemberName, [], expr.Location, _ctx, out var result))
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
                        return Values.Value.Enum(typeIndex, i);
                    }
                }
            }
        }

        var obj = Evaluate(expr.Object);

        if (obj.Kind == Values.ValueKind.Object && obj.AsObject is Values.HeapObjectInstance instance)
        {
            // Find field index
            int fieldIndex = -1;
            var names = instance.ClassDefinition.FieldNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].Equals(expr.MemberName, System.StringComparison.OrdinalIgnoreCase))
                {
                    fieldIndex = i;
                    break;
                }
            }

            if (fieldIndex >= 0 && fieldIndex < instance.Fields.Length)
                return instance.Fields[fieldIndex];
        }

        _ctx.Throw($"Member '{expr.MemberName}' not found", expr.Location);
        return Values.Value.NullValue;
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

    private readonly Dictionary<string, int> _enumTypeIndices = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _enumNames = new();

    private Values.Value EvaluateIndex(Lang.IndexExpr expr)
    {
        var arr = Evaluate(expr.Array);
        var idx = Evaluate(expr.Index);

        // String indexing (SQ-001: code-point indexing)
        if (arr.Kind == Values.ValueKind.String)
        {
            if (idx.Kind != Values.ValueKind.Number)
            {
                _ctx.Throw("String index must be a number", expr.Location);
                return Values.Value.NullValue;
            }

            int strIndex = (int)idx.AsNumber;
            var str = arr.AsString;
            if (strIndex < 0 || strIndex >= str.CodePointCount)
            {
                _ctx.Throw("String index out of bounds", expr.Location);
                return Values.Value.NullValue;
            }

            // Return a new HeapString with a single code point (SQ-001)
            var codePointStr = str.GetCodePoint(strIndex);
            return Values.Value.String(_ctx.Heap.AllocString(codePointStr.ToString()));
        }

        // Array indexing
        if (arr.Kind != Values.ValueKind.Array)
        {
            _ctx.Throw("Index operator only supported on arrays and strings", expr.Location);
            return Values.Value.NullValue;
        }

        if (idx.Kind != Values.ValueKind.Number)
        {
            _ctx.Throw("Array index must be a number", expr.Location);
            return Values.Value.NullValue;
        }

        int arrIndex = (int)idx.AsNumber;
        if (arrIndex < 0 || arrIndex >= arr.AsArray.Length)
        {
            _ctx.Throw("Array index out of bounds", expr.Location);
            return Values.Value.NullValue;
        }

        return arr.AsArray.Items[arrIndex];
    }

    private Values.Value EvaluateNew(Lang.NewExpr expr)
    {
        if (!_classes.TryGetValue(expr.ClassName, out var cls))
        {
            _ctx.Throw($"Class '{expr.ClassName}' not found", expr.Location);
            return Values.Value.NullValue;
        }

        var args = new List<Values.Value>();
        foreach (var arg in expr.Args)
            args.Add(Evaluate(arg));

        // Find constructor
        var ctor = cls.FindMethod("CONSTRUCTOR");
        var instance = _ctx.Heap.AllocObject(cls);

        if (ctor != null)
        {
            CallMethod(instance, ctor, args, expr.Location);
        }

        return Values.Value.Object(instance);
    }

    private Values.Value EvaluateUnary(Lang.UnaryExpr expr)
    {
        var operand = Evaluate(expr.Operand);
        return expr.Operator switch
        {
            "-" when operand.Kind == Values.ValueKind.Number => Values.Value.Number(-operand.AsNumber),
            "NOT" => Values.Value.Boolean(!IsTruthy(operand)),
            _ => operand,
        };
    }

    private Values.Value EvaluateBinary(Lang.BinaryExpr expr)
    {
        var left = Evaluate(expr.Left);
        var right = Evaluate(expr.Right);

        // String concatenation
        if (expr.Operator == "+" && left.Kind == Values.ValueKind.String && right.Kind == Values.ValueKind.String)
        {
            string combined = left.AsString.ToString() + right.AsString.ToString();
            return Values.Value.String(_ctx.Heap.AllocString(combined));
        }

        // Numeric operations
        if (left.Kind == Values.ValueKind.Number && right.Kind == Values.ValueKind.Number)
        {
            double l = left.AsNumber, r = right.AsNumber;
            if (expr.Operator == "/")
            {
                if (r == 0)
                {
                    _ctx.Throw("Division by zero.", expr.Location);
                    return Values.Value.NullValue;
                }
                return Values.Value.Number(l / r);
            }
            return expr.Operator switch
            {
                "+" => Values.Value.Number(l + r),
                "-" => Values.Value.Number(l - r),
                "*" => Values.Value.Number(l * r),
                "%" => Values.Value.Number(l % r),
                "**" => Values.Value.Number(System.Math.Pow(l, r)),
                "=" => Values.Value.Boolean(l == r),
                "<>" => Values.Value.Boolean(l != r),
                "<" => Values.Value.Boolean(l < r),
                ">" => Values.Value.Boolean(l > r),
                "<=" => Values.Value.Boolean(l <= r),
                ">=" => Values.Value.Boolean(l >= r),
                _ => Values.Value.NullValue,
            };
        }

        // Equality for other types
        if (expr.Operator == "=" || expr.Operator == "<>")
        {
            bool eq = ValuesEqual(left, right);
            return Values.Value.Boolean(expr.Operator == "=" ? eq : !eq);
        }

        _ctx.Throw($"Binary operator '{expr.Operator}' not supported for types {left.Kind} and {right.Kind}", expr.Location);
        return Values.Value.NullValue;
    }

    private Values.Value EvaluateNamespace(Lang.NamespaceExpr expr)
    {
        // Return a namespace marker object
        return Values.Value.NativeFunction((args, loc) =>
        {
            if (StandardLibrary.TryCallNamespace(expr.NamespaceName, "CALL", args, loc, _ctx, out var result))
                return result;
            _ctx.Throw($"Unknown namespace call: {expr.NamespaceName}", loc);
            return Values.Value.NullValue;
        });
    }

    private Values.Value EvaluateNamespaceCall(Lang.NamespaceCallExpr expr)
    {
        var args = new List<Values.Value>();
        foreach (var arg in expr.Args)
            args.Add(Evaluate(arg));

        if (StandardLibrary.TryCallNamespace(expr.NamespaceName, expr.NamespaceName, args, expr.Location, _ctx, out var result))
            return result;

        _ctx.Throw($"Unknown namespace function: {expr.NamespaceName}", expr.Location);
        return Values.Value.NullValue;
    }

    private Values.Value EvaluatePostfix(Lang.PostfixExpr expr)
    {
        // ++/-- on variables
        var target = expr.Operand;
        if (target is Lang.IdentifierExpr id)
        {
            if (!_ctx.CurrentFrame.Locals.TryGetValue(id.Name, out var current))
            {
                _ctx.Throw($"Undefined variable '{id.Name}'", expr.Location);
                return Values.Value.NullValue;
            }

            if (current.Kind != Values.ValueKind.Number)
            {
                _ctx.Throw("Increment/decrement only supported on numbers", expr.Location);
                return Values.Value.NullValue;
            }

            double newVal = expr.Operator == "++" ? current.AsNumber + 1 : current.AsNumber - 1;
            _ctx.CurrentFrame.Locals[id.Name] = Values.Value.Number(newVal);
            return Values.Value.Number(newVal);
        }

        _ctx.Throw("Increment/decrement only supported on variables", expr.Location);
        return Values.Value.NullValue;
    }

    private void AssignToTarget(Lang.AssignTarget target, Values.Value value)
    {
        switch (target)
        {
            case Lang.VariableTarget vt:
                _ctx.CurrentFrame.Locals[vt.Name] = value;
                break;
            case Lang.IndexTarget it:
                var arr = Evaluate(it.Array);
                var idx = Evaluate(it.Index);
                if (arr.Kind == Values.ValueKind.Array && idx.Kind == Values.ValueKind.Number)
                {
                    int i = (int)idx.AsNumber;
                    if (i >= 0 && i < arr.AsArray.Length)
                        arr.AsArray.Items[i] = value;
                }
                break;
            case Lang.MemberTarget mt:
                var obj = Evaluate(mt.Object);
                if (obj.Kind == Values.ValueKind.Object && obj.AsObject is Values.HeapObjectInstance instance)
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

    public static bool IsTruthy(Values.Value value) => value switch
    {
        { Kind: Values.ValueKind.Null } => false,
        { Kind: Values.ValueKind.Boolean, AsBoolean: var b } => b,
        { Kind: Values.ValueKind.Number, AsNumber: var n } => n != 0,
        { Kind: Values.ValueKind.String } => value.AsString.CodePointCount > 0,
        { Kind: Values.ValueKind.Array } => true,
        { Kind: Values.ValueKind.Object } => true,
        { Kind: Values.ValueKind.Enum } => true,
        { Kind: Values.ValueKind.EnumSet } => true,
        _ => false,
    };

    private static bool ValuesEqual(Values.Value a, Values.Value b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            Values.ValueKind.Null => true,
            Values.ValueKind.Boolean => a.AsBoolean == b.AsBoolean,
            Values.ValueKind.Number => a.AsNumber == b.AsNumber,
            Values.ValueKind.String => a.AsString.ToString() == b.AsString.ToString(),
            Values.ValueKind.Array => a.AsArray == b.AsArray, // reference equality
            Values.ValueKind.Object => a.AsObject == b.AsObject,
            Values.ValueKind.Enum => a.AsEnum == b.AsEnum,
            Values.ValueKind.EnumSet => a.AsEnumSet == b.AsEnumSet,
            _ => false,
        };
    }

    private string ToDisplayString(Values.Value value) => value.Kind switch
    {
        Values.ValueKind.Null => "NULL",
        Values.ValueKind.String => value.AsString.ToString(),
        Values.ValueKind.Number => value.ToString(), // uses FormatNumber
        Values.ValueKind.Boolean => value.AsBoolean ? "TRUE" : "FALSE",
        Values.ValueKind.Array => throw new System.InvalidOperationException("Cannot convert ARRAY to STRING"),
        Values.ValueKind.Object => value.AsObject switch
        {
            Values.HeapObjectInstance inst => inst.ClassName,
            Values.HeapClass cls => cls.Name,
            _ => value.AsObject?.GetType().Name ?? "NULL",
        },
        Values.ValueKind.Enum => FormatEnum(value),
        Values.ValueKind.EnumSet => $"ENUMSET[{value.AsEnumSet.Count}]",
_ => value.ToString(),
    };

    private string FormatEnum(Values.Value value)
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

/// <summary>
/// Standard library implementation.
/// </summary>
public static class StandardLibrary
{
    public static bool TryCall(string name, IReadOnlyList<Values.Value> args, SourceLocation location, ExecutionContext ctx, out Values.Value result)
    {
        var upper = name.ToUpperInvariant();

        // String functions
        if (upper == "STR")
        {
            result = args.Count == 1 ? Values.Value.String(ctx.Heap.AllocString(args[0].ToString())) : Values.Value.NullValue;
            return true;
        }
        if (upper == "NUMBER")
        {
            result = args.Count == 1 && args[0].Kind == Values.ValueKind.Number ? args[0] : Values.Value.Number(0);
            return true;
        }
        if (upper == "BOOL")
        {
            result = Values.Value.Boolean(args.Count > 0 && Interpreter.IsTruthy(args[0]));
            return true;
        }
        if (upper == "COUNT")
        {
            result = args.Count == 1 ? Count(args[0]) : Values.Value.Number(0);
            return true;
        }
        if (upper == "TYPEOF")
        {
            result = args.Count == 1 ? Values.Value.String(ctx.Heap.AllocString(args[0].Kind.ToString())) : Values.Value.NullValue;
            return true;
        }
        if (upper == "LEFT")
        {
            result = StringOp(args, (s, n) => Values.Value.String(s.SubstringByCodePoints(0, (int)n)), ctx);
            return true;
        }
        if (upper == "RIGHT")
        {
            result = StringOp(args, (s, n) => Values.Value.String(s.SubstringByCodePoints(s.CodePointCount - (int)n, (int)n)), ctx);
            return true;
        }
        if (upper == "TOUPPER") // SQ-002: ASCII-only
        {
            result = StringOp(args, s => Values.Value.String(ctx.Heap.AllocString(AsciiToUpper(s.ToString()))), ctx);
            return true;
        }
        if (upper == "TOLOWER") // SQ-002: ASCII-only
        {
            result = StringOp(args, s => Values.Value.String(ctx.Heap.AllocString(AsciiToLower(s.ToString()))), ctx);
            return true;
        }
        if (upper == "TRIM")
        {
            result = StringOp(args, s => Values.Value.String(ctx.Heap.AllocString(s.ToString().Trim())), ctx);
            return true;
        }

        // Math namespace handled separately

        result = Values.Value.NullValue;
        return false;
    }

    public static bool TryCallNamespace(string ns, string method, IReadOnlyList<Values.Value> args, SourceLocation location, ExecutionContext ctx, out Values.Value result)
    {
        var upperNs = ns.ToUpperInvariant();
        var upperMethod = method.ToUpperInvariant();

        if (upperNs == "MATH")
        {
            return MathNamespace.Call(upperMethod, args, location, ctx, out result);
        }
        if (upperNs == "DATE" || upperNs == "TIME")
        {
            return DateTimeNamespace.Call(upperNs, upperMethod, args, location, ctx, out result);
        }

        result = Values.Value.NullValue;
        return false;
    }

    private static Values.Value Count(Values.Value value) => value.Kind switch
    {
        Values.ValueKind.String => Values.Value.Number(value.AsString.CodePointCount), // SQ-001: code points
        Values.ValueKind.Array => Values.Value.Number(value.AsArray.Length),
        Values.ValueKind.Null => Values.Value.Number(0),
        _ => throw new System.InvalidOperationException($"COUNT not supported for {value.Kind}"),
    };

    private static Values.Value StringOp(IReadOnlyList<Values.Value> args, Func<Values.HeapString, Values.Value> op, ExecutionContext ctx)
    {
        if (args.Count < 1 || args[0].Kind != Values.ValueKind.String)
            return Values.Value.NullValue;
        return op(args[0].AsString);
    }

    private static Values.Value StringOp(IReadOnlyList<Values.Value> args, Func<Values.HeapString, double, Values.Value> op, ExecutionContext ctx)
    {
        if (args.Count < 2 || args[0].Kind != Values.ValueKind.String || args[1].Kind != Values.ValueKind.Number)
            return Values.Value.NullValue;
        return op(args[0].AsString, args[1].AsNumber);
    }

    private static string AsciiToUpper(string s)
    {
        // SQ-002: ASCII-only
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(c >= 'a' && c <= 'z' ? (char)(c - 32) : c);
        return sb.ToString();
    }

    private static string AsciiToLower(string s)
    {
        // SQ-002: ASCII-only
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(c >= 'A' && c <= 'Z' ? (char)(c + 32) : c);
        return sb.ToString();
    }
}

/// <summary>
/// MATH namespace functions.
/// </summary>
public static class MathNamespace
{
    public static bool Call(string method, IReadOnlyList<Values.Value> args, SourceLocation location, ExecutionContext ctx, out Values.Value result)
    {
        result = method switch
        {
            "PI" => Values.Value.Number(System.Math.PI),
            "E" => Values.Value.Number(System.Math.E),
            "SQRT" => OneArg(args, x => System.Math.Sqrt(x), ctx),
            "ABS" => OneArg(args, x => System.Math.Abs(x), ctx),
            "POW" => TwoArgs(args, (x, y) => System.Math.Pow(x, y), ctx),
            "FLOOR" => OneArg(args, x => System.Math.Floor(x), ctx),
            "CEIL" => OneArg(args, x => System.Math.Ceiling(x), ctx),
            "ROUND" => OneArg(args, x => System.Math.Round(x), ctx), // banker's rounding
            "TRUNC" => OneArg(args, x => System.Math.Truncate(x), ctx),
            "SIN" => OneArg(args, x => System.Math.Sin(x), ctx),
            "COS" => OneArg(args, x => System.Math.Cos(x), ctx),
            "TAN" => OneArg(args, x => System.Math.Tan(x), ctx),
            "ASIN" => OneArg(args, x => System.Math.Asin(x), ctx),
            "ACOS" => OneArg(args, x => System.Math.Acos(x), ctx),
            "ATAN" => OneArg(args, x => System.Math.Atan(x), ctx),
            "LOG" => OneArg(args, x => System.Math.Log(x), ctx),
            "LOG10" => OneArg(args, x => System.Math.Log10(x), ctx),
            "EXP" => OneArg(args, x => System.Math.Exp(x), ctx),
            "SIGN" => OneArg(args, x => System.Math.Sign(x), ctx),
            "MIN" => TwoArgs(args, (x, y) => System.Math.Min(x, y), ctx),
            "MAX" => TwoArgs(args, (x, y) => System.Math.Max(x, y), ctx),
            "CLAMP" => ThreeArgs(args, (x, min, max) => System.Math.Clamp(x, min, max), ctx),
            "MOD" => TwoArgs(args, (x, y) => x % y, ctx),
            "RANDOM" => Random(args, ctx),
            _ => Values.Value.NullValue,
        };
        return result.Kind != Values.ValueKind.Null;
    }

    private static Values.Value OneArg(IReadOnlyList<Values.Value> args, Func<double, double> fn, ExecutionContext ctx)
    {
        if (args.Count != 1 || args[0].Kind != Values.ValueKind.Number) return Values.Value.NullValue;
        return Values.Value.Number(fn(args[0].AsNumber));
    }

    private static Values.Value TwoArgs(IReadOnlyList<Values.Value> args, Func<double, double, double> fn, ExecutionContext ctx)
    {
        if (args.Count != 2 || args[0].Kind != Values.ValueKind.Number || args[1].Kind != Values.ValueKind.Number) return Values.Value.NullValue;
        return Values.Value.Number(fn(args[0].AsNumber, args[1].AsNumber));
    }

    private static Values.Value ThreeArgs(IReadOnlyList<Values.Value> args, Func<double, double, double, double> fn, ExecutionContext ctx)
    {
        if (args.Count != 3 || args[0].Kind != Values.ValueKind.Number || args[1].Kind != Values.ValueKind.Number || args[2].Kind != Values.ValueKind.Number) return Values.Value.NullValue;
        return Values.Value.Number(fn(args[0].AsNumber, args[1].AsNumber, args[2].AsNumber));
    }

    private static Values.Value Random(IReadOnlyList<Values.Value> args, ExecutionContext ctx)
    {
        var rand = new System.Random();
        if (args.Count == 0) return Values.Value.Number(rand.NextDouble());
        if (args.Count == 1 && args[0].Kind == Values.ValueKind.Number) return Values.Value.Number(rand.Next((int)args[0].AsNumber));
        if (args.Count == 2 && args[0].Kind == Values.ValueKind.Number && args[1].Kind == Values.ValueKind.Number) return Values.Value.Number(rand.Next((int)args[0].AsNumber, (int)args[1].AsNumber));
        return Values.Value.NullValue;
    }
}

/// <summary>
/// DATE/TIME namespace functions (OSLANG 0.5).
/// </summary>
public static class DateTimeNamespace
{
    public static bool Call(string ns, string method, IReadOnlyList<Values.Value> args, SourceLocation location, ExecutionContext ctx, out Values.Value result)
    {
        var upperNs = ns.ToUpperInvariant();
        var upperMethod = method.ToUpperInvariant();

        // Constructor-style calls: DATE(year, month, day) or TIME(hour, minute, second)
        if (upperMethod == upperNs)
        {
            result = upperNs switch
            {
                "DATE" => DateConstructor(args, ctx),
                "TIME" => TimeConstructor(args, ctx),
                _ => Values.Value.NullValue,
            };
            return result.Kind != Values.ValueKind.Null;
        }

        // Method calls: DATE.YEAR(d), TIME.HOUR(t), etc.
        result = upperMethod switch
        {
            "NOW" => Values.Value.Number(0), // placeholder
            "YEAR" => DateComponent(args, d => d.Year, ctx),
            "MONTH" => DateComponent(args, d => d.Month, ctx),
            "DAY" => DateComponent(args, d => d.Day, ctx),
            "DAYOFWEEK" => DateComponent(args, d => (int)d.DayOfWeek, ctx),
            "HOUR" => TimeComponent(args, m => (int)(m / 3600000), ctx),
            "MINUTE" => TimeComponent(args, m => (int)((m % 3600000) / 60000), ctx),
            "SECOND" => TimeComponent(args, m => (int)((m % 60000) / 1000), ctx),
            _ => Values.Value.NullValue,
        };
        return result.Kind != Values.ValueKind.Null;
    }

    private static Values.Value DateConstructor(IReadOnlyList<Values.Value> args, ExecutionContext ctx)
    {
        if (args.Count != 3) return Values.Value.NullValue;
        if (args[0].Kind != Values.ValueKind.Number || args[1].Kind != Values.ValueKind.Number || args[2].Kind != Values.ValueKind.Number)
            return Values.Value.NullValue;
        
        int year = (int)args[0].AsNumber;
        int month = (int)args[1].AsNumber;
        int day = (int)args[2].AsNumber;
        
        try
        {
            var dt = new System.DateTime(year, month, day);
            long daysSinceEpoch = new DateTimeOffset(dt).ToUnixTimeSeconds() / 86400;
            return Values.Value.Number(daysSinceEpoch);
        }
        catch
        {
            return Values.Value.NullValue;
        }
    }

    private static Values.Value TimeConstructor(IReadOnlyList<Values.Value> args, ExecutionContext ctx)
    {
        if (args.Count < 2 || args.Count > 3) return Values.Value.NullValue;
        if (args[0].Kind != Values.ValueKind.Number || args[1].Kind != Values.ValueKind.Number)
            return Values.Value.NullValue;
        
        int hour = (int)args[0].AsNumber;
        int minute = (int)args[1].AsNumber;
        int second = args.Count == 3 && args[2].Kind == Values.ValueKind.Number ? (int)args[2].AsNumber : 0;
        
        if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59)
            return Values.Value.NullValue;
        
        long millis = ((long)hour * 3600 + minute * 60 + second) * 1000;
        return Values.Value.Number(millis);
    }

    private static Values.Value DateComponent(IReadOnlyList<Values.Value> args, System.Func<System.DateTime, int> selector, ExecutionContext ctx)
    {
        if (args.Count < 1 || args[0].Kind != Values.ValueKind.Number) return Values.Value.NullValue;
        // days since epoch -> DateTime
        var dt = DateTimeOffset.FromUnixTimeSeconds((long)args[0].AsNumber * 86400).DateTime;
        return Values.Value.Number(selector(dt));
    }

    private static Values.Value TimeComponent(IReadOnlyList<Values.Value> args, System.Func<long, int> selector, ExecutionContext ctx)
    {
        if (args.Count < 1 || args[0].Kind != Values.ValueKind.Number) return Values.Value.NullValue;
        long millis = (long)args[0].AsNumber; // milliseconds since midnight
        return Values.Value.Number(selector(millis));
    }
}

/// <summary>
/// Array method implementations.
/// </summary>
public static class ArrayMethods
{
    public static Values.Value Call(Values.HeapArray array, string methodName, IReadOnlyList<Values.Value> args, SourceLocation location, ExecutionContext ctx)
    {
        return methodName.ToUpperInvariant() switch
        {
            "COUNT" => Values.Value.Number(array.Length),
            "FIRST" => array.Length > 0 ? array.Items[0] : Values.Value.NullValue,
            "LAST" => array.Length > 0 ? array.Items[array.Length - 1] : Values.Value.NullValue,
            "PUSH" => ArrayPush(array, args, ctx),
            "POP" => ArrayPop(array, ctx),
            "CLEAR" => ArrayClear(array, ctx),
            "CONTAINS" => ArrayContains(array, args, ctx),
            "REMOVEAT" => ArrayRemoveAt(array, args, ctx),
            "INSERTAT" => ArrayInsertAt(array, args, ctx),
            _ => Values.Value.NullValue,
        };
    }

    private static Values.Value ArrayPush(Values.HeapArray array, IReadOnlyList<Values.Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1) return Values.Value.NullValue;
        var newItems = new Values.Value[array.Length + 1];
        System.Array.Copy(array.Items, newItems, array.Length);
        newItems[array.Length] = args[0];
        array.Items = newItems;
        return Values.Value.Number(array.Length);
    }

    private static Values.Value ArrayPop(Values.HeapArray array, ExecutionContext ctx)
    {
        if (array.Length == 0) return Values.Value.NullValue;
        var last = array.Items[array.Length - 1];
        var newItems = new Values.Value[array.Length - 1];
        System.Array.Copy(array.Items, newItems, array.Length - 1);
        array.Items = newItems;
        return last;
    }

    private static Values.Value ArrayClear(Values.HeapArray array, ExecutionContext ctx)
    {
        array.Items = [];
        return Values.Value.Number(0);
    }

    private static Values.Value ArrayContains(Values.HeapArray array, IReadOnlyList<Values.Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1) return Values.Value.Boolean(false);
        var search = args[0];
        foreach (var item in array.Items)
        {
            if (ValuesEqual(item, search)) return Values.Value.Boolean(true);
        }
        return Values.Value.Boolean(false);
    }

    private static Values.Value ArrayRemoveAt(Values.HeapArray array, IReadOnlyList<Values.Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1 || args[0].Kind != Values.ValueKind.Number) return Values.Value.NullValue;
        int index = (int)args[0].AsNumber;
        if (index < 0 || index >= array.Length) return Values.Value.NullValue;
        var newItems = new Values.Value[array.Length - 1];
        System.Array.Copy(array.Items, 0, newItems, 0, index);
        System.Array.Copy(array.Items, index + 1, newItems, index, array.Length - index - 1);
        array.Items = newItems;
        return Values.Value.Number(array.Length);
    }

    private static Values.Value ArrayInsertAt(Values.HeapArray array, IReadOnlyList<Values.Value> args, ExecutionContext ctx)
    {
        if (args.Count != 2 || args[0].Kind != Values.ValueKind.Number) return Values.Value.NullValue;
        int index = (int)args[0].AsNumber;
        if (index < 0 || index > array.Length) return Values.Value.NullValue;
        var newItems = new Values.Value[array.Length + 1];
        System.Array.Copy(array.Items, 0, newItems, 0, index);
        newItems[index] = args[1];
        System.Array.Copy(array.Items, index, newItems, index + 1, array.Length - index);
        array.Items = newItems;
        return Values.Value.Number(array.Length);
    }

    private static bool ValuesEqual(Values.Value a, Values.Value b)
    {
        if (a.Kind != b.Kind) return false;
        return a.Kind switch
        {
            Values.ValueKind.Null => true,
            Values.ValueKind.Boolean => a.AsBoolean == b.AsBoolean,
            Values.ValueKind.Number => a.AsNumber == b.AsNumber,
            Values.ValueKind.String => a.AsString.ToString() == b.AsString.ToString(),
            _ => a.ToString() == b.ToString(),
        };
    }
}

/// <summary>
/// String method implementations.
/// </summary>
public static class StringMethods
{
    public static Values.Value Call(Values.HeapString str, string methodName, IReadOnlyList<Values.Value> args, SourceLocation location, ExecutionContext ctx)
    {
        return methodName.ToUpperInvariant() switch
        {
            "TOUPPER" => StringToUpper(str, ctx),
            "TOLOWER" => StringToLower(str, ctx),
            "LENGTH" or "COUNT" => Values.Value.Number(str.CodePointCount),
            "SUBSTRING" or "SUBSTR" => StringSubstring(str, args, ctx),
            "INDEXOF" => StringIndexOf(str, args, ctx),
            "TRIM" => StringTrim(str, ctx),
            _ => Values.Value.NullValue,
        };
    }

    private static Values.Value StringToUpper(Values.HeapString str, ExecutionContext ctx)
    {
        // SQ-002: ASCII-only case folding
        var bytes = new byte[str.ByteLength];
        for (int i = 0; i < str.ByteLength; i++)
        {
            byte b = str.Utf8Bytes[i];
            if (b >= 0x61 && b <= 0x7A) // 'a'-'z'
                bytes[i] = (byte)(b - 0x20);
            else
                bytes[i] = b;
        }
        return Values.Value.String(ctx.Heap.AllocString(System.Text.Encoding.UTF8.GetString(bytes)));
    }

    private static Values.Value StringToLower(Values.HeapString str, ExecutionContext ctx)
    {
        // SQ-002: ASCII-only case folding
        var bytes = new byte[str.ByteLength];
        for (int i = 0; i < str.ByteLength; i++)
        {
            byte b = str.Utf8Bytes[i];
            if (b >= 0x41 && b <= 0x5A) // 'A'-'Z'
                bytes[i] = (byte)(b + 0x20);
            else
                bytes[i] = b;
        }
        return Values.Value.String(ctx.Heap.AllocString(System.Text.Encoding.UTF8.GetString(bytes)));
    }

    private static Values.Value StringSubstring(Values.HeapString str, IReadOnlyList<Values.Value> args, ExecutionContext ctx)
    {
        if (args.Count < 1 || args[0].Kind != Values.ValueKind.Number) return Values.Value.NullValue;
        int start = (int)args[0].AsNumber;
        int length = args.Count > 1 && args[1].Kind == Values.ValueKind.Number ? (int)args[1].AsNumber : str.CodePointCount - start;
        if (start < 0 || start >= str.CodePointCount || length < 0 || start + length > str.CodePointCount)
            return Values.Value.NullValue;
        var subStr = str.SubstringByCodePoints(start, length);
        return Values.Value.String(ctx.Heap.AllocString(subStr.ToString()));
    }

    private static Values.Value StringIndexOf(Values.HeapString str, IReadOnlyList<Values.Value> args, ExecutionContext ctx)
    {
        if (args.Count != 1 || args[0].Kind != Values.ValueKind.String) return Values.Value.Number(-1);
        var needle = args[0].AsString;
        int index = str.IndexOf(needle);
        return Values.Value.Number(index);
    }

    private static Values.Value StringTrim(Values.HeapString str, ExecutionContext ctx)
    {
        // Trim ASCII whitespace from both ends
        int start = 0;
        int end = str.CodePointCount - 1;
        while (start < str.CodePointCount && IsAsciiWhitespace(str.GetCodePoint(start).Utf8Bytes[0])) start++;
        while (end >= start && IsAsciiWhitespace(str.GetCodePoint(end).Utf8Bytes[0])) end--;
        int length = end - start + 1;
        if (length <= 0) return Values.Value.String(ctx.Heap.AllocString(""));
        var subStr = str.SubstringByCodePoints(start, length);
        return Values.Value.String(ctx.Heap.AllocString(subStr.ToString()));
    }

    private static bool IsAsciiWhitespace(byte b) => b == 0x20 || b == 0x09 || b == 0x0A || b == 0x0D || b == 0x0B || b == 0x0C;
}
}