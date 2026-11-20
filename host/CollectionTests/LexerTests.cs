using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;

namespace CollectionTests;

internal static class LexerTests
{
    internal static void RunAll()
    {
        TestEmptySource();
        TestIntegerLiteral();
        TestDecimalLiteral();
        TestIdentifier();
        TestKeyword();
        TestCaseInsensitiveKeyword();
        TestUnknownIdentifier();
        TestStringLiteral();
        TestEmptyString();
        TestEscapeSequences();
        TestCommentSingleQuote();
        TestCommentRem();
        TestBlockComment();
        TestOperators();
        TestPunctuation();
        TestWhitespace();
        TestNewlines();
        TestInvalidCharacter();
        TestUnterminatedString();
        TestEofToken();
        TestLineColumnTracking();
        TestAllKeywords();
        TestDateLiteral();
        TestTimeLiteral();
    }

    private static void TestEmptySource()
    {
        var lexer = new Lexer("");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.EmptySource.EofToken", tokens.Count == 1);
        Program.Check("Lexer.EmptySource.TokenType", tokens[0].Type == TokenType.Eof);
        Program.Check("Lexer.EmptySource.TokenText", tokens[0].Text == "");
    }

    private static void TestIntegerLiteral()
    {
        var lexer = new Lexer("42");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Integer.Count", tokens.Count == 2);
        Program.Check("Lexer.Integer.Type", tokens[0].Type == TokenType.NumberLiteral);
        Program.Check("Lexer.Integer.Text", tokens[0].Text == "42");
        Program.Check("Lexer.Integer.Value", tokens[0].NumberValue == 42.0);
    }

    private static void TestDecimalLiteral()
    {
        var lexer = new Lexer("3.14");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Decimal.Type", tokens[0].Type == TokenType.NumberLiteral);
        Program.Check("Lexer.Decimal.Text", tokens[0].Text == "3.14");
        Program.Check("Lexer.Decimal.Value", tokens[0].NumberValue == 3.14);
    }

    private static void TestIdentifier()
    {
        var lexer = new Lexer("myVar");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Identifier.Type", tokens[0].Type == TokenType.Identifier);
        Program.Check("Lexer.Identifier.Text", tokens[0].Text == "myVar");
    }

    private static void TestKeyword()
    {
        var lexer = new Lexer("PRINT");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Keyword.Type", tokens[0].Type == TokenType.Print);
        Program.Check("Lexer.Keyword.Text", tokens[0].Text == "PRINT");
    }

    private static void TestCaseInsensitiveKeyword()
    {
        var lexer = new Lexer("print");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.CaseInsensitive.Type", tokens[0].Type == TokenType.Print);
        Program.Check("Lexer.CaseInsensitive.Text", tokens[0].Text == "print");
    }

    private static void TestUnknownIdentifier()
    {
        var lexer = new Lexer("xyzzy");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Unknown.Type", tokens[0].Type == TokenType.Identifier);
        Program.Check("Lexer.Unknown.Text", tokens[0].Text == "xyzzy");
    }

    private static void TestStringLiteral()
    {
        var lexer = new Lexer("\"hello\"");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.String.Type", tokens[0].Type == TokenType.StringLiteral);
        Program.Check("Lexer.String.Value", tokens[0].StringValue == "hello");
    }

    private static void TestEmptyString()
    {
        var lexer = new Lexer("\"\"");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.EmptyString.Type", tokens[0].Type == TokenType.StringLiteral);
        Program.Check("Lexer.EmptyString.Value", tokens[0].StringValue == "");
    }

    private static void TestEscapeSequences()
    {
        var lexer = new Lexer("\"a\\nb\\tc\\\"d\"");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Escape.Type", tokens[0].Type == TokenType.StringLiteral);
        var value = tokens[0].StringValue;
        Program.Check("Lexer.Escape.Newline", value.Contains("\n"));
        Program.Check("Lexer.Escape.Tab", value.Contains("\t"));
        Program.Check("Lexer.Escape.Quote", value.Contains("\""));
    }

    private static void TestCommentSingleQuote()
    {
        var lexer = new Lexer("' this is a comment\nPRINT");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.CommentQuote.SkipComment", tokens.Count >= 2);
        bool hasPrint = false;
        for (int i = 0; i < tokens.Count; i++)
            if (tokens[i].Type == TokenType.Print) hasPrint = true;
        Program.Check("Lexer.CommentQuote.HasPrint", hasPrint);
    }

    private static void TestCommentRem()
    {
        var lexer = new Lexer("REM this is a comment\nPRINT");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.CommentRem.SkipComment", tokens.Count >= 2);
        bool hasPrint = false;
        for (int i = 0; i < tokens.Count; i++)
            if (tokens[i].Type == TokenType.Print) hasPrint = true;
        Program.Check("Lexer.CommentRem.HasPrint", hasPrint);
    }

    private static void TestBlockComment()
    {
        var lexer = new Lexer("/* block comment */PRINT");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.BlockComment.SkipComment", tokens.Count >= 1);
        bool hasPrint = false;
        for (int i = 0; i < tokens.Count; i++)
            if (tokens[i].Type == TokenType.Print) hasPrint = true;
        Program.Check("Lexer.BlockComment.HasPrint", hasPrint);
    }

    private static void TestOperators()
    {
        var lexer = new Lexer("+-*/%");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Operators.Plus", tokens[0].Type == TokenType.Plus);
        Program.Check("Lexer.Operators.Minus", tokens[1].Type == TokenType.Minus);
        Program.Check("Lexer.Operators.Star", tokens[2].Type == TokenType.Star);
        Program.Check("Lexer.Operators.Slash", tokens[3].Type == TokenType.Slash);
        Program.Check("Lexer.Operators.Percent", tokens[4].Type == TokenType.Percent);
    }

    private static void TestPunctuation()
    {
        var lexer = new Lexer("()[]{}");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Punct.LParen", tokens[0].Type == TokenType.LParen);
        Program.Check("Lexer.Punct.RParen", tokens[1].Type == TokenType.RParen);
        Program.Check("Lexer.Punct.LBracket", tokens[2].Type == TokenType.LBracket);
        Program.Check("Lexer.Punct.RBracket", tokens[3].Type == TokenType.RBracket);
        Program.Check("Lexer.Punct.LBrace", tokens[4].Type == TokenType.LBrace);
        Program.Check("Lexer.Punct.RBrace", tokens[5].Type == TokenType.RBrace);
    }

    private static void TestWhitespace()
    {
        var lexer = new Lexer("  \t  PRINT");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Whitespace.Skipped", tokens.Count == 2);
        Program.Check("Lexer.Whitespace.Print", tokens[0].Type == TokenType.Print);
    }

    private static void TestNewlines()
    {
        var lexer = new Lexer("PRINT\nPRINT");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Newline.FirstPrint", tokens[0].Type == TokenType.Print);
        Program.Check("Lexer.Newline.Newline", tokens[1].Type == TokenType.Newline);
        Program.Check("Lexer.Newline.SecondPrint", tokens[2].Type == TokenType.Print);
    }

    private static void TestInvalidCharacter()
    {
        bool threw = false;
        try
        {
            var lexer = new Lexer("@");
            var tokens = lexer.Tokenize();
        }
        catch
        {
            threw = true;
        }
        Program.Check("Lexer.InvalidChar.Throws", threw);
    }

    private static void TestUnterminatedString()
    {
        var lexer = new Lexer("\"unterminated");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.UnterminatedString.EofAfter", tokens.Count == 2);
        Program.Check("Lexer.UnterminatedString.EofType", tokens[1].Type == TokenType.Eof);
    }

    private static void TestEofToken()
    {
        var lexer = new Lexer("PRINT");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Eof.LastToken", tokens[tokens.Count - 1].Type == TokenType.Eof);
        Program.Check("Lexer.Eof.Count", tokens.Count == 2);
    }

    private static void TestLineColumnTracking()
    {
        var lexer = new Lexer("PRINT\n  X");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.LineColumn.PrintLine", tokens[0].Location.Line == 1);
        Program.Check("Lexer.LineColumn.PrintColumn", tokens[0].Location.Column == 1);
        var xToken = tokens[2];
        Program.Check("Lexer.LineColumn.XLine", xToken.Location.Line == 2);
        Program.Check("Lexer.LineColumn.XColumn", xToken.Location.Column == 3);
    }

    private static void TestAllKeywords()
    {
        var keywords = new[]
        {
            ("AND", TokenType.And), ("BASE", TokenType.Base), ("BOOLEAN", TokenType.Boolean),
            ("BOOL", TokenType.Bool), ("BREAK", TokenType.Break), ("CATCH", TokenType.Catch),
            ("CEIL", TokenType.Ceil), ("CLASS", TokenType.Class), ("CLEAR", TokenType.Clear),
            ("CONSTRUCTOR", TokenType.Constructor), ("CONTINUE", TokenType.Continue),
            ("COUNT", TokenType.Count), ("DO", TokenType.Do), ("ELIF", TokenType.Elif),
            ("ELSE", TokenType.Else), ("END", TokenType.End), ("FALSE", TokenType.False),
            ("FLOOR", TokenType.Floor), ("FOR", TokenType.For), ("FUNCTION", TokenType.Function),
            ("GLOBAL", TokenType.Global), ("IF", TokenType.If), ("INPUT", TokenType.Input),
            ("INTERFACE", TokenType.Interface), ("ME", TokenType.Me), ("NEW", TokenType.New),
            ("NOT", TokenType.Not), ("NULL", TokenType.Null), ("NUMBER", TokenType.Number),
            ("OR", TokenType.Or), ("POW", TokenType.Pow), ("PRINT", TokenType.Print),
            ("PRIVATE", TokenType.Private), ("PROTECTED", TokenType.Protected),
            ("PUBLIC", TokenType.Public), ("RETURN", TokenType.Return), ("SQRT", TokenType.Sqrt),
            ("STEP", TokenType.Step), ("STRING", TokenType.String), ("STR", TokenType.Str),
            ("THEN", TokenType.Then), ("TO", TokenType.To), ("TRUE", TokenType.True),
            ("TRY", TokenType.Try), ("TYPEOF", TokenType.TypeOf), ("VAR", TokenType.Var),
            ("WHILE", TokenType.While), ("SWITCH", TokenType.Switch), ("CASE", TokenType.Case),
            ("DEFAULT", TokenType.Default), ("VIRTUAL", TokenType.Virtual),
            ("OVERRIDE", TokenType.Override), ("EVENT", TokenType.Event), ("ON", TokenType.On),
            ("RAISE", TokenType.Raise), ("USING", TokenType.Using), ("MATH", TokenType.Math),
            ("FILE", TokenType.File), ("DIR", TokenType.Dir), ("DATE", TokenType.Date),
            ("TIME", TokenType.Time), ("SHOW", TokenType.Show), ("MOD", TokenType.Mod),
            ("OSL", TokenType.Osl), ("ENUM", TokenType.Enum), ("IN", TokenType.In),
            ("EACH", TokenType.Each), ("OSB", TokenType.Osb),
        };

        foreach (var (text, expectedType) in keywords)
        {
            var lexer = new Lexer(text);
            var tokens = lexer.Tokenize();
            Program.Check($"Lexer.Keyword.{text}", tokens[0].Type == expectedType, $"got {tokens[0].Type}");
        }
    }

    private static void TestDateLiteral()
    {
        var lexer = new Lexer("#2024-01-15#");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Date.Type", tokens[0].Type == TokenType.DateLiteral);
    }

    private static void TestTimeLiteral()
    {
        var lexer = new Lexer("#14:30:00#");
        var tokens = lexer.Tokenize();
        Program.Check("Lexer.Time.Type", tokens[0].Type == TokenType.TimeLiteral);
    }
}
