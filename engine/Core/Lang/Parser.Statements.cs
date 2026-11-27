using Osb.Engine.Core.Collections;

namespace Osb.Engine.Core.Lang;

public partial class Parser
{
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
        var elifs = new NodeList<ElifClause>();
        while (Check(TokenType.Elif))
        {
            Advance(); // ELIF
            var elifCond = ParseExpression();
            Expect(TokenType.Then, "Expected THEN after ELIF condition");
            Expect(TokenType.Newline, "Expected end of line after THEN");
            var elifBody = ParseBlockUntil(TokenType.Elif, TokenType.Else, TokenType.End);
            elifs.Add(new ElifClause(elifCond, elifBody, elifCond.Location));
        }

        NodeList<Stmt>? elseBody = null;
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
        var currentToken = Current;
        var start = currentToken.Location;
        Advance(); // PRINT
        var expressions = new NodeList<Expr>();
        if (!Check(TokenType.Newline) && !IsAtEnd)
        {
            var expr = ParseExpression();
            expressions.Add(expr);
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
        var expressions = new NodeList<Expr>();
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

        var cases = new NodeList<SwitchCase>();
        NodeList<Stmt>? defaultBody = null;

        while (!Check(TokenType.End) && !IsAtEnd)
        {
            if (Check(TokenType.Case))
            {
                Advance(); // CASE
                var values = new NodeList<Expr>();
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

    private Stmt ParseNamespaceLedStatement(TokenType nsType, string nsName)
    {
        var start = Current.Location;
        Advance(); // namespace keyword
        var expr = ParsePostfixStarting(new NamespaceExpr(nsName, start));
        Expect(TokenType.Newline, "Expected end of line after namespace statement");
        return new ExpressionStmt(expr, start);
    }
}
