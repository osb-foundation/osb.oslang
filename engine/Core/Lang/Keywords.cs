using Osb.Engine.Core.Collections;

namespace Osb.Engine.Core.Lang;

/// <summary>
/// Keyword lookup table for the lexer.
/// All keywords are stored in uppercase for case-insensitive matching (ASCII-only per SQ-002).
/// Uses freestanding StringMap instead of BCL Dictionary.
///
/// The map is built lazily on first use via a static field with no explicit
/// static constructor. Freestanding NativeAOT has no GC-statics / cctor-context
/// initializer in the Ring 3 entry path (crt0 -> StartupCodeMain), so an
/// explicit `static Keywords()` would leave the __GCSTATICS base uninitialized
/// and fault on first access. Lazy field init matches the proven pattern used by
/// MiniCoreLib (String.s_stringMethodTable) and the shell.
/// </summary>
internal static class Keywords
{
    private static StringMap<TokenType>? _map;

    private static StringMap<TokenType> Map => _map ??= BuildMap();

    private static StringMap<TokenType> BuildMap()
    {
        var map = new StringMap<TokenType>();
        // Core keywords (OSLANG 0.1+)
        map.Add("AND", TokenType.And);
        map.Add("BASE", TokenType.Base);
        map.Add("BOOLEAN", TokenType.Boolean);
        map.Add("BOOL", TokenType.Bool);
        map.Add("BREAK", TokenType.Break);
        map.Add("CATCH", TokenType.Catch);
        map.Add("CEIL", TokenType.Ceil);
        map.Add("CLASS", TokenType.Class);
        map.Add("CLEAR", TokenType.Clear);
        map.Add("CONSTRUCTOR", TokenType.Constructor);
        map.Add("CONTINUE", TokenType.Continue);
        map.Add("COUNT", TokenType.Count);
        map.Add("DO", TokenType.Do);
        map.Add("ELIF", TokenType.Elif);
        map.Add("ELSE", TokenType.Else);
        map.Add("END", TokenType.End);
        map.Add("FALSE", TokenType.False);
        map.Add("FLOOR", TokenType.Floor);
        map.Add("FOR", TokenType.For);
        map.Add("FUNCTION", TokenType.Function);
        map.Add("GLOBAL", TokenType.Global);
        map.Add("IF", TokenType.If);
        map.Add("INPUT", TokenType.Input);
        map.Add("INTERFACE", TokenType.Interface);
        map.Add("ME", TokenType.Me);
        map.Add("NEW", TokenType.New);
        map.Add("NOT", TokenType.Not);
        map.Add("NULL", TokenType.Null);
        map.Add("NUMBER", TokenType.Number);
        map.Add("OR", TokenType.Or);
        map.Add("POW", TokenType.Pow);
        map.Add("PRINT", TokenType.Print);
        map.Add("PRIVATE", TokenType.Private);
        map.Add("PROTECTED", TokenType.Protected);
        map.Add("PUBLIC", TokenType.Public);
        map.Add("RETURN", TokenType.Return);
        map.Add("SQRT", TokenType.Sqrt);
        map.Add("STEP", TokenType.Step);
        map.Add("STRING", TokenType.String);
        map.Add("STR", TokenType.Str);
        map.Add("THEN", TokenType.Then);
        map.Add("TO", TokenType.To);
        map.Add("TRUE", TokenType.True);
        map.Add("TRY", TokenType.Try);
        map.Add("TYPEOF", TokenType.TypeOf);
        map.Add("VAR", TokenType.Var);
        map.Add("WHILE", TokenType.While);

        // OSLANG 0.3
        map.Add("SWITCH", TokenType.Switch);
        map.Add("CASE", TokenType.Case);
        map.Add("DEFAULT", TokenType.Default);
        map.Add("VIRTUAL", TokenType.Virtual);
        map.Add("OVERRIDE", TokenType.Override);
        map.Add("EVENT", TokenType.Event);
        map.Add("ON", TokenType.On);
        map.Add("RAISE", TokenType.Raise);
        map.Add("USING", TokenType.Using);

        // OSLANG 0.4
        map.Add("MATH", TokenType.Math);
        map.Add("FILE", TokenType.File);
        map.Add("DIR", TokenType.Dir);

        // OSLANG 0.5
        map.Add("DATE", TokenType.Date);
        map.Add("TIME", TokenType.Time);

        // OSLANG 0.6
        map.Add("SHOW", TokenType.Show);
        map.Add("MOD", TokenType.Mod);

        // OSLANG 0.6 - Standard Library namespace
        map.Add("OSL", TokenType.Osl);

        // OSLANG 0.61
        map.Add("ENUM", TokenType.Enum);
        // OSLANG 0.62
        map.Add("IN", TokenType.In);
        map.Add("EACH", TokenType.Each);

        // OSLANG 0.62
        map.Add("OSB", TokenType.Osb);
        return map;
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
