using Osb.Engine.Core.Collections;

namespace Osb.Engine.Core.Lang;

public partial class Parser
{
    private UsingDecl ParseUsing()
    {
        var start = Current.Location;
        Advance(); // USING
        var name = ExpectIdentifier("Expected module name after USING");
        Expect(TokenType.Newline, "Expected end of line after USING");
        return new UsingDecl(name, start);
    }

    private FunctionDecl ParseFunction()
    {
        var start = Current.Location;
        Advance(); // FUNCTION
        var name = ExpectIdentifier("Expected function name after FUNCTION");
        Expect(TokenType.LParen, "Expected '(' after function name");

        var parameters = new NodeList<ParameterDecl>();
        if (!Check(TokenType.RParen))
        {
            parameters.Add(ParseParameter());
            while (Check(TokenType.Comma))
            {
                Advance();
                parameters.Add(ParseParameter());
            }
        }
        Expect(TokenType.RParen, "Expected ')' after parameter list");
        Expect(TokenType.Newline, "Expected end of line after function header");

        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close function");
        if (Check(TokenType.Function)) Advance(); // optional END FUNCTION

        return new FunctionDecl(name, parameters, body, start);
    }

    private ParameterDecl ParseParameter()
    {
        var start = Current.Location;
        var name = ExpectIdentifier("Expected parameter name");
        string? typeAnnotation = null;
        if (CheckAny(TokenType.Number, TokenType.String, TokenType.Boolean))
        {
            typeAnnotation = Current.Text;
            Advance();
        }
        return new ParameterDecl(name, typeAnnotation, start);
    }

    private ClassDecl ParseClass()
    {
        var start = Current.Location;
        Advance(); // CLASS
        var name = ExpectIdentifier("Expected class name after CLASS");

        var baseClasses = new NodeList<string>();
        if (Check(TokenType.Colon))
        {
            Advance(); // :
            baseClasses.Add(ExpectIdentifier("Expected base class or interface name after ':'"));
            while (Check(TokenType.Comma))
            {
                Advance();
                baseClasses.Add(ExpectIdentifier("Expected interface name after ','"));
            }
        }

        var members = new NodeList<MemberDecl>();
        while (!Check(TokenType.End) && !IsAtEnd)
        {
            var visibility = ParseVisibility();
            if (Check(TokenType.Var))
            {
                members.Add(ParseProperty(visibility));
            }
            else if (Check(TokenType.Constructor))
            {
                members.Add(ParseConstructor(visibility));
            }
            else if (Check(TokenType.Function))
            {
                Advance(); // FUNCTION
                members.Add(ParseMethod(visibility));
            }
            else if (Check(TokenType.Identifier))
            {
                // Implicit method (no FUNCTION keyword)
                members.Add(ParseMethod(visibility));
            }
            else if (Check(TokenType.Newline))
            {
                Advance();
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Text}' in class body. Expected VAR, CONSTRUCTOR, FUNCTION, method name, or END.");
            }
        }

        Expect(TokenType.End, "Expected END to close class");
        if (Check(TokenType.Class)) Advance(); // optional END CLASS

        return new ClassDecl(name, baseClasses, members, start);
    }

    private Visibility ParseVisibility()
    {
        if (Check(TokenType.Public)) { Advance(); return Visibility.Public; }
        if (Check(TokenType.Protected)) { Advance(); return Visibility.Protected; }
        if (Check(TokenType.Private)) { Advance(); return Visibility.Private; }
        return Visibility.Public; // default
    }

    private PropertyDecl ParseProperty(Visibility visibility)
    {
        var start = Current.Location;
        Advance(); // VAR
        var name = ExpectIdentifier("Expected property name after VAR");
        string? typeAnnotation = null;
        if (CheckAny(TokenType.Number, TokenType.String, TokenType.Boolean))
        {
            typeAnnotation = Current.Text;
            Advance();
        }
        Expr? defaultValue = null;
        if (Check(TokenType.Assign))
        {
            Advance();
            defaultValue = ParseExpression();
        }
        Expect(TokenType.Newline, "Expected end of line after property declaration");
        return new PropertyDecl(visibility, name, typeAnnotation, defaultValue, start);
    }

    private ConstructorDecl ParseConstructor(Visibility visibility)
    {
        var start = Current.Location;
        Advance(); // CONSTRUCTOR
        Expect(TokenType.LParen, "Expected '(' after CONSTRUCTOR");

        var parameters = new NodeList<ParameterDecl>();
        if (!Check(TokenType.RParen))
        {
            parameters.Add(ParseParameter());
            while (Check(TokenType.Comma))
            {
                Advance();
                parameters.Add(ParseParameter());
            }
        }
        Expect(TokenType.RParen, "Expected ')' after constructor parameter list");

        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close constructor");
        if (Check(TokenType.Constructor)) Advance(); // optional END CONSTRUCTOR

        return new ConstructorDecl(visibility, parameters, body, start);
    }

    private MethodDecl ParseMethod(Visibility visibility)
    {
        var start = Current.Location;
        var name = ExpectIdentifier("Expected method name");
        Expect(TokenType.LParen, "Expected '(' after method name");

        var parameters = new NodeList<ParameterDecl>();
        if (!Check(TokenType.RParen))
        {
            parameters.Add(ParseParameter());
            while (Check(TokenType.Comma))
            {
                Advance();
                parameters.Add(ParseParameter());
            }
        }
        Expect(TokenType.RParen, "Expected ')' after method parameter list");
        Expect(TokenType.Newline, "Expected end of line after method header");

        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close method");
        if (Check(TokenType.Function)) Advance(); // optional END FUNCTION

        return new MethodDecl(visibility, name, parameters, body, start);
    }

    private InterfaceDecl ParseInterface()
    {
        var start = Current.Location;
        Advance(); // INTERFACE
        var name = ExpectIdentifier("Expected interface name after INTERFACE");

        var baseInterfaces = new NodeList<string>();
        if (Check(TokenType.Colon))
        {
            Advance();
            baseInterfaces.Add(ExpectIdentifier("Expected base interface name after ':'"));
            while (Check(TokenType.Comma))
            {
                Advance();
                baseInterfaces.Add(ExpectIdentifier("Expected interface name after ','"));
            }
        }

        var methods = new NodeList<MethodDecl>();
        while (!Check(TokenType.End) && !IsAtEnd)
        {
            var visibility = ParseVisibility();
            if (Check(TokenType.Function))
            {
                Advance();
                methods.Add(ParseMethod(visibility));
            }
            else if (Check(TokenType.Identifier))
            {
                methods.Add(ParseMethod(visibility));
            }
            else if (Check(TokenType.Newline))
            {
                Advance();
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Text}' in interface body. Expected FUNCTION, method name, or END.");
            }
        }

        Expect(TokenType.End, "Expected END to close interface");
        if (Check(TokenType.Interface)) Advance();

        return new InterfaceDecl(name, baseInterfaces, methods, start);
    }

    private EnumDecl ParseEnum()
    {
        var start = Current.Location;
        Advance(); // ENUM
        var name = ExpectIdentifier("Expected enum name after ENUM");

        var members = new NodeList<EnumMemberDecl>();
        while (!Check(TokenType.End) && !IsAtEnd)
        {
            if (Check(TokenType.Identifier))
            {
                var memberStart = Current.Location;
                var memberName = ExpectIdentifier("");
                Expr? value = null;
                if (Check(TokenType.Assign))
                {
                    Advance();
                    value = ParseExpression();
                }
                Expect(TokenType.Newline, "Expected end of line after enum member");
                members.Add(new EnumMemberDecl(memberName, value, memberStart));
            }
            else if (Check(TokenType.Newline))
            {
                Advance();
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Text}' in enum body. Expected identifier or END.");
            }
        }

        Expect(TokenType.End, "Expected END to close enum");
        if (Check(TokenType.Enum)) Advance(); // optional END ENUM

        return new EnumDecl(name, members, start);
    }

    private EventDecl ParseEvent()
    {
        var start = Current.Location;
        Advance(); // EVENT
        var name = ExpectIdentifier("Expected event name after EVENT");

        var parameters = new NodeList<ParameterDecl>();
        if (Check(TokenType.LParen))
        {
            Advance();
            if (!Check(TokenType.RParen))
            {
                parameters.Add(ParseParameter());
                while (Check(TokenType.Comma))
                {
                    Advance();
                    parameters.Add(ParseParameter());
                }
            }
            Expect(TokenType.RParen, "Expected ')' after event parameter list");
        }
        Expect(TokenType.Newline, "Expected end of line after event declaration");

        return new EventDecl(name, parameters, start);
    }

    private NodeList<Stmt> ParseBlockUntil(TokenType terminator)
    {
        var statements = new NodeList<Stmt>();
        while (!Check(terminator) && !IsAtEnd)
        {
            SkipNewlines();
            if (Check(terminator) || IsAtEnd) break;
            statements.Add(ParseStatement());
        }
        return statements;
    }

    private NodeList<Stmt> ParseBlockUntil(TokenType terminator1, TokenType terminator2, TokenType terminator3)
    {
        var statements = new NodeList<Stmt>();
        while (!Check(terminator1) && !Check(terminator2) && !Check(terminator3) && !IsAtEnd)
        {
            SkipNewlines();
            if (Check(terminator1) || Check(terminator2) || Check(terminator3) || IsAtEnd) break;
            statements.Add(ParseStatement());
        }
        return statements;
    }

    private NodeList<Stmt> ParseBlockUntil(TokenType terminator1, TokenType terminator2)
    {
        var statements = new NodeList<Stmt>();
        while (!Check(terminator1) && !Check(terminator2) && !IsAtEnd)
        {
            SkipNewlines();
            if (Check(terminator1) || Check(terminator2) || IsAtEnd) break;
            statements.Add(ParseStatement());
        }
        return statements;
    }
}
