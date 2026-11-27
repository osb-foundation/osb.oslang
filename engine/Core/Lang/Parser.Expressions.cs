using Osb.Engine.Core.Collections;

namespace Osb.Engine.Core.Lang;

public partial class Parser
{
    private Expr ParseExpression() => ParseOr();

    private Expr ParseOr()
    {
        var left = ParseAnd();
        while (Check(TokenType.Or))
        {
            var op = Current.Text;
            Advance();
            var right = ParseAnd();
            left = new BinaryExpr(op, left, right, left.Location);
        }
        return left;
    }

    private Expr ParseAnd()
    {
        var left = ParseEquality();
        while (Check(TokenType.And))
        {
            var op = Current.Text;
            Advance();
            var right = ParseEquality();
            left = new BinaryExpr(op, left, right, left.Location);
        }
        return left;
    }

    private Expr ParseEquality()
    {
        var left = ParseComparison();
        while (Check(TokenType.Equal) || Check(TokenType.NotEqual) || Check(TokenType.Assign))
        {
            var op = Current.Text;
            Advance();
            var right = ParseComparison();
            left = new BinaryExpr(op, left, right, left.Location);
        }
        return left;
    }

    private Expr ParseComparison()
    {
        var left = ParseTerm();
        while (Check(TokenType.Less) || Check(TokenType.Greater) || Check(TokenType.LessEqual) || Check(TokenType.GreaterEqual))
        {
            var op = Current.Text;
            Advance();
            var right = ParseTerm();
            left = new BinaryExpr(op, left, right, left.Location);
        }
        return left;
    }

    private Expr ParseTerm()
    {
        var left = ParseFactor();
        while (Check(TokenType.Plus) || Check(TokenType.Minus))
        {
            var op = Current.Text;
            Advance();
            var right = ParseFactor();
            left = new BinaryExpr(op, left, right, left.Location);
        }
        return left;
    }

    private Expr ParseFactor()
    {
        var left = ParseUnary();
        while (Check(TokenType.Star) || Check(TokenType.Slash) || Check(TokenType.Percent))
        {
            var op = Current.Text;
            Advance();
            var right = ParseUnary();
            left = new BinaryExpr(op, left, right, left.Location);
        }
        return left;
    }

    private Expr ParseUnary()
    {
        if (Check(TokenType.Not) || Check(TokenType.Minus) || Check(TokenType.Plus))
        {
            var op = Current.Text;
            var start = Current.Location;
            Advance();
            var operand = ParseUnary();
            return new UnaryExpr(op, operand, start);
        }
        return ParsePower();
    }

    private Expr ParsePower()
    {
        var left = ParsePostfix();
        if (Check(TokenType.StarStar))
        {
            var op = Current.Text;
            Advance();
            var right = ParsePower();
            return new BinaryExpr(op, left, right, left.Location);
        }
        return left;
    }

    private Expr ParsePostfix()
    {
        var expr = ParsePrimary();
        while (true)
        {
            if (Check(TokenType.LBracket))
            {
                var loc = Current.Location;
                Advance(); // [
                var index = ParseExpression();
                Expect(TokenType.RBracket, "Expected ']' after array index");
                expr = new IndexExpr(expr, index, loc);
            }
            else if (Check(TokenType.Dot))
            {
                var loc = Current.Location;
                Advance(); // .
                var member = ExpectMemberName("Expected member name after '.'");
                if (Check(TokenType.LParen))
                {
                    Advance(); // (
                    var args = ParseArgList();
                    expr = new MethodCallExpr(expr, member, args, loc);
                }
                else
                {
                    expr = new MemberAccessExpr(expr, member, loc);
                }
            }
            else if (Check(TokenType.PlusPlus) || Check(TokenType.MinusMinus))
            {
                var op = Current.Text;
                var loc = Current.Location;
                Advance();
                expr = new PostfixExpr(op, expr, loc);
            }
            else if (expr is NamespaceExpr ns && Check(TokenType.LParen))
            {
                // Namespace function call: DATE(...), TIME(...), MATH.PI(...) etc.
                Advance(); // (
                var args = ParseArgList();
                expr = new NamespaceCallExpr(ns.NamespaceName, args, ns.Location);
            }
            else
            {
                break;
            }
        }
        return expr;
    }

    private Expr ParsePostfixStarting(Expr initial)
    {
        var expr = initial;
        while (true)
        {
            if (Check(TokenType.LBracket))
            {
                var loc = Current.Location;
                Advance();
                var index = ParseExpression();
                Expect(TokenType.RBracket, "Expected ']' after array index");
                expr = new IndexExpr(expr, index, loc);
            }
            else if (Check(TokenType.Dot))
            {
                var loc = Current.Location;
                Advance();
                var member = ExpectMemberName("Expected member name after '.'");
                if (Check(TokenType.LParen))
                {
                    Advance();
                    var args = ParseArgList();
                    expr = new MethodCallExpr(expr, member, args, loc);
                }
                else
                {
                    expr = new MemberAccessExpr(expr, member, loc);
                }
            }
            else
            {
                break;
            }
        }
        return expr;
    }

    private Expr ParsePrimary()
    {
        var tok = Current;
#if FREESTANDING
        OsbKT.Syscall.PrintString("DBG: ParsePrimary: tok.Type=");
        if (tok.Type == TokenType.StringLiteral) OsbKT.Syscall.PrintString("StringLiteral");
        else if (tok.Type == TokenType.NumberLiteral) OsbKT.Syscall.PrintString("NumberLiteral");
        else if (tok.Type == TokenType.Identifier) OsbKT.Syscall.PrintString("Identifier");
        else OsbKT.Syscall.PrintString("OTHER");
        OsbKT.Syscall.PrintString("\r\n");
#endif

        if (tok.Type == TokenType.StringLiteral)
        {
#if FREESTANDING
            OsbKT.Syscall.PrintString("DBG: ParsePrimary: StringLiteral case, StringValue=");
            OsbKT.Syscall.PrintString(tok.StringValue ?? "NULL");
            OsbKT.Syscall.PrintString("\r\n");
#endif
            return MakePrimary(new StringLiteralExpr(tok.StringValue!, tok.Location));
        }

        return tok.Type switch
        {
            TokenType.NumberLiteral => MakePrimary(new NumberLiteralExpr(tok.NumberValue, tok.Location)),
            TokenType.DateLiteral => MakePrimary(new DateLiteralExpr(tok.IntValue, tok.Location)),
            TokenType.TimeLiteral => MakePrimary(new TimeLiteralExpr(tok.LongValue, tok.Location)),
            TokenType.True => MakePrimary(new BooleanLiteralExpr(true, tok.Location)),
            TokenType.False => MakePrimary(new BooleanLiteralExpr(false, tok.Location)),
            TokenType.Null => MakePrimary(new NullLiteralExpr(tok.Location)),
            TokenType.LParen => ParseParenOrArrow(),
            TokenType.LBracket => ParseArrayLiteral(),
            TokenType.Me => MakePrimary(new MeExpr(Current.Location)),
            TokenType.Base => MakePrimary(new BaseExpr(Current.Location)),
            TokenType.New => ParseNewExpression(),
            TokenType.Identifier => ParseIdentifierOrArrow(),
            TokenType.Math => MakePrimary(new NamespaceExpr("MATH", Current.Location)),
            TokenType.File => MakePrimary(new NamespaceExpr("FILE", Current.Location)),
            TokenType.Dir => MakePrimary(new NamespaceExpr("DIR", Current.Location)),
            TokenType.Osl => MakePrimary(new NamespaceExpr("OSL", Current.Location)),
            TokenType.Date => MakePrimary(new NamespaceExpr("DATE", Current.Location)),
            TokenType.Time => MakePrimary(new NamespaceExpr("TIME", Current.Location)),
            TokenType.Osb => MakePrimary(new NamespaceExpr("OSB", Current.Location)),
            _ when IsBuiltinCallKeyword(tok.Type) => ParseBuiltinCall(tok),
            _ => throw new SyntaxException(tok.Location, $"Unexpected token '{tok.Text}' in expression."),
        };
    }

    private Expr ParseParenOrArrow()
    {
        var start = Current.Location;
        Advance(); // (

        if (Check(TokenType.RParen))
        {
            Advance(); // )
            if (Check(TokenType.DoubleArrow))
            {
                Advance(); // =>
                throw new SyntaxException(start, "Arrow functions (=>) are not supported in OSLANG 0.73.");
            }
            throw new SyntaxException(start, "Empty parentheses are not valid as an expression.");
        }

        var expr = ParseExpression();
        Expect(TokenType.RParen, "Expected ')' after expression");
        return expr;
    }

    private Expr ParseArrayLiteral()
    {
        var start = Current.Location;
        Advance(); // [

        var elements = new NodeList<Expr>();
        if (!Check(TokenType.RBracket))
        {
            elements.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                elements.Add(ParseExpression());
            }
        }
        Expect(TokenType.RBracket, "Expected ']' after array literal");
        return new ArrayLiteralExpr(elements, start);
    }

    private Expr ParseNewExpression()
    {
        var start = Current.Location;
        Advance(); // NEW
        var className = ExpectIdentifier("Expected class name after NEW");
        Expect(TokenType.LParen, "Expected '(' after class name");
        var args = ParseArgList();
        return new NewExpr(className, args, start);
    }

    private NodeList<Expr> ParseArgList()
    {
        var args = new NodeList<Expr>();
        if (!Check(TokenType.RParen))
        {
            args.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                args.Add(ParseExpression());
            }
        }
        Expect(TokenType.RParen, "Expected ')' after argument list");
        return args;
    }

    private static readonly TokenTypeSet BuiltinCallKeywords = new(
        TokenType.Sqrt, TokenType.Pow, TokenType.Floor, TokenType.Ceil,
        TokenType.Str, TokenType.Number, TokenType.Bool, TokenType.Count, TokenType.TypeOf
    );

    private bool IsBuiltinCallKeyword(TokenType type) => BuiltinCallKeywords.Contains(type);

    private Expr ParseBuiltinCall(Token tok)
    {
        var start = tok.Location;
        Advance(); // consume keyword
        Expect(TokenType.LParen, $"Expected '(' after {tok.Text}");
        var args = ParseArgList();
        return new CallExpr(tok.Text, args, start);
    }

    private Expr ParseIdentifierOrArrow()
    {
        var tok = Advance(); // consume identifier

        if (Check(TokenType.DoubleArrow))
        {
            Advance(); // =>
            // Arrow function - not in 0.73 per SQ-003 (deferred)
            throw new SyntaxException(tok.Location, "Arrow functions (=>) are not supported in OSLANG 0.73.");
        }

        if (Check(TokenType.LParen))
        {
            Advance(); // (
            var args = ParseArgList();
            return new CallExpr(tok.Text, args, tok.Location);
        }

        return new IdentifierExpr(tok.Text, tok.Location);
    }
}
