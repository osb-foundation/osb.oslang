namespace Osb.Engine.Core.Lang;

/// <summary>
/// Base class for all AST nodes.
/// </summary>
public abstract record AstNode(SourceLocation Location);

/// <summary>
/// Expression base class.
/// </summary>
public abstract record Expr(SourceLocation Location) : AstNode(Location);

/// <summary>
/// Statement base class.
/// </summary>
public abstract record Stmt(SourceLocation Location) : AstNode(Location);

/// <summary>
/// Program - top-level container.
/// </summary>
public sealed record Program(
    IReadOnlyList<FunctionDecl> Functions,
    IReadOnlyList<ClassDecl> Classes,
    IReadOnlyList<InterfaceDecl> Interfaces,
    IReadOnlyList<EnumDecl> Enums,
    IReadOnlyList<UsingDecl> Usings,
    IReadOnlyList<EventDecl> Events
) : AstNode(SourceLocation.Unknown);

/// <summary>
/// Function declaration.
/// </summary>
public sealed record FunctionDecl(
    string Name,
    IReadOnlyList<ParameterDecl> Parameters,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Parameter declaration.
/// </summary>
public sealed record ParameterDecl(
    string Name,
    string? TypeAnnotation,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Class declaration.
/// </summary>
public sealed record ClassDecl(
    string Name,
    IReadOnlyList<string> BaseClasses,
    IReadOnlyList<MemberDecl> Members,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Class member (field, constructor, method).
/// </summary>
public abstract record MemberDecl(
    Visibility Visibility,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Field/property declaration.
/// </summary>
public sealed record PropertyDecl(
    Visibility Visibility,
    string Name,
    string? TypeAnnotation,
    Expr? DefaultValue,
    SourceLocation Location
) : MemberDecl(Visibility, Location);

/// <summary>
/// Constructor declaration.
/// </summary>
public sealed record ConstructorDecl(
    Visibility Visibility,
    IReadOnlyList<ParameterDecl> Parameters,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location
) : MemberDecl(Visibility, Location);

/// <summary>
/// Method declaration.
/// </summary>
public sealed record MethodDecl(
    Visibility Visibility,
    string Name,
    IReadOnlyList<ParameterDecl> Parameters,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location
) : MemberDecl(Visibility, Location);

/// <summary>
/// Interface declaration.
/// </summary>
public sealed record InterfaceDecl(
    string Name,
    IReadOnlyList<string> BaseInterfaces,
    IReadOnlyList<MethodDecl> Methods,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Enum declaration.
/// </summary>
public sealed record EnumDecl(
    string Name,
    IReadOnlyList<EnumMemberDecl> Members,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Enum member.
/// </summary>
public sealed record EnumMemberDecl(
    string Name,
    Expr? Value,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Using declaration (module import).
/// </summary>
public sealed record UsingDecl(
    string ModuleName,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Event declaration.
/// </summary>
public sealed record EventDecl(
    string Name,
    IReadOnlyList<ParameterDecl> Parameters,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Visibility modifier.
/// </summary>
public enum Visibility
{
    Public,
    Protected,
    Private,
}

#region Expressions

/// <summary>
/// Literal expressions.
/// </summary>
public sealed record NumberLiteralExpr(double Value, SourceLocation Location) : Expr(Location);
public sealed record StringLiteralExpr(string Value, SourceLocation Location) : Expr(Location);
public sealed record DateLiteralExpr(int DaysSinceEpoch, SourceLocation Location) : Expr(Location);
public sealed record TimeLiteralExpr(long MillisSinceMidnight, SourceLocation Location) : Expr(Location);
public sealed record BooleanLiteralExpr(bool Value, SourceLocation Location) : Expr(Location);
public sealed record NullLiteralExpr(SourceLocation Location) : Expr(Location);

/// <summary>
/// Identifier expression.
/// </summary>
public sealed record IdentifierExpr(string Name, SourceLocation Location) : Expr(Location);

/// <summary>
/// Array literal expression.
/// </summary>
public sealed record ArrayLiteralExpr(IReadOnlyList<Expr> Elements, SourceLocation Location) : Expr(Location);

/// <summary>
/// Binary operation.
/// </summary>
public sealed record BinaryExpr(
    string Operator,
    Expr Left,
    Expr Right,
    SourceLocation Location
) : Expr(Location);

/// <summary>
/// Unary operation.
/// </summary>
public sealed record UnaryExpr(
    string Operator,
    Expr Operand,
    SourceLocation Location
) : Expr(Location);

/// <summary>
/// Function call.
/// </summary>
public sealed record CallExpr(
    string Name,
    IReadOnlyList<Expr> Args,
    SourceLocation Location
) : Expr(Location);

/// <summary>
/// Method call.
/// </summary>
public sealed record MethodCallExpr(
    Expr Object,
    string MethodName,
    IReadOnlyList<Expr> Args,
    SourceLocation Location
) : Expr(Location);

/// <summary>
/// Member access (obj.prop).
/// </summary>
public sealed record MemberAccessExpr(
    Expr Object,
    string MemberName,
    SourceLocation Location
) : Expr(Location);

/// <summary>
/// Index access (arr[i]).
/// </summary>
public sealed record IndexExpr(
    Expr Array,
    Expr Index,
    SourceLocation Location
) : Expr(Location);

/// <summary>
/// New expression (NEW Class(args)).
/// </summary>
public sealed record NewExpr(
    string ClassName,
    IReadOnlyList<Expr> Args,
    SourceLocation Location
) : Expr(Location);

/// <summary>
/// ME expression (current object).
/// </summary>
public sealed record MeExpr(SourceLocation Location) : Expr(Location);

/// <summary>
/// BASE expression (base class).
/// </summary>
public sealed record BaseExpr(SourceLocation Location) : Expr(Location);

/// <summary>
/// Namespace expression (MATH, FILE, DATE, etc.).
/// </summary>
public sealed record NamespaceExpr(string NamespaceName, SourceLocation Location) : Expr(Location);

/// <summary>
/// Namespace function call (DATE(...), TIME(...), MATH.PI(...) etc.).
/// </summary>
public sealed record NamespaceCallExpr(
    string NamespaceName,
    IReadOnlyList<Expr> Args,
    SourceLocation Location
) : Expr(Location);

/// <summary>
/// Postfix increment/decrement.
/// </summary>
public sealed record PostfixExpr(
    string Operator,
    Expr Operand,
    SourceLocation Location
) : Expr(Location);

#endregion

#region Statements

/// <summary>
/// Variable declaration.
/// </summary>
public sealed record VarDeclStmt(
    string Name,
    string? TypeAnnotation,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Assignment statement.
/// </summary>
public sealed record AssignStmt(
    AssignTarget Target,
    Expr Value,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Assignment target (variable, index, member).
/// </summary>
public abstract record AssignTarget(SourceLocation Location) : AstNode(Location);

public sealed record VariableTarget(string Name, SourceLocation Location) : AssignTarget(Location);
public sealed record IndexTarget(Expr Array, Expr Index, SourceLocation Location) : AssignTarget(Location);
public sealed record MemberTarget(Expr Object, string MemberName, SourceLocation Location) : AssignTarget(Location);

/// <summary>
/// Print statement.
/// </summary>
public sealed record PrintStmt(
    IReadOnlyList<Expr> Expressions,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Show statement (no newline).
/// </summary>
public sealed record ShowStmt(
    IReadOnlyList<Expr> Expressions,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Input statement.
/// </summary>
public sealed record InputStmt(
    string VariableName,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// If statement.
/// </summary>
public sealed record IfStmt(
    Expr Condition,
    IReadOnlyList<Stmt> ThenBody,
    IReadOnlyList<ElifClause> ElifClauses,
    IReadOnlyList<Stmt>? ElseBody,
    SourceLocation Location
) : Stmt(Location);

public sealed record ElifClause(
    Expr Condition,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// For loop (numeric).
/// </summary>
public sealed record ForStmt(
    string Variable,
    Expr From,
    Expr To,
    Expr? Step,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// For-In loop.
/// </summary>
public sealed record ForInStmt(
    string Variable,
    Expr Iterable,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// While loop.
/// </summary>
public sealed record WhileStmt(
    Expr Condition,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Do-While loop.
/// </summary>
public sealed record DoWhileStmt(
    IReadOnlyList<Stmt> Body,
    Expr Condition,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Break statement.
/// </summary>
public sealed record BreakStmt(SourceLocation Location) : Stmt(Location);

/// <summary>
/// Continue statement.
/// </summary>
public sealed record ContinueStmt(SourceLocation Location) : Stmt(Location);

/// <summary>
/// Return statement.
/// </summary>
public sealed record ReturnStmt(
    Expr? Value,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Try-Catch statement.
/// </summary>
public sealed record TryCatchStmt(
    IReadOnlyList<Stmt> TryBody,
    string ErrorVariable,
    IReadOnlyList<Stmt> CatchBody,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Switch statement.
/// </summary>
public sealed record SwitchStmt(
    Expr Value,
    IReadOnlyList<SwitchCase> Cases,
    IReadOnlyList<Stmt>? DefaultBody,
    SourceLocation Location
) : Stmt(Location);

public sealed record SwitchCase(
    IReadOnlyList<Expr> Values,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location
) : AstNode(Location);

/// <summary>
/// Clear statement.
/// </summary>
public sealed record ClearStmt(SourceLocation Location) : Stmt(Location);

/// <summary>
/// Base call statement.
/// </summary>
public sealed record BaseCallStmt(
    IReadOnlyList<Expr> Args,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Global declaration.
/// </summary>
public sealed record GlobalDeclStmt(
    string Name,
    Expr Value,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Expression statement (standalone expression).
/// </summary>
public sealed record ExpressionStmt(
    Expr Expression,
    SourceLocation Location
) : Stmt(Location);

/// <summary>
/// Block statement (for scopes).
/// </summary>
public sealed record BlockStmt(
    IReadOnlyList<Stmt> Statements,
    SourceLocation Location
) : Stmt(Location);

#endregion