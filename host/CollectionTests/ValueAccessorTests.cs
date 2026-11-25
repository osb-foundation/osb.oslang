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

internal static class ValueAccessorTests
{
    private static readonly SourceLocation Loc = SourceLocation.Unknown;

    internal static void RunAll()
    {
        Console.WriteLine("=== Value Accessor Explicit-Failure Tests ===");
        TryAsNumberCorrectAndMismatch();
        TryAsStringCorrectAndMismatch();
        TryAsArrayCorrectAndMismatch();
        TryAsObjectNullIsFalse();
        TryAsBooleanCorrectAndMismatch();

        NumberValidViaInterpreter();
        NumberInvalidViaInterpreter();
        NumberNoArgsViaInterpreter();
        LeftValidViaInterpreter();
        LeftInvalidArgTypesViaInterpreter();
        BinarySubExpressionErrorPreserved();
        FunctionCallErrorRemainsObservable();
        ValidCoercionsContinueToWork();
    }

    // ── direct Value.TryAsX accessors ──────────────────────────────

    private static void TryAsNumberCorrectAndMismatch()
    {
        var num = Value.Number(42.5);
        Program.Check("Value.TryAsNumber.Correct",
            num.TryAsNumber(out var n) && n == 42.5);

        var str = Value.String(HeapStringTest("abc"));
        Program.Check("Value.TryAsNumber.Mismatch",
            !str.TryAsNumber(out var n2) && n2 == 0);
    }

    private static void TryAsStringCorrectAndMismatch()
    {
        var str = Value.String(HeapStringTest("hello"));
        Program.Check("Value.TryAsString.Correct",
            str.TryAsString(out var s) && s.ToString() == "hello");

        var num = Value.Number(7);
        Program.Check("Value.TryAsString.Mismatch",
            !num.TryAsString(out var s2));
    }

    private static void TryAsArrayCorrectAndMismatch()
    {
        using var provider = new ValueTestMemoryProvider();
        var heap = new EngineHeap(provider);
        var arr = heap.AllocArray(3);
        var val = Value.Array(arr);
        Program.Check("Value.TryAsArray.Correct",
            val.TryAsArray(out var a) && a.Length == 3);

        var str = Value.String(HeapStringTest("x"));
        Program.Check("Value.TryAsArray.Mismatch",
            !str.TryAsArray(out var a2));
    }

    private static void TryAsObjectNullIsFalse()
    {
        Program.Check("Value.TryAsObject.NullIsFalse",
            !Value.NullValue.TryAsObject(out var _));
    }

    private static void TryAsBooleanCorrectAndMismatch()
    {
        var tru = Value.Boolean(true);
        Program.Check("Value.TryAsBoolean.Correct",
            tru.TryAsBoolean(out var b) && b);

        var num = Value.Number(0);
        Program.Check("Value.TryAsBoolean.Mismatch",
            !num.TryAsBoolean(out var b2));
    }

    // ── helper heap string ─────────────────────────────────────────

    private static HeapString HeapStringTest(string text)
    {
        using var provider = new ValueTestMemoryProvider();
        var heap = new EngineHeap(provider);
        return heap.AllocString(text);
    }

    // ── interpreter-driven tests ───────────────────────────────────

    private static Interpreter NewInterpreter()
    {
        using var provider = new ValueTestMemoryProvider();
        var heap = new EngineHeap(provider);
        return new Interpreter(heap, new ValueTestHostApi(), new MathProvider(),
            new StubTimeProvider(), new StubRandomProvider(), new StubUtf8Provider());
    }

    private static (Interpreter interp, Value result) RunMain(params Stmt[] body)
    {
        var interp = NewInterpreter();
        var program = new Lang.Program(
            new[] { MainFn(body) },
            Array.Empty<Lang.ClassDecl>(), Array.Empty<Lang.InterfaceDecl>(),
            Array.Empty<Lang.EnumDecl>(), Array.Empty<Lang.UsingDecl>(),
            Array.Empty<Lang.EventDecl>());
        Value result = interp.Execute(program);
        return (interp, result);
    }

    private static Lang.FunctionDecl MainFn(IReadOnlyList<Stmt> body) =>
        new Lang.FunctionDecl("MAIN", Array.Empty<Lang.ParameterDecl>(), new List<Stmt>(body), Loc);

    private static Lang.Expr Num(double v) => new Lang.NumberLiteralExpr(v, Loc);
    private static Lang.Expr Str(string v) => new Lang.StringLiteralExpr(v, Loc);
    private static Lang.Expr Bin(string op, Lang.Expr l, Lang.Expr r) =>
        new Lang.BinaryExpr(op, l, r, Loc);
    private static Lang.Expr Call(string name, params Lang.Expr[] args) =>
        new Lang.CallExpr(name, args, Loc);
    private static Lang.Stmt Assign(string name, Lang.Expr expr) =>
        new Lang.AssignStmt(new Lang.VariableTarget(name, Loc), expr, Loc);
    private static Lang.Stmt Ret(Lang.Expr expr) =>
        new Lang.ReturnStmt(expr, Loc);

    private static void NumberValidViaInterpreter()
    {
        var (interp, result) = RunMain(Assign("x", Call("NUMBER", Num(5))), Ret(new Lang.IdentifierExpr("x", Loc)));
        Program.Check("Interp.NumberValid.Flow", interp.CurrentFlow == ControlFlow.Normal,
            $"flow={interp.CurrentFlow}");
        Program.Check("Interp.NumberValid.Result",
            result.TryAsNumber(out var n) && n == 5);
    }

    private static void NumberInvalidViaInterpreter()
    {
        var (interp, result) = RunMain(Assign("x", Call("NUMBER", Str("abc"))));
        Program.Check("Interp.NumberInvalid.Flow", interp.CurrentFlow == ControlFlow.Exception,
            $"flow={interp.CurrentFlow}");
        Program.Check("Interp.NumberInvalid.Message",
            interp.CurrentExceptionMessage == "NUMBER expects a single numeric argument",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void NumberNoArgsViaInterpreter()
    {
        var (interp, result) = RunMain(Assign("x", Call("NUMBER")));
        Program.Check("Interp.NumberNoArgs.Flow", interp.CurrentFlow == ControlFlow.Exception,
            $"flow={interp.CurrentFlow}");
        Program.Check("Interp.NumberNoArgs.Message",
            interp.CurrentExceptionMessage == "NUMBER expects a single numeric argument",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void LeftValidViaInterpreter()
    {
        var (interp, result) = RunMain(Ret(Call("LEFT", Str("hello"), Num(2))));
        Program.Check("Interp.LeftValid.Flow", interp.CurrentFlow == ControlFlow.Normal);
        Program.Check("Interp.LeftValid.Result",
            result.TryAsString(out var s) && s.ToString() == "he");
    }

    private static void LeftInvalidArgTypesViaInterpreter()
    {
        var (interp1, _) = RunMain(Ret(Call("LEFT", Str("hello"), Str("x"))));
        Program.Check("Interp.LeftInvalidSecond.Flow", interp1.CurrentFlow == ControlFlow.Exception);
        Program.Check("Interp.LeftInvalidSecond.Message",
            interp1.CurrentExceptionMessage == "Expected a string and a number argument",
            $"msg={interp1.CurrentExceptionMessage}");

        var (interp2, _) = RunMain(Ret(Call("LEFT", Num(5), Num(2))));
        Program.Check("Interp.LeftInvalidFirst.Flow", interp2.CurrentFlow == ControlFlow.Exception);
        Program.Check("Interp.LeftInvalidFirst.Message",
            interp2.CurrentExceptionMessage == "Expected a string and a number argument",
            $"msg={interp2.CurrentExceptionMessage}");
    }

    // ── EvaluateBinary: sub-expression error must not be overwritten ──

    private static void BinarySubExpressionErrorPreserved()
    {
        // (1 / 0) + "abc" : left raises "Division by zero."; without the fix the
        // generic "Binary operator '+' not supported ..." would overwrite it.
        var (interp, result) = RunMain(Ret(Bin("+", Bin("/", Num(1), Num(0)), Str("abc"))));
        Program.Check("Interp.BinarySubExprError.Flow", interp.CurrentFlow == ControlFlow.Exception,
            $"flow={interp.CurrentFlow}");
        Program.Check("Interp.BinarySubExprError.Message",
            interp.CurrentExceptionMessage == "Division by zero.",
            $"msg={interp.CurrentExceptionMessage}");
    }

    // ── R1: function-call error remains observable ──

    private static void FunctionCallErrorRemainsObservable()
    {
        var inner = new Lang.FunctionDecl("boom", Array.Empty<Lang.ParameterDecl>(),
            new List<Lang.Stmt>
            {
                new Lang.AssignStmt(new Lang.VariableTarget("t", Loc),
                    new Lang.BinaryExpr("/", Num(1), Num(0), Loc), Loc),
                new Lang.ReturnStmt(new Lang.NumberLiteralExpr(0, Loc), Loc)
            }, Loc);
        var interp = NewInterpreter();
        interp.RegisterFunction(inner);
        var program = new Lang.Program(
            new[]
            {
                new Lang.FunctionDecl("MAIN", Array.Empty<Lang.ParameterDecl>(),
                    new List<Lang.Stmt>
                    {
                        Ret(Call("boom")),
                        new Lang.ReturnStmt(new Lang.NumberLiteralExpr(0, Loc), Loc)
                    }, Loc)
            },
            Array.Empty<Lang.ClassDecl>(), Array.Empty<Lang.InterfaceDecl>(),
            Array.Empty<Lang.EnumDecl>(), Array.Empty<Lang.UsingDecl>(),
            Array.Empty<Lang.EventDecl>());
        interp.Execute(program);
        Program.Check("Interp.FunctionCallError.Flow", interp.CurrentFlow == ControlFlow.Exception,
            $"flow={interp.CurrentFlow}");
        Program.Check("Interp.FunctionCallError.Message",
            interp.CurrentExceptionMessage == "Division by zero.",
            $"msg={interp.CurrentExceptionMessage}");
    }

    // ── valid coercions ──

    private static void ValidCoercionsContinueToWork()
    {
        var (interp1, result1) = RunMain(Ret(Bin("+", Num(1), Num(2))));
        Program.Check("Interp.Coercion.NumberAdd.Flow", interp1.CurrentFlow == ControlFlow.Normal);
        Program.Check("Interp.Coercion.NumberAdd.Result",
            result1.TryAsNumber(out var n) && n == 3);

        var (interp2, result2) = RunMain(Ret(Bin("+", Str("a"), Str("b"))));
        Program.Check("Interp.Coercion.StringConcat.Flow", interp2.CurrentFlow == ControlFlow.Normal);
        Program.Check("Interp.Coercion.StringConcat.Result",
            result2.TryAsString(out var s) && s.ToString() == "ab");
    }

    // ── stubs ──────────────────────────────────────────────────────

    private sealed class ValueTestMemoryProvider : IMemoryProvider, IDisposable
    {
        private readonly byte[] _region = new byte[1024 * 1024];
        private int _bumpPtr;
        private readonly GCHandle _handle;

        public ValueTestMemoryProvider()
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

    private sealed class ValueTestHostApi : IHostApi
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
