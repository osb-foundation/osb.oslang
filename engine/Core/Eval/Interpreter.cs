using System.Collections.Generic;
using Osb.Engine.Core.Collections;
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
    internal readonly StatementExecutor _statements;
    internal readonly ExpressionEvaluator _expressions;

    internal readonly ExecutionContext _ctx;
    internal readonly StringMap<Lang.FunctionDecl> _functions = new(8, StringComparer.OrdinalIgnoreCase);
    internal readonly StringMap<HeapClass> _classes = new(8, StringComparer.OrdinalIgnoreCase);
    internal readonly StringMap<Lang.EnumDecl> _enums = new(8, StringComparer.OrdinalIgnoreCase);
    internal readonly UsingList _usings = new();
    internal readonly StringMap<NativeFunction> _extensions = new(8, StringComparer.OrdinalIgnoreCase);
    internal readonly ValueMap _globals = new(8, StringComparer.OrdinalIgnoreCase);

    internal readonly IMathProvider _math;
    internal readonly ITimeProvider _time;
    internal readonly IRandomProvider _random;
    internal readonly IUtf8Provider _utf8;

    internal readonly StringMap<int> _enumTypeIndices = new(8, StringComparer.OrdinalIgnoreCase);
    internal readonly StringList _enumNames = new();

    public Interpreter(EngineHeap heap, IHostApi host, IMathProvider math, ITimeProvider time, IRandomProvider random, IUtf8Provider utf8)
    {
        _ctx = new ExecutionContext(heap, host);
        _math = math;
        _time = time;
        _random = random;
        _utf8 = utf8;

        _statements = new StatementExecutor(this);
        _expressions = new ExpressionEvaluator(this);
    }

    public void RegisterFunction(Lang.FunctionDecl fn) => _functions[fn.Name] = fn;
    public void RegisterClass(HeapClass cls) => _classes[cls.Name] = cls;
    public void RegisterEnum(Lang.EnumDecl en) => _enums[en.Name] = en;
    public void RegisterUsing(Lang.UsingDecl u) => _usings.Add(u);
    public void RegisterExtension(string name, NativeFunction fn) => _extensions[name] = fn;
    public void SetGlobal(string name, Value value) => _globals[name] = value;

    /// <summary>
    /// Executes a program starting from MAIN().
    /// </summary>
    public Value Execute(Lang.Program program)
    {
        // Freestanding-safe indexed iteration over the program collections.
        // The parser backs these with NodeList<T>; in the freestanding runtime
        // interface dispatch on IReadOnlyList<T> returns 0/empty, so a foreach
        // or interface-Count loop would silently register nothing (MAIN never
        // inserted -> lookup fails). CollectionIteration uses the concrete
        // NodeList<T> Count/indexer when available.
        var functions = program.Functions;
        for (int i = 0; i < CollectionIteration.Count(functions); i++) RegisterFunction(CollectionIteration.Get(functions, i));
        var classes = program.Classes;
        for (int i = 0; i < CollectionIteration.Count(classes); i++) RegisterClass(CreateHeapClass(CollectionIteration.Get(classes, i)));
        var enums = program.Enums;
        for (int i = 0; i < CollectionIteration.Count(enums); i++) RegisterEnum(CollectionIteration.Get(enums, i));

        // Built-in KEYCODE enum (PC key codes), used by KISS and other OSLANG programs.
        // Member values are stored as NumberLiteralExpr so EvaluateMemberAccess
        // returns Value.Number(memberValue) — matching Key.KEY (the raw key code).
        var keycodeMembers = new Lang.EnumMemberDecl[]
        {
            new Lang.EnumMemberDecl("UNKNOWN", new Lang.NumberLiteralExpr(0, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("ENTER", new Lang.NumberLiteralExpr(13, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("ESC", new Lang.NumberLiteralExpr(27, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("BACKSPACE", new Lang.NumberLiteralExpr(8, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("TAB", new Lang.NumberLiteralExpr(9, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("SPACE", new Lang.NumberLiteralExpr(32, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("UP", new Lang.NumberLiteralExpr(101, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("DOWN", new Lang.NumberLiteralExpr(102, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("RIGHT", new Lang.NumberLiteralExpr(103, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("LEFT", new Lang.NumberLiteralExpr(104, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("HOME", new Lang.NumberLiteralExpr(105, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("END", new Lang.NumberLiteralExpr(106, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("PAGEUP", new Lang.NumberLiteralExpr(107, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("PAGEDOWN", new Lang.NumberLiteralExpr(108, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("DELETE", new Lang.NumberLiteralExpr(109, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("F1", new Lang.NumberLiteralExpr(110, SourceLocation.Unknown), SourceLocation.Unknown),
            new Lang.EnumMemberDecl("F12", new Lang.NumberLiteralExpr(111, SourceLocation.Unknown), SourceLocation.Unknown),
        };
        RegisterEnum(new Lang.EnumDecl("KEYCODE", keycodeMembers, SourceLocation.Unknown));
        var usings = program.Usings;
        for (int i = 0; i < CollectionIteration.Count(usings); i++) RegisterUsing(CollectionIteration.Get(usings, i));

        if (!_functions.TryGetValue("MAIN", out var mainFn))
        {
            _ctx.Throw("No MAIN function found", SourceLocation.Unknown);
            return Value.NullValue;
        }

        return CallFunction(mainFn, [], SourceLocation.Unknown);
    }

    private HeapClass CreateHeapClass(Lang.ClassDecl decl)
    {
        var baseClasses = decl.BaseClasses;
        var members = decl.Members;
        var baseClass = CollectionIteration.Count(baseClasses) > 0 && _classes.TryGetValue(CollectionIteration.Get(baseClasses, 0), out var baseCls)
            ? baseCls : null;

        var fieldNames = new List<string>();
        var methods = new List<Lang.MethodDecl>();

        for (int mi = 0; mi < CollectionIteration.Count(members); mi++)
        {
            var member = CollectionIteration.Get(members, mi);
            switch (member)
            {
                case Lang.PropertyDecl prop:
                    fieldNames.Add(prop.Name);
                    break;
                case Lang.MethodDecl method:
                    methods.Add(method);
                    break;
                case Lang.ConstructorDecl ctor:
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

    internal Value CallFunction(Lang.FunctionDecl fn, IReadOnlyList<Value> args, SourceLocation location)
    {
        var parameters = fn.Parameters;
        var body = fn.Body;
        int argCount = CollectionIteration.Count(args);
        int paramCount = CollectionIteration.Count(parameters);
        if (argCount != paramCount)
        {
            _ctx.Throw($"Function '{fn.Name}' expects {paramCount} arguments, got {argCount}", location);
            return Value.NullValue;
        }

        var frame = new Frame(fn);
        for (int i = 0; i < paramCount; i++)
        {
            frame.Locals[CollectionIteration.Get(parameters, i).Name] = CollectionIteration.Get(args, i);
        }

        _ctx.PushFrame(frame);
        _ctx.CurrentFlow = ControlFlow.Normal;

        for (int si = 0; si < CollectionIteration.Count(body); si++)
        {
            _statements.Execute(CollectionIteration.Get(body, si));
            if (_ctx.CurrentFlow != ControlFlow.Normal)
                break;
        }

        var retVal = _ctx.ReturnValue;
        _ctx.ReturnValue = Value.NullValue;
        _ctx.PopFrame();
        // Preserve a pending exception so the caller's control-flow guard propagates it.
        // Only normalize Return/Break/Continue/Normal; never swallow an Exception.
        if (_ctx.CurrentFlow != ControlFlow.Exception)
            _ctx.CurrentFlow = ControlFlow.Normal;

        return retVal;
    }

    internal Value CallMethod(HeapObjectInstance instance, Lang.MethodDecl method, IReadOnlyList<Value> args, SourceLocation location)
    {
        var parameters = method.Parameters;
        var body = method.Body;
        int argCount = CollectionIteration.Count(args);
        int paramCount = CollectionIteration.Count(parameters);
        if (argCount != paramCount)
        {
            _ctx.Throw($"Method '{method.Name}' expects {paramCount} arguments, got {argCount}", location);
            return Value.NullValue;
        }

        var fn = new Lang.FunctionDecl(method.Name, method.Parameters, method.Body, location);

        var frame = new Frame(fn, Value.Object(instance), Value.NullValue);
        for (int i = 0; i < paramCount; i++)
        {
            frame.Locals[CollectionIteration.Get(parameters, i).Name] = CollectionIteration.Get(args, i);
        }

        _ctx.PushFrame(frame);
        _ctx.CurrentFlow = ControlFlow.Normal;

        for (int si = 0; si < CollectionIteration.Count(body); si++)
        {
            _statements.Execute(CollectionIteration.Get(body, si));
            if (_ctx.CurrentFlow != ControlFlow.Normal)
                break;
        }

        var retVal = _ctx.ReturnValue;
        _ctx.ReturnValue = Value.NullValue;
        _ctx.PopFrame();
        if (_ctx.CurrentFlow != ControlFlow.Exception)
            _ctx.CurrentFlow = ControlFlow.Normal;

        return retVal;
    }

    /// <summary>
    /// Current interpreter control-flow state. Exception indicates a runtime error
    /// is pending and has not been consumed by a TRY/CATCH.
    /// </summary>
    public ControlFlow CurrentFlow => _ctx.CurrentFlow;

    /// <summary>
    /// The pending exception value, or NullValue when none is pending.
    /// </summary>
    public Value CurrentException => _ctx.CurrentException;

    /// <summary>
    /// The pending exception message, or null when none is pending.
    /// </summary>
    public string? CurrentExceptionMessage => _ctx.CurrentExceptionMessage;

    /// <summary>
    /// The source location of the pending exception, or Unknown when none is pending.
    /// </summary>
    public SourceLocation ExceptionLocation => _ctx.ExceptionLocation;

    internal Value Evaluate(Lang.Expr expr) => _expressions.Evaluate(expr);

    public void AssignToTarget(Lang.AssignTarget target, Value value)
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

    public static bool IsTruthy(Value value) => ValueUtilities.IsTruthy(value);

    public string ToDisplayString(Value value) => ValueUtilities.ToDisplayString(this, value);
}