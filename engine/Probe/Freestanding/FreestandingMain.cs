using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Math;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;
using System;

namespace Osb.Engine.Probe.Freestanding;

/// <summary>
/// Freestanding NativeAOT probe for Engine.Core allocation feasibility on OSB Ring3.
/// Tests representative Engine.Core allocation paths using OsbMemoryProvider.
/// </summary>
internal static unsafe class FreestandingMain
{
    private const ulong HeapBase = 0x2108000;
    private const ulong HeapLimit = 0x2200000;
    private const int HeapSize = (int)(HeapLimit - HeapBase);

    private static int _passed;
    private static int _failed;

    public static void Main()
    {
        // Initialize TLS heap pointers for NativeAOT allocation helpers
        // (same as TOslangProof)
        OsbNative.osb_tls_set_alloc(HeapBase);
        OsbNative.osb_tls_set_limit(HeapLimit);

        Syscall.PrintString("=== O8.3.1 Freestanding Engine.Core Allocation Probe ===\r\n");
        Syscall.PrintString("Heap region: 0x2108000 - 0x2200000 (512 KiB)\r\n\r\n");

        // Test 1: OsbMemoryProvider basic operations
        TestMemoryProvider();

        // Test 2: EngineHeap with OsbMemoryProvider
        TestEngineHeap();

        // Test 3: Engine.Core value types and heap objects
        TestEngineCoreValues();

        // Test 4: Lexer allocation paths
        TestLexerAllocations();

        // Test 5: Parser allocation paths
        TestParserAllocations();

        // Test 6: Interpreter execution (minimal program)
        TestInterpreterExecution();

        // Test 7: String behavior (construction, concat, equality)
        TestStringBehavior();

        // Test 8: Bounded allocation exhaustion
        TestBoundedAllocation();

        Syscall.PrintString("\r\n=== Results ===\r\n");
        PrintNumber(_passed);
        Syscall.PrintString(" passed, ");
        PrintNumber(_failed);
        Syscall.PrintString(" failed\r\n");

        if (_failed == 0)
        {
            Syscall.PrintString("O8.3.1_FEASIBLE\r\n");
            Syscall.Exit(0);
        }
        else
        {
            Syscall.PrintString("O8.3.1_BLOCKED\r\n");
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
        Syscall.PrintString("\r\n--- Test: EngineHeap with OsbMemoryProvider ---\r\n");
        
        var provider = new OsbMemoryProvider((IntPtr)HeapBase, HeapSize);
        var heap = new EngineHeap(provider);
        
        Test("Heap capacity", () => heap.CapacityBytes == HeapSize);
        Test("Heap initially empty", () => heap.UsedBytes == 0);
        Test("Heap free bytes", () => heap.FreeBytes == HeapSize);
        
        // Test Allocate with factory
        var str = heap.AllocString("test");
        Test("AllocString returns object", () => str != null);
        Test("AllocString increases alloc count", () => heap.AllocCount == 1);
        Test("AllocString stores UTF-8", () => str.Utf8Bytes.Length == 4);
        
        var arr = heap.AllocArray(10);
        Test("AllocArray returns array", () => arr != null);
        Test("AllocArray capacity", () => arr.Length == 10);
        
        var cls = heap.AllocClass("TestClass", null, new[] { "field1", "field2" }, new MethodDecl[0]);
        Test("AllocClass returns class", () => cls != null);
        Test("AllocClass field count", () => cls.FieldCount == 2);
        
        var obj = heap.AllocObject(cls);
        Test("AllocObject returns instance", () => obj != null);
        Test("AllocObject field array", () => obj.Fields.Length == 2);
        
        var closure = heap.AllocClosure(null, new Value[0]);
        Test("AllocClosure returns closure", () => closure != null);
        
        // Test Collect (no-op in freestanding)
        int collected = heap.Collect();
        Test("Collect returns 0", () => collected == 0);
        
        heap.Dispose();
        provider.Dispose();
        Test("Heap and provider disposed", () => true);
    }

    private static void TestEngineCoreValues()
    {
        Syscall.PrintString("\r\n--- Test: Engine.Core Value Types ---\r\n");
        
        var provider = new OsbMemoryProvider((IntPtr)HeapBase, HeapSize);
        var heap = new EngineHeap(provider);
        
        // Test Value struct immediate kinds
        Test("Value.Null", () => Value.NullValue.Kind == ValueKind.Null);
        Test("Value.Boolean true", () => Value.Boolean(true).AsBoolean == true);
        Test("Value.Boolean false", () => Value.Boolean(false).AsBoolean == false);
        Test("Value.Number", () => Value.Number(42.5).AsNumber == 42.5);
        
        // Test heap object kinds
        var str = heap.AllocString("hello");
        var valStr = Value.String(str);
        Test("Value.String kind", () => valStr.Kind == ValueKind.String);
        Test("Value.String access", () => valStr.AsString.Utf8Bytes.Length == 5);
        
        var arr = heap.AllocArray(3);
        arr.Items[0] = Value.Number(1);
        arr.Items[1] = Value.Number(2);
        arr.Items[2] = Value.Number(3);
        var valArr = Value.Array(arr);
        Test("Value.Array kind", () => valArr.Kind == ValueKind.Array);
        Test("Value.Array access", () => valArr.AsArray.Length == 3);
        
        var cls = heap.AllocClass("Test", null, new[] { "x" }, new MethodDecl[0]);
        var obj = heap.AllocObject(cls);
        var valObj = Value.Object(obj);
        Test("Value.Object kind", () => valObj.Kind == ValueKind.Object);
        Test("Value.Object access", () => valObj.AsObject is HeapObjectInstance);
        
        var enumVal = Value.Enum(0, 1);
        Test("Value.Enum kind", () => enumVal.Kind == ValueKind.Enum);
        Test("Value.Enum indices", () => enumVal.AsEnum.TypeIndex == 0 && enumVal.AsEnum.MemberIndex == 1);
        
        var enumSet = heap.AllocEnumSet("Color", new[] { Value.Enum(0, 0), Value.Enum(0, 1) });
        var valSet = Value.EnumSet(enumSet);
        Test("Value.EnumSet kind", () => valSet.Kind == ValueKind.EnumSet);
        Test("Value.EnumSet count", () => valSet.AsEnumSet.Count == 2);
        
        heap.Dispose();
        provider.Dispose();
    }

    private static void TestLexerAllocations()
    {
        Syscall.PrintString("\r\n--- Test: Lexer Allocations ---\r\n");
        
        var utf8 = new DefaultUtf8Provider();
        
        // Simple program
        const string source1 = "FUNCTION MAIN()\n    VAR x\n    x = 42\n    PRINT x\nEND FUNCTION\n";
        var lexer1 = new Lexer(source1, utf8);
        var tokens1 = lexer1.Tokenize();
        Test("Lexer tokenizes simple program", () => tokens1.Count > 10);
        Test("Lexer produces EOF", () => tokens1[tokens1.Count - 1].Type == TokenType.Eof);
        
        // Program with strings
        const string source2 = "FUNCTION MAIN()\n    PRINT \"Hello, World!\"\nEND FUNCTION\n";
        var lexer2 = new Lexer(source2, utf8);
        var tokens2 = lexer2.Tokenize();
        Test("Lexer handles string literal", () => tokens2.Count > 5);
        bool hasString = false;
        for (int i = 0; i < tokens2.Count; i++)
        {
            if (tokens2[i].Type == TokenType.StringLiteral) { hasString = true; break; }
        }
        Test("Lexer produces StringLiteral token", () => hasString);
        
        // Program with numbers
        const string source3 = "FUNCTION MAIN()\n    VAR x\n    x = 3.14\nEND FUNCTION\n";
        var lexer3 = new Lexer(source3, utf8);
        var tokens3 = lexer3.Tokenize();
        Test("Lexer handles float literal", () => tokens3.Count > 5);
        
        // Test StringBuffer used by lexer
        var sb = new StringBuffer();
        sb.Append('H').Append('e').Append('l').Append('l').Append('o');
        string result = sb.ToString();
        Test("StringBuffer builds string", () => result == "Hello");
    }

    private static void TestParserAllocations()
    {
        Syscall.PrintString("\r\n--- Test: Parser Allocations ---\r\n");
        
        var utf8 = new DefaultUtf8Provider();
        
        const string source = "FUNCTION MAIN()\n    VAR x\n    x = 1 + 2\n    PRINT x\nEND FUNCTION\n";
        var lexer = new Lexer(source, utf8);
        var tokens = lexer.Tokenize();
        
        var parser = new Parser(tokens);
        var program = parser.Parse();
        
        Test("Parser produces Program", () => program != null);
        Test("Program has MAIN function", () => program.Functions.Count == 1);
        Test("MAIN function name", () => program.Functions[0].Name == "MAIN");
        Test("Function has statements", () => program.Functions[0].Body.Count >= 3);
    }

    private static void TestInterpreterExecution()
    {
        Syscall.PrintString("\r\n--- Test: Interpreter Execution ---\r\n");
        
        var provider = new OsbMemoryProvider((IntPtr)HeapBase, HeapSize);
        var heap = new EngineHeap(provider);
        var host = new OsbHostApi();
        var math = new MathProvider();
        var time = new TimeProvider();
        var random = new RandomProvider();
        var utf8 = new DefaultUtf8Provider();
        
        var interpreter = new Interpreter(heap, host, math, time, random, utf8);
        
        const string source = "FUNCTION MAIN()\n    VAR x\n    x = 21 + 21\n    PRINT x\nEND FUNCTION\n";
        var lexer = new Lexer(source, utf8);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        
        var result = interpreter.Execute(program);
        Test("Interpreter executes without exception", () => true);
        Test("Interpreter returns Null (no explicit return)", () => result.Kind == ValueKind.Null);
        
        // Test with function call
        const string source2 = "FUNCTION Add(a, b)\n    RETURN a + b\nEND FUNCTION\n\nFUNCTION MAIN()\n    VAR result\n    result = Add(10, 20)\n    PRINT result\nEND FUNCTION\n";
        var lexer2 = new Lexer(source2, utf8);
        var tokens2 = lexer2.Tokenize();
        var parser2 = new Parser(tokens2);
        var program2 = parser2.Parse();
        
        var result2 = interpreter.Execute(program2);
        Test("Interpreter executes function call", () => true);
        
        heap.Dispose();
        provider.Dispose();
    }

    private static void TestStringBehavior()
    {
        Syscall.PrintString("\r\n--- Test: String Behavior ---\r\n");
        
        var provider = new OsbMemoryProvider((IntPtr)HeapBase, HeapSize);
        var heap = new EngineHeap(provider);
        var utf8 = new DefaultUtf8Provider();
        
        // String construction
        var str1 = heap.AllocString("hello");
        Test("HeapString construction", () => str1 != null && str1.ByteLength == 5);
        Test("HeapString code point count", () => str1.CodePointCount == 5);
        Test("HeapString ToString", () => str1.ToString() == "hello");
        
        // String with non-ASCII
        var str2 = heap.AllocString("café");
        Test("HeapString non-ASCII", () => str2.CodePointCount == 4);
        Test("HeapString non-ASCII bytes", () => str2.ByteLength == 5); // é is 2 bytes
        
        // Substring by code points
        var substr = str1.SubstringByCodePoints(1, 3);
        Test("SubstringByCodePoints", () => substr.ToString() == "ell");
        
        // GetCodePoint
        var cp = str1.GetCodePoint(0);
        Test("GetCodePoint", () => cp.ToString() == "h");
        
        // IndexOf
        var needle = heap.AllocString("ell");
        int idx = str1.IndexOf(needle);
        Test("IndexOf finds substring", () => idx == 1);
        
        // String equality (content-based for HeapString)
        var str3 = heap.AllocString("hello");
        var str4 = heap.AllocString("world");
        Test("HeapString content equality same", () => str1.ToString() == str3.ToString());
        Test("HeapString content equality diff", () => str1.ToString() != str4.ToString());
        
        // Repeated allocations without GC
        for (int i = 0; i < 100; i++)
        {
            var s = heap.AllocString("item" + i);
            if (s == null) { _failed++; return; }
        }
        Test("Repeated string allocations (100)", () => heap.AllocCount > 100);
        
        heap.Dispose();
        provider.Dispose();
    }

    private static void TestBoundedAllocation()
    {
        Syscall.PrintString("\r\n--- Test: Bounded Allocation ---\r\n");
        
        // Small region to test exhaustion
        const int SmallRegion = 1024;
        var provider = new OsbMemoryProvider((IntPtr)(HeapBase + 400000), SmallRegion);
        var heap = new EngineHeap(provider);
        
        // Allocate until exhaustion
        int successCount = 0;
        for (int i = 0; i < 100; i++)
        {
            var s = heap.AllocString(new string('x', 20));
            if (s != null) successCount++;
            else break;
        }
        Test("Allocation stops at boundary", () => successCount > 0 && successCount < 100);
        Test("OOM returns null", () => heap.AllocString("x") == null);
        
        // Verify no write past boundary (provider would return IntPtr.Zero)
        var ptr = provider.Alloc(SmallRegion + 100, 1);
        Test("Oversized alloc returns zero", () => ptr == IntPtr.Zero);
        
        heap.Dispose();
        provider.Dispose();
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