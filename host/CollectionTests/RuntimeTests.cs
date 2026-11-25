using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Host;
using Osb.Engine.Core.Lang;
using Lang = Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace CollectionTests;

internal static class RuntimeTests
{
    private static readonly SourceLocation Loc = SourceLocation.Unknown;

    internal static void RunAll()
    {
        Console.WriteLine("=== Interpreter Runtime Behavioral Tests (R5) ===");
        FunctionNoArgsReturnsValue();
        FunctionWithArgsAndReturn();
        NestedFunctionCalls();
        FunctionReturnUsedInExpression();
        RepeatedFunctionCallsSameResult();
        FunctionArgCountMismatch();
        UnknownFunction();
        FunctionReferenceYieldsClosure();
        ExceptionNestedLevelsPropagates();
        ExpressionStatementAfterFailureNotExecuted();
        FramesPoppedAfterException();

        // ── expressions ──
        BinaryArithmetic();
        BinaryComparisons();
        StringConcatenation();
        UnaryMinus();
        NotOperator();
        UndefinedVariable();

        // ── statements / flow ──
        IfThenElifElse();
        ForLoopBreak();
        ForLoopContinue();
        WhileLoop();
        DoWhileRunsAtLeastOnce();
        ReturnNoValue();

        // ── objects / methods ──
        ObjectFieldRoundTrip();
        ConstructorInitializesField();
        MethodCallOnObject();
        MethodInvocationExceptionPropagates();
        NewWithArgThatThrowsPreservesException();
        MemberAccessOnNonObject();
        MethodNotFound();

        // ── arrays ──
        EmptyArrayCount();
        ArrayIndexReadWrite();
        ArrayPushPop();
        ArrayPopEmptyReturnsNull();
        ArrayFirstEmptyReturnsNull();
        ArrayContainsTrue();
        ArrayContainsFalse();
        ArrayRemoveAtThenRetrieve();
        ArrayInsertAt();
        ArrayIndexOutOfBounds();
        ArrayIndexMustBeNumber();
        ArrayRemoveThenReinsert();

        // ── exception flow ──
        DivisionByZeroMessage();
        TryCatchConsumesFunctionCall();
        TryCatchPropagatesToOuterCatch();
        CatchBodyThrowPropagates();
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static Interpreter NewInterpreter()
    {
        using var provider = new BumpMemoryProvider();
        var heap = new EngineHeap(provider);
        return new Interpreter(heap, new StubHostApi(), new MathProvider(), new StubTimeProvider(),
            new StubRandomProvider(), new StubUtf8Provider());
    }

    private static (Interpreter interp, Value result) RunMain(params Stmt[] body) =>
        Run(Array.Empty<Lang.FunctionDecl>(), body);

    private static (Interpreter interp, Value result) Run(
        IReadOnlyList<Lang.FunctionDecl> functions,
        params Stmt[] mainBody)
    {
        var interp = NewInterpreter();
        var all = new List<Lang.FunctionDecl> { MainFn(mainBody) };
        all.AddRange(functions);
        var program = new Lang.Program(all, Array.Empty<Lang.ClassDecl>(), Array.Empty<Lang.InterfaceDecl>(),
            Array.Empty<Lang.EnumDecl>(), Array.Empty<Lang.UsingDecl>(), Array.Empty<Lang.EventDecl>());
        var result = interp.Execute(program);
        return (interp, result);
    }

    private static Lang.FunctionDecl MainFn(IReadOnlyList<Stmt> body) =>
        new Lang.FunctionDecl("MAIN", Array.Empty<Lang.ParameterDecl>(), new List<Stmt>(body), Loc);

    private static Expr Num(double v) => new NumberLiteralExpr(v, Loc);
    private static Expr Str(string v) => new StringLiteralExpr(v, Loc);
    private static Expr Id(string name) => new IdentifierExpr(name, Loc);
    private static Expr Bin(string op, Expr l, Expr r) => new BinaryExpr(op, l, r, Loc);
    private static Expr Un(string op, Expr o) => new UnaryExpr(op, o, Loc);
    private static Expr Call(string name, params Expr[] args) => new Lang.CallExpr(name, args, Loc);
    private static Expr New(string className, params Expr[] args) => new Lang.NewExpr(className, args, Loc);
    private static Expr Arr(params Expr[] elements) => new Lang.ArrayLiteralExpr(elements, Loc);
    private static Expr IndexOf(Expr array, Expr index) => new Lang.IndexExpr(array, index, Loc);
    private static Expr Member(Expr obj, string name) => new Lang.MemberAccessExpr(obj, name, Loc);
    private static Expr MethodCall(Expr obj, string method, params Expr[] args) =>
        new Lang.MethodCallExpr(obj, method, args, Loc);

    private static Stmt Var(string name) => new Lang.VarDeclStmt(name, null, Loc);
    private static Stmt Assign(string name, Expr value) =>
        new Lang.AssignStmt(new Lang.VariableTarget(name, Loc), value, Loc);
    private static Stmt AssignField(Expr obj, string field, Expr value) =>
        new Lang.AssignStmt(new Lang.MemberTarget(obj, field, Loc), value, Loc);
    private static Stmt Ret(Expr? value) => new Lang.ReturnStmt(value, Loc);
    private static Stmt ExprStmt(Expr e) => new Lang.ExpressionStmt(e, Loc);
    private static Stmt IfStmt(Expr cond, IReadOnlyList<Stmt> thenBody,
        IReadOnlyList<ElifClause> elif, IReadOnlyList<Stmt>? elseBody) =>
        new Lang.IfStmt(cond, thenBody, elif, elseBody, Loc);
    private static ElifClause Elif(Expr cond, IReadOnlyList<Stmt> body) =>
        new Lang.ElifClause(cond, body, Loc);
    private static Stmt For(string variable, Expr from, Expr to, Expr? step, IReadOnlyList<Stmt> body) =>
        new Lang.ForStmt(variable, from, to, step, body, Loc);
    private static Stmt WhileLoop(Expr cond, IReadOnlyList<Stmt> body) =>
        new Lang.WhileStmt(cond, body, Loc);
    private static Stmt DoWhile(IReadOnlyList<Stmt> body, Expr cond) =>
        new Lang.DoWhileStmt(body, cond, Loc);
    private static Stmt Break() => new Lang.BreakStmt(Loc);
    private static Stmt Continue() => new Lang.ContinueStmt(Loc);
    private static Stmt TryCatch(IReadOnlyList<Stmt> tryBody, string errorVar,
        IReadOnlyList<Stmt> catchBody) =>
        new Lang.TryCatchStmt(tryBody, errorVar, catchBody, Loc);

    // ── function invocation ──
    // (R5 gap: happy-path function calls, args, return values, nesting were untested)

    private static void FunctionNoArgsReturnsValue()
    {
        var fn = new Lang.FunctionDecl("foo", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Ret(Num(42)) }, Loc);
        var (interp, result) = Run(new[] { fn }, Ret(Call("foo")));
        Program.Check("Runtime.FunctionNoArgsReturnsValue.Flow", interp.CurrentFlow == ControlFlow.Normal,
            $"flow={interp.CurrentFlow}");
        Program.Check("Runtime.FunctionNoArgsReturnsValue.Result",
            result.TryAsNumber(out var n) && n == 42,
            $"kind={result.Kind}");
    }

    private static void FunctionWithArgsAndReturn()
    {
        var fn = new Lang.FunctionDecl("add", new[]
            {
                new Lang.ParameterDecl("a", null, Loc),
                new Lang.ParameterDecl("b", null, Loc)
            },
            new List<Stmt> { Ret(Bin("+", Id("a"), Id("b"))) }, Loc);
        var (interp, result) = Run(new[] { fn }, Ret(Call("add", Num(3), Num(4))));
        Program.Check("Runtime.FunctionWithArgsAndReturn.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.FunctionWithArgsAndReturn.Result",
            result.TryAsNumber(out var n) && n == 7,
            $"kind={result.Kind}");
    }

    private static void NestedFunctionCalls()
    {
        var bar = new Lang.FunctionDecl("bar", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Ret(Num(10)) }, Loc);
        var foo = new Lang.FunctionDecl("foo", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Ret(Call("bar")) }, Loc);
        var (interp, result) = Run(new[] { bar, foo }, Ret(Call("foo")));
        Program.Check("Runtime.NestedFunctionCalls.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.NestedFunctionCalls.Result",
            result.TryAsNumber(out var n) && n == 10,
            $"kind={result.Kind}");
    }

    private static void FunctionReturnUsedInExpression()
    {
        var fn = new Lang.FunctionDecl("five", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Ret(Num(5)) }, Loc);
        var (interp, result) = Run(new[] { fn }, Ret(Bin("+", Call("five"), Num(1))));
        Program.Check("Runtime.FunctionReturnUsedInExpression.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.FunctionReturnUsedInExpression.Result",
            result.TryAsNumber(out var n) && n == 6,
            $"kind={result.Kind}");
    }

    private static void RepeatedFunctionCallsSameResult()
    {
        var fn = new Lang.FunctionDecl("id", new[] { new Lang.ParameterDecl("x", null, Loc) },
            new List<Stmt> { Ret(Id("x")) }, Loc);
        var (interp1, r1) = Run(new[] { fn }, Ret(Call("id", Num(1))));
        var (interp2, r2) = Run(new[] { fn }, Ret(Call("id", Num(1))));
        Program.Check("Runtime.RepeatedFunctionCallsSameResult.First",
            r1.TryAsNumber(out var n1) && n1 == 1);
        Program.Check("Runtime.RepeatedFunctionCallsSameResult.Second",
            r2.TryAsNumber(out var n2) && n2 == 1);
        Program.Check("Runtime.RepeatedFunctionCallsSameResult.Flow", interp2.CurrentFlow == ControlFlow.Normal);
    }

    private static void FunctionArgCountMismatch()
    {
        var fn = new Lang.FunctionDecl("foo", new[] { new Lang.ParameterDecl("a", null, Loc) },
            new List<Stmt> { Ret(Id("a")) }, Loc);
        var (interp, _) = Run(new[] { fn }, ExprStmt(Call("foo")));
        Program.Check("Runtime.FunctionArgCountMismatch.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.FunctionArgCountMismatch.Message",
            interp.CurrentExceptionMessage == "Function 'foo' expects 1 arguments, got 0",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void UnknownFunction()
    {
        var (interp, _) = RunMain(ExprStmt(Call("nope")));
        Program.Check("Runtime.UnknownFunction.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.UnknownFunction.Message",
            interp.CurrentExceptionMessage == "Unknown function 'nope'",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void FunctionReferenceYieldsClosure()
    {
        var fn = new Lang.FunctionDecl("foo", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Ret(Num(1)) }, Loc);
        var (interp, result) = Run(new[] { fn },
            Var("c"), Assign("c", Id("foo")), Ret(Id("c")));
        Program.Check("Runtime.FunctionReferenceYieldsClosure.Kind", result.Kind == ValueKind.Closure,
            $"kind={result.Kind}");
        Program.Check("Runtime.FunctionReferenceYieldsClosure.Flow", interp.CurrentFlow == ControlFlow.Normal);
    }

    private static void ExceptionNestedLevelsPropagates()
    {
        var l3 = new Lang.FunctionDecl("l3", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt>
            {
                Assign("t", Bin("/", Num(1), Num(0))),
                Ret(Num(0))
            }, Loc);
        var l2 = new Lang.FunctionDecl("l2", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { ExprStmt(Call("l3")) }, Loc);
        var l1 = new Lang.FunctionDecl("l1", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { ExprStmt(Call("l2")) }, Loc);
        var (interp, _) = Run(new[] { l3, l2, l1 }, ExprStmt(Call("l1")));
        Program.Check("Runtime.ExceptionNestedLevelsPropagates.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.ExceptionNestedLevelsPropagates.Message",
            interp.CurrentExceptionMessage == "Division by zero.",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void ExpressionStatementAfterFailureNotExecuted()
    {
        var boom = new Lang.FunctionDecl("boom", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Assign("t", Bin("/", Num(1), Num(0))) }, Loc);
        var (interp, result) = Run(new[] { boom },
            ExprStmt(Call("boom")), Assign("x", Num(99)), Ret(Id("x")));
        Program.Check("Runtime.ExpressionStatementAfterFailureNotExecuted.Flow",
            interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.ExpressionStatementAfterFailureNotExecuted.Result", result.IsNull);
    }

    private static void FramesPoppedAfterException()
    {
        var boom = new Lang.FunctionDecl("boom", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Assign("t", Bin("/", Num(1), Num(0))) }, Loc);
        var interp = NewInterpreter();
        var program = new Lang.Program(
            new[] { boom, MainFn(new List<Stmt> { ExprStmt(Call("boom")) }) },
            Array.Empty<Lang.ClassDecl>(), Array.Empty<Lang.InterfaceDecl>(), Array.Empty<Lang.EnumDecl>(),
            Array.Empty<Lang.UsingDecl>(), Array.Empty<Lang.EventDecl>());
        interp.Execute(program);
        Program.Check("Runtime.FramesPoppedAfterException.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.FramesPoppedAfterException.FrameCountZero",
            interp._ctx.Frames.Count == 0,
            $"count={interp._ctx.Frames.Count}");
    }

    // ── expressions ──
    // (R5 gap: binary/unary expression evaluation at runtime was untested)

    private static void BinaryArithmetic()
    {
        var (_, rAdd) = RunMain(Ret(Bin("+", Num(2), Num(3))));
        var (_, rSub) = RunMain(Ret(Bin("-", Num(5), Num(3))));
        var (_, rMul) = RunMain(Ret(Bin("*", Num(4), Num(3))));
        var (_, rDiv) = RunMain(Ret(Bin("/", Num(10), Num(2))));
        var (_, rMod) = RunMain(Ret(Bin("%", Num(7), Num(3))));
        Program.Check("Runtime.BinaryArithmetic.Add",
            rAdd.TryAsNumber(out var nAdd) && nAdd == 5, $"kind={rAdd.Kind}");
        Program.Check("Runtime.BinaryArithmetic.Sub",
            rSub.TryAsNumber(out var nSub) && nSub == 2, $"kind={rSub.Kind}");
        Program.Check("Runtime.BinaryArithmetic.Mul",
            rMul.TryAsNumber(out var nMul) && nMul == 12, $"kind={rMul.Kind}");
        Program.Check("Runtime.BinaryArithmetic.Div",
            rDiv.TryAsNumber(out var nDiv) && nDiv == 5, $"kind={rDiv.Kind}");
        Program.Check("Runtime.BinaryArithmetic.Mod",
            rMod.TryAsNumber(out var nMod) && nMod == 1, $"kind={rMod.Kind}");
    }

    private static void BinaryComparisons()
    {
        var (_, rEq) = RunMain(Ret(Bin("=", Num(3), Num(3))));
        var (_, rNe) = RunMain(Ret(Bin("<>", Num(3), Num(4))));
        var (_, rLt) = RunMain(Ret(Bin("<", Num(2), Num(3))));
        var (_, rGt) = RunMain(Ret(Bin(">", Num(3), Num(2))));
        Program.Check("Runtime.BinaryComparisons.Equal", rEq.AsBoolean);
        Program.Check("Runtime.BinaryComparisons.NotEqual", rNe.AsBoolean);
        Program.Check("Runtime.BinaryComparisons.LessThan", rLt.AsBoolean);
        Program.Check("Runtime.BinaryComparisons.GreaterThan", rGt.AsBoolean);
    }

    private static void StringConcatenation()
    {
        var (interp, result) = RunMain(Ret(Bin("+", Str("a"), Str("b"))));
        Program.Check("Runtime.StringConcatenation.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.StringConcatenation.Result",
            result.TryAsString(out var s) && s.ToString() == "ab",
            $"kind={result.Kind}");
    }

    private static void UnaryMinus()
    {
        var (interp, result) = RunMain(Ret(Un("-", Num(5))));
        Program.Check("Runtime.UnaryMinus.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.UnaryMinus.Result",
            result.TryAsNumber(out var n) && n == -5,
            $"kind={result.Kind}");
    }

    private static void NotOperator()
    {
        var (i1, r1) = RunMain(Ret(Un("NOT", Num(0))));
        var (i2, r2) = RunMain(Ret(Un("NOT", Num(5))));
        Program.Check("Runtime.NotOperator.ZeroIsTrue", r1.TryAsBoolean(out var b1) && b1);
        Program.Check("Runtime.NotOperator.NonZeroIsFalse", r2.TryAsBoolean(out var b2) && !b2);
        Program.Check("Runtime.NotOperator.Flow", i2.CurrentFlow == ControlFlow.Normal);
    }

    private static void UndefinedVariable()
    {
        var (interp, _) = RunMain(Ret(Id("nope")));
        Program.Check("Runtime.UndefinedVariable.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.UndefinedVariable.Message",
            interp.CurrentExceptionMessage == "Undefined variable 'nope'",
            $"msg={interp.CurrentExceptionMessage}");
    }

    // ── statements / flow ──
    // (R5 gap: if/elif/else, loops, break/continue were untested)

    private static void IfThenElifElse()
    {
        var (interp, result) = RunMain(
            Var("x"),
            Assign("x", Num(2)),
            IfStmt(Bin("=", Id("x"), Num(1)), new List<Stmt> { Assign("r", Num(1)) },
                new List<ElifClause> { Elif(Bin("=", Id("x"), Num(2)), new List<Stmt> { Assign("r", Num(2)) }) },
                new List<Stmt> { Assign("r", Num(3)) }),
            Ret(Id("r")));
        Program.Check("Runtime.IfThenElifElse.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.IfThenElifElse.Result",
            result.TryAsNumber(out var n) && n == 2,
            $"kind={result.Kind}");
    }

    private static void ForLoopBreak()
    {
        var (interp, result) = RunMain(
            Var("i"),
            For("i", Num(1), Num(10), null, new List<Stmt>
            {
                IfStmt(Bin("=", Id("i"), Num(5)), new List<Stmt> { Break() }, Array.Empty<ElifClause>(), null)
            }),
            Ret(Id("i")));
        Program.Check("Runtime.ForLoopBreak.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ForLoopBreak.Result",
            result.TryAsNumber(out var n) && n == 5,
            $"kind={result.Kind}");
    }

    private static void ForLoopContinue()
    {
        var (interp, result) = RunMain(
            Var("count"),
            Assign("count", Num(0)),
            For("i", Num(1), Num(5), null, new List<Stmt>
            {
                IfStmt(Bin("=", Id("i"), Num(2)), new List<Stmt> { Continue() }, Array.Empty<ElifClause>(), null),
                Assign("count", Bin("+", Id("count"), Num(1)))
            }),
            Ret(Id("count")));
        Program.Check("Runtime.ForLoopContinue.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ForLoopContinue.Result",
            result.TryAsNumber(out var n) && n == 4,
            $"kind={result.Kind}");
    }

    private static void WhileLoop()
    {
        var (interp, result) = RunMain(
            Var("i"),
            Assign("i", Num(0)),
            Var("sum"),
            Assign("sum", Num(0)),
            WhileLoop(Bin("<", Id("i"), Num(3)), new List<Stmt>
            {
                Assign("sum", Bin("+", Id("sum"), Id("i"))),
                Assign("i", Bin("+", Id("i"), Num(1)))
            }),
            Ret(Id("sum")));
        Program.Check("Runtime.WhileLoop.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.WhileLoop.Result",
            result.TryAsNumber(out var n) && n == 3,
            $"kind={result.Kind}");
    }

    private static void DoWhileRunsAtLeastOnce()
    {
        var (interp, result) = RunMain(
            Var("sum"),
            Assign("sum", Num(0)),
            DoWhile(new List<Stmt> { Assign("sum", Bin("+", Id("sum"), Num(1))) }, Bin("<", Num(0), Num(0))),
            Ret(Id("sum")));
        Program.Check("Runtime.DoWhileRunsAtLeastOnce.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.DoWhileRunsAtLeastOnce.Result",
            result.TryAsNumber(out var n) && n == 1,
            $"kind={result.Kind}");
    }

    private static void ReturnNoValue()
    {
        var (interp, result) = RunMain(Ret(null));
        Program.Check("Runtime.ReturnNoValue.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ReturnNoValue.Result", result.IsNull);
    }

    // ── objects / methods ──
    // (R5 gap: NEW, field access, method invocation, constructors were untested)

    private static void ObjectFieldRoundTrip()
    {
        var cls = new Lang.ClassDecl("Foo", Array.Empty<string>(), new Lang.MemberDecl[]
            {
                new Lang.PropertyDecl(Visibility.Public, "val", null, null, Loc)
            }, Loc);
        var (interp, result) = RunWithClasses(Array.Empty<Lang.FunctionDecl>(), new[] { cls },
            Var("o"), Assign("o", New("Foo")),
            AssignField(Id("o"), "val", Num(5)),
            Ret(Member(Id("o"), "val")));
        Program.Check("Runtime.ObjectFieldRoundTrip.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ObjectFieldRoundTrip.Result",
            result.TryAsNumber(out var n) && n == 5,
            $"kind={result.Kind}");
    }

    private static void ConstructorInitializesField()
    {
        var ctorBody = new List<Stmt>
        {
            AssignField(new Lang.MeExpr(Loc), "val", Id("v"))
        };
        var cls = new Lang.ClassDecl("Foo", Array.Empty<string>(), new Lang.MemberDecl[]
            {
                new Lang.PropertyDecl(Visibility.Public, "val", null, null, Loc),
                new Lang.ConstructorDecl(Visibility.Public,
                    new[] { new Lang.ParameterDecl("v", null, Loc) }, ctorBody, Loc)
            }, Loc);
        var (interp, result) = RunWithClasses(Array.Empty<Lang.FunctionDecl>(), new[] { cls },
            Var("o"), Assign("o", New("Foo", Num(7))),
            Ret(Member(Id("o"), "val")));
        Program.Check("Runtime.ConstructorInitializesField.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ConstructorInitializesField.Result",
            result.TryAsNumber(out var n) && n == 7,
            $"kind={result.Kind}");
    }

    private static void MethodCallOnObject()
    {
        var methodBody = new List<Stmt>
        {
            Ret(Bin("*", Member(new Lang.MeExpr(Loc), "val"), Num(2)))
        };
        var ctorBody = new List<Stmt>
        {
            AssignField(new Lang.MeExpr(Loc), "val", Id("v"))
        };
        var cls = new Lang.ClassDecl("Foo", Array.Empty<string>(), new Lang.MemberDecl[]
            {
                new Lang.PropertyDecl(Visibility.Public, "val", null, null, Loc),
                new Lang.ConstructorDecl(Visibility.Public,
                    new[] { new Lang.ParameterDecl("v", null, Loc) }, ctorBody, Loc),
                new Lang.MethodDecl(Visibility.Public, "doubleIt",
                    Array.Empty<Lang.ParameterDecl>(), methodBody, Loc)
            }, Loc);
        var (interp, result) = RunWithClasses(Array.Empty<Lang.FunctionDecl>(), new[] { cls },
            Var("o"), Assign("o", New("Foo", Num(5))),
            Ret(MethodCall(Id("o"), "doubleIt")));
        Program.Check("Runtime.MethodCallOnObject.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.MethodCallOnObject.Result",
            result.TryAsNumber(out var n) && n == 10,
            $"kind={result.Kind}");
    }

    private static void MethodInvocationExceptionPropagates()
    {
        var methodBody = new List<Stmt> { Assign("t", Bin("/", Num(1), Num(0))) };
        var cls = new Lang.ClassDecl("Foo", Array.Empty<string>(), new Lang.MemberDecl[]
            {
                new Lang.MethodDecl(Visibility.Public, "boom",
                    Array.Empty<Lang.ParameterDecl>(), methodBody, Loc)
            }, Loc);
        var (interp, _) = RunWithClasses(Array.Empty<Lang.FunctionDecl>(), new[] { cls },
            Var("o"), Assign("o", New("Foo")),
            ExprStmt(MethodCall(Id("o"), "boom")));
        Program.Check("Runtime.MethodInvocationExceptionPropagates.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.MethodInvocationExceptionPropagates.Message",
            interp.CurrentExceptionMessage == "Division by zero.",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void NewWithArgThatThrowsPreservesException()
    {
        var ctorBody = new List<Stmt>
        {
            AssignField(new Lang.MeExpr(Loc), "val", Id("v"))
        };
        var cls = new Lang.ClassDecl("Foo", Array.Empty<string>(), new Lang.MemberDecl[]
            {
                new Lang.PropertyDecl(Visibility.Public, "val", null, null, Loc),
                new Lang.ConstructorDecl(Visibility.Public,
                    new[] { new Lang.ParameterDecl("v", null, Loc) }, ctorBody, Loc)
            }, Loc);
        var (interp, _) = RunWithClasses(Array.Empty<Lang.FunctionDecl>(), new[] { cls },
            ExprStmt(New("Foo", Bin("/", Num(1), Num(0)))));
        Program.Check("Runtime.NewWithArgThatThrowsPreservesException.Flow",
            interp.CurrentFlow == ControlFlow.Exception,
            $"flow={interp.CurrentFlow}");
        Program.Check("Runtime.NewWithArgThatThrowsPreservesException.Message",
            interp.CurrentExceptionMessage == "Division by zero.",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void MemberAccessOnNonObject()
    {
        var (interp, _) = RunMain(Var("x"), Assign("x", Num(5)), Ret(Member(Id("x"), "foo")));
        Program.Check("Runtime.MemberAccessOnNonObject.Flow", interp.CurrentFlow == ControlFlow.Exception);
    }

    private static void MethodNotFound()
    {
        var cls = new Lang.ClassDecl("Foo", Array.Empty<string>(), Array.Empty<Lang.MemberDecl>(), Loc);
        var (interp, _) = RunWithClasses(Array.Empty<Lang.FunctionDecl>(), new[] { cls },
            Var("o"), Assign("o", New("Foo")),
            Ret(MethodCall(Id("o"), "nope")));
        Program.Check("Runtime.MethodNotFound.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.MethodNotFound.Message",
            interp.CurrentExceptionMessage == "Method 'nope' not found on type 'Object'",
            $"msg={interp.CurrentExceptionMessage}");
    }

    // ── arrays ──
    // (R5 gap: array mutation methods at runtime were untested)

    private static void EmptyArrayCount()
    {
        var (interp, result) = RunMain(Var("a"), Assign("a", Arr()), Ret(MethodCall(Id("a"), "COUNT")));
        Program.Check("Runtime.EmptyArrayCount.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.EmptyArrayCount.Result",
            result.TryAsNumber(out var n) && n == 0,
            $"kind={result.Kind}");
    }

    private static void ArrayIndexReadWrite()
    {
        var (interp, result) = RunMain(
            Var("a"), Assign("a", Arr(Num(1), Num(2), Num(3))),
            new Lang.AssignStmt(new Lang.IndexTarget(Id("a"), Num(1), Loc), Num(9), Loc),
            Ret(IndexOf(Id("a"), Num(1))));
        Program.Check("Runtime.ArrayIndexReadWrite.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ArrayIndexReadWrite.Result",
            result.TryAsNumber(out var n) && n == 9,
            $"kind={result.Kind}");
    }

    private static void ArrayPushPop()
    {
        var (interp, result) = RunMain(
            Var("a"), Assign("a", Arr()),
            ExprStmt(MethodCall(Id("a"), "PUSH", Num(5))),
            ExprStmt(MethodCall(Id("a"), "PUSH", Num(6))),
            Ret(MethodCall(Id("a"), "POP")));
        Program.Check("Runtime.ArrayPushPop.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ArrayPushPop.Result",
            result.TryAsNumber(out var n) && n == 6,
            $"kind={result.Kind}");
    }

    private static void ArrayPopEmptyReturnsNull()
    {
        var (interp, result) = RunMain(Var("a"), Assign("a", Arr()), Ret(MethodCall(Id("a"), "POP")));
        Program.Check("Runtime.ArrayPopEmptyReturnsNull.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ArrayPopEmptyReturnsNull.Result", result.IsNull);
    }

    private static void ArrayFirstEmptyReturnsNull()
    {
        var (interp, result) = RunMain(Var("a"), Assign("a", Arr()), Ret(MethodCall(Id("a"), "FIRST")));
        Program.Check("Runtime.ArrayFirstEmptyReturnsNull.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ArrayFirstEmptyReturnsNull.Result", result.IsNull);
    }

    private static void ArrayContainsTrue()
    {
        var (interp, result) = RunMain(
            Var("a"), Assign("a", Arr(Num(1), Num(2), Num(3))),
            Ret(MethodCall(Id("a"), "CONTAINS", Num(2))));
        Program.Check("Runtime.ArrayContainsTrue.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ArrayContainsTrue.Result",
            result.TryAsBoolean(out var b) && b,
            $"kind={result.Kind}");
    }

    private static void ArrayContainsFalse()
    {
        var (interp, result) = RunMain(
            Var("a"), Assign("a", Arr(Num(1), Num(2), Num(3))),
            Ret(MethodCall(Id("a"), "CONTAINS", Num(9))));
        Program.Check("Runtime.ArrayContainsFalse.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ArrayContainsFalse.Result",
            result.TryAsBoolean(out var b) && !b,
            $"kind={result.Kind}");
    }

    private static void ArrayRemoveAtThenRetrieve()
    {
        var (interp, result) = RunMain(
            Var("a"), Assign("a", Arr(Num(1), Num(2), Num(3))),
            ExprStmt(MethodCall(Id("a"), "REMOVEAT", Num(1))),
            Ret(IndexOf(Id("a"), Num(1))));
        Program.Check("Runtime.ArrayRemoveAtThenRetrieve.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ArrayRemoveAtThenRetrieve.Result",
            result.TryAsNumber(out var n) && n == 3,
            $"kind={result.Kind}");
    }

    private static void ArrayInsertAt()
    {
        var (interp, result) = RunMain(
            Var("a"), Assign("a", Arr(Num(1), Num(3))),
            ExprStmt(MethodCall(Id("a"), "INSERTAT", Num(1), Num(2))),
            Ret(IndexOf(Id("a"), Num(1))));
        Program.Check("Runtime.ArrayInsertAt.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ArrayInsertAt.Result",
            result.TryAsNumber(out var n) && n == 2,
            $"kind={result.Kind}");
    }

    private static void ArrayIndexOutOfBounds()
    {
        var (interp, _) = RunMain(
            Var("a"), Assign("a", Arr(Num(1))),
            Ret(IndexOf(Id("a"), Num(5))));
        Program.Check("Runtime.ArrayIndexOutOfBounds.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.ArrayIndexOutOfBounds.Message",
            interp.CurrentExceptionMessage == "Array index out of bounds",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void ArrayIndexMustBeNumber()
    {
        var (interp, _) = RunMain(
            Var("a"), Assign("a", Arr(Num(1))),
            Ret(IndexOf(Id("a"), Str("x"))));
        Program.Check("Runtime.ArrayIndexMustBeNumber.Flow", interp.CurrentFlow == ControlFlow.Exception);
    }

    private static void ArrayRemoveThenReinsert()
    {
        var (interp, result) = RunMain(
            Var("a"), Assign("a", Arr(Num(1), Num(2), Num(3))),
            ExprStmt(MethodCall(Id("a"), "REMOVEAT", Num(1))),
            ExprStmt(MethodCall(Id("a"), "PUSH", Num(4))),
            Ret(MethodCall(Id("a"), "COUNT")));
        Program.Check("Runtime.ArrayRemoveThenReinsert.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.ArrayRemoveThenReinsert.Result",
            result.TryAsNumber(out var n) && n == 3,
            $"kind={result.Kind}");
    }

    // ── exception flow ──
    // (R1 already covered single-level; these extend to nested/statement boundaries)

    private static void DivisionByZeroMessage()
    {
        var (interp, _) = RunMain(Assign("x", Bin("/", Num(1), Num(0))));
        Program.Check("Runtime.DivisionByZeroMessage.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.DivisionByZeroMessage.Message",
            interp.CurrentExceptionMessage == "Division by zero.",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void TryCatchConsumesFunctionCall()
    {
        var boom = new Lang.FunctionDecl("boom", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Assign("t", Bin("/", Num(1), Num(0))) }, Loc);
        var (interp, _) = Run(new[] { boom },
            TryCatch(new List<Stmt> { ExprStmt(Call("boom")) }, "e", Array.Empty<Stmt>()));
        Program.Check("Runtime.TryCatchConsumesFunctionCall.Flow", interp.CurrentFlow == ControlFlow.Normal);
    }

    private static void TryCatchPropagatesToOuterCatch()
    {
        var boom = new Lang.FunctionDecl("boom", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Assign("t", Bin("/", Num(1), Num(0))) }, Loc);
        var (interp, result) = Run(new[] { boom },
            Var("x"),
            TryCatch(new List<Stmt> { ExprStmt(Call("boom")) }, "e",
                new List<Stmt> { Assign("x", Num(99)) }),
            Ret(Id("x")));
        Program.Check("Runtime.TryCatchPropagatesToOuterCatch.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Runtime.TryCatchPropagatesToOuterCatch.Result",
            result.TryAsNumber(out var n) && n == 99,
            $"kind={result.Kind}");
    }

    private static void CatchBodyThrowPropagates()
    {
        var boom = new Lang.FunctionDecl("boom", Array.Empty<Lang.ParameterDecl>(),
            new List<Stmt> { Assign("t", Bin("/", Num(1), Num(0))) }, Loc);
        var (interp, _) = Run(new[] { boom },
            TryCatch(new List<Stmt> { ExprStmt(Call("boom")) }, "e",
                new List<Stmt> { ExprStmt(Call("boom")) }));
        Program.Check("Runtime.CatchBodyThrowPropagates.Flow", interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("Runtime.CatchBodyThrowPropagates.Message",
            interp.CurrentExceptionMessage == "Division by zero.",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static (Interpreter interp, Value result) RunWithClasses(
        IReadOnlyList<Lang.FunctionDecl> functions,
        IReadOnlyList<Lang.ClassDecl> classes,
        params Stmt[] mainBody)
    {
        var interp = NewInterpreter();
        var all = new List<Lang.FunctionDecl> { MainFn(mainBody) };
        all.AddRange(functions);
        var program = new Lang.Program(all, classes, Array.Empty<Lang.InterfaceDecl>(),
            Array.Empty<Lang.EnumDecl>(), Array.Empty<Lang.UsingDecl>(), Array.Empty<Lang.EventDecl>());
        var result = interp.Execute(program);
        return (interp, result);
    }

    // ── stubs ──────────────────────────────────────────────────────

    private sealed class BumpMemoryProvider : IMemoryProvider
    {
        private readonly byte[] _region = new byte[1024 * 1024];
        private int _bumpPtr;
        private readonly GCHandle _handle;

        public BumpMemoryProvider()
        {
            _handle = GCHandle.Alloc(_region, GCHandleType.Pinned);
        }

        public IntPtr BaseAddress => _handle.AddrOfPinnedObject();
        public int RegionSize => _region.Length;
        public byte[]? ManagedRegion => _region;

        public IntPtr Alloc(int sizeBytes, int alignment = 8)
        {
            if (sizeBytes <= 0) return IntPtr.Zero;
            int aligned = (_bumpPtr + alignment - 1) & ~(alignment - 1);
            int newPtr = aligned + sizeBytes;
            if (newPtr > _region.Length) return IntPtr.Zero;
            IntPtr result = BaseAddress + aligned;
            _bumpPtr = newPtr;
            return result;
        }

        public void Free(IntPtr ptr, int sizeBytes) { }
        public void Reset() { _bumpPtr = 0; }
        public void Collect() { }
        public void Dispose()
        {
            if (_handle.IsAllocated) _handle.Free();
        }
    }

    private sealed class StubHostApi : IHostApi
    {
        public void Write(string text) { }
        public string? ReadLine() => null;
        public void Clear() { }
        public void Exit(int code = 0) { }
    }

    private sealed class MathProvider : IMathProvider
    {
        public double PI => Math.PI;
        public double E => Math.E;
        public double Sqrt(double x) => Math.Sqrt(x);
        public double Abs(double x) => Math.Abs(x);
        public double Pow(double x, double y) => Math.Pow(x, y);
        public double Floor(double x) => Math.Floor(x);
        public double Ceil(double x) => Math.Ceiling(x);
        public double Round(double x) => Math.Round(x);
        public double Trunc(double x) => Math.Truncate(x);
        public double Sin(double x) => Math.Sin(x);
        public double Cos(double x) => Math.Cos(x);
        public double Tan(double x) => Math.Tan(x);
        public double Asin(double x) => Math.Asin(x);
        public double Acos(double x) => Math.Acos(x);
        public double Atan(double x) => Math.Atan(x);
        public double Log(double x) => Math.Log(x);
        public double Log10(double x) => Math.Log10(x);
        public double Exp(double x) => Math.Exp(x);
        public double Sign(double x) => Math.Sign(x);
        public double Min(double x, double y) => Math.Min(x, y);
        public double Max(double x, double y) => Math.Max(x, y);
        public double Clamp(double x, double min, double max) => Math.Clamp(x, min, max);
    }

    private sealed class StubTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public ulong Ticks => 0;
        public void Sleep(ulong ticks) { }
        public DateTime? CreateDate(int year, int month, int day) => null;
        public DateTime? CreateTime(int hour, int minute, int second, int millisecond = 0) => null;
        public (int Year, int Month, int Day, int DayOfWeek)? DaysToDate(long daysSinceEpoch) => null;
        public (int Hour, int Minute, int Second)? MillisToTime(long millisSinceMidnight) => null;
    }

    private sealed class StubRandomProvider : IRandomProvider
    {
        public int Next() => 0;
        public int Next(int maxValue) => 0;
        public int Next(int minValue, int maxValue) => minValue;
        public double NextDouble() => 0;
    }

    private sealed class StubUtf8Provider : IUtf8Provider
    {
        public byte[] GetBytes(string s) => throw new NotImplementedException();
        public string GetString(byte[] bytes) => throw new NotImplementedException();
        public string GetString(ReadOnlySpan<byte> bytes) => throw new NotImplementedException();
        public bool IsValidUtf8(ReadOnlySpan<byte> bytes) => throw new NotImplementedException();
    }
}
