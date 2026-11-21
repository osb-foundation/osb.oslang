using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Host;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Math;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace Osb.Engine.Probe;

/// <summary>
/// NativeAOT probe entry point for Engine.Core freestanding validation.
/// </summary>
public static class ProbeMain
{
    public static void Main()
    {
        // Basic Engine.Core type smoke tests
        var nl = new NodeList<int>();
        nl.Add(1);
        nl.Add(2);
        nl.Add(3);

        var sm = new StringMap<int>(8, StringComparer.OrdinalIgnoreCase);
        sm["abc"] = 1;
        sm["def"] = 2;

        var va = new ValueArray();
        va.Add(Value.Number(42));
        va.Add(Value.String(null));

        // Lexer smoke test
        var lexer = new Lexer("PRINT 1 + 2");
        var tokens = lexer.Tokenize();

        // Parser smoke test (top-level expects declarations, not statements)
        try
        {
            var parser = new Parser(tokens);
            var program = parser.Parse();
        }
        catch
        {
            // Parser rejects statement-only input at top level -- expected.
        }

        System.Console.WriteLine("Probe: Engine.Core smoke test passed");

        // NativeAOT runtime smoke tests (M8.3.6)
        int rc = NativeRuntimeSmokeTests.RunAll();
        if (rc != 0)
        {
            System.Environment.Exit(rc);
        }

        // NativeAOT GC architecture experiments (M8.4a)
        int gcRc = NativeGCTests.RunAll();
        if (gcRc != 0)
        {
            System.Environment.Exit(gcRc);
        }
    }
}
