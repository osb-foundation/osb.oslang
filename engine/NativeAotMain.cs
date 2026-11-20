// M8.5 NativeAOT Engine.Core integration entry point.
// Executes real OSLANG programs through Engine.Core inside a native ELF executable.
// Uses Hosted providers (BCL-based) with NativeAOT GC for managed object lifetime.

using System;
using System.IO;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Host;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;
using Osb.Engine.Hosted;
using Osb.Engine.Hosted.Providers;

namespace Osb.Engine.NativeAot;

internal static class NativeAotMain
{
    private static int _passed;
    private static int _failed;
    private static readonly IUtf8Provider Utf8 = new Utf8Provider();

    public static int Main()
    {
        Console.WriteLine("=== Osb.Engine NativeAOT Integration Test (M8.5) ===");
        Console.WriteLine();

        RunProgram("basic-print",
            """
            FUNCTION MAIN()
                PRINT "Hello, OSLANG!"
            END FUNCTION
            """,
            "Hello, OSLANG!");

        RunProgram("basic-var",
            """
            FUNCTION MAIN()
                VAR x
                x = 42
                PRINT x
            END FUNCTION
            """,
            "42");

        RunProgram("factorial",
            """
            FUNCTION Factorial(n)
                IF n <= 1 THEN
                    RETURN 1
                ELSE
                    RETURN n * Factorial(n - 1)
                END
            END FUNCTION

            FUNCTION MAIN()
                PRINT Factorial(5)
            END FUNCTION
            """,
            "120");

        RunProgram("basic-function",
            """
            FUNCTION Add(a, b)
                RETURN a + b
            END FUNCTION

            FUNCTION MAIN()
                PRINT Add(2, 3)
            END FUNCTION
            """,
            "5");

        RunProgram("basic-array",
            """
            FUNCTION MAIN()
                VAR arr
                arr = [1, 2, 3, 4, 5]
                PRINT arr.COUNT()
                PRINT arr.FIRST()
                PRINT arr.LAST()
            END FUNCTION
            """,
            "5\n1\n5");

        RunProgram("basic-class",
            """
            CLASS Person
                CONSTRUCTOR(name, age)
                END CONSTRUCTOR

                FUNCTION Greet()
                    PRINT "Hello"
                END FUNCTION
            END CLASS

            FUNCTION MAIN()
                VAR p
                p = NEW Person("Alice", 30)
                p.Greet()
            END FUNCTION
            """,
            "Hello");

        RunProgram("basic-string",
            """
            FUNCTION MAIN()
                VAR s
                s = "Hello, World!"
                PRINT s.TOUPPER()
                PRINT s.TOLOWER()
                PRINT s.COUNT()
                PRINT s.SUBSTR(0, 5)
            END FUNCTION
            """,
            "HELLO, WORLD!\nhello, world!\n13\nHello");

        RunProgram("unicode-indexing",
            """
            FUNCTION MAIN()
                VAR s
                s = "ABC"
                PRINT s.COUNT()
            END FUNCTION
            """,
            "3");

        RunProgram("try-catch",
            """
            FUNCTION MAIN()
                TRY
                    PRINT "before error"
                    VAR x
                    x = 1 / 0
                    PRINT "after error"
                CATCH ERR
                    PRINT "caught: " + ERR
                END

                PRINT "after try-catch"
            END FUNCTION
            """,
            "before error\ncaught: Division by zero.\nafter try-catch");

        RunProgram("if-else",
            """
            FUNCTION MAIN()
                VAR x
                x = 10
                IF x > 5 THEN
                    PRINT "greater"
                ELSE
                    PRINT "not greater"
                END
            END FUNCTION
            """,
            "greater");

        RunProgram("while-loop",
            """
            FUNCTION MAIN()
                VAR i
                i = 1
                WHILE i <= 5
                    PRINT i
                    i = i + 1
                END
            END FUNCTION
            """,
            "1\n2\n3\n4\n5");

        RunProgram("repeated-alloc",
            """
            FUNCTION MAIN()
                VAR i
                i = 0
                WHILE i < 20
                    PRINT i
                    i = i + 1
                END
            END FUNCTION
            """,
            "0\n1\n2\n3\n4\n5\n6\n7\n8\n9\n10\n11\n12\n13\n14\n15\n16\n17\n18\n19");

        Console.WriteLine();
        Console.WriteLine($"Basic tests: {_passed} passed, {_failed} failed");
        Console.WriteLine();

        RunGcIntegrationTests();

        Console.WriteLine();
        Console.WriteLine($"Total Results: {_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }

    private static void RunProgram(string name, string source, string expected)
    {
        try
        {
            var memoryProvider = new HostedMemoryProvider();
            var heap = new EngineHeap(memoryProvider);
            var output = new StringWriter();
            var host = new HostTest(output);
            var math = new MathProvider();
            var time = new Osb.Engine.Hosted.Providers.TimeProvider();
            var random = new RandomProvider();
            var interpreter = new Interpreter(heap, host, math, time, random, Utf8);

            var lexer = new Lexer(source, Utf8);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var program = parser.Parse();

            interpreter.Execute(program);
            output.Flush();

            string actual = output.ToString().Replace("\r\n", "\n").TrimEnd('\n', '\r');
            string expectedNorm = expected.Replace("\r\n", "\n").TrimEnd('\n', '\r');

            if (actual == expectedNorm)
            {
                _passed++;
                Console.WriteLine($"  PASS {name}");
            }
            else
            {
                _failed++;
                Console.WriteLine($"  FAIL {name}");
                Console.WriteLine($"    Expected: [{expectedNorm}]");
                Console.WriteLine($"    Actual:   [{actual}]");
            }
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL {name} - exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void RunGcIntegrationTests()
    {
        Console.WriteLine("--- GC Integration Tests ---");

        // Single interpreter shared across GC tests; heap persists between them
        var memoryProvider = new HostedMemoryProvider();
        var heap = new EngineHeap(memoryProvider);
        var output = new StringWriter();
        var host = new HostTest(output);
        var math = new MathProvider();
        var time = new Osb.Engine.Hosted.Providers.TimeProvider();
        var random = new RandomProvider();
        var interpreter = new Interpreter(heap, host, math, time, random, Utf8);

        // GC 1: Register a function, collect, then call it from another program
        ExecuteGc(interpreter, output, "gc-function-registry",
            """
            FUNCTION Add(a, b)
                RETURN a + b
            END FUNCTION
            """, null);
        heap.Collect();
        ExecuteGc(interpreter, output, "gc-call-registered-function",
            """
            FUNCTION MAIN()
                PRINT Add(10, 20)
            END FUNCTION
            """, "30");

        // GC 2: Register a class, collect, then instantiate it
        ExecuteGc(interpreter, output, "gc-set-class",
            """
            CLASS C
                CONSTRUCTOR()
                END CONSTRUCTOR

                FUNCTION GetValue()
                    RETURN 42
                END FUNCTION
            END CLASS
            """, null);
        heap.Collect();
        ExecuteGc(interpreter, output, "gc-use-registered-class",
            """
            FUNCTION MAIN()
                VAR c
                c = NEW C()
                PRINT c.GetValue()
            END FUNCTION
            """, "42");

        // GC 3: Repeated allocations with GC between programs (10 cycles)
        for (int cycle = 0; cycle < 10; cycle++)
        {
            ExecuteGc(interpreter, output, $"gc-cycle-{cycle}",
                """
                FUNCTION MAIN()
                    VAR i
                    i = 0
                    WHILE i < 5
                        PRINT i
                        i = i + 1
                    END
                END FUNCTION
                """, "0\n1\n2\n3\n4");
            heap.Collect();
        }

        // GC 4: Weak handle does not prevent collection
        var weakTarget = new object();
        var weakRef = new WeakReference<object>(weakTarget);
        weakTarget = null;
        heap.Collect();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        if (!weakRef.TryGetTarget(out _))
        {
            _passed++;
            Console.WriteLine("  PASS gc-weak-handle-collected");
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL gc-weak-handle-collected - weak reference still alive");
        }

        Console.WriteLine();
    }

    private static void ExecuteGc(Interpreter interpreter, StringWriter output, string name, string source, string? expected)
    {
        try
        {
            output.GetStringBuilder().Clear();
            var lexer = new Lexer(source, Utf8);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var program = parser.Parse();

            interpreter.Execute(program);

            if (expected == null)
            {
                _passed++;
                Console.WriteLine($"  PASS {name} (registered)");
            }
            else
            {
                string actual = output.ToString().Replace("\r\n", "\n").TrimEnd('\n', '\r');
                if (actual == expected)
                {
                    _passed++;
                    Console.WriteLine($"  PASS {name}");
                }
                else
                {
                    _failed++;
                    Console.WriteLine($"  FAIL {name}");
                    Console.WriteLine($"    Expected: [{expected}]");
                    Console.WriteLine($"    Actual:   [{actual}]");
                }
            }
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL {name} - exception: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
