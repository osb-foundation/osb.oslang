using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;

namespace CollectionTests;

internal static partial class ParserTests
{
    internal static void RunAll()
    {
        TestEmptySource();
        TestSinglePrintStatement();
        TestMultipleStatements();
        TestVariableDeclaration();
        TestIntegerExpression();
        TestDecimalExpression();
        TestOperatorPrecedence();
        TestParenthesizedExpression();
        TestFunctionDeclaration();
        TestFunctionCall();
        TestArrayLiteral();
        TestArrayIndexing();
        TestIfStatement();
        TestForLoop();
        TestWhileLoop();
        TestNestedBlocks();
        TestEmptyParameterList();
        TestEmptyArgumentList();
        TestParserErrorInvalidCharacter();
        TestErrorSourceLocation();
        TestAstChildOrdering();
    }

    private static void TestEmptySource()
    {
        var lexer = new Lexer("");
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        Program.Check("Parser.EmptySource.Functions", program.Functions.Count == 0);
        Program.Check("Parser.EmptySource.Classes", program.Classes.Count == 0);
        Program.Check("Parser.EmptySource.Enums", program.Enums.Count == 0);
    }

    private static void TestSinglePrintStatement()
    {
        var source = "FUNCTION MAIN()\n    PRINT \"hello\"\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        Program.Check("Parser.SinglePrint.OneFunction", program.Functions.Count == 1);
        var func = program.Functions[0];
        Program.Check("Parser.SinglePrint.OneStatement", func.Body.Count == 1);
    }

    private static void TestMultipleStatements()
    {
        var source = "FUNCTION MAIN()\n    PRINT 1\n    PRINT 2\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        Program.Check("Parser.MultipleStatements.OneFunction", program.Functions.Count == 1);
        var func = program.Functions[0];
        Program.Check("Parser.MultipleStatements.FunctionName", func.Name == "MAIN");
        Program.Check("Parser.MultipleStatements.TwoStatements", func.Body.Count == 2);
    }

    private static void TestVariableDeclaration()
    {
        var source = "FUNCTION MAIN()\n    VAR x\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        Program.Check("Parser.VarDecl.OneStatement", func.Body.Count == 1);
        Program.Check("Parser.VarDecl.IsVarDecl", func.Body[0] is VarDeclStmt);
    }

    private static void TestIntegerExpression()
    {
        var source = "FUNCTION MAIN()\n    VAR x\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0];
        Program.Check("Parser.IntegerExpr.VarName", stmt is VarDeclStmt vd && vd.Name == "x");
    }

    private static void TestDecimalExpression()
    {
        var source = "FUNCTION MAIN()\n    VAR x\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0];
        Program.Check("Parser.DecimalExpr.NoCrash", stmt is VarDeclStmt);
    }

    private static void TestOperatorPrecedence()
    {
        var source = "FUNCTION MAIN()\n    PRINT 2 + 3 * 4\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.Precedence.PrintStmt", stmt != null);
        if (stmt != null && stmt.Expressions.Count > 0)
        {
            var expr = stmt.Expressions[0];
            Program.Check("Parser.Precedence.IsBinary", expr is BinaryExpr);
            if (expr is BinaryExpr be)
            {
                Program.Check("Parser.Precedence.OpIsPlus", be.Operator == "+");
                Program.Check("Parser.Precedence.RightIsBinary", be.Right is BinaryExpr);
            }
        }
    }

    private static void TestParenthesizedExpression()
    {
        var source = "FUNCTION MAIN()\n    PRINT (2 + 3) * 4\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.ParenExpr.PrintStmt", stmt != null);
    }

    private static void TestFunctionDeclaration()
    {
        var source = "FUNCTION ADD(a, b)\n    RETURN a + b\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        Program.Check("Parser.FuncDecl.OneFunction", program.Functions.Count == 1);
        var func = program.Functions[0];
        Program.Check("Parser.FuncDecl.Name", func.Name == "ADD");
        Program.Check("Parser.FuncDecl.TwoParams", func.Parameters.Count == 2);
        Program.Check("Parser.FuncDecl.Param1Name", func.Parameters[0].Name == "a");
        Program.Check("Parser.FuncDecl.Param2Name", func.Parameters[1].Name == "b");
        Program.Check("Parser.FuncDecl.HasReturn", func.Body.Count == 1);
    }

    private static void TestFunctionCall()
    {
        var source = "FUNCTION MAIN()\n    PRINT ADD(1, 2)\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.FuncCall.PrintStmt", stmt != null);
        if (stmt != null && stmt.Expressions.Count > 0)
        {
            Program.Check("Parser.FuncCall.IsCall", stmt.Expressions[0] is CallExpr);
        }
    }

    private static void TestArrayLiteral()
    {
        var source = "FUNCTION MAIN()\n    PRINT [1, 2, 3]\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.ArrayLiteral.PrintStmt", stmt != null);
        if (stmt != null && stmt.Expressions.Count > 0)
        {
            Program.Check("Parser.ArrayLiteral.IsArray", stmt.Expressions[0] is ArrayLiteralExpr);
            if (stmt.Expressions[0] is ArrayLiteralExpr ale)
            {
                Program.Check("Parser.ArrayLiteral.ThreeElements", ale.Elements.Count == 3);
            }
        }
    }

    private static void TestArrayIndexing()
    {
        var source = "FUNCTION MAIN()\n    VAR arr\n    PRINT arr[0]\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        Program.Check("Parser.ArrayIndex.TwoStatements", func.Body.Count == 2);
    }

    private static void TestIfStatement()
    {
        var source = "FUNCTION MAIN()\n    IF x THEN\n        PRINT 1\n    END IF\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as IfStmt;
        Program.Check("Parser.IfStmt.IsIf", stmt != null);
        if (stmt != null)
        {
            Program.Check("Parser.IfStmt.ThenBody", stmt.ThenBody.Count == 1);
            Program.Check("Parser.IfStmt.NoElse", stmt.ElseBody == null);
            Program.Check("Parser.IfStmt.NoElifs", stmt.ElifClauses.Count == 0);
        }
    }

    private static void TestForLoop()
    {
        var source = "FUNCTION MAIN()\n    FOR i = 1 TO 10\n        PRINT i\n    END FOR\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as ForStmt;
        Program.Check("Parser.ForStmt.IsFor", stmt != null);
        if (stmt != null)
        {
            Program.Check("Parser.ForStmt.VarName", stmt.Variable == "i");
            Program.Check("Parser.ForStmt.Body", stmt.Body.Count == 1);
        }
    }

    private static void TestWhileLoop()
    {
        var source = "FUNCTION MAIN()\n    WHILE x > 0\n        PRINT x\n    END WHILE\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as WhileStmt;
        Program.Check("Parser.WhileStmt.IsWhile", stmt != null);
        if (stmt != null)
        {
            Program.Check("Parser.WhileStmt.Body", stmt.Body.Count == 1);
        }
    }

    private static void TestNestedBlocks()
    {
        var source = "FUNCTION MAIN()\n    IF x THEN\n        IF y THEN\n            PRINT 1\n        END IF\n    END IF\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as IfStmt;
        Program.Check("Parser.NestedBlocks.OuterIf", stmt != null);
        if (stmt != null && stmt.ThenBody.Count > 0)
        {
            var inner = stmt.ThenBody[0] as IfStmt;
            Program.Check("Parser.NestedBlocks.InnerIf", inner != null);
        }
    }

    private static void TestEmptyParameterList()
    {
        var source = "FUNCTION FOO()\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        Program.Check("Parser.EmptyParams.ZeroParams", func.Parameters.Count == 0);
    }

    private static void TestEmptyArgumentList()
    {
        var source = "FUNCTION MAIN()\n    PRINT FOO()\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.EmptyArgs.PrintStmt", stmt != null);
        if (stmt != null && stmt.Expressions.Count > 0)
        {
            var call = stmt.Expressions[0] as CallExpr;
            Program.Check("Parser.EmptyArgs.IsCall", call != null);
            if (call != null)
            {
                Program.Check("Parser.EmptyArgs.ZeroArgs", call.Args.Count == 0);
            }
        }
    }

    private static void TestParserErrorInvalidCharacter()
    {
        bool threw = false;
        try
        {
            var lexer = new Lexer("@");
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            parser.Parse();
        }
        catch
        {
            threw = true;
        }
        Program.Check("Parser.InvalidChar.Throws", threw);
    }

    private static void TestErrorSourceLocation()
    {
        bool threwWithLocation = false;
        try
        {
            var lexer = new Lexer("FUNCTION MAIN()\n    @\nEND FUNCTION");
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            parser.Parse();
        }
        catch (SyntaxException ex)
        {
            threwWithLocation = ex.Location.Line > 0;
        }
        Program.Check("Parser.ErrorLocation.HasLine", threwWithLocation);
    }

    private static void TestAstChildOrdering()
    {
        var source = "FUNCTION MAIN()\n    PRINT 1, 2, 3\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.AstOrdering.PrintStmt", stmt != null);
        if (stmt != null)
        {
            Program.Check("Parser.AstOrdering.ThreeExpressions", stmt.Expressions.Count == 3);
        }
    }
}
