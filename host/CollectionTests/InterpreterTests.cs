using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Host;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace CollectionTests;

internal static class InterpreterTests
{
    private static readonly SourceLocation Loc = SourceLocation.Unknown;

    internal static void RunAll()
    {
        Console.WriteLine("=== Interpreter / Error Propagation Tests ===");
        ExceptionPropagatesAcrossFunctionBoundary();
        TryCatchConsumesException();
        NoMainSurfacesException();
        NormalReturnNormalizesFlow();
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static Interpreter NewInterpreter()
    {
        using var provider = new BumpMemoryProvider();
        var heap = new EngineHeap(provider);
        return new Interpreter(heap, new StubHostApi(), new MathProvider(), new StubTimeProvider(),
            new StubRandomProvider(), new StubUtf8Provider());
    }

    private static FunctionDecl MainFn(IReadOnlyList<Stmt> body) => new("MAIN", Array.Empty<ParameterDecl>(), body, Loc);

    private static global::Osb.Engine.Core.Lang.Program MakeProgram(params Stmt[] body) => new global::Osb.Engine.Core.Lang.Program(
        new[] { MainFn(new List<Stmt>(body)) },
        Array.Empty<ClassDecl>(), Array.Empty<InterfaceDecl>(), Array.Empty<EnumDecl>(),
        Array.Empty<UsingDecl>(), Array.Empty<EventDecl>());

    private static Expr Num(double v) => new NumberLiteralExpr(v, Loc);

    private static void Run(Interpreter interpreter, global::Osb.Engine.Core.Lang.Program program)
    {
        foreach (var fn in program.Functions) interpreter.RegisterFunction(fn);
        interpreter.Execute(program);
    }

    // ── R1: exceptions thrown inside a called function must propagate ─────

    private static void ExceptionPropagatesAcrossFunctionBoundary()
    {
        var interp = NewInterpreter();
        var body = new List<Stmt>
        {
            new VarDeclStmt("x", null, Loc),
            new AssignStmt(new VariableTarget("x", Loc),
                new BinaryExpr("/", Num(1), Num(0), Loc), Loc)
        };
        Run(interp, MakeProgram(body.ToArray()));

        Program.Check("Interp.ExceptionPropagatesAcrossFunctionBoundary",
            interp.CurrentFlow == ControlFlow.Exception &&
            interp.CurrentExceptionMessage == "Division by zero.",
            $"flow={interp.CurrentFlow} msg={interp.CurrentExceptionMessage}");
    }

    // ── R1 regression: TRY/CATCH must still consume an exception ──────────

    private static void TryCatchConsumesException()
    {
        var interp = NewInterpreter();
        var tryBody = new List<Stmt>
        {
            new VarDeclStmt("x", null, Loc),
            new AssignStmt(new VariableTarget("x", Loc),
                new BinaryExpr("/", Num(1), Num(0), Loc), Loc)
        };
        var body = new List<Stmt>
        {
            new TryCatchStmt(tryBody, "e", Array.Empty<Stmt>(), Loc)
        };
        Run(interp, MakeProgram(body.ToArray()));

        Program.Check("Interp.TryCatchConsumesException",
            interp.CurrentFlow == ControlFlow.Normal,
            $"flow={interp.CurrentFlow}");
    }

    // ── R1: a program with no MAIN must surface an exception, not NullValue ────

    private static void NoMainSurfacesException()
    {
        var interp = NewInterpreter();
        var program = new global::Osb.Engine.Core.Lang.Program(
            Array.Empty<FunctionDecl>(), Array.Empty<ClassDecl>(), Array.Empty<InterfaceDecl>(),
            Array.Empty<EnumDecl>(), Array.Empty<UsingDecl>(), Array.Empty<EventDecl>());
        Run(interp, program);

        Program.Check("Interp.NoMainSurfacesException",
            interp.CurrentFlow == ControlFlow.Exception &&
            interp.CurrentExceptionMessage == "No MAIN function found",
            $"flow={interp.CurrentFlow} msg={interp.CurrentExceptionMessage}");
    }

    // ── R1 regression: a normal RETURN must leave flow Normal for the caller ─

    private static void NormalReturnNormalizesFlow()
    {
        var interp = NewInterpreter();
        var body = new List<Stmt>
        {
            new ReturnStmt(Num(42), Loc)
        };
        Run(interp, MakeProgram(body.ToArray()));

        Program.Check("Interp.NormalReturnNormalizesFlow",
            interp.CurrentFlow == ControlFlow.Normal,
            $"flow={interp.CurrentFlow}");
    }

    // ── stubs ──────────────────────────────────────────────────────────

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
        private readonly StringBuilder _output = new();
        public void Write(string text) { if (text != null) _output.Append(text); }
        public string? ReadLine() => null;
        public void Clear() { }
        public void Exit(int code = 0) { }
        public string Output => _output.ToString();
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
