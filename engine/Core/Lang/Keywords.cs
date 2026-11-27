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
    private static StringMap<ushort>? _map;

    private static StringMap<ushort> Map
    {
        get
        {
            return _map ??= BuildMap();
        }
    }

    private static StringMap<ushort> BuildMap()
    {
        var map = new StringMap<ushort>();
        // Core keywords (OSLANG 0.1+)
        AddKeyword(map, "AND", TokenType.And);
        AddKeyword(map, "BASE", TokenType.Base);
        AddKeyword(map, "BOOLEAN", TokenType.Boolean);
        AddKeyword(map, "BOOL", TokenType.Bool);
        AddKeyword(map, "BREAK", TokenType.Break);
        AddKeyword(map, "CATCH", TokenType.Catch);
        AddKeyword(map, "CEIL", TokenType.Ceil);
        AddKeyword(map, "CLASS", TokenType.Class);
        AddKeyword(map, "CLEAR", TokenType.Clear);
        AddKeyword(map, "CONSTRUCTOR", TokenType.Constructor);
        AddKeyword(map, "CONTINUE", TokenType.Continue);
        AddKeyword(map, "COUNT", TokenType.Count);
        AddKeyword(map, "DO", TokenType.Do);
        AddKeyword(map, "ELIF", TokenType.Elif);
        AddKeyword(map, "ELSE", TokenType.Else);
        AddKeyword(map, "END", TokenType.End);
        AddKeyword(map, "FALSE", TokenType.False);
        AddKeyword(map, "FLOOR", TokenType.Floor);
        AddKeyword(map, "FOR", TokenType.For);
        AddKeyword(map, "FUNCTION", TokenType.Function);
        AddKeyword(map, "GLOBAL", TokenType.Global);
        AddKeyword(map, "IF", TokenType.If);
        AddKeyword(map, "INPUT", TokenType.Input);
        AddKeyword(map, "INTERFACE", TokenType.Interface);
        AddKeyword(map, "ME", TokenType.Me);
        AddKeyword(map, "NEW", TokenType.New);
        AddKeyword(map, "NOT", TokenType.Not);
        AddKeyword(map, "NULL", TokenType.Null);
        AddKeyword(map, "NUMBER", TokenType.Number);
        AddKeyword(map, "OR", TokenType.Or);
        AddKeyword(map, "POW", TokenType.Pow);
        AddKeyword(map, "PRINT", TokenType.Print);
        AddKeyword(map, "PRIVATE", TokenType.Private);
        AddKeyword(map, "PROTECTED", TokenType.Protected);
        AddKeyword(map, "PUBLIC", TokenType.Public);
        AddKeyword(map, "RETURN", TokenType.Return);
        AddKeyword(map, "SQRT", TokenType.Sqrt);
        AddKeyword(map, "STEP", TokenType.Step);
        AddKeyword(map, "STRING", TokenType.String);
        AddKeyword(map, "STR", TokenType.Str);
        AddKeyword(map, "THEN", TokenType.Then);
        AddKeyword(map, "TO", TokenType.To);
        AddKeyword(map, "TRUE", TokenType.True);
        AddKeyword(map, "TRY", TokenType.Try);
        AddKeyword(map, "TYPEOF", TokenType.TypeOf);
        AddKeyword(map, "VAR", TokenType.Var);
        AddKeyword(map, "WHILE", TokenType.While);

        // OSLANG 0.3
        AddKeyword(map, "SWITCH", TokenType.Switch);
        AddKeyword(map, "CASE", TokenType.Case);
        AddKeyword(map, "DEFAULT", TokenType.Default);
        AddKeyword(map, "VIRTUAL", TokenType.Virtual);
        AddKeyword(map, "OVERRIDE", TokenType.Override);
        AddKeyword(map, "EVENT", TokenType.Event);
        AddKeyword(map, "ON", TokenType.On);
        AddKeyword(map, "RAISE", TokenType.Raise);
        AddKeyword(map, "USING", TokenType.Using);

        // OSLANG 0.4
        AddKeyword(map, "MATH", TokenType.Math);
        AddKeyword(map, "FILE", TokenType.File);
        AddKeyword(map, "DIR", TokenType.Dir);

        // OSLANG 0.5
        AddKeyword(map, "DATE", TokenType.Date);
        AddKeyword(map, "TIME", TokenType.Time);

        // OSLANG 0.6
        AddKeyword(map, "SHOW", TokenType.Show);
        AddKeyword(map, "MOD", TokenType.Mod);

        // OSLANG 0.6 - Standard Library namespace
        AddKeyword(map, "OSL", TokenType.Osl);

        // OSLANG 0.61
        AddKeyword(map, "ENUM", TokenType.Enum);
        // OSLANG 0.62
        AddKeyword(map, "IN", TokenType.In);
        AddKeyword(map, "EACH", TokenType.Each);

        // OSLANG 0.62
        AddKeyword(map, "OSB", TokenType.Osb);
        return map;
    }

    private static void AddKeyword(StringMap<ushort> map, string key, TokenType type)
    {
        map.Add(key, (ushort)type);
    }

    /// <summary>
    /// Attempts to look up a keyword. Uses ASCII-only case-insensitive comparison (SQ-002).
    /// </summary>
    public static bool TryGetKeyword(string upperText, out TokenType type)
    {
        var result = Map.TryGetValue(upperText, out ushort value);
        type = (TokenType)value;
        return result;
    }

    /// <summary>
    /// Checks if a token type is a keyword.
    /// </summary>
    public static bool IsKeyword(TokenType type) => (ushort)type <= (ushort)TokenType.Osb;
}