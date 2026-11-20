using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;

namespace CollectionTests;

internal static partial class ParserTests
{
    private static void TestStringLiteral()
    {
        var source = "FUNCTION MAIN()\n    PRINT \"hello\"\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.StringLiteral.PrintStmt", stmt != null);
        if (stmt != null && stmt.Expressions.Count > 0)
        {
            Program.Check("Parser.StringLiteral.IsString", stmt.Expressions[0] is StringLiteralExpr);
        }
    }

    private static void TestBooleanLiteral()
    {
        var source = "FUNCTION MAIN()\n    PRINT TRUE\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.BoolLiteral.PrintStmt", stmt != null);
        if (stmt != null && stmt.Expressions.Count > 0)
        {
            Program.Check("Parser.BoolLiteral.IsBoolean", stmt.Expressions[0] is BooleanLiteralExpr);
        }
    }

    private static void TestNullLiteral()
    {
        var source = "FUNCTION MAIN()\n    PRINT NULL\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.NullLiteral.PrintStmt", stmt != null);
        if (stmt != null && stmt.Expressions.Count > 0)
        {
            Program.Check("Parser.NullLiteral.IsNull", stmt.Expressions[0] is NullLiteralExpr);
        }
    }

    private static void TestBuiltinCall()
    {
        var source = "FUNCTION MAIN()\n    PRINT Sqrt(16)\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.BuiltinCall.PrintStmt", stmt != null);
        if (stmt != null && stmt.Expressions.Count > 0)
        {
            Program.Check("Parser.BuiltinCall.IsCall", stmt.Expressions[0] is CallExpr);
        }
    }

    private static void TestSwitchStatement()
    {
        var source = "FUNCTION MAIN()\n    SWITCH x\n        CASE 1\n            PRINT 1\n        DEFAULT\n            PRINT 2\n    END SWITCH\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as SwitchStmt;
        Program.Check("Parser.SwitchStmt.IsSwitch", stmt != null);
        if (stmt != null)
        {
            Program.Check("Parser.SwitchStmt.OneCase", stmt.Cases.Count == 1);
            Program.Check("Parser.SwitchStmt.HasDefault", stmt.DefaultBody != null);
        }
    }

    private static void TestTryCatchStatement()
    {
        var source = "FUNCTION MAIN()\n    TRY\n        PRINT 1\n    CATCH\n        PRINT 2\n    END CATCH\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as TryCatchStmt;
        Program.Check("Parser.TryCatchStmt.IsTryCatch", stmt != null);
        if (stmt != null)
        {
            Program.Check("Parser.TryCatchStmt.TryBody", stmt.TryBody.Count == 1);
            Program.Check("Parser.TryCatchStmt.CatchBody", stmt.CatchBody.Count == 1);
        }
    }

    private static void TestReturnStatement()
    {
        var source = "FUNCTION MAIN()\n    RETURN 42\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as ReturnStmt;
        Program.Check("Parser.ReturnStmt.IsReturn", stmt != null);
        if (stmt != null)
        {
            Program.Check("Parser.ReturnStmt.HasValue", stmt.Value != null);
        }
    }

    private static void TestBreakContinue()
    {
        var source = "FUNCTION MAIN()\n    WHILE true\n        BREAK\n        CONTINUE\n    END WHILE\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as WhileStmt;
        Program.Check("Parser.BreakContinue.IsWhile", stmt != null);
        if (stmt != null)
        {
            Program.Check("Parser.BreakContinue.TwoStatements", stmt.Body.Count == 2);
        }
    }

    private static void TestClassDeclaration()
    {
        var source = "CLASS Foo\n    VAR x\nEND CLASS\n";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        Program.Check("Parser.ClassDecl.OneClass", program.Classes.Count == 1);
        if (program.Classes.Count > 0)
        {
            Program.Check("Parser.ClassDecl.Name", program.Classes[0].Name == "Foo");
            Program.Check("Parser.ClassDecl.OneMember", program.Classes[0].Members.Count == 1);
        }
    }

    private static void TestEnumDeclaration()
    {
        var source = "ENUM Color\nRED\nGREEN\nBLUE\nEND ENUM\n";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        Program.Check("Parser.EnumDecl.OneEnum", program.Enums.Count == 1);
        if (program.Enums.Count > 0)
        {
            Program.Check("Parser.EnumDecl.Name", program.Enums[0].Name == "Color");
            Program.Check("Parser.EnumDecl.ThreeMembers", program.Enums[0].Members.Count == 3);
        }
    }

    private static void TestEventDeclaration()
    {
        var source = "EVENT OnClick(x, y)\n";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        Program.Check("Parser.EventDecl.OneEvent", program.Events.Count == 1);
        if (program.Events.Count > 0)
        {
            Program.Check("Parser.EventDecl.Name", program.Events[0].Name == "OnClick");
            Program.Check("Parser.EventDecl.TwoParams", program.Events[0].Parameters.Count == 2);
        }
    }

    private static void TestNamespaceCall()
    {
        var source = "FUNCTION MAIN()\n    PRINT DATE()\nEND FUNCTION";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var func = program.Functions[0];
        var stmt = func.Body[0] as PrintStmt;
        Program.Check("Parser.NamespaceCall.PrintStmt", stmt != null);
        if (stmt != null && stmt.Expressions.Count > 0)
        {
            Program.Check("Parser.NamespaceCall.IsNamespaceCall", stmt.Expressions[0] is NamespaceCallExpr);
        }
    }
}
