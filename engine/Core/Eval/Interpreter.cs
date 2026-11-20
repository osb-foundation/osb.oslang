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

    /// <summary>
    /// Executes a program starting from MAIN().
    /// </summary>
    public Value Execute(Lang.Program program)
    {
        foreach (var fn in program.Functions) RegisterFunction(fn);
        foreach (var cls in program.Classes) RegisterClass(CreateHeapClass(cls));
        foreach (var en in program.Enums) RegisterEnum(en);
        foreach (var u in program.Usings) RegisterUsing(u);

        if (!_functions.TryGetValue("MAIN", out var mainFn))
        {
            _ctx.Throw("No MAIN function found", SourceLocation.Unknown);
            return Value.NullValue;
        }

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
        if (args.Count != fn.Parameters.Count)
        {
            _ctx.Throw($"Function '{fn.Name}' expects {fn.Parameters.Count} arguments, got {args.Count}", location);
            return Value.NullValue;
        }

        var frame = new Frame(fn);
        for (int i = 0; i < fn.Parameters.Count; i++)
        {
            frame.Locals[fn.Parameters[i].Name] = args[i];
        }

        _ctx.PushFrame(frame);
        _ctx.CurrentFlow = ControlFlow.Normal;

        foreach (var stmt in fn.Body)
        {
            _statements.Execute(stmt);
            if (_ctx.CurrentFlow != ControlFlow.Normal)
                break;
        }

        var retVal = _ctx.ReturnValue;
        _ctx.ReturnValue = Value.NullValue;
        _ctx.PopFrame();
        _ctx.CurrentFlow = ControlFlow.Normal;

        return retVal;
    }

    internal Value CallMethod(HeapObjectInstance instance, Lang.MethodDecl method, IReadOnlyList<Value> args, SourceLocation location)
    {
        if (args.Count != method.Parameters.Count)
        {
            _ctx.Throw($"Method '{method.Name}' expects {method.Parameters.Count} arguments, got {args.Count}", location);
            return Value.NullValue;
        }

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
            _statements.Execute(stmt);
            if (_ctx.CurrentFlow != ControlFlow.Normal)
                break;
        }

        var retVal = _ctx.ReturnValue;
        _ctx.ReturnValue = Value.NullValue;
        _ctx.PopFrame();
        _ctx.CurrentFlow = ControlFlow.Normal;

        return retVal;
    }

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