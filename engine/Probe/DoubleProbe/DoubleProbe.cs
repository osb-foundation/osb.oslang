using System;
using System.Runtime.InteropServices;
using OsbKT;

namespace Osb.Engine.Probe.Freestanding;

internal static class OsbNative
{
    [DllImport("osbnative")]
    public static extern void osb_tls_set_alloc(ulong v);

    [DllImport("osbnative")]
    public static extern void osb_tls_set_limit(ulong v);
}

internal static unsafe class DoubleProbe
{
    private const ulong HeapBase = 0x2180000;
    private const ulong HeapLimit = 0x21FFFF0;

    private static int _pass;
    private static int _fail;

    private static void Out(string s) => Syscall.PrintString(s);

    private static bool RefEq(object a, object b) => a == b;

    private static bool StrEq(string a, string b)
    {
        if (a == null || b == null) return a == b;
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static void Check(string name, string actual, string expected)
    {
        bool ok = StrEq(actual, expected);
        if (ok) { _pass++; Out("  PASS: " + name + " => '" + actual + "'\r\n"); }
        else { _fail++; Out("  FAIL: " + name + " => '" + actual + "' (expected '" + expected + "')\r\n"); }
    }

    private static int Main()
    {
        OsbNative.osb_tls_set_alloc(HeapBase);
        OsbNative.osb_tls_set_limit(HeapLimit);

        Out("=== Double FORMAT FREESTANDING PROBE ===\r\n");

        // zero
        Check("0", 0.0.ToString(), "0");
        // positive / negative integers as doubles
        Check("42", 42.0.ToString(), "42");
        Check("-42", (-42.0).ToString(), "-42");
        Check("123", 123.0.ToString(), "123");
        Check("1", 1.0.ToString(), "1");
        // fractional
        Check("3.25", 3.25.ToString(), "3.25");
        Check("-13.5", (-13.5).ToString(), "-13.5");
        Check("0.5", 0.5.ToString(), "0.5");
        Check("0.1", 0.1.ToString(), "0.1");
        // large / small representable
        Check("1000000", 1000000.0.ToString(), "1000000");
        Check("1e12", 1000000000000.0.ToString(), "1000000000000");
        Check("0.0001", 0.0001.ToString(), "0.0001");

        Out("=== " + (_pass + _fail) + " checks, " + _fail + " failed ===\r\n");
        Syscall.Exit(_fail == 0 ? 0 : 1);
        return _fail == 0 ? 0 : 1;
    }
}