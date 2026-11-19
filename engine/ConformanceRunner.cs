using System;
using System.IO;
using System.Collections.Generic;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Host;
using Osb.Engine.Hosted;
using Osb.Engine.Hosted.Providers;
using HostedTimeProvider = Osb.Engine.Hosted.Providers.TimeProvider;

namespace Osb.Engine.Conformance
{
    internal sealed class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: Osb.Engine.Conformance <test-root> [filter]");
                return 1;
            }

            string testRoot = args[0];
            string filter = args.Length > 1 ? args[1] : null;

            if (!Directory.Exists(testRoot))
            {
                Console.Error.WriteLine($"Test root not found: {testRoot}");
                return 1;
            }

            var runner = new ConformanceRunner(testRoot, filter);
            return runner.Run() ? 0 : 1;
        }

    internal sealed class ConformanceRunner
    {
        private readonly string _testRoot;
        private readonly string _filter;
        private int _passed = 0;
        private int _failed = 0;
        private int _skipped = 0;
        private readonly List<string> _failures = new();

        public ConformanceRunner(string testRoot, string filter)
        {
            _testRoot = testRoot;
            _filter = filter;
        }

        public bool Run()
        {
            Console.WriteLine($"Running conformance tests from: {_testRoot}");
            if (_filter != null) Console.WriteLine($"Filter: {_filter}");

            var testFiles = Directory.GetFiles(_testRoot, "*.osl", SearchOption.AllDirectories);
            Array.Sort(testFiles);

            foreach (var testFile in testFiles)
            {
                string relPath = testFile.Substring(_testRoot.Length + 1);
                if (_filter != null && !relPath.StartsWith(_filter + Path.DirectorySeparatorChar) && relPath != _filter + ".osl")
                    continue;

                RunTest(testFile, relPath);
            }

            Console.WriteLine();
            Console.WriteLine($"Results: {_passed} passed, {_failed} failed, {_skipped} skipped");

            if (_failed > 0)
            {
                Console.WriteLine("\nFailures:");
                foreach (var f in _failures)
                    Console.WriteLine($"  - {f}");
                return false;
            }
            return true;
        }

        private void RunTest(string testFile, string relPath)
        {
            string baseDir = Path.GetDirectoryName(testFile)!;
            string testName = Path.GetFileNameWithoutExtension(testFile);

            string outFile = Path.ChangeExtension(testFile, ".out");
            string inFile = Path.ChangeExtension(testFile, ".in");
            string tagsFile = Path.ChangeExtension(testFile, ".tags");

            if (!File.Exists(outFile))
            {
                Console.WriteLine($"  SKIP {relPath}: no .out file");
                _skipped++;
                return;
            }

            string expected = File.ReadAllText(outFile).Replace("\r\n", "\n").TrimEnd('\n', '\r');
            string input = File.Exists(inFile) ? File.ReadAllText(inFile) : "";
            string source = File.ReadAllText(testFile);

            // Check tags for needs-file
            bool skip = false;
            if (File.Exists(tagsFile))
            {
                var tags = File.ReadAllLines(tagsFile);
                foreach (var tag in tags)
                {
                    if (tag.StartsWith("needs-file:"))
                    {
                        string neededFile = tag.Substring("needs-file:".Length).Trim();
                        string fullPath = Path.Combine(baseDir, neededFile);
                        if (!File.Exists(fullPath))
                        {
                            Console.WriteLine($"  SKIP {relPath}: missing required file {neededFile}");
                            _skipped++;
                            skip = true;
                            break;
                        }
                    }
                }
            }
            if (skip) return;

            try
            {
                // Create engine with hosted providers
                var heap = new EngineHeap();
                var output = new StringWriter();
                var host = new HostTest(output);
                var math = new MathProvider();
                var time = new HostedTimeProvider();
                var random = new RandomProvider();
                var utf8 = new Utf8Provider();
                var interpreter = new Interpreter(heap, host, math, time, random, utf8);

                // Parse
                var lexer = new Lexer(source, utf8);
                var tokens = lexer.Tokenize();
                var parser = new Parser(tokens);
                var program = parser.Parse();

                // Execute
                try
                {
                    interpreter.Execute(program);
                }
                finally
                {
                    output.Flush();
                }

                string actual = output.ToString().Replace("\r\n", "\n").TrimEnd('\n', '\r');

                if (actual == expected)
                {
                    Console.WriteLine($"  PASS {relPath}");
                    _passed++;
                }
                else
                {
                    Console.WriteLine($"  FAIL {relPath}");
                    Console.WriteLine($"    Expected: [{expected}]");
                    Console.WriteLine($"    Actual:   [{actual}]");
                    _failed++;
                    _failures.Add($"{relPath}: expected [{expected}] but got [{actual}]");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAIL {relPath}: {ex.Message}");
                _failed++;
                _failures.Add($"{relPath}: exception: {ex.Message}");
            }
        }
    }
}
}