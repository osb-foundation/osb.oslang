// M8.3.6 NativeAOT runtime smoke tests.
// All tests execute inside the published NativeAOT ELF executable.
// Exercises actual runtime allocation paths, not only compile-time constants.

using System;
using System.Collections.Generic;

namespace Osb.Engine.Probe;

internal static class NativeRuntimeSmokeTests
{
    private static int _passed;
    private static int _failed;

    public static int RunAll()
    {
        Console.WriteLine("=== NativeAOT Runtime Smoke Tests ===");
        Console.WriteLine();

        Test("String literal: static", TestStringLiteralStatic);
        Test("String literal: empty", TestStringLiteralEmpty);
        Test("String literal: ASCII", TestStringLiteralAscii);
        Test("String literal: non-ASCII", TestStringLiteralNonAscii);

        Test("String allocation: from char array", TestStringFromCharArray);
        Test("String allocation: length", TestStringAllocationLength);
        Test("String allocation: contents", TestStringAllocationContents);
        Test("String allocation: empty", TestStringAllocationEmpty);
        Test("String allocation: non-empty", TestStringAllocationNonEmpty);

        Test("String operation: concatenation", TestStringConcat);
        Test("String operation: equality", TestStringEquality);
        Test("String operation: inequality", TestStringInequality);
        Test("String operation: char access", TestStringCharAccess);
        Test("String operation: to char array", TestStringToCharArray);
        Test("String operation: from char array", TestStringFromCharArrayRoundTrip);

        Test("Object allocation: ordinary object", TestObjectAllocation);
        Test("Object allocation: box value type", TestBoxValueType);
        Test("Object allocation: unbox value type", TestUnboxValueType);
        Test("Object allocation: type check 'is'", TestIsTypeCheck);
        Test("Object allocation: string array", TestStringArray);
        Test("Object allocation: List<string>", TestListOfStrings);

        Test("Exception handling: throw and catch", TestThrowAndCatch);
        Test("Exception handling: continue after catch", TestContinueAfterCatch);

        Test("Repeated allocations: bounded sequence", TestRepeatedAllocations);

        Console.WriteLine();
        Console.WriteLine($"Results: {_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }

    private static void Test(string name, Func<bool> func)
    {
        try
        {
            if (func())
            {
                _passed++;
                Console.WriteLine($"  PASS {name}");
            }
            else
            {
                _failed++;
                Console.WriteLine($"  FAIL {name} - returned false");
            }
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL {name} - exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // --- 1. String literals ---

    private static bool TestStringLiteralStatic()
    {
        const string s = "hello";
        return s.Length == 5 && s[0] == 'h' && s[4] == 'o';
    }

    private static bool TestStringLiteralEmpty()
    {
        const string s = "";
        return s.Length == 0;
    }

    private static bool TestStringLiteralAscii()
    {
        const string s = "ABC123";
        return s.Length == 6 && s[3] == '1';
    }

    private static bool TestStringLiteralNonAscii()
    {
        const string s = "caf\u00E9 na\u00EFve";
        return s.Length == 10 && s[3] == '\u00E9' && s[7] == '\u00EF';
    }

    // --- 2. Runtime string allocation ---

    private static bool TestStringFromCharArray()
    {
        char[] chars = { 'h', 'i' };
        string s = new string(chars);
        return s.Length == 2 && s[0] == 'h' && s[1] == 'i';
    }

    private static bool TestStringAllocationLength()
    {
        string s = new string(new char[] { 'a', 'b', 'c', 'd' });
        return s.Length == 4;
    }

    private static bool TestStringAllocationContents()
    {
        string s = new string(new char[] { 'X', 'Y', 'Z' });
        return s[0] == 'X' && s[1] == 'Y' && s[2] == 'Z';
    }

    private static bool TestStringAllocationEmpty()
    {
        string s = new string(Array.Empty<char>());
        return s.Length == 0;
    }

    private static bool TestStringAllocationNonEmpty()
    {
        string s = new string(new char[] { '!' });
        return s.Length == 1 && s[0] == '!';
    }

    // --- 3. String operations ---

    private static bool TestStringConcat()
    {
        string a = "hello";
        string b = "world";
        string c = a + " " + b;
        return c == "hello world" && c.Length == 11;
    }

    private static bool TestStringEquality()
    {
        string a = "foo";
        string b = "foo";
        string c = "bar";
        return a.Equals(b) && !a.Equals(c) && a == b && a != c;
    }

    private static bool TestStringInequality()
    {
        string a = "abc";
        string b = "abd";
        return a != b && !(a == b);
    }

    private static bool TestStringCharAccess()
    {
        string s = "abcdef";
        return s[0] == 'a' && s[3] == 'd' && s[5] == 'f';
    }

    private static bool TestStringToCharArray()
    {
        string s = "test";
        char[] arr = s.ToCharArray();
        return arr.Length == 4 && arr[0] == 't' && arr[3] == 't';
    }

    private static bool TestStringFromCharArrayRoundTrip()
    {
        string original = "roundtrip";
        char[] arr = original.ToCharArray();
        string restored = new string(arr);
        return restored == original && restored.Length == 9;
    }

    // --- 4. Object allocation and type behavior ---

    private static bool TestObjectAllocation()
    {
        object o = new object();
        return o != null && o.GetType() == typeof(object);
    }

    private static bool TestBoxValueType()
    {
        object boxed = 42;
        return boxed != null && boxed is int && (int)boxed == 42;
    }

    private static bool TestUnboxValueType()
    {
        object boxed = 100;
        int unboxed = (int)boxed;
        return unboxed == 100;
    }

    private static bool TestIsTypeCheck()
    {
        object o = "hello";
        return o is string && !(o is int);
    }

    private static bool TestStringArray()
    {
        string[] arr = new string[3];
        arr[0] = "one";
        arr[1] = "two";
        arr[2] = "three";
        return arr.Length == 3 && arr[0] == "one" && arr[2] == "three";
    }

    private static bool TestListOfStrings()
    {
        List<string> list = new List<string>();
        list.Add("alpha");
        list.Add("beta");
        list.Add("gamma");
        return list.Count == 3 && list[0] == "alpha" && list[2] == "gamma";
    }

    // --- 5. Exception handling ---

    private static bool TestThrowAndCatch()
    {
        try
        {
            throw new InvalidOperationException("test");
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == "test";
        }
        throw new InvalidOperationException("unreachable");
    }

    private static bool TestContinueAfterCatch()
    {
        int after = 0;
        try
        {
            throw new Exception("boom");
        }
        catch
        {
            after = 42;
        }
        return after == 42;
    }

    // --- 6. Repeated allocations ---

    private static bool TestRepeatedAllocations()
    {
        List<string> accumulated = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            string s = $"item{i}";
            accumulated.Add(s);
            object boxed = i;
            if ((int)boxed != i) return false;
        }
        if (accumulated.Count != 100) return false;
        if (accumulated[0] != "item0") return false;
        if (accumulated[99] != "item99") return false;
        return true;
    }
}
