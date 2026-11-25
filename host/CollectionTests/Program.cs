using System;
using System.Collections.Generic;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace CollectionTests;

internal static class Program
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Main(string[] args)
    {
        Console.WriteLine("=== NodeList<T> Tests ===");
        NodeListTests.RunAll();

        Console.WriteLine();
        Console.WriteLine("=== TokenTypeSet Tests ===");
        TokenTypeSetTests.RunAll();

        Console.WriteLine();
        Console.WriteLine("=== NodeList<T> GC Visibility Tests ===");
        GcTests.RunAll();

        Console.WriteLine();
        Console.WriteLine("=== Lexer Tests ===");
        LexerTests.RunAll();

        Console.WriteLine();
        Console.WriteLine("=== Parser Tests ===");
        ParserTests.RunAll();

        Console.WriteLine();
        Console.WriteLine("=== DateTime Tests ===");
        DateTimeTests.RunAll();

        Console.WriteLine();
        Console.WriteLine("=== Interpreter / Error Propagation Tests ===");
        InterpreterTests.RunAll();

        Console.WriteLine();
        Console.WriteLine("=== Allocation Failure Reliability Tests ===");
        AllocationTests.RunAll();

        Console.WriteLine();
        Console.WriteLine("=== Collection Integrity Tests ===");
        StringMapTests.RunAll();
        FrameStackTests.RunAll();

        Console.WriteLine();
        Console.WriteLine($"Results: {_passed} passed, {_failed} failed");
        if (_failed > 0) Environment.Exit(1);
    }

    internal static void Check(string name, bool condition, string? detail = null)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  PASS {name}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL {name}" + (detail != null ? $" - {detail}" : ""));
        }
    }

    internal static void CheckThrows<T>(string name, Action action) where T : Exception
    {
        try
        {
            action();
            _failed++;
            Console.WriteLine($"  FAIL {name} - no exception thrown");
        }
        catch (T)
        {
            _passed++;
            Console.WriteLine($"  PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL {name} - wrong exception type: {ex.GetType().Name}");
        }
    }
}
