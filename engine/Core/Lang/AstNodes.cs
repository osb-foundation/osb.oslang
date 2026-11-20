namespace Osb.Engine.Core.Lang;

/// <summary>
/// Base class for all AST nodes.
/// </summary>
public abstract class AstNode
{
    public SourceLocation Location { get; }

    protected AstNode(SourceLocation location) => Location = location;
}

/// <summary>
/// Expression base class.
/// </summary>
public abstract class Expr : AstNode
{
    protected Expr(SourceLocation location) : base(location) { }
}

/// <summary>
/// Statement base class.
/// </summary>
public abstract class Stmt : AstNode
{
    protected Stmt(SourceLocation location) : base(location) { }
}

/// <summary>
/// Class member base class.
/// </summary>
public abstract class MemberDecl : AstNode
{
    public Visibility Visibility { get; }

    protected MemberDecl(Visibility visibility, SourceLocation location) : base(location)
    {
        Visibility = visibility;
    }
}

/// <summary>
/// Assignment target base class.
/// </summary>
public abstract class AssignTarget : AstNode
{
    protected AssignTarget(SourceLocation location) : base(location) { }
}

/// <summary>
/// Visibility modifier.
/// </summary>
public enum Visibility
{
    Public,
    Protected,
    Private,
}

/// <summary>
/// Program - top-level container.
/// </summary>
public sealed class Program : AstNode
{
    public IReadOnlyList<FunctionDecl> Functions { get; }
    public IReadOnlyList<ClassDecl> Classes { get; }
    public IReadOnlyList<InterfaceDecl> Interfaces { get; }
    public IReadOnlyList<EnumDecl> Enums { get; }
    public IReadOnlyList<UsingDecl> Usings { get; }
    public IReadOnlyList<EventDecl> Events { get; }

    public Program(
        IReadOnlyList<FunctionDecl> functions,
        IReadOnlyList<ClassDecl> classes,
        IReadOnlyList<InterfaceDecl> interfaces,
        IReadOnlyList<EnumDecl> enums,
        IReadOnlyList<UsingDecl> usings,
        IReadOnlyList<EventDecl> events)
        : base(SourceLocation.Unknown)
    {
        Functions = functions;
        Classes = classes;
        Interfaces = interfaces;
        Enums = enums;
        Usings = usings;
        Events = events;
    }
}

/// <summary>
/// Function declaration.
/// </summary>
public sealed class FunctionDecl : AstNode
{
    public string Name { get; }
    public IReadOnlyList<ParameterDecl> Parameters { get; }
    public IReadOnlyList<Stmt> Body { get; }

    public FunctionDecl(string name, IReadOnlyList<ParameterDecl> parameters, IReadOnlyList<Stmt> body, SourceLocation location)
        : base(location)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
    }
}

/// <summary>
/// Parameter declaration.
/// </summary>
public sealed class ParameterDecl : AstNode
{
    public string Name { get; }
    public string? TypeAnnotation { get; }

    public ParameterDecl(string name, string? typeAnnotation, SourceLocation location)
        : base(location)
    {
        Name = name;
        TypeAnnotation = typeAnnotation;
    }
}

/// <summary>
/// Class declaration.
/// </summary>
public sealed class ClassDecl : AstNode
{
    public string Name { get; }
    public IReadOnlyList<string> BaseClasses { get; }
    public IReadOnlyList<MemberDecl> Members { get; }

    public ClassDecl(string name, IReadOnlyList<string> baseClasses, IReadOnlyList<MemberDecl> members, SourceLocation location)
        : base(location)
    {
        Name = name;
        BaseClasses = baseClasses;
        Members = members;
    }
}

/// <summary>
/// Field/property declaration.
/// </summary>
public sealed class PropertyDecl : MemberDecl
{
    public string Name { get; }
    public string? TypeAnnotation { get; }
    public Expr? DefaultValue { get; }

    public PropertyDecl(Visibility visibility, string name, string? typeAnnotation, Expr? defaultValue, SourceLocation location)
        : base(visibility, location)
    {
        Name = name;
        TypeAnnotation = typeAnnotation;
        DefaultValue = defaultValue;
    }
}

/// <summary>
/// Constructor declaration.
/// </summary>
public sealed class ConstructorDecl : MemberDecl
{
    public IReadOnlyList<ParameterDecl> Parameters { get; }
    public IReadOnlyList<Stmt> Body { get; }

    public ConstructorDecl(Visibility visibility, IReadOnlyList<ParameterDecl> parameters, IReadOnlyList<Stmt> body, SourceLocation location)
        : base(visibility, location)
    {
        Parameters = parameters;
        Body = body;
    }
}

/// <summary>
/// Method declaration.
/// </summary>
public sealed class MethodDecl : MemberDecl
{
    public string Name { get; }
    public IReadOnlyList<ParameterDecl> Parameters { get; }
    public IReadOnlyList<Stmt> Body { get; }

    public MethodDecl(Visibility visibility, string name, IReadOnlyList<ParameterDecl> parameters, IReadOnlyList<Stmt> body, SourceLocation location)
        : base(visibility, location)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
    }
}

/// <summary>
/// Interface declaration.
/// </summary>
public sealed class InterfaceDecl : AstNode
{
    public string Name { get; }
    public IReadOnlyList<string> BaseInterfaces { get; }
    public IReadOnlyList<MethodDecl> Methods { get; }

    public InterfaceDecl(string name, IReadOnlyList<string> baseInterfaces, IReadOnlyList<MethodDecl> methods, SourceLocation location)
        : base(location)
    {
        Name = name;
        BaseInterfaces = baseInterfaces;
        Methods = methods;
    }
}

/// <summary>
/// Enum declaration.
/// </summary>
public sealed class EnumDecl : AstNode
{
    public string Name { get; }
    public IReadOnlyList<EnumMemberDecl> Members { get; }

    public EnumDecl(string name, IReadOnlyList<EnumMemberDecl> members, SourceLocation location)
        : base(location)
    {
        Name = name;
        Members = members;
    }
}

/// <summary>
/// Enum member.
/// </summary>
public sealed class EnumMemberDecl : AstNode
{
    public string Name { get; }
    public Expr? Value { get; }

    public EnumMemberDecl(string name, Expr? value, SourceLocation location)
        : base(location)
    {
        Name = name;
        Value = value;
    }
}

/// <summary>
/// Using declaration (module import).
/// </summary>
public sealed class UsingDecl : AstNode
{
    public string ModuleName { get; }

    public UsingDecl(string moduleName, SourceLocation location)
        : base(location)
    {
        ModuleName = moduleName;
    }
}

/// <summary>
/// Event declaration.
/// </summary>
public sealed class EventDecl : AstNode
{
    public string Name { get; }
    public IReadOnlyList<ParameterDecl> Parameters { get; }

    public EventDecl(string name, IReadOnlyList<ParameterDecl> parameters, SourceLocation location)
        : base(location)
    {
        Name = name;
        Parameters = parameters;
    }
}
