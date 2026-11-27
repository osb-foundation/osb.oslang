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

/// <summary>
/// Regression coverage for freestanding collection iteration in the interpreter.
///
/// The OSLANG parser always backs AST collections (Program.Functions,
/// FunctionDecl.Body, PrintStmt.Expressions, etc.) with NodeList&lt;T&gt;, exposed
/// through IReadOnlyList&lt;T&gt;. In the freestanding NativeAOT runtime, interface
/// dispatch on these collections is broken (IReadOnlyList&lt;T&gt;.Count returns 0
/// and GetEnumerator yields nothing), so a plain foreach / interface-Count loop
/// silently skips every statement. The interpreter must iterate the concrete
/// NodeList&lt;T&gt; API instead.
///
/// These tests build the AST the same way the parser does (NodeList-backed) and
/// assert that every statement is enumerated and executed in order, observed via
/// PRINT output. They fail against the previous interface-only enumeration and
/// pass with the concrete NodeList iteration fix.
/// </summary>
internal static class InterpreterCollectionIterationTests
{
    private static readonly SourceLocation Loc = SourceLocation.Unknown;

    internal static void RunAll()
    {
        Console.WriteLine("=== Interpreter Collection Iteration Tests (freestanding NodeList) ===");
        NodeListBackedBodyExecutesAllStatementsInOrder();
        NodeListBackedPrintExecutes();
        NodeListBackedProgramRegistersMain();
        NodeListBackedIfBranchesExecute();
        NodeListBackedLoopBodyExecutes();
    }

    // ── helpers ──────────────────────────────────────────────────────

    private static Interpreter NewInterpreter(out StubHostApi host)
    {
        using var provider = new BumpMemoryProvider();
        var heap = new EngineHeap(provider);
        host = new StubHostApi();
        return new Interpreter(heap, host, new MathProvider(), new StubTimeProvider(),
            new StubRandomProvider(), new StubUtf8Provider());
    }

    private static FunctionDecl MainFn(NodeList<Stmt> body) =>
        new("MAIN", new NodeList<ParameterDecl>(), body, Loc);

    private static global::Osb.Engine.Core.Lang.Program MakeProgram(NodeList<FunctionDecl> functions) =>
        new(functions, new NodeList<ClassDecl>(), new NodeList<InterfaceDecl>(),
            new NodeList<EnumDecl>(), new NodeList<UsingDecl>(), new NodeList<EventDecl>());

    private static Expr Num(double v) => new NumberLiteralExpr(v, Loc);
    private static Expr Str(string v) => new StringLiteralExpr(v, Loc);
    private static Expr Id(string name) => new IdentifierExpr(name, Loc);

    private static PrintStmt Print(params Expr[] exprs)
    {
        var list = new NodeList<Expr>();
        foreach (var e in exprs) list.Add(e);
        return new PrintStmt(list, Loc);
    }

    // ── 1. A NodeList<Stmt>-backed body is fully enumerated in order ─────

    private static void NodeListBackedBodyExecutesAllStatementsInOrder()
    {
        var interp = NewInterpreter(out var host);
        var body = new NodeList<Stmt>();
        // Print four markers in sequence; all four must appear in order.
        body.Add(Print(Str("A")));
        body.Add(Print(Str("B")));
        body.Add(Print(Str("C")));
        body.Add(Print(Str("D")));

        var functions = new NodeList<FunctionDecl>();
        functions.Add(MainFn(body));
        interp.Execute(MakeProgram(functions));

        var outp = host.Output;
        bool ordered = outp.IndexOf("A", StringComparison.Ordinal) >= 0 &&
                       outp.IndexOf("B", StringComparison.Ordinal) > outp.IndexOf("A", StringComparison.Ordinal) &&
                       outp.IndexOf("C", StringComparison.Ordinal) > outp.IndexOf("B", StringComparison.Ordinal) &&
                       outp.IndexOf("D", StringComparison.Ordinal) > outp.IndexOf("C", StringComparison.Ordinal);
        Program.Check("Interp.NodeListBodyExecutesAllInOrder", ordered, $"output='{outp}'");
    }

    // ── 2. A PrintStmt in a NodeList-backed body actually executes ───────

    private static void NodeListBackedPrintExecutes()
    {
        var interp = NewInterpreter(out var host);
        var body = new NodeList<Stmt>();
        body.Add(Print(Str("Hello, World!")));

        var functions = new NodeList<FunctionDecl>();
        functions.Add(MainFn(body));
        interp.Execute(MakeProgram(functions));

        Program.Check("Interp.NodeListPrintExecutes",
            host.Output.Contains("Hello, World!"),
            $"output='{host.Output}'");
    }

    // ── 3. MAIN registration + lookup works with NodeList-backed program ─

    private static void NodeListBackedProgramRegistersMain()
    {
        var interp = NewInterpreter(out _);
        var body = new NodeList<Stmt>();
        body.Add(new ReturnStmt(Num(7), Loc));
        var functions = new NodeList<FunctionDecl>();
        functions.Add(MainFn(body));
        var program = MakeProgram(functions);

        interp.Execute(program);

        Program.Check("Interp.NodeListProgramRegistersMain",
            interp.CurrentFlow != ControlFlow.Exception,
            $"flow={interp.CurrentFlow} msg={interp.CurrentExceptionMessage}");
    }

    // ── 4. If/Else branches backed by NodeList execute ──────────────────

    private static void NodeListBackedIfBranchesExecute()
    {
        var interp = NewInterpreter(out var host);
        var thenBody = new NodeList<Stmt>();
        thenBody.Add(Print(Str("THEN")));
        var elseBody = new NodeList<Stmt>();
        elseBody.Add(Print(Str("ELSE")));

        var body = new NodeList<Stmt>();
        body.Add(new IfStmt(Num(1), thenBody, new NodeList<ElifClause>(), elseBody, Loc));

        var functions = new NodeList<FunctionDecl>();
        functions.Add(MainFn(body));
        interp.Execute(MakeProgram(functions));

        Program.Check("Interp.NodeListIfBranchesExecute",
            host.Output.Contains("THEN") && !host.Output.Contains("ELSE"),
            $"output='{host.Output}'");
    }

    // ── 5. A NodeList-backed loop body executes each iteration ───────────

    private static void NodeListBackedLoopBodyExecutes()
    {
        var interp = NewInterpreter(out var host);
        var loopBody = new NodeList<Stmt>();
        // Print the loop variable each iteration; expect 1,2,3.
        loopBody.Add(Print(Id("i")));

        var body = new NodeList<Stmt>();
        body.Add(new ForStmt("i", Num(1), Num(3), null, loopBody, Loc));

        var functions = new NodeList<FunctionDecl>();
        functions.Add(MainFn(body));
        interp.Execute(MakeProgram(functions));

        var outp = host.Output;
        bool ok = outp.Contains("1") && outp.Contains("2") && outp.Contains("3");
        Program.Check("Interp.NodeListLoopBodyExecutes", ok, $"output='{outp}'");
    }

    // ── stubs (mirror InterpreterTests) ─────────────────────────────────

    private sealed class BumpMemoryProvider : IMemoryProvider
    {
        private readonly byte[] _region = new byte[1024 * 1024];
        private int _bumpPtr;
        private readonly GCHandle _handle;

        public BumpMemoryProvider() { _handle = GCHandle.Alloc(_region, GCHandleType.Pinned); }
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
        public void Dispose() { if (_handle.IsAllocated) _handle.Free(); }
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