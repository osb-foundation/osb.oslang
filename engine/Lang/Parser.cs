using System.Collections.Generic;

namespace Osb.Engine.Lang;

/// <summary>
/// Recursive descent parser for OSLANG.
/// Produces arena-allocated AST nodes (no BCL allocations in freestanding target).
/// </summary>
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _pos;

    public Parser(IReadOnlyList<Token> tokens) => _tokens = tokens;

    public Program Parse()
    {
        var functions = new List<FunctionDecl>();
        var classes = new List<ClassDecl>();
        var interfaces = new List<InterfaceDecl>();
        var enums = new List<EnumDecl>();
        var usings = new List<UsingDecl>();
        var events = new List<EventDecl>();

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

        var parameters = new List<ParameterDecl>();
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

        var baseClasses = new List<string>();
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

        var members = new List<MemberDecl>();
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

        var parameters = new List<ParameterDecl>();
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

        var parameters = new List<ParameterDecl>();
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

        var baseInterfaces = new List<string>();
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

        var methods = new List<MethodDecl>();
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

        var members = new List<EnumMemberDecl>();
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

        var parameters = new List<ParameterDecl>();
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

    private List<Stmt> ParseBlockUntil(TokenType terminator)
    {
        var statements = new List<Stmt>();
        while (!Check(terminator) && !IsAtEnd)
        {
            SkipNewlines();
            if (Check(terminator) || IsAtEnd) break;
            statements.Add(ParseStatement());
        }
        return statements;
    }

    private List<Stmt> ParseBlockUntil(TokenType terminator1, TokenType terminator2, TokenType terminator3)
    {
        var statements = new List<Stmt>();
        while (!Check(terminator1) && !Check(terminator2) && !Check(terminator3) && !IsAtEnd)
        {
            SkipNewlines();
            if (Check(terminator1) || Check(terminator2) || Check(terminator3) || IsAtEnd) break;
            statements.Add(ParseStatement());
        }
        return statements;
    }

    private List<Stmt> ParseBlockUntil(TokenType terminator1, TokenType terminator2)
    {
        var statements = new List<Stmt>();
        while (!Check(terminator1) && !Check(terminator2) && !IsAtEnd)
        {
            SkipNewlines();
            if (Check(terminator1) || Check(terminator2) || IsAtEnd) break;
            statements.Add(ParseStatement());
        }
        return statements;
    }

    private Stmt ParseStatement()
    {
        SkipNewlines();
        if (IsAtEnd) return new BlockStmt([], Current.Location);

        return Current.Type switch
        {
            TokenType.Var => ParseVarDecl(),
            TokenType.Global => ParseGlobalDecl(),
            TokenType.If => ParseIf(),
            TokenType.For => ParseFor(),
            TokenType.While => ParseWhile(),
            TokenType.Do => ParseDoWhile(),
            TokenType.Break => MakeStmt(new BreakStmt(Advance().Location)),
            TokenType.Continue => MakeStmt(new ContinueStmt(Advance().Location)),
            TokenType.Return => ParseReturn(),
            TokenType.Try => ParseTryCatch(),
            TokenType.Print => ParsePrint(),
            TokenType.Show => ParseShow(),
            TokenType.Input => ParseInput(),
            TokenType.Clear => MakeStmt(new ClearStmt(Advance().Location)),
            TokenType.Base => ParseBaseCall(),
            TokenType.Switch => ParseSwitch(),
            TokenType.Identifier => ParseIdentifierLedStatement(),
            TokenType.Math => ParseNamespaceLedStatement(TokenType.Math, "MATH"),
            TokenType.File => ParseNamespaceLedStatement(TokenType.File, "FILE"),
            TokenType.Dir => ParseNamespaceLedStatement(TokenType.Dir, "DIR"),
            TokenType.Osl => ParseNamespaceLedStatement(TokenType.Osl, "OSL"),
            TokenType.Date => ParseNamespaceLedStatement(TokenType.Date, "DATE"),
            TokenType.Time => ParseNamespaceLedStatement(TokenType.Time, "TIME"),
            TokenType.Osb => ParseNamespaceLedStatement(TokenType.Osb, "OSB"),
            _ => throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Text}' at start of statement."),
        };
    }

    private VarDeclStmt ParseVarDecl()
    {
        var start = Current.Location;
        Advance(); // VAR
        var name = ExpectIdentifier("Expected variable name after VAR");
        string? typeAnnotation = null;
        if (CheckAny(TokenType.Number, TokenType.String, TokenType.Boolean))
        {
            typeAnnotation = Current.Text;
            Advance();
        }
        Expect(TokenType.Newline, "Expected end of line after VAR declaration");
        return new VarDeclStmt(name, typeAnnotation, start);
    }

    private GlobalDeclStmt ParseGlobalDecl()
    {
        var start = Current.Location;
        Advance(); // GLOBAL
        var name = ExpectIdentifier("Expected variable name after GLOBAL");
        Expect(TokenType.Assign, "GLOBAL declarations require an initializer");
        var value = ParseExpression();
        Expect(TokenType.Newline, "Expected end of line after GLOBAL declaration");
        return new GlobalDeclStmt(name, value, start);
    }

    private IfStmt ParseIf()
    {
        var start = Current.Location;
        Advance(); // IF
        var condition = ParseExpression();
        Expect(TokenType.Then, "Expected THEN after IF condition");
        Expect(TokenType.Newline, "Expected end of line after THEN");

        var thenBody = ParseBlockUntil(TokenType.Elif, TokenType.Else, TokenType.End);
        var elifs = new List<ElifClause>();
        while (Check(TokenType.Elif))
        {
            Advance(); // ELIF
            var elifCond = ParseExpression();
            Expect(TokenType.Then, "Expected THEN after ELIF condition");
            Expect(TokenType.Newline, "Expected end of line after THEN");
            var elifBody = ParseBlockUntil(TokenType.Elif, TokenType.Else, TokenType.End);
            elifs.Add(new ElifClause(elifCond, elifBody, elifCond.Location));
        }

        List<Stmt>? elseBody = null;
        if (Check(TokenType.Else))
        {
            Advance(); // ELSE
            Expect(TokenType.Newline, "Expected end of line after ELSE");
            elseBody = ParseBlockUntil(TokenType.End);
        }

        Expect(TokenType.End, "Expected END to close IF");
        if (Check(TokenType.If)) Advance(); // optional END IF

        return new IfStmt(condition, thenBody, elifs, elseBody, start);
    }

    private ForStmt ParseFor()
    {
        var start = Current.Location;
        Advance(); // FOR
        var variable = ExpectIdentifier("Expected loop variable after FOR");
        Expect(TokenType.Assign, "Expected '=' after loop variable");
        var from = ParseExpression();
        Expect(TokenType.To, "Expected TO after FROM expression");
        var to = ParseExpression();

        Expr? step = null;
        if (Check(TokenType.Step))
        {
            Advance(); // STEP
            step = ParseExpression();
        }
        Expect(TokenType.Newline, "Expected end of line after FOR header");

        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close FOR");
        if (Check(TokenType.For)) Advance(); // optional END FOR

        return new ForStmt(variable, from, to, step, body, start);
    }

    private WhileStmt ParseWhile()
    {
        var start = Current.Location;
        Advance(); // WHILE
        var condition = ParseExpression();
        Expect(TokenType.Newline, "Expected end of line after WHILE condition");

        var body = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close WHILE");
        if (Check(TokenType.While)) Advance(); // optional END WHILE

        return new WhileStmt(condition, body, start);
    }

    private DoWhileStmt ParseDoWhile()
    {
        var start = Current.Location;
        Advance(); // DO
        Expect(TokenType.Newline, "Expected end of line after DO");

        var body = ParseBlockUntil(TokenType.While);
        Expect(TokenType.While, "Expected WHILE after DO block");
        var condition = ParseExpression();
        Expect(TokenType.Newline, "Expected end of line after WHILE condition");

        return new DoWhileStmt(body, condition, start);
    }

    private ReturnStmt ParseReturn()
    {
        var start = Current.Location;
        Advance(); // RETURN
        Expr? value = null;
        if (!Check(TokenType.Newline) && !IsAtEnd)
        {
            value = ParseExpression();
        }
        Expect(TokenType.Newline, "Expected end of line after RETURN");
        return new ReturnStmt(value, start);
    }

    private TryCatchStmt ParseTryCatch()
    {
        var start = Current.Location;
        Advance(); // TRY
        Expect(TokenType.Newline, "Expected end of line after TRY");

        var tryBody = ParseBlockUntil(TokenType.Catch);
        Expect(TokenType.Catch, "Expected CATCH after TRY block");

        string errorVar = "ERR";
        if (Check(TokenType.Identifier))
        {
            errorVar = Current.Text;
            Advance();
        }
        Expect(TokenType.Newline, "Expected end of line after CATCH");

        var catchBody = ParseBlockUntil(TokenType.End);
        Expect(TokenType.End, "Expected END to close CATCH");
        if (Check(TokenType.Catch)) Advance(); // optional END CATCH

        return new TryCatchStmt(tryBody, errorVar, catchBody, start);
    }

    private PrintStmt ParsePrint()
    {
        var start = Current.Location;
        Advance(); // PRINT
        var expressions = new List<Expr>();
        if (!Check(TokenType.Newline) && !IsAtEnd)
        {
            expressions.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                expressions.Add(ParseExpression());
            }
        }
        Expect(TokenType.Newline, "Expected end of line after PRINT");
        return new PrintStmt(expressions, start);
    }

    private ShowStmt ParseShow()
    {
        var start = Current.Location;
        Advance(); // SHOW
        var expressions = new List<Expr>();
        if (!Check(TokenType.Newline) && !IsAtEnd)
        {
            expressions.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                expressions.Add(ParseExpression());
            }
        }
        Expect(TokenType.Newline, "Expected end of line after SHOW");
        return new ShowStmt(expressions, start);
    }

    private InputStmt ParseInput()
    {
        var start = Current.Location;
        Advance(); // INPUT
        var varName = ExpectIdentifier("Expected variable name after INPUT");
        Expect(TokenType.Newline, "Expected end of line after INPUT");
        return new InputStmt(varName, start);
    }

    private BaseCallStmt ParseBaseCall()
    {
        var start = Current.Location;
        Advance(); // BASE
        Expect(TokenType.LParen, "Expected '(' after BASE");
        var args = new List<Expr>();
        if (!Check(TokenType.RParen))
        {
            args.Add(ParseExpression());
            while (Check(TokenType.Comma))
            {
                Advance();
                args.Add(ParseExpression());
            }
        }
        Expect(TokenType.RParen, "Expected ')' to close BASE argument list");
        Expect(TokenType.Newline, "Expected end of line after BASE");
        return new BaseCallStmt(args, start);
    }

    private SwitchStmt ParseSwitch()
    {
        var start = Current.Location;
        Advance(); // SWITCH
        var value = ParseExpression();
        Expect(TokenType.Newline, "Expected end of line after SWITCH expression");

        var cases = new List<SwitchCase>();
        List<Stmt>? defaultBody = null;

        while (!Check(TokenType.End) && !IsAtEnd)
        {
            if (Check(TokenType.Case))
            {
                Advance(); // CASE
                var values = new List<Expr>();
                values.Add(ParseExpression());
                while (Check(TokenType.Comma))
                {
                    Advance();
                    values.Add(ParseExpression());
                }
                Expect(TokenType.Newline, "Expected end of line after CASE");
                var body = ParseBlockUntil(TokenType.Case, TokenType.Default, TokenType.End);
                cases.Add(new SwitchCase(values, body, values[0].Location));
            }
            else if (Check(TokenType.Default))
            {
                Advance(); // DEFAULT
                Expect(TokenType.Newline, "Expected end of line after DEFAULT");
                defaultBody = ParseBlockUntil(TokenType.End);
            }
            else if (Check(TokenType.Newline))
            {
                Advance();
            }
            else
            {
                throw new SyntaxException(Current.Location, $"Unexpected token '{Current.Text}' in SWITCH body. Expected CASE, DEFAULT, or END.");
            }
        }

        Expect(TokenType.End, "Expected END to close SWITCH");
        if (Check(TokenType.Switch)) Advance(); // optional END SWITCH

        return new SwitchStmt(value, cases, defaultBody, start);
    }

    private Stmt ParseIdentifierLedStatement()
    {
        var start = Current.Location;
        var expr = ParsePostfix();

        if (Check(TokenType.Assign))
        {
            Advance();
            var value = ParseExpression();
            var target = ToAssignTarget(expr);
            Expect(TokenType.Newline, "Expected end of line after assignment");
            return new AssignStmt(target, value, start);
        }

        if (Check(TokenType.PlusEqual))
        {
            Advance();
            var right = ParseExpression();
            var target = ToAssignTarget(expr);
            var addExpr = new BinaryExpr("+", expr, right, start);
            Expect(TokenType.Newline, "Expected end of line after +=");
            return new AssignStmt(target, addExpr, start);
        }

        Expect(TokenType.Newline, "Expected end of line after expression statement");
        return new ExpressionStmt(expr, start);
    }

    private static AssignTarget ToAssignTarget(Expr expr) => expr switch
    {
        IdentifierExpr id => new VariableTarget(id.Name, id.Location),
        IndexExpr ix => new IndexTarget(ix.Array, ix.Index, ix.Location),
        MemberAccessExpr ma => new MemberTarget(ma.Object, ma.MemberName, ma.Location),
        _ => throw new SyntaxException(expr.Location, "Invalid assignment target."),
    };

    private Stmt ParseNamespaceLedStatement(TokenType nsType, string nsName)
    {
        var start = Current.Location;
        Advance(); // namespace keyword
        var expr = ParsePostfixStarting(new NamespaceExpr(nsName, start));
        Expect(TokenType.Newline, "Expected end of line after namespace statement");
        return new ExpressionStmt(expr, start);
    }

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

        return tok.Type switch
        {
            TokenType.NumberLiteral => MakePrimary(new NumberLiteralExpr(tok.NumberValue, tok.Location)),
            TokenType.StringLiteral => MakePrimary(new StringLiteralExpr(tok.StringValue!, tok.Location)),
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

    private static readonly HashSet<TokenType> BuiltinCallKeywords = new()
    {
        TokenType.Sqrt, TokenType.Pow, TokenType.Floor, TokenType.Ceil,
        TokenType.Str, TokenType.Number, TokenType.Bool, TokenType.Count, TokenType.TypeOf,
    };

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

        var elements = new List<Expr>();
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

    private List<Expr> ParseArgList()
    {
        var args = new List<Expr>();
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

    private T MakePrimary<T>(T expr) where T : Expr
    {
        Advance();
        return expr;
    }

    private void SkipNewlines()
    {
        while (Check(TokenType.Newline)) Advance();
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

    private void Expect(TokenType type, string errorMessage)
    {
        if (!Check(type))
            throw new SyntaxException(Current.Location, $"{errorMessage}. Found '{Current.Text}'.");
        Advance();
    }

    private Stmt MakeStmt(Stmt stmt) { Expect(TokenType.Newline, "Expected end of line after statement"); return stmt; }
}