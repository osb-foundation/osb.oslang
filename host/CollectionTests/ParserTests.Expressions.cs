using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;

namespace CollectionTests;

internal static partial class ParserTests
{
    private static void TestBinaryOperators()
    {
        var source = "FUNCTION MAIN()\n    PRINT 1 + 2 - 3 * 4 / 5 % 6\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.BinaryOps.PrintStmt", stmt != null);
    }

    private static void TestUnaryOperators()
    {
        var source = "FUNCTION MAIN()\n    PRINT -x\n    PRINT NOT y\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        Program.Check("Parser.UnaryOps.TwoStatements", func.Body.Count == 2);
    }
}
