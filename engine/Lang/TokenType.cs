namespace Osb.Engine.Lang;

/// <summary>
/// OSLANG token types. Matches the reference implementation's TokenType.
/// </summary>
public enum TokenType : ushort
{
    // Literals and identifiers
    Identifier = 0,
    NumberLiteral,
    StringLiteral,
    DateLiteral,
    TimeLiteral,

    // Keywords (normalized to uppercase by lexer)
    And,
    Base,
    Boolean,
    Bool,
    Break,
    Catch,
    Ceil,
    Class,
    Clear,
    Constructor,
    Continue,
    Count,
    Do,
    Elif,
    Else,
    End,
    False,
    Floor,
    For,
    Function,
    Global,
    If,
    Input,
    Interface,
    Me,
    New,
    Not,
    Null,
    Number,
    Or,
    Pow,
    Print,
    Private,
    Protected,
    Public,
    Return,
    Sqrt,
    Step,
    String,
    Str,
    Then,
    To,
    True,
    Try,
    TypeOf,
    Var,
    While,

    // OSLANG 0.3
    Switch,
    Case,
    Default,
    Virtual,
    Override,
    Event,
    On,
    Raise,
    Using,

    // OSLANG 0.4
    Math,
    File,
    Dir,

    // OSLANG 0.5
    Date,
    Time,

    // OSLANG 0.6
    Show,
    Mod,

    // OSLANG 0.6 - Standard Library namespace
    Osl,

    // OSLANG 0.61
    Enum,
    Pipe,
    // OSLANG 0.62
    In,
    Each,

    // OSLANG 0.62
    Osb,

    // Arithmetic operators
    Plus,
    Minus,
    Star,
    StarStar,
    Slash,
    Percent,

    // Increment/decrement
    PlusPlus,
    MinusMinus,
    PlusEqual,

    // Comparison operators
    Equal,
    NotEqual,
    Less,
    Greater,
    LessEqual,
    GreaterEqual,

    // Assignment
    Assign,

    // Punctuation
    LParen,
    RParen,
    LBracket,
    RBracket,
    LBrace,
    RBrace,
    Comma,
    Colon,
    Dot,
    DoubleArrow,

    // Structure
    Newline,
    Eof,
}