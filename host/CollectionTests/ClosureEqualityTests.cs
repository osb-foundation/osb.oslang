using System;
using System.Runtime.InteropServices;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Host;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace CollectionTests;

/// <summary>
/// Regression tests for R7: closure identity-based equality.
///
/// Exercises the actual <see cref="ValueUtilities.ValuesEqual(Value,Value)"/>
/// path used by language-level equality.
/// </summary>
internal static class ClosureEqualityTests
{
    private static readonly SourceLocation Loc = SourceLocation.Unknown;

    internal static void RunAll()
    {
        Console.WriteLine("=== Closure Identity Equality Tests (R7) ===");
        ClosureSelfEqualityIsTrue();
        DistinctClosuresSameBodyAreNotEqual();
        DistinctClosuresEquivalentCapturesAreNotEqual();
        DistinctClosuresDifferentCapturesAreNotEqual();
        ClosureVersusNonKindIsFalse();
        ClosureEqualsDoesNotExecuteBody();
        ArrayIdentityUnchanged();
        ObjectIdentityUnchanged();
        PrimitiveEqualityUnchanged();
    }

    private static Value Closure(FunctionDecl fn, params Value[] captured)
        => Value.Closure(new HeapClosure(fn, captured));

    // ── R7: closure identity contract ──────────────────────────────

    private static void ClosureSelfEqualityIsTrue()
    {
        var fn = new FunctionDecl("f", Array.Empty<ParameterDecl>(), Array.Empty<Stmt>(), Loc);
        var c = Closure(fn);

        Program.Check("Closure.SelfEqualityIsTrue",
            ValueUtilities.ValuesEqual(c, c),
            "a closure compared with itself must be true");
    }

    private static void DistinctClosuresSameBodyAreNotEqual()
    {
        var fn1 = new FunctionDecl("f", Array.Empty<ParameterDecl>(), Array.Empty<Stmt>(), Loc);
        var fn2 = new FunctionDecl("f", Array.Empty<ParameterDecl>(), Array.Empty<Stmt>(), Loc);
        var c1 = Closure(fn1);
        var c2 = Closure(fn2); // same body (empty), distinct instance

        Program.Check("Closure.DistinctSameBodyNotEqual",
            !ValueUtilities.ValuesEqual(c1, c2),
            "distinct closures with the same body must be false");
    }

    private static void DistinctClosuresEquivalentCapturesAreNotEqual()
    {
        var fn = new FunctionDecl("f", Array.Empty<ParameterDecl>(), Array.Empty<Stmt>(), Loc);
        var c1 = Closure(fn, Value.Number(1), Value.String(new HeapString("hi")), Value.Boolean(true));
        var c2 = Closure(fn, Value.Number(1), Value.String(new HeapString("hi")), Value.Boolean(true)); // equivalent captures, distinct instance

        Program.Check("Closure.DistinctEquivalentCapturesNotEqual",
            !ValueUtilities.ValuesEqual(c1, c2),
            "distinct closures with equivalent captures must be false");
    }

    private static void DistinctClosuresDifferentCapturesAreNotEqual()
    {
        var fn = new FunctionDecl("f", Array.Empty<ParameterDecl>(), Array.Empty<Stmt>(), Loc);
        var c1 = Closure(fn, Value.Number(1));
        var c2 = Closure(fn, Value.Number(2)); // different captures

        Program.Check("Closure.DistinctDifferentCapturesNotEqual",
            !ValueUtilities.ValuesEqual(c1, c2),
            "distinct closures with different captures must be false");
    }

    private static void ClosureVersusNonKindIsFalse()
    {
        var fn = new FunctionDecl("f", Array.Empty<ParameterDecl>(), Array.Empty<Stmt>(), Loc);
        var c = Closure(fn);

        Program.Check("Closure.VersusNumberIsFalse",
            !ValueUtilities.ValuesEqual(c, Value.Number(0)),
            "closure compared to a number must be false");
        Program.Check("Closure.VersusNullIsFalse",
            !ValueUtilities.ValuesEqual(c, Value.NullValue),
            "closure compared to null must be false");
        Program.Check("Closure.VersusStringIsFalse",
            !ValueUtilities.ValuesEqual(c, Value.String(new HeapString("x"))),
            "closure compared to a string must be false");
    }

    // ── Array / object / primitive regression guards ───────────────

    private static void ArrayIdentityUnchanged()
    {
        var a1 = Value.Array(new HeapArray(new[] { Value.Number(1), Value.Number(2) }));
        var a2 = Value.Array(new HeapArray(new[] { Value.Number(1), Value.Number(2) })); // identical content, distinct

        Program.Check("Array.DistinctSameContentNotEqual",
            !ValueUtilities.ValuesEqual(a1, a2),
            "distinct arrays with identical content must be false");
        Program.Check("Array.SameReferenceEqual",
            ValueUtilities.ValuesEqual(a1, a1),
            "the same array reference must be true");
    }

    private static void ObjectIdentityUnchanged()
    {
        var cls = new HeapClass("C", null, Array.Empty<string>(), Array.Empty<MethodDecl>());
        var o1 = Value.Object(cls);
        var o2 = Value.Object(new HeapClass("C", null, Array.Empty<string>(), Array.Empty<MethodDecl>())); // identical, distinct

        Program.Check("Object.DistinctSameContentNotEqual",
            !ValueUtilities.ValuesEqual(o1, o2),
            "distinct objects must be false");
        Program.Check("Object.SameReferenceEqual",
            ValueUtilities.ValuesEqual(o1, o1),
            "the same object reference must be true");
    }

    private static void PrimitiveEqualityUnchanged()
    {
        Program.Check("Primitive.NumberEqual",
            ValueUtilities.ValuesEqual(Value.Number(3), Value.Number(3)),
            "equal numbers must be true");
        Program.Check("Primitive.NumberNotEqual",
            !ValueUtilities.ValuesEqual(Value.Number(3), Value.Number(4)),
            "different numbers must be false");
        Program.Check("Primitive.StringEqual",
            ValueUtilities.ValuesEqual(Value.String(new HeapString("abc")), Value.String(new HeapString("abc"))),
            "equal strings must be true");
        Program.Check("Primitive.StringNotEqual",
            !ValueUtilities.ValuesEqual(Value.String(new HeapString("abc")), Value.String(new HeapString("abd"))),
            "different strings must be false");
        Program.Check("Primitive.BooleanEqual",
            ValueUtilities.ValuesEqual(Value.Boolean(true), Value.Boolean(true)),
            "equal booleans must be true");
        Program.Check("Primitive.NullEqual",
            ValueUtilities.ValuesEqual(Value.NullValue, Value.NullValue),
            "null must equal null");
    }

    // ── R7: equality must not execute the closure body (language-level == path) ─

    private static void ClosureEqualsDoesNotExecuteBody()
    {
        var interp = NewInterpreter();

        // FOO's body divides by zero; equality compares references only, so the
        // body must never be invoked. Expect TRUE with Normal flow.
        var fooBody = new List<Stmt>
        {
            new VarDeclStmt("x", null, Loc),
            new AssignStmt(new VariableTarget("x", Loc),
                new BinaryExpr("/", Num(1), Num(0), Loc), Loc)
        };
        var fooFn = new FunctionDecl("FOO", Array.Empty<ParameterDecl>(), fooBody, Loc);

        var mainBody = new List<Stmt>
        {
            new VarDeclStmt("c", null, Loc),
            new AssignStmt(new VariableTarget("c", Loc), new IdentifierExpr("FOO", Loc), Loc),
            new ReturnStmt(new BinaryExpr("=", new IdentifierExpr("c", Loc), new IdentifierExpr("c", Loc), Loc), Loc)
        };
        var program = new global::Osb.Engine.Core.Lang.Program(
            new[] { fooFn, MainFn(mainBody) },
            Array.Empty<ClassDecl>(), Array.Empty<InterfaceDecl>(), Array.Empty<EnumDecl>(),
            Array.Empty<UsingDecl>(), Array.Empty<EventDecl>());

        var result = interp.Execute(program);

        Program.Check("Closure.EqualsDoesNotExecuteBody",
            result.Kind == ValueKind.Boolean && result.AsBoolean == true &&
            interp.CurrentFlow == ControlFlow.Normal,
            $"kind={result.Kind} flow={interp.CurrentFlow}");
    }

    private static FunctionDecl MainFn(IReadOnlyList<Stmt> body) => new("MAIN", Array.Empty<ParameterDecl>(), body, Loc);
    private static Expr Num(double v) => new NumberLiteralExpr(v, Loc);

    // ── interpreter harness (minimal, mirrors InterpreterTests) ─────────

    private static Interpreter NewInterpreter()
    {
        using var provider = new BumpMemoryProvider();
        var heap = new EngineHeap(provider);
        return new Interpreter(heap, new StubHostApi(), new MathProvider(), new StubTimeProvider(),
            new StubRandomProvider(), new StubUtf8Provider());
    }

    private sealed class BumpMemoryProvider : IMemoryProvider
    {
        private readonly byte[] _region = new byte[1024 * 1024];
        private int _bumpPtr;
        private readonly GCHandle _handle;

        public BumpMemoryProvider() => _handle = GCHandle.Alloc(_region, GCHandleType.Pinned);

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
