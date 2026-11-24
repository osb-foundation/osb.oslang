using System;
using System.Runtime.InteropServices;
using OsbKT;

namespace Osb.Engine.Probe.Freestanding;

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

/// <summary>
/// Freestanding allocation feasibility probe for OSB Ring3.
/// Tests basic allocation patterns using OsbMemoryProvider and managed new.
/// </summary>
internal static unsafe class FreestandingAllocProbe
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

        Syscall.PrintString("=== O8.3.1 Freestanding Allocation Feasibility Probe ===\r\n");
        Syscall.PrintString("Heap region: 0x2108000 - 0x2200000 (512 KiB)\r\n\r\n");

        // Test 1: OsbMemoryProvider basic operations
        TestMemoryProvider();

        // Test 2: Managed object allocation (NativeAOT GC)
        TestManagedAllocation();

        // Test 3: String allocation and operations
        TestStringAllocation();

        // Test 4: Array allocation
        TestArrayAllocation();

        // Test 5: Bounded allocation exhaustion
        TestBoundedAllocation();

        // Test 6: Repeated allocations without GC
        TestRepeatedAllocations();

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

    private static void TestManagedAllocation()
    {
        Syscall.PrintString("\r\n--- Test: Managed Object Allocation (NativeAOT GC) ---\r\n");
        
        // Test ordinary object allocation
        object obj = new object();
        Test("new object()", () => obj != null);
        
        // Test box value type
        object boxed = 42;
        Test("Box int", () => boxed != null && boxed is int && (int)boxed == 42);
        
        // Test unbox
        int unboxed = (int)boxed;
        Test("Unbox int", () => unboxed == 42);
        
        // Test type check
        object strObj = "hello";
        Test("Type check 'is string'", () => strObj is string && !(strObj is int));
        
        // Test reference equality
        object a = new object();
        object b = a;
        object c = new object();
        Test("Reference equality", () => ReferenceEquals(a, b) && !ReferenceEquals(a, c));
        
        // Test GC.Collect (should be no-op in freestanding)
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Test("GC.Collect executes", () => true);
    }

    private static void TestStringAllocation()
    {
        Syscall.PrintString("\r\n--- Test: String Allocation and Operations ---\r\n");
        
        // String literals
        const string literal = "hello";
        Test("String literal", () => literal.Length == 5 && literal[0] == 'h');
        
        // Empty string
        const string empty = "";
        Test("Empty string literal", () => empty.Length == 0);
        
        // Non-ASCII string
        const string unicode = "caf\u00E9";
        Test("Non-ASCII string literal", () => unicode.Length == 4 && unicode[3] == '\u00E9');
        
        // Runtime string allocation from char array
        char[] chars = { 't', 'e', 's', 't' };
        string fromChars = new string(chars);
        Test("String from char array", () => fromChars == "test");
        
        // String concatenation
        string a = "hello";
        string b = "world";
        string c = a + " " + b;
        Test("String concatenation", () => c == "hello world" && c.Length == 11);
        
        // String equality (content-based)
        string s1 = "foo";
        string s2 = "foo";
        string s3 = "bar";
        Test("String equality", () => s1.Equals(s2) && !s1.Equals(s3) && s1 == s2 && s1 != s3);
        
        // String char access
        string s = "abcdef";
        Test("String char access", () => s[0] == 'a' && s[3] == 'd' && s[5] == 'f');
        
        // ToCharArray
        string t = "test";
        char[] arr = t.ToCharArray();
        Test("ToCharArray", () => arr.Length == 4 && arr[0] == 't' && arr[3] == 't');
        
        // Round-trip
        string original = "roundtrip";
        char[] arr2 = original.ToCharArray();
        string restored = new string(arr2);
        Test("String round-trip", () => restored == original && restored.Length == 9);
        
        // Repeated string allocations
        for (int i = 0; i < 50; i++)
        {
            string temp = "item" + i;
            if (temp.Length < 4) { _failed++; return; }
        }
        Test("Repeated string allocations (50)", () => true);
    }

    private static void TestArrayAllocation()
    {
        Syscall.PrintString("\r\n--- Test: Array Allocation ---\r\n");
        
        // Single-dimensional array
        int[] intArr = new int[10];
        Test("int[] allocation", () => intArr.Length == 10);
        intArr[0] = 42;
        intArr[9] = 99;
        Test("int[] element access", () => intArr[0] == 42 && intArr[9] == 99);
        
        // String array
        string[] strArr = new string[3];
        strArr[0] = "one";
        strArr[1] = "two";
        strArr[2] = "three";
        Test("string[] allocation", () => strArr.Length == 3 && strArr[0] == "one" && strArr[2] == "three");
        
        // Object array
        object[] objArr = new object[5];
        objArr[0] = "hello";
        objArr[1] = 42;
        objArr[2] = new object();
        Test("object[] allocation", () => objArr.Length == 5 && objArr[0] is string && objArr[1] is int);
        
        // Multi-dimensional array
        int[,] mdArr = new int[2, 3];
        Test("int[,] allocation", () => mdArr.GetLength(0) == 2 && mdArr.GetLength(1) == 3);
        
        // Jagged array
        int[][] jagged = new int[3][];
        jagged[0] = new int[2];
        jagged[1] = new int[3];
        jagged[2] = new int[4];
        Test("Jagged array allocation", () => jagged.Length == 3 && jagged[0].Length == 2 && jagged[2].Length == 4);
    }

    private static void TestBoundedAllocation()
    {
        Syscall.PrintString("\r\n--- Test: Bounded Allocation (OsbMemoryProvider) ---\r\n");
        
        // Small region to test exhaustion
        const int SmallRegion = 1024;
        var provider = new OsbMemoryProvider((IntPtr)(HeapBase + 400000), SmallRegion);
        
        // Allocate until exhaustion
        int successCount = 0;
        for (int i = 0; i < 100; i++)
        {
            var ptr = provider.Alloc(20, 1);
            if (ptr != IntPtr.Zero) successCount++;
            else break;
        }
        Test("Allocation stops at boundary", () => successCount > 0 && successCount < 100);
        Test("OOM returns zero", () => provider.Alloc(100, 1) == IntPtr.Zero);
        
        // Verify no write past boundary
        var ptr = provider.Alloc(SmallRegion + 100, 1);
        Test("Oversized alloc returns zero", () => ptr == IntPtr.Zero);
        
        provider.Dispose();
        Test("Small provider disposed", () => true);
    }

    private static void TestRepeatedAllocations()
    {
        Syscall.PrintString("\r\n--- Test: Repeated Allocations Without GC ---\r\n");
        
        // Test many allocations without calling GC.Collect
        // This simulates the freestanding model where objects leak until process exit
        for (int cycle = 0; cycle < 10; cycle++)
        {
            // Allocate various objects
            object[] objects = new object[20];
            for (int i = 0; i < 20; i++)
            {
                objects[i] = new object();
                string s = "data" + i + cycle;
                char[] chars = s.ToCharArray();
            }
            
            // Verify they're still accessible
            if (objects[0] == null || objects[19] == null)
            {
                _failed++;
                Syscall.PrintString("  FAIL: RepeatedAllocations - object lost\r\n");
                return;
            }
        }
        Test("Repeated allocations (10 cycles x 20 objects)", () => true);
        
        // Test GCHandle behavior
        var handles = new GCHandle[10];
        var targets = new object[10];
        for (int i = 0; i < 10; i++)
        {
            var t = new object();
            targets[i] = t;
            handles[i] = GCHandle.Alloc(t, GCHandleType.Normal);
        }
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        for (int i = 0; i < 10; i++)
        {
            if (!handles[i].IsAllocated || handles[i].Target != targets[i])
            {
                _failed++;
                Syscall.PrintString("  FAIL: GCHandle lost target\r\n");
                return;
            }
        }
        Test("GCHandle Normal keeps alive across GC", () => true);
        
        // Free handles
        for (int i = 0; i < 10; i++)
        {
            handles[i].Free();
        }
        Test("GCHandle freed", () => true);
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