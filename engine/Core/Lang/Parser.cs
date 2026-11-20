using Osb.Engine.Core.Collections;

namespace Osb.Engine.Core.Lang;

/// <summary>
/// Recursive descent parser for OSLANG.
/// Produces arena-allocated AST nodes (no BCL allocations in freestanding target).
/// Uses freestanding collections (NodeList<T>, TokenTypeSet) instead of BCL collections.
/// </summary>
public partial class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _pos;

    public Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    public Program Parse()
    {
        var functions = new NodeList<FunctionDecl>();
        var classes = new NodeList<ClassDecl>();
        var interfaces = new NodeList<InterfaceDecl>();
        var enums = new NodeList<EnumDecl>();
        var usings = new NodeList<UsingDecl>();
        var events = new NodeList<EventDecl>();

        while (!IsAtEnd && Current.Type != TokenType.Eof)
        {
            SkipNewlines();
            if (IsAtEnd || Current.Type == TokenType.Eof) break;

            if (Current.Type == TokenType.Using)
            {
                usings.Add(ParseUsing());
            }
            else if (Current.Type == TokenType.Function)
            {
                functions.Add(ParseFunction());
            }
            else if (Current.Type == TokenType.Class)
            {
                classes.Add(ParseClass());
            }
            else if (Current.Type == TokenType.Interface)
            {
                interfaces.Add(ParseInterface());
            }
            else if (Current.Type == TokenType.Enum)
            {
                enums.Add(ParseEnum());
            }
            else if (Current.Type == TokenType.Event)
            {
                events.Add(ParseEvent());
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Text}' at top level. Expected USING, FUNCTION, CLASS, INTERFACE, ENUM, or EVENT.");
            }
        }

        return new Program(functions, classes, interfaces, enums, usings, events);
    }

    private bool Check(TokenType type) => !IsAtEnd && Current.Type == type;
    private bool CheckAny(params TokenType[] types) => !IsAtEnd && types.Contains(Current.Type);

    private Token Current => IsAtEnd ? _tokens[^1] : _tokens[_pos];
    private bool IsAtEnd => _pos >= _tokens.Count || _tokens[_pos].Type == TokenType.Eof;

    private Token Advance()
    {
        var current = Current;
        if (!IsAtEnd) _pos++;
        return current;
    }

    private void SkipNewlines()
    {
        while (Check(TokenType.Newline)) Advance();
    }

    private string ExpectIdentifier(string errorMessage)
    {
        if (!Check(TokenType.Identifier))
            throw new SyntaxException(Current.Location, errorMessage);
        var name = Current.Text;
        Advance();
        return name;
    }

    private string ExpectMemberName(string errorMessage)
    {
        // Member names can be identifiers or keywords
        if (!Check(TokenType.Identifier) && !IsKeyword(Current.Type))
            throw new SyntaxException(Current.Location, errorMessage);
        var name = Current.Text;
        Advance();
        return name;
    }

    private void Expect(TokenType type, string errorMessage)
    {
        if (!Check(type))
            throw new SyntaxException(Current.Location, $"{errorMessage}. Found '{Current.Text}'.");
        Advance();
    }

    private Stmt MakeStmt(Stmt stmt) { Expect(TokenType.Newline, "Expected end of line after statement"); return stmt; }

    private T MakePrimary<T>(T expr) where T : Expr
    {
        Advance();
        return expr;
    }

    private static bool IsKeyword(TokenType type) => type switch
    {
        // Structural tokens that cannot be member names
        TokenType.Newline or TokenType.Eof or
        TokenType.LParen or TokenType.RParen or
        TokenType.LBracket or TokenType.RBracket or
        TokenType.LBrace or TokenType.RBrace or
        TokenType.Comma or TokenType.Colon or
        TokenType.Dot or TokenType.DoubleArrow or
        TokenType.Plus or TokenType.Minus or
        TokenType.Star or TokenType.Slash or
        TokenType.Percent or TokenType.PlusPlus or
        TokenType.MinusMinus or TokenType.PlusEqual or
        TokenType.Equal or TokenType.NotEqual or
        TokenType.Less or TokenType.Greater or
        TokenType.LessEqual or TokenType.GreaterEqual or
        TokenType.Assign or TokenType.End or
        TokenType.Then or TokenType.Else or
        TokenType.Elif or TokenType.To or
        TokenType.Step or TokenType.While or
        TokenType.Catch or TokenType.Case or
        TokenType.Default or TokenType.Pipe =>
            false,
        _ => true, // All other tokens (identifiers, keywords, literals) can be member names
    };

    private static AssignTarget ToAssignTarget(Expr expr) => expr switch
    {
        IdentifierExpr id => new VariableTarget(id.Name, id.Location),
        IndexExpr ix => new IndexTarget(ix.Array, ix.Index, ix.Location),
        MemberAccessExpr ma => new MemberTarget(ma.Object, ma.MemberName, ma.Location),
        _ => throw new SyntaxException(expr.Location, "Invalid assignment target."),
    };
}
