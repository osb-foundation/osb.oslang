using System;
using System.Runtime.InteropServices;
using OsbKT;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Probe.Freestanding;

internal static class OsbNative
{
    [DllImport("osbnative")]
    public static extern void osb_tls_set_alloc(ulong v);

    [DllImport("osbnative")]
    public static extern void osb_tls_set_limit(ulong v);
}

// Card 2 continuation regression harness (freestanding NativeAOT/QEMU).
//
// Isolates the OSLANG 'variables read back NULL' symptom at the ValueMap layer
// (no interpreter). Proves:
//   * direct object[]/bool[]/byte[] round-trip (incl. reference identity);
//   * the production read path ValueMap.GetValue (return-buffer) returns the
//     correct ValueKind, payload and heap reference for every supported kind,
//     including when called immediately after the indexer.
//
// The historical failure was the freestanding ILC out-parameter struct-copy in
// TryGetValue(key, out Value) zeroing the Value after get_Item; GetValue avoids
// the out parameter and round-trips correctly. See O8.25 continuation report.
internal static unsafe class ValueMapProbe
{
    private const ulong HeapBase = 0x2180000;
    private const ulong HeapLimit = 0x21FFFF0;
    private const int HeapSize = (int)(HeapLimit - HeapBase);

    private static int _pass;
    private static int _fail;

    private static void Out(string s) => Syscall.PrintString(s);

    private static string Num(long n)
    {
        if (n == 0) return "0";
        bool neg = n < 0;
        ulong u = neg ? (ulong)(-n) : (ulong)n;
        var buf = new char[24];
        int i = buf.Length;
        while (u > 0) { i--; buf[i] = (char)('0' + (int)(u % 10)); u /= 10; }
        if (neg) { i--; buf[i] = '-'; }
        return new string(buf, i, buf.Length - i);
    }

    private static bool RefEq(object a, object b) => a == b;

    private static void Check(string name, bool ok)
    {
        if (ok) { _pass++; Out("  PASS: " + name + "\r\n"); }
        else { _fail++; Out("  FAIL: " + name + "\r\n"); }
    }

    private static int Main()
    {
        OsbNative.osb_tls_set_alloc(HeapBase);
        OsbNative.osb_tls_set_limit(HeapLimit);

        Out("=== ValueMap FREESTANDING REGRESSION ===\r\n");

        var provider = new OsbMemoryProvider((IntPtr)HeapBase, HeapSize);
        var heap = new EngineHeap(provider);
        var map = new ValueMap(8, StringComparer.OrdinalIgnoreCase);

        // ---- Direct array round-trip (object/bool/byte) ----
        var hs0 = heap.AllocString("zero");
        var hs1 = heap.AllocString("one");
        var oarr = new object[4];
        oarr[0] = hs0; oarr[1] = hs1; oarr[2] = null; oarr[3] = hs0;
        Check("object[] identity [0]", RefEq(oarr[0], hs0));
        Check("object[] identity [1]", RefEq(oarr[1], hs1));
        Check("object[] null [2]", oarr[2] == null);
        Check("object[] identity [3]", RefEq(oarr[3], hs0));

        var barr = new bool[4] { true, false, true, false };
        Check("bool[] roundtrip", barr[0] && !barr[1] && barr[2] && !barr[3]);

        var byarr = new byte[4] { 0, 1, 127, 255 };
        Check("byte[] roundtrip", byarr[0] == 0 && byarr[1] == 1 && byarr[2] == 127 && byarr[3] == 255);

        // ---- ValueMap multiple kinds via the production GetValue path ----
        map["n"] = Value.Number(42);
        map["b"] = Value.Boolean(true);
        map["bf"] = Value.Boolean(false);
        map["s"] = Value.String(hs1);
        map["z"] = Value.NullValue;
        var arr = heap.AllocArray(3);
        map["a"] = Value.Array(arr);

        // Enum tests (immediate type - heapRef == null)
        map["e1"] = Value.Enum(0, 1);   // Color.RED
        map["e2"] = Value.Enum(2, 5);   // Different enum type/member

        Check("count==8", map.Count == 8);

        Value v;
        v = map.GetValue("n", out bool f1);
        Check("n Number 42", f1 && v.Kind == ValueKind.Number && v.AsNumber == 42);
        v = map.GetValue("b", out bool f2);
        Check("b Boolean true", f2 && v.Kind == ValueKind.Boolean && v.AsBoolean);
        v = map.GetValue("bf", out bool f3);
        Check("bf Boolean false", f3 && v.Kind == ValueKind.Boolean && !v.AsBoolean);
        v = map.GetValue("s", out bool f4);
        Check("s String identity", f4 && v.Kind == ValueKind.String && RefEq(v.AsString, hs1));
        v = map.GetValue("z", out bool f5);
        Check("z Null", f5 && v.Kind == ValueKind.Null);
        v = map.GetValue("a", out bool f6);
        Check("a Array identity", f6 && v.Kind == ValueKind.Array && RefEq(v.AsArray, arr));

        // Enum retrieval tests
        v = map.GetValue("e1", out bool f7);
        Check("e1 Enum kind", f7 && v.Kind == ValueKind.Enum);
        Check("e1 Enum indices (0,1)", f7 && v.AsEnum.TypeIndex == 0 && v.AsEnum.MemberIndex == 1);
        v = map.GetValue("e2", out bool f8);
        Check("e2 Enum kind", f8 && v.Kind == ValueKind.Enum);
        Check("e2 Enum indices (2,5)", f8 && v.AsEnum.TypeIndex == 2 && v.AsEnum.MemberIndex == 5);

        // ---- GetValue immediately after the indexer (the historical failure) ----
        Value _ = map["n"];
        v = map.GetValue("n", out bool g1);
        Check("n after indexer", g1 && v.Kind == ValueKind.Number && v.AsNumber == 42);
        Value _2 = map["s"];
        v = map.GetValue("s", out bool g2);
        Check("s after indexer", g2 && v.Kind == ValueKind.String && RefEq(v.AsString, hs1));
        // Enum after indexer
        Value _3 = map["e1"];
        v = map.GetValue("e1", out bool g3);
        Check("e1 after indexer", g3 && v.Kind == ValueKind.Enum && v.AsEnum.TypeIndex == 0 && v.AsEnum.MemberIndex == 1);

        // ---- Repeated reads ----
        bool repeatOk = true;
        for (int i = 0; i < 100; i++)
        {
            v = map.GetValue("n", out bool r1);
            if (!r1 || v.AsNumber != 42) { repeatOk = false; break; }
            v = map.GetValue("s", out bool r2);
            if (!r2 || !RefEq(v.AsString, hs1)) { repeatOk = false; break; }
            v = map.GetValue("e1", out bool r3);
            if (!r3 || v.Kind != ValueKind.Enum || v.AsEnum.TypeIndex != 0 || v.AsEnum.MemberIndex != 1) { repeatOk = false; break; }
        }
        Check("100x repeated reads", repeatOk);

        // ---- Replacement ----
        map["n"] = Value.Number(99);
        v = map.GetValue("n", out bool p1);
        Check("replace n -> 99", p1 && v.AsNumber == 99);
        map["s"] = Value.String(hs0);
        v = map.GetValue("s", out bool p2);
        Check("replace s identity hs0", p2 && RefEq(v.AsString, hs0));
        map["e1"] = Value.Enum(3, 7);
        v = map.GetValue("e1", out bool p3);
        Check("replace e1 -> Enum(3,7)", p3 && v.Kind == ValueKind.Enum && v.AsEnum.TypeIndex == 3 && v.AsEnum.MemberIndex == 7);

        // ---- Grow / rehash with unrelated insertions ----
        for (int i = 0; i < 50; i++) map["k" + i] = Value.Number(i);
        Check("count after grow == 58", map.Count == 58);
        v = map.GetValue("n", out bool q1);
        Check("n survives grow (99)", q1 && v.AsNumber == 99);
        v = map.GetValue("s", out bool q2);
        Check("s survives grow (hs0)", q2 && RefEq(v.AsString, hs0));
        v = map.GetValue("a", out bool q3);
        Check("a survives grow", q3 && v.Kind == ValueKind.Array && RefEq(v.AsArray, arr));
        v = map.GetValue("k37", out bool q4);
        Check("k37 == 37", q4 && v.AsNumber == 37);
        v = map.GetValue("e1", out bool q5);
        Check("e1 survives grow (3,7)", q5 && v.Kind == ValueKind.Enum && v.AsEnum.TypeIndex == 3 && v.AsEnum.MemberIndex == 7);
        v = map.GetValue("e2", out bool q6);
        Check("e2 survives grow (2,5)", q6 && v.Kind == ValueKind.Enum && v.AsEnum.TypeIndex == 2 && v.AsEnum.MemberIndex == 5);

        // ---- Missing key ----
        v = map.GetValue("nope", out bool m1);
        Check("missing key false", !m1);
        Check("missing key NullValue", v.Kind == ValueKind.Null);

        Out("=== " + Num(_pass) + " passed, " + Num(_fail) + " failed ===\r\n");
        Syscall.Exit(_fail == 0 ? 0 : 1);
        return _fail == 0 ? 0 : 1;
    }
}