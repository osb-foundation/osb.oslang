using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Host;
using Osb.Engine.Core.Lang;
using Lang = Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace CollectionTests;

internal static class HostErrorSurfacingTests
{
    private static readonly SourceLocation Loc = SourceLocation.Unknown;

    internal static void RunAll()
    {
        Console.WriteLine("=== Host-Level Error Surfacing Tests (R9) ===");
        HostSurfacesRuntimeErrorAfterExecute();
        HostSurfacesNoMainError();
        HostSurfacesUnknownFunctionError();
        HostNormalExecutionFlowIsNormal();
        HostBoundaryIntegrationTest();
    }

    private static Interpreter NewInterpreter()
    {
        using var provider = new BumpMemoryProvider();
        var heap = new EngineHeap(provider);
        return new Interpreter(heap, new StubHostApi(), new MathProvider(), new StubTimeProvider(),
            new StubRandomProvider(), new StubUtf8Provider());
    }

    private static Interpreter NewInterpreterWithHostedProviders()
    {
        var provider = new HostedMemoryProvider();
        var heap = new EngineHeap(provider);
        var host = new HostedHostApi();
        var math = new HostedMathProvider();
        var time = new HostedTimeProvider();
        var random = new HostedRandomProvider();
        var utf8 = new HostedUtf8Provider();
        return new Interpreter(heap, host, math, time, random, utf8);
    }

    private static FunctionDecl MainFn(IReadOnlyList<Stmt> body) => new("MAIN", Array.Empty<ParameterDecl>(), body, Loc);

    private static global::Osb.Engine.Core.Lang.Program MakeProgram(params Stmt[] body) => new global::Osb.Engine.Core.Lang.Program(
        new[] { MainFn(new List<Stmt>(body)) },
        Array.Empty<Lang.ClassDecl>(), Array.Empty<Lang.InterfaceDecl>(), Array.Empty<Lang.EnumDecl>(),
        Array.Empty<Lang.UsingDecl>(), Array.Empty<Lang.EventDecl>());

    private static Expr Num(double v) => new NumberLiteralExpr(v, Loc);
    private static Expr Str(string v) => new StringLiteralExpr(v, Loc);
    private static Expr Id(string name) => new IdentifierExpr(name, Loc);
    private static Expr Bin(string op, Expr l, Expr r) => new BinaryExpr(op, l, r, Loc);
    private static Expr Call(string name, params Expr[] args) => new Lang.CallExpr(name, args, Loc);

    private static Stmt Assign(string name, Expr value) =>
        new Lang.AssignStmt(new Lang.VariableTarget(name, Loc), value, Loc);
    private static Stmt Ret(Expr? value) => new Lang.ReturnStmt(value, Loc);
    private static Stmt ExprStmt(Expr e) => new Lang.ExpressionStmt(e, Loc);
    private static Stmt Var(string name) => new Lang.VarDeclStmt(name, null, Loc);

    // ── R9: Host-level error surfacing tests ──────────────────────────────

    private static void HostSurfacesRuntimeErrorAfterExecute()
    {
        var interp = NewInterpreter();
        var body = new List<Stmt>
        {
            Var("x"),
            Assign("x", Bin("/", Num(1), Num(0)))
        };
        var program = MakeProgram(body.ToArray());

        interp.Execute(program);

        // After Execute returns, CurrentFlow must be Exception for runtime errors
        Program.Check("HostErrorSurfacing.RuntimeErrorAfterExecute.Flow",
            interp.CurrentFlow == ControlFlow.Exception,
            $"flow={interp.CurrentFlow}");

        // Error message must be preserved and accessible
        Program.Check("HostErrorSurfacing.RuntimeErrorAfterExecute.Message",
            interp.CurrentExceptionMessage == "Division by zero.",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void HostSurfacesNoMainError()
    {
        var interp = NewInterpreter();
        var program = new global::Osb.Engine.Core.Lang.Program(
            Array.Empty<FunctionDecl>(), Array.Empty<ClassDecl>(), Array.Empty<InterfaceDecl>(),
            Array.Empty<EnumDecl>(), Array.Empty<UsingDecl>(), Array.Empty<EventDecl>());

        interp.Execute(program);

        Program.Check("HostErrorSurfacing.NoMainError.Flow",
            interp.CurrentFlow == ControlFlow.Exception,
            $"flow={interp.CurrentFlow}");

        Program.Check("HostErrorSurfacing.NoMainError.Message",
            interp.CurrentExceptionMessage == "No MAIN function found",
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void HostSurfacesUnknownFunctionError()
    {
        var interp = NewInterpreter();
        var body = new List<Stmt> { ExprStmt(Call("nonexistent")) };
        var program = MakeProgram(body.ToArray());

        interp.Execute(program);

        Program.Check("HostErrorSurfacing.UnknownFunction.Flow",
            interp.CurrentFlow == ControlFlow.Exception);
        Program.Check("HostErrorSurfacing.UnknownFunction.Message",
            interp.CurrentExceptionMessage != null && interp.CurrentExceptionMessage.Contains("Unknown function"),
            $"msg={interp.CurrentExceptionMessage}");
    }

    private static void HostNormalExecutionFlowIsNormal()
    {
        var interp = NewInterpreter();
        var body = new List<Stmt>
        {
            Var("x"),
            Assign("x", Num(42)),
            Ret(Id("x"))
        };
        var program = MakeProgram(body.ToArray());

        interp.Execute(program);

        Program.Check("HostErrorSurfacing.NormalExecution.Flow",
            interp.CurrentFlow == ControlFlow.Normal,
            $"flow={interp.CurrentFlow}");
        Program.Check("HostErrorSurfacing.NormalExecution.NoException",
            interp.CurrentExceptionMessage == null || interp.CurrentExceptionMessage == "",
            $"msg={interp.CurrentExceptionMessage}");
    }

    // ── M7.1: Host-boundary integration test ───────────────────────────────
    //
    // This test exercises the full OSLANG pipeline (Lexer -> Parser -> Interpreter)
    // with hosted providers that mirror the freestanding providers used by OsbMain.
    // It verifies the error message format that OsbMain.Main() produces when a
    // runtime error occurs.
    //
    // LIMITATION: A true end-to-end test would require running the native OSLANG.EXE
    // (freestanding NativeAOT) inside the OSB kernel (QEMU). The freestanding
    // executable depends on osbkt syscalls (OsbKT.Syscall) and the kernel's TLS
    // memory layout, which are not available in the host .NET test environment.
    // This test provides the closest valid integration boundary by exercising
    // the same components and error-handling logic as OsbMain.

    private static void HostBoundaryIntegrationTest()
    {
        // Use hosted providers that mirror OsbMain's freestanding providers
        var interp = NewInterpreterWithHostedProviders();

        // OSLANG source that triggers division by zero at runtime
        const string source = @"
FUNCTION MAIN()
    VAR x
    x = 1 / 0
END FUNCTION
";

        // Full pipeline: Lexer -> Parser -> Interpreter
        var lexer = new Lexer(source, new HostedUtf8Provider());
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        interp.Execute(program);

        // Verify the interpreter caught the runtime error
        Program.Check("HostBoundaryIntegration.DivisionByZero.Flow",
            interp.CurrentFlow == ControlFlow.Exception,
            $"flow={interp.CurrentFlow}");

        // Verify the error message matches what OsbMain would surface
        // OsbMain formats: "Runtime error: " + CurrentExceptionMessage + "\r\n"
        string expectedMessage = "Division by zero.";
        Program.Check("HostBoundaryIntegration.DivisionByZero.Message",
            interp.CurrentExceptionMessage == expectedMessage,
            $"msg={interp.CurrentExceptionMessage}, expected={expectedMessage}");

        // Verify the formatted host error message (what OsbMain prints)
        string hostErrorMessage = "Runtime error: " + interp.CurrentExceptionMessage;
        Program.Check("HostBoundaryIntegration.DivisionByZero.HostFormat",
            hostErrorMessage == "Runtime error: Division by zero.",
            $"hostMsg={hostErrorMessage}");

        // Verify successful execution still works
        const string successSource = @"
FUNCTION MAIN()
    VAR x
    x = 42
    PRINT x
END FUNCTION
";
        var lexer2 = new Lexer(successSource, new HostedUtf8Provider());
        var tokens2 = lexer2.Tokenize();
        var parser2 = new Parser(tokens2);
        var program2 = parser2.Parse();

        var interp2 = NewInterpreterWithHostedProviders();
        interp2.Execute(program2);

        Program.Check("HostBoundaryIntegration.Success.Flow",
            interp2.CurrentFlow == ControlFlow.Normal,
            $"flow={interp2.CurrentFlow}");
        Program.Check("HostBoundaryIntegration.Success.NoException",
            interp2.CurrentExceptionMessage == null || interp2.CurrentExceptionMessage == "",
            $"msg={interp2.CurrentExceptionMessage}");
    }

    // ── stubs ────────────────────────────────────────────────────────────

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

    // ── Hosted providers (mirror freestanding providers used by OsbMain) ──

    private sealed class HostedMemoryProvider : IMemoryProvider
    {
        private readonly byte[] _region = new byte[2 * 1024 * 1024];
        private readonly GCHandle _handle;
        private int _bumpPtr;

        public HostedMemoryProvider()
        {
            _handle = GCHandle.Alloc(_region, GCHandleType.Pinned);
        }

        public IntPtr BaseAddress => _handle.AddrOfPinnedObject();
        public int RegionSize => _region.Length;
        public byte[]? ManagedRegion => _region;

        public IntPtr Alloc(int sizeBytes, int alignment = 8)
        {
            if (sizeBytes <= 0) return IntPtr.Zero;
            int alignedPtr = (_bumpPtr + alignment - 1) & ~(alignment - 1);
            int newPtr = alignedPtr + sizeBytes;
            if (newPtr > _region.Length) return IntPtr.Zero;
            IntPtr result = BaseAddress + alignedPtr;
            _bumpPtr = newPtr;
            return result;
        }

        public void Free(IntPtr ptr, int sizeBytes) { }
        public void Reset() { _bumpPtr = 0; }
        public void Collect() { }
        public void Dispose() { if (_handle.IsAllocated) _handle.Free(); }
    }

    private sealed class HostedHostApi : IHostApi
    {
        private readonly StringWriter _output = new();

        public void Write(string text) { if (text != null) _output.Write(text); }
        public string? ReadLine() => null;
        public void Clear() { }
        public void Exit(int code = 0) { }
        public string GetOutput() => _output.ToString();
    }

    private sealed class HostedMathProvider : IMathProvider
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

    private sealed class HostedTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public ulong Ticks => (ulong)DateTime.UtcNow.Ticks;
        public void Sleep(ulong ticks) { }
        public DateTime? CreateDate(int year, int month, int day)
        {
            try { return new DateTime(year, month, day); } catch { return null; }
        }
        public DateTime? CreateTime(int hour, int minute, int second, int millisecond = 0)
        {
            try { return new DateTime(1, 1, 1, hour, minute, second, millisecond); } catch { return null; }
        }
        public (int Year, int Month, int Day, int DayOfWeek)? DaysToDate(long daysSinceEpoch)
        {
            try { var dt = DateTimeOffset.FromUnixTimeSeconds(daysSinceEpoch * 86400).DateTime; return (dt.Year, dt.Month, dt.Day, (int)dt.DayOfWeek); } catch { return null; }
        }
        public (int Hour, int Minute, int Second)? MillisToTime(long millisSinceMidnight)
        {
            if (millisSinceMidnight < 0 || millisSinceMidnight >= 86400000) return null;
            int hour = (int)(millisSinceMidnight / 3600000);
            int minute = (int)((millisSinceMidnight % 3600000) / 60000);
            int second = (int)((millisSinceMidnight % 60000) / 1000);
            return (hour, minute, second);
        }
    }

    private sealed class HostedRandomProvider : IRandomProvider
    {
        private readonly Random _random = new();
        public double NextDouble() => _random.NextDouble();
        public int Next(int maxValue) => _random.Next(maxValue);
        public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);
    }

    private sealed class HostedUtf8Provider : IUtf8Provider
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        public byte[] GetBytes(string s) => Utf8.GetBytes(s ?? string.Empty);
        public string GetString(byte[] bytes) => Utf8.GetString(bytes ?? []);
        public string GetString(ReadOnlySpan<byte> bytes) => Utf8.GetString(bytes);
        public bool IsValidUtf8(ReadOnlySpan<byte> bytes)
        {
            try { Utf8.GetString(bytes); return true; } catch { return false; }
        }
    }
}