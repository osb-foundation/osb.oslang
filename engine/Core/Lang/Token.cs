namespace Osb.Engine.Core.Lang;

/// <summary>
/// Source location in a script file.
/// </summary>
public readonly struct SourceLocation
{
    public readonly int Line;
    public readonly int Column;
    public readonly int Offset;

    public SourceLocation(int line, int column, int offset)
    {
        Line = line;
        Column = column;
        Offset = offset;
    }

    public static readonly SourceLocation Unknown = new(0, 0, 0);

    public override string ToString() => $"Line {Line}, Column {Column}";
}

/// <summary>
/// A lexical token with type, text, location, and optional typed values.
/// </summary>
public readonly struct Token
{
    public readonly TokenType Type;
    public readonly string Text;
    public readonly SourceLocation Location;
    public readonly double NumberValue;
    public readonly string StringValue;
    public readonly int IntValue;
    public readonly long LongValue;

    public Token(TokenType type, string text, SourceLocation location,
        double numberValue = 0, string stringValue = null, int intValue = 0, long longValue = 0)
    {
        Type = type;
        Text = text;
        Location = location;
        NumberValue = numberValue;
        StringValue = stringValue;
        IntValue = intValue;
        LongValue = longValue;
    }

    public override string ToString() => $"{Type,-15} '{Text}' @ {Location}";
}
