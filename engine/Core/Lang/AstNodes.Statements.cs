namespace Osb.Engine.Core.Lang;

#region Statements

/// <summary>
/// Variable declaration statement.
/// </summary>
public sealed class VarDeclStmt : Stmt
{
    public string Name { get; }
    public string? TypeAnnotation { get; }

    public VarDeclStmt(string name, string? typeAnnotation, SourceLocation location)
        : base(location)
    {
        Name = name;
        TypeAnnotation = typeAnnotation;
    }
}

/// <summary>
/// Assignment statement.
/// </summary>
public sealed class AssignStmt : Stmt
{
    public AssignTarget Target { get; }
    public Expr Value { get; }

    public AssignStmt(AssignTarget target, Expr value, SourceLocation location)
        : base(location)
    {
        Target = target;
        Value = value;
    }
}

/// <summary>
/// Variable assignment target.
/// </summary>
public sealed class VariableTarget : AssignTarget
{
    public string Name { get; }

    public VariableTarget(string name, SourceLocation location)
        : base(location)
    {
        Name = name;
    }
}

/// <summary>
/// Index assignment target.
/// </summary>
public sealed class IndexTarget : AssignTarget
{
    public Expr Array { get; }
    public Expr Index { get; }

    public IndexTarget(Expr array, Expr index, SourceLocation location)
        : base(location)
    {
        Array = array;
        Index = index;
    }
}

/// <summary>
/// Member assignment target.
/// </summary>
public sealed class MemberTarget : AssignTarget
{
    public Expr Object { get; }
    public string MemberName { get; }

    public MemberTarget(Expr obj, string memberName, SourceLocation location)
        : base(location)
    {
        Object = obj;
        MemberName = memberName;
    }
}

/// <summary>
/// Print statement.
/// </summary>
public sealed class PrintStmt : Stmt
{
    public IReadOnlyList<Expr> Expressions { get; }

    public PrintStmt(IReadOnlyList<Expr> expressions, SourceLocation location)
        : base(location)
    {
        Expressions = expressions;
    }
}

/// <summary>
/// Show statement (no newline).
/// </summary>
public sealed class ShowStmt : Stmt
{
    public IReadOnlyList<Expr> Expressions { get; }

    public ShowStmt(IReadOnlyList<Expr> expressions, SourceLocation location)
        : base(location)
    {
        Expressions = expressions;
    }
}

/// <summary>
/// Input statement.
/// </summary>
public sealed class InputStmt : Stmt
{
    public string VariableName { get; }

    public InputStmt(string variableName, SourceLocation location)
        : base(location)
    {
        VariableName = variableName;
    }
}

/// <summary>
/// If statement.
/// </summary>
public sealed class IfStmt : Stmt
{
    public Expr Condition { get; }
    public IReadOnlyList<Stmt> ThenBody { get; }
    public IReadOnlyList<ElifClause> ElifClauses { get; }
    public IReadOnlyList<Stmt>? ElseBody { get; }

    public IfStmt(Expr condition, IReadOnlyList<Stmt> thenBody, IReadOnlyList<ElifClause> elifClauses, IReadOnlyList<Stmt>? elseBody, SourceLocation location)
        : base(location)
    {
        Condition = condition;
        ThenBody = thenBody;
        ElifClauses = elifClauses;
        ElseBody = elseBody;
    }
}

/// <summary>
/// Elif clause.
/// </summary>
public sealed class ElifClause : AstNode
{
    public Expr Condition { get; }
    public IReadOnlyList<Stmt> Body { get; }

    public ElifClause(Expr condition, IReadOnlyList<Stmt> body, SourceLocation location)
        : base(location)
    {
        Condition = condition;
        Body = body;
    }
}

/// <summary>
/// For loop (numeric).
/// </summary>
public sealed class ForStmt : Stmt
{
    public string Variable { get; }
    public Expr From { get; }
    public Expr To { get; }
    public Expr? Step { get; }
    public IReadOnlyList<Stmt> Body { get; }

    public ForStmt(string variable, Expr from, Expr to, Expr? step, IReadOnlyList<Stmt> body, SourceLocation location)
        : base(location)
    {
        Variable = variable;
        From = from;
        To = to;
        Step = step;
        Body = body;
    }
}

/// <summary>
/// For-In loop.
/// </summary>
public sealed class ForInStmt : Stmt
{
    public string Variable { get; }
    public Expr Iterable { get; }
    public IReadOnlyList<Stmt> Body { get; }

    public ForInStmt(string variable, Expr iterable, IReadOnlyList<Stmt> body, SourceLocation location)
        : base(location)
    {
        Variable = variable;
        Iterable = iterable;
        Body = body;
    }
}

/// <summary>
/// While loop.
/// </summary>
public sealed class WhileStmt : Stmt
{
    public Expr Condition { get; }
    public IReadOnlyList<Stmt> Body { get; }

    public WhileStmt(Expr condition, IReadOnlyList<Stmt> body, SourceLocation location)
        : base(location)
    {
        Condition = condition;
        Body = body;
    }
}

/// <summary>
/// Do-While loop.
/// </summary>
public sealed class DoWhileStmt : Stmt
{
    public IReadOnlyList<Stmt> Body { get; }
    public Expr Condition { get; }

    public DoWhileStmt(IReadOnlyList<Stmt> body, Expr condition, SourceLocation location)
        : base(location)
    {
        Body = body;
        Condition = condition;
    }
}

/// <summary>
/// Break statement.
/// </summary>
public sealed class BreakStmt : Stmt
{
    public BreakStmt(SourceLocation location)
        : base(location) { }
}

/// <summary>
/// Continue statement.
/// </summary>
public sealed class ContinueStmt : Stmt
{
    public ContinueStmt(SourceLocation location)
        : base(location) { }
}

/// <summary>
/// Return statement.
/// </summary>
public sealed class ReturnStmt : Stmt
{
    public Expr? Value { get; }

    public ReturnStmt(Expr? value, SourceLocation location)
        : base(location)
    {
        Value = value;
    }
}

/// <summary>
/// Try-Catch statement.
/// </summary>
public sealed class TryCatchStmt : Stmt
{
    public IReadOnlyList<Stmt> TryBody { get; }
    public string ErrorVariable { get; }
    public IReadOnlyList<Stmt> CatchBody { get; }

    public TryCatchStmt(IReadOnlyList<Stmt> tryBody, string errorVariable, IReadOnlyList<Stmt> catchBody, SourceLocation location)
        : base(location)
    {
        TryBody = tryBody;
        ErrorVariable = errorVariable;
        CatchBody = catchBody;
    }
}

/// <summary>
/// Switch statement.
/// </summary>
public sealed class SwitchStmt : Stmt
{
    public Expr Value { get; }
    public IReadOnlyList<SwitchCase> Cases { get; }
    public IReadOnlyList<Stmt>? DefaultBody { get; }

    public SwitchStmt(Expr value, IReadOnlyList<SwitchCase> cases, IReadOnlyList<Stmt>? defaultBody, SourceLocation location)
        : base(location)
    {
        Value = value;
        Cases = cases;
        DefaultBody = defaultBody;
    }
}

/// <summary>
/// Switch case.
/// </summary>
public sealed class SwitchCase : AstNode
{
    public IReadOnlyList<Expr> Values { get; }
    public IReadOnlyList<Stmt> Body { get; }

    public SwitchCase(IReadOnlyList<Expr> values, IReadOnlyList<Stmt> body, SourceLocation location)
        : base(location)
    {
        Values = values;
        Body = body;
    }
}

/// <summary>
/// Clear statement.
/// </summary>
public sealed class ClearStmt : Stmt
{
    public ClearStmt(SourceLocation location)
        : base(location) { }
}

/// <summary>
/// Base call statement.
/// </summary>
public sealed class BaseCallStmt : Stmt
{
    public IReadOnlyList<Expr> Args { get; }

    public BaseCallStmt(IReadOnlyList<Expr> args, SourceLocation location)
        : base(location)
    {
        Args = args;
    }
}

/// <summary>
/// Global declaration statement.
/// </summary>
public sealed class GlobalDeclStmt : Stmt
{
    public string Name { get; }
    public Expr Value { get; }

    public GlobalDeclStmt(string name, Expr value, SourceLocation location)
        : base(location)
    {
        Name = name;
        Value = value;
    }
}

/// <summary>
/// Expression statement (standalone expression).
/// </summary>
public sealed class ExpressionStmt : Stmt
{
    public Expr Expression { get; }

    public ExpressionStmt(Expr expression, SourceLocation location)
        : base(location)
    {
        Expression = expression;
    }
}

/// <summary>
/// Block statement (for scopes).
/// </summary>
public sealed class BlockStmt : Stmt
{
    public IReadOnlyList<Stmt> Statements { get; }

    public BlockStmt(IReadOnlyList<Stmt> statements, SourceLocation location)
        : base(location)
    {
        Statements = statements;
    }

    #endregion
}
