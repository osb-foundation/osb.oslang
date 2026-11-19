namespace Osb.Engine.Lang;

/// <summary>
/// Keyword lookup table for the lexer.
/// All keywords are stored in uppercase for case-insensitive matching (ASCII-only per SQ-002).
/// </summary>
internal static class Keywords
{
    private static readonly Dictionary<string, TokenType> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core keywords (OSLANG 0.1+)
        ["AND"] = TokenType.And,
        ["BASE"] = TokenType.Base,
        ["BOOLEAN"] = TokenType.Boolean,
        ["BOOL"] = TokenType.Bool,
        ["BREAK"] = TokenType.Break,
        ["CATCH"] = TokenType.Catch,
        ["CEIL"] = TokenType.Ceil,
        ["CLASS"] = TokenType.Class,
        ["CLEAR"] = TokenType.Clear,
        ["CONSTRUCTOR"] = TokenType.Constructor,
        ["CONTINUE"] = TokenType.Continue,
        ["COUNT"] = TokenType.Count,
        ["DO"] = TokenType.Do,
        ["ELIF"] = TokenType.Elif,
        ["ELSE"] = TokenType.Else,
        ["END"] = TokenType.End,
        ["FALSE"] = TokenType.False,
        ["FLOOR"] = TokenType.Floor,
        ["FOR"] = TokenType.For,
        ["FUNCTION"] = TokenType.Function,
        ["GLOBAL"] = TokenType.Global,
        ["IF"] = TokenType.If,
        ["INPUT"] = TokenType.Input,
        ["INTERFACE"] = TokenType.Interface,
        ["ME"] = TokenType.Me,
        ["NEW"] = TokenType.New,
        ["NOT"] = TokenType.Not,
        ["NULL"] = TokenType.Null,
        ["NUMBER"] = TokenType.Number,
        ["OR"] = TokenType.Or,
        ["POW"] = TokenType.Pow,
        ["PRINT"] = TokenType.Print,
        ["PRIVATE"] = TokenType.Private,
        ["PROTECTED"] = TokenType.Protected,
        ["PUBLIC"] = TokenType.Public,
        ["RETURN"] = TokenType.Return,
        ["SQRT"] = TokenType.Sqrt,
        ["STEP"] = TokenType.Step,
        ["STRING"] = TokenType.String,
        ["STR"] = TokenType.Str,
        ["THEN"] = TokenType.Then,
        ["TO"] = TokenType.To,
        ["TRUE"] = TokenType.True,
        ["TRY"] = TokenType.Try,
        ["TYPEOF"] = TokenType.TypeOf,
        ["VAR"] = TokenType.Var,
        ["WHILE"] = TokenType.While,

        // OSLANG 0.3
        ["SWITCH"] = TokenType.Switch,
        ["CASE"] = TokenType.Case,
        ["DEFAULT"] = TokenType.Default,
        ["VIRTUAL"] = TokenType.Virtual,
        ["OVERRIDE"] = TokenType.Override,
        ["EVENT"] = TokenType.Event,
        ["ON"] = TokenType.On,
        ["RAISE"] = TokenType.Raise,
        ["USING"] = TokenType.Using,

        // OSLANG 0.4
        ["MATH"] = TokenType.Math,
        ["FILE"] = TokenType.File,
        ["DIR"] = TokenType.Dir,

        // OSLANG 0.5
        ["DATE"] = TokenType.Date,
        ["TIME"] = TokenType.Time,

        // OSLANG 0.6
        ["SHOW"] = TokenType.Show,
        ["MOD"] = TokenType.Mod,

        // OSLANG 0.6 - Standard Library namespace
        ["OSL"] = TokenType.Osl,

        // OSLANG 0.61
        ["ENUM"] = TokenType.Enum,
        // OSLANG 0.62
        ["IN"] = TokenType.In,
        ["EACH"] = TokenType.Each,

        // OSLANG 0.62
        ["OSB"] = TokenType.Osb,
    };

    /// <summary>
    /// Attempts to look up a keyword. Uses ASCII-only case-insensitive comparison (SQ-002).
    /// </summary>
    public static bool TryGetKeyword(string upperText, out TokenType type) => Map.TryGetValue(upperText, out type);

    /// <summary>
    /// Checks if a token type is a keyword.
    /// </summary>
    public static bool IsKeyword(TokenType type) => (ushort)type <= (ushort)TokenType.Osb;
}