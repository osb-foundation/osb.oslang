using System.Collections.Generic;
namespace Osb.Engine.Core.Lang;

#region Expressions

/// <summary>
/// Number literal expression.
/// </summary>
public sealed class NumberLiteralExpr : Expr
{
    public double Value { get; }

    public NumberLiteralExpr(double value, SourceLocation location)
        : base(location)
    {
        Value = value;
    }
}

/// <summary>
    /// String literal expression.
    /// </summary>
    public sealed class StringLiteralExpr : Expr
    {
        public string Value { get; }

        public StringLiteralExpr(string value, SourceLocation location)
            : base(location)
        {
#if FREESTANDING
            OsbKT.Syscall.PrintString("DBG: StringLiteralExpr ctor: value=");
            OsbKT.Syscall.PrintString(value ?? "NULL");
            OsbKT.Syscall.PrintString("\r\n");
#endif
            Value = value;
        }
    }

/// <summary>
/// Date literal expression.
/// </summary>
public sealed class DateLiteralExpr : Expr
{
    public int DaysSinceEpoch { get; }

    public DateLiteralExpr(int daysSinceEpoch, SourceLocation location)
        : base(location)
    {
        DaysSinceEpoch = daysSinceEpoch;
    }
}

/// <summary>
/// Time literal expression.
/// </summary>
public sealed class TimeLiteralExpr : Expr
{
    public long MillisSinceMidnight { get; }

    public TimeLiteralExpr(long millisSinceMidnight, SourceLocation location)
        : base(location)
    {
        MillisSinceMidnight = millisSinceMidnight;
    }
}

/// <summary>
/// Boolean literal expression.
/// </summary>
public sealed class BooleanLiteralExpr : Expr
{
    public bool Value { get; }

    public BooleanLiteralExpr(bool value, SourceLocation location)
        : base(location)
    {
        Value = value;
    }
}

/// <summary>
/// Null literal expression.
/// </summary>
public sealed class NullLiteralExpr : Expr
{
    public NullLiteralExpr(SourceLocation location)
        : base(location) { }
}

/// <summary>
/// Identifier expression.
/// </summary>
public sealed class IdentifierExpr : Expr
{
    public string Name { get; }

    public IdentifierExpr(string name, SourceLocation location)
        : base(location)
    {
        Name = name;
    }
}

/// <summary>
/// Array literal expression.
/// </summary>
public sealed class ArrayLiteralExpr : Expr
{
    public IReadOnlyList<Expr> Elements { get; }

    public ArrayLiteralExpr(IReadOnlyList<Expr> elements, SourceLocation location)
        : base(location)
    {
        Elements = elements;
    }
}

/// <summary>
/// Binary operation.
/// </summary>
public sealed class BinaryExpr : Expr
{
    public string Operator { get; }
    public Expr Left { get; }
    public Expr Right { get; }

    public BinaryExpr(string @operator, Expr left, Expr right, SourceLocation location)
        : base(location)
    {
        Operator = @operator;
        Left = left;
        Right = right;
    }
}

/// <summary>
/// Unary operation.
/// </summary>
public sealed class UnaryExpr : Expr
{
    public string Operator { get; }
    public Expr Operand { get; }

    public UnaryExpr(string @operator, Expr operand, SourceLocation location)
        : base(location)
    {
        Operator = @operator;
        Operand = operand;
    }
}

/// <summary>
/// Function call.
/// </summary>
public sealed class CallExpr : Expr
{
    public string Name { get; }
    public IReadOnlyList<Expr> Args { get; }

    public CallExpr(string name, IReadOnlyList<Expr> args, SourceLocation location)
        : base(location)
    {
        Name = name;
        Args = args;
    }
}

/// <summary>
/// Method call.
/// </summary>
public sealed class MethodCallExpr : Expr
{
    public Expr Object { get; }
    public string MethodName { get; }
    public IReadOnlyList<Expr> Args { get; }

    public MethodCallExpr(Expr obj, string methodName, IReadOnlyList<Expr> args, SourceLocation location)
        : base(location)
    {
        Object = obj;
        MethodName = methodName;
        Args = args;
    }
}

/// <summary>
/// Member access (obj.prop).
/// </summary>
public sealed class MemberAccessExpr : Expr
{
    public Expr Object { get; }
    public string MemberName { get; }

    public MemberAccessExpr(Expr obj, string memberName, SourceLocation location)
        : base(location)
    {
        Object = obj;
        MemberName = memberName;
    }
}

/// <summary>
/// Index access (arr[i]).
/// </summary>
public sealed class IndexExpr : Expr
{
    public Expr Array { get; }
    public Expr Index { get; }

    public IndexExpr(Expr array, Expr index, SourceLocation location)
        : base(location)
    {
        Array = array;
        Index = index;
    }
}

/// <summary>
/// New expression (NEW Class(args)).
/// </summary>
public sealed class NewExpr : Expr
{
    public string ClassName { get; }
    public IReadOnlyList<Expr> Args { get; }

    public NewExpr(string className, IReadOnlyList<Expr> args, SourceLocation location)
        : base(location)
    {
        ClassName = className;
        Args = args;
    }
}

/// <summary>
/// ME expression (current object).
/// </summary>
public sealed class MeExpr : Expr
{
    public MeExpr(SourceLocation location)
        : base(location) { }
}

/// <summary>
/// BASE expression (base class).
/// </summary>
public sealed class BaseExpr : Expr
{
    public BaseExpr(SourceLocation location)
        : base(location) { }
}

/// <summary>
/// Namespace expression (MATH, FILE, DATE, etc.).
/// </summary>
public sealed class NamespaceExpr : Expr
{
    public string NamespaceName { get; }

    public NamespaceExpr(string namespaceName, SourceLocation location)
        : base(location)
    {
        NamespaceName = namespaceName;
    }
}

/// <summary>
/// Namespace function call (DATE(...), TIME(...), MATH.PI(...) etc.).
/// </summary>
public sealed class NamespaceCallExpr : Expr
{
    public string NamespaceName { get; }
    public IReadOnlyList<Expr> Args { get; }

    public NamespaceCallExpr(string namespaceName, IReadOnlyList<Expr> args, SourceLocation location)
        : base(location)
    {
        NamespaceName = namespaceName;
        Args = args;
    }
}

/// <summary>
/// Postfix increment/decrement.
/// </summary>
public sealed class PostfixExpr : Expr
{
    public string Operator { get; }
    public Expr Operand { get; }

    public PostfixExpr(string @operator, Expr operand, SourceLocation location)
        : base(location)
    {
        Operator = @operator;
        Operand = operand;
    }

    #endregion
}
