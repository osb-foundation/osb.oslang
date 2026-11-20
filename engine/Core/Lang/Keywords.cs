using Osb.Engine.Core.Collections;

namespace Osb.Engine.Core.Lang;

/// <summary>
/// Keyword lookup table for the lexer.
/// All keywords are stored in uppercase for case-insensitive matching (ASCII-only per SQ-002).
/// Uses freestanding StringMap instead of BCL Dictionary.
/// </summary>
internal static class Keywords
{
    private static readonly StringMap<TokenType> Map;

    static Keywords()
    {
        Map = new StringMap<TokenType>();
        // Core keywords (OSLANG 0.1+)
        Map.Add("AND", TokenType.And);
        Map.Add("BASE", TokenType.Base);
        Map.Add("BOOLEAN", TokenType.Boolean);
        Map.Add("BOOL", TokenType.Bool);
        Map.Add("BREAK", TokenType.Break);
        Map.Add("CATCH", TokenType.Catch);
        Map.Add("CEIL", TokenType.Ceil);
        Map.Add("CLASS", TokenType.Class);
        Map.Add("CLEAR", TokenType.Clear);
        Map.Add("CONSTRUCTOR", TokenType.Constructor);
        Map.Add("CONTINUE", TokenType.Continue);
        Map.Add("COUNT", TokenType.Count);
        Map.Add("DO", TokenType.Do);
        Map.Add("ELIF", TokenType.Elif);
        Map.Add("ELSE", TokenType.Else);
        Map.Add("END", TokenType.End);
        Map.Add("FALSE", TokenType.False);
        Map.Add("FLOOR", TokenType.Floor);
        Map.Add("FOR", TokenType.For);
        Map.Add("FUNCTION", TokenType.Function);
        Map.Add("GLOBAL", TokenType.Global);
        Map.Add("IF", TokenType.If);
        Map.Add("INPUT", TokenType.Input);
        Map.Add("INTERFACE", TokenType.Interface);
        Map.Add("ME", TokenType.Me);
        Map.Add("NEW", TokenType.New);
        Map.Add("NOT", TokenType.Not);
        Map.Add("NULL", TokenType.Null);
        Map.Add("NUMBER", TokenType.Number);
        Map.Add("OR", TokenType.Or);
        Map.Add("POW", TokenType.Pow);
        Map.Add("PRINT", TokenType.Print);
        Map.Add("PRIVATE", TokenType.Private);
        Map.Add("PROTECTED", TokenType.Protected);
        Map.Add("PUBLIC", TokenType.Public);
        Map.Add("RETURN", TokenType.Return);
        Map.Add("SQRT", TokenType.Sqrt);
        Map.Add("STEP", TokenType.Step);
        Map.Add("STRING", TokenType.String);
        Map.Add("STR", TokenType.Str);
        Map.Add("THEN", TokenType.Then);
        Map.Add("TO", TokenType.To);
        Map.Add("TRUE", TokenType.True);
        Map.Add("TRY", TokenType.Try);
        Map.Add("TYPEOF", TokenType.TypeOf);
        Map.Add("VAR", TokenType.Var);
        Map.Add("WHILE", TokenType.While);

        // OSLANG 0.3
        Map.Add("SWITCH", TokenType.Switch);
        Map.Add("CASE", TokenType.Case);
        Map.Add("DEFAULT", TokenType.Default);
        Map.Add("VIRTUAL", TokenType.Virtual);
        Map.Add("OVERRIDE", TokenType.Override);
        Map.Add("EVENT", TokenType.Event);
        Map.Add("ON", TokenType.On);
        Map.Add("RAISE", TokenType.Raise);
        Map.Add("USING", TokenType.Using);

        // OSLANG 0.4
        Map.Add("MATH", TokenType.Math);
        Map.Add("FILE", TokenType.File);
        Map.Add("DIR", TokenType.Dir);

        // OSLANG 0.5
        Map.Add("DATE", TokenType.Date);
        Map.Add("TIME", TokenType.Time);

        // OSLANG 0.6
        Map.Add("SHOW", TokenType.Show);
        Map.Add("MOD", TokenType.Mod);

        // OSLANG 0.6 - Standard Library namespace
        Map.Add("OSL", TokenType.Osl);

        // OSLANG 0.61
        Map.Add("ENUM", TokenType.Enum);
        // OSLANG 0.62
        Map.Add("IN", TokenType.In);
        Map.Add("EACH", TokenType.Each);

        // OSLANG 0.62
        Map.Add("OSB", TokenType.Osb);
    }

    /// <summary>
    /// Attempts to look up a keyword. Uses ASCII-only case-insensitive comparison (SQ-002).
    /// </summary>
    public static bool TryGetKeyword(string upperText, out TokenType type) => Map.TryGetValue(upperText, out type);

    /// <summary>
    /// Checks if a token type is a keyword.
    /// </summary>
    public static bool IsKeyword(TokenType type) => (ushort)type <= (ushort)TokenType.Osb;
}
