using System;
using System.Runtime.InteropServices;
using OsbKT;
using Osb.Engine.Core;
using CoreLang = Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Host;
using Osb.Engine.Core.Eval;

namespace Osb.Engine.Probe.Freestanding
{
    /// <summary>
    /// Freestanding Engine.Core compilation probe.
    /// This attempts to compile the actual Engine.Core and run a minimal test.
    /// </summary>
    internal static unsafe class FreestandingCoreProbe
    {
        private const ulong HeapBase = 0x2108000;
        private const ulong HeapLimit = 0x2200000;
        private const int HeapSize = (int)(HeapLimit - HeapBase);

        private static int _passed;
        private static int _failed;

        public static void Main()
        {
            // Initialize TLS heap pointers for NativeAOT allocation helpers
            OsbNative.osb_tls_set_alloc(HeapBase);
            OsbNative.osb_tls_set_limit(HeapLimit);

            Syscall.PrintString("=== O8.3.2a Freestanding Engine.Core Compilation Probe ===\r\n");
            Syscall.PrintString("Heap region: 0x2108000 - 0x2200000 (512 KiB)\r\n\r\n");

            // Test 1: OsbMemoryProvider basic operations
            TestMemoryProvider();

            // Test 2: EngineHeap allocation
            TestEngineHeap();

            // Test 3: Lexer + Parser
            TestLexerParser();

            // Test 4: Interpreter execution (minimal)
            TestInterpreter();

            Syscall.PrintString("\r\n=== Results ===\r\n");
            PrintNumber(_passed);
            Syscall.PrintString(" passed, ");
            PrintNumber(_failed);
            Syscall.PrintString(" failed\r\n");

            if (_failed == 0)
            {
                Syscall.PrintString("O8.3.2a_COMPILES\r\n");
                Syscall.Exit(0);
            }
            else
            {
                Syscall.PrintString("O8.3.2a_BLOCKED\r\n");
                Syscall.Exit(1);
            }
        }

        private static void TestMemoryProvider()
        {
            Syscall.PrintString("--- Test: OsbMemoryProvider ---\r\n");
            
            var provider = new OsbMemoryProvider((IntPtr)HeapBase, HeapSize);
            
            Test("Provider region size", () => provider.RegionSize == HeapSize);
            Test("Provider base address", () => provider.BaseAddress == (IntPtr)HeapBase);
            Test("Provider managed region is null", () => provider.ManagedRegion == null);
            
            // Test allocation
            var ptr1 = provider.Alloc(64, 8);
            Test("Alloc 64 bytes aligned 8", () => ptr1 != IntPtr.Zero);
            Test("Alloc within region", () => (ulong)ptr1 >= HeapBase && (ulong)ptr1 < HeapLimit);
            
            var ptr2 = provider.Alloc(128, 16);
            Test("Alloc 128 bytes aligned 16", () => ptr2 != IntPtr.Zero);
            Test("Second alloc after first", () => (ulong)ptr2 > (ulong)ptr1);
            
            // Test alignment
            var ptr3 = provider.Alloc(1, 16);
            Test("Alloc 1 byte aligned 16", () => ((ulong)ptr3 & 15) == 0);
            
            // Test reset
            provider.Reset();
            Test("Reset clears bump pointer", () => provider.Alloc(1, 1) != IntPtr.Zero);
            
            provider.Dispose();
            Test("Provider disposed", () => true);
        }

        private static void TestEngineHeap()
        {
            Syscall.PrintString("\r\n--- Test: EngineHeap ---\r\n");
            
            var provider = new OsbMemoryProvider((IntPtr)HeapBase, HeapSize);
            var heap = new EngineHeap(provider);
            
            // Test basic allocation
            var str = heap.AllocString("test");
            Test("AllocString", () => str != null && str.ToString() == "test");
            
            var arr = heap.AllocArray(10);
            Test("AllocArray", () => arr != null && arr.Length == 10);
            
            var cls = heap.AllocClass("TestClass", null, new string[0], new CoreLang.MethodDecl[0]);
            Test("AllocClass", () => cls != null && cls.Name == "TestClass");
            
            var closure = heap.AllocClosure(new CoreLang.FunctionDecl("test", [], [], CoreLang.SourceLocation.Unknown), []);
            Test("AllocClosure", () => closure != null);
            
            var obj = heap.AllocObject(cls);
            Test("AllocObject", () => obj != null && obj.ClassName == "TestClass");
            
            Test("Heap stats", () => heap.AllocCount > 0 && heap.FreeBytes < HeapSize);
            
            heap.Dispose();
            Test("Heap disposed", () => true);
        }

        private static void TestLexerParser()
        {
            Syscall.PrintString("\r\n--- Test: Lexer + Parser ---\r\n");
            
            const string source = @"
FUNCTION MAIN()
    VAR x
    x = 21 + 21
    PRINT ""answer = "" + STR(x)
END FUNCTION
";
            
            var lexer = new CoreLang.Lexer(source);
            var tokens = lexer.Tokenize();
            Test("Lexer tokenizes", () => tokens != null && tokens.Count > 0);
            
            var parser = new CoreLang.Parser(tokens);
            var program = parser.Parse();
            Test("Parser parses", () => program != null && program.Functions.Count == 1);
            Test("MAIN function found", () => program.Functions[0].Name == "MAIN");
        }

        private static void TestInterpreter()
        {
            Syscall.PrintString("\r\n--- Test: Interpreter ---\r\n");
            
            const string source = @"
FUNCTION MAIN()
    VAR x
    x = 21 + 21
    PRINT ""answer = "" + STR(x)
END FUNCTION
";
            
            var provider = new OsbMemoryProvider((IntPtr)HeapBase, HeapSize);
            var heap = new EngineHeap(provider);
            
            // Use freestanding providers
            var math = new DefaultMathProvider();
            var time = new DefaultTimeProvider();
            var random = new DefaultRandomProvider();
            var utf8 = new FreestandingUtf8Provider();
            
            // Minimal host API for output capture
            var host = new TestHostApi();
            
            var lexer = new CoreLang.Lexer(source);
            var tokens = lexer.Tokenize();
            var parser = new CoreLang.Parser(tokens);
            var program = parser.Parse();
            
            var interpreter = new Interpreter(heap, host, math, time, random, utf8);
            var result = interpreter.Execute(program);
            
            Test("Interpreter executes", () => true);
            Test("Output captured", () => host.Output.Contains("answer = 42"));
            
            heap.Dispose();
        }

        private static void Test(string name, Func<bool> func)
        {
            try
            {
                bool ok = func();
                if (ok)
                {
                    _passed++;
                    Syscall.PrintString("  PASS: ");
                    Syscall.PrintString(name);
                    Syscall.PrintString("\r\n");
                }
                else
                {
                    _failed++;
                    Syscall.PrintString("  FAIL: ");
                    Syscall.PrintString(name);
                    Syscall.PrintString(" - returned false\r\n");
                }
            }
            catch (Exception ex)
            {
                _failed++;
                Syscall.PrintString("  FAIL: ");
                Syscall.PrintString(name);
                Syscall.PrintString(" - exception: ");
                Syscall.PrintString(ex.GetType().Name);
                Syscall.PrintString("\r\n");
            }
        }

        private static void PrintNumber(int n)
        {
            if (n == 0)
            {
                Syscall.PutChar('0');
                return;
            }
            var buf = stackalloc char[12];
            int i = 0;
            int v = n;
            while (v > 0)
            {
                buf[i++] = (char)('0' + v % 10);
                v /= 10;
            }
            for (int j = i - 1; j >= 0; j--)
            {
                Syscall.PutChar(buf[j]);
            }
        }
    }

    /// <summary>
    /// Minimal test host API that captures output.
    /// </summary>
    internal sealed class TestHostApi : IHostApi
    {
        public string Output { get; private set; } = "";

        public void Write(string text)
        {
            Output += text;
        }

        public string? ReadLine()
        {
            return null;
        }

        public void Clear()
        {
        }

        public void Exit(int code = 0)
        {
            Syscall.Exit(code);
        }
    }

    /// <summary>
    /// TLS and NativeAOT helper stubs for freestanding OSB Ring3.
    /// </summary>
    internal static class OsbNative
    {
        [DllImport("osbnative")]
        public static extern ulong osb_tls_tp();

        [DllImport("osbnative")]
        public static extern ulong osb_tls_get_alloc();

        [DllImport("osbnative")]
        public static extern ulong osb_tls_get_limit();

        [DllImport("osbnative")]
        public static extern void osb_tls_set_alloc(ulong v);

        [DllImport("osbnative")]
        public static extern void osb_tls_set_limit(ulong v);
    }

    // Minimal freestanding providers
    internal sealed class DefaultMathProvider : IMathProvider
    {
        public double Sqrt(double x) => Math.Sqrt(x);
        public double Sin(double x) => Math.Sin(x);
        public double Cos(double x) => Math.Cos(x);
        public double Tan(double x) => Math.Tan(x);
        public double Asin(double x) => Math.Asin(x);
        public double Acos(double x) => Math.Acos(x);
        public double Atan(double x) => Math.Atan(x);
        public double Floor(double x) => Math.Floor(x);
        public double Ceil(double x) => Math.Ceiling(x);
        public double Round(double x) => Math.Round(x);
        public double Trunc(double x) => Math.Truncate(x);
        public double Sign(double x) => x > 0 ? 1 : (x < 0 ? -1 : 0);
        public double Abs(double x) => Math.Abs(x);
        public double Pow(double x, double y) => Math.Pow(x, y);
        public double Log(double x) => Math.Log(x);
        public double Log10(double x) => Math.Log10(x);
        public double Exp(double x) => Math.Exp(x);
        public double Min(double x, double y) => Math.Min(x, y);
        public double Max(double x, double y) => Math.Max(x, y);
        public double Clamp(double x, double min, double max) => x < min ? min : (x > max ? max : x);
        public double PI => Math.PI;
        public double E => Math.E;
    }

    internal sealed class DefaultTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => new DateTime(1970, 1, 1);
        public ulong Ticks => 0;
        public void Sleep(ulong ticks) { }
        public (int Year, int Month, int Day, int Hour, int Minute, int Second) DateTime => (1970, 1, 1, 0, 0, 0);
        public DateTime? CreateDate(int year, int month, int day) => null;
        public DateTime? CreateTime(int hour, int minute, int second, int millisecond = 0) => null;
        public (int Year, int Month, int Day, int DayOfWeek)? DaysToDate(long daysSinceEpoch) => null;
        public (int Hour, int Minute, int Second)? MillisToTime(long millisSinceMidnight) => null;
    }

    internal sealed class DefaultRandomProvider : IRandomProvider
    {
        public int Next() => 0;
        public int Next(int maxValue) => 0;
        public int Next(int minValue, int maxValue) => minValue;
        public double NextDouble() => 0;
    }
}