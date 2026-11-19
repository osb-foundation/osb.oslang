using System.Text;

namespace Osb.Engine.Lang;

/// <summary>
/// Lexical analyzer for OSLANG.
/// Implements case-insensitive keywords (ASCII-only per SQ-002).
/// Strings are UTF-8 encoded; code-point indexing handled at runtime (SQ-001).
/// </summary>
public sealed class Lexer
{
    private readonly string _source;
    private int _pos;
    private int _line = 1;
    private int _column = 1;
    private int _lineStart = 0;

    public Lexer(string source) => _source = source ?? string.Empty;

    /// <summary>
    /// Tokenizes the entire source into a list of tokens.
    /// </summary>
    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (!IsAtEnd)
        {
            tokens.Add(NextToken());
        }
        tokens.Add(new Token(TokenType.Eof, "", CurrentLocation));
        return tokens;
    }

    private bool IsAtEnd => _pos >= _source.Length;

    private char Current => IsAtEnd ? '\0' : _source[_pos];
    private char Peek => _pos + 1 >= _source.Length ? '\0' : _source[_pos + 1];

    private SourceLocation CurrentLocation => new(_line, _column, _pos);

    private char PeekAt(int pos) => pos + 1 >= _source.Length ? '\0' : _source[pos + 1];

    private char Advance()
    {
        char c = Current;
        _pos++;
        if (c == '\n')
        {
            _line++;
            _column = 1;
            _lineStart = _pos;
        }
        else
        {
            _column++;
        }
        return c;
    }

    private Token NextToken()
    {
        var start = CurrentLocation;
        char c = Advance();

        // Whitespace (excluding newlines)
        if (c == ' ' || c == '\t' || c == '\r')
        {
            return NextToken(); // skip and recurse
        }

        // Newline
        if (c == '\n')
        {
            return new Token(TokenType.Newline, "\n", start);
        }

        // Single-line comment: REM or '
        char next = _pos < _source.Length ? _source[_pos] : '\0';
        if (c == '\'' || (c == 'R' && next == 'E' && _pos + 1 < _source.Length && _source[_pos + 1] == 'E' && _source[_pos + 2] == 'M'))
        {
            if (c == '\'')
            {
                while (!IsAtEnd && Current != '\n') Advance();
            }
            else
            {
                Advance(); // E
                Advance(); // M
                while (!IsAtEnd && Current != '\n') Advance();
            }
            return NextToken();
        }

        // Block comment: /* ... */
        if (c == '/' && next == '*')
        {
            Advance(); // *
            while (!IsAtEnd && !(Current == '*' && _pos + 1 < _source.Length && _source[_pos + 1] == '/'))
            {
                Advance();
            }
            if (!IsAtEnd) { Advance(); Advance(); } // consume */
            return NextToken();
        }

        // Identifiers and keywords
        if (IsIdentifierStart(c))
        {
            return ReadIdentifierOrKeyword(start);
        }

        // Numbers
        if (char.IsDigit(c))
        {
            return ReadNumber(start);
        }

        // String literals
        if (c == '"')
        {
            return ReadString(start);
        }

        // Date literal: #YYYY-MM-DD#
        if (c == '#')
        {
            return ReadDateOrTime(start);
        }

        // Operators and punctuation
        return ReadOperatorOrPunctuation(c, start);
    }

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_';

    private Token ReadIdentifierOrKeyword(SourceLocation start)
    {
        var sb = new StringBuilder();
        sb.Append(_source[_pos - 1]); // first char already consumed

        while (!IsAtEnd && IsIdentifierPart(Current))
        {
            sb.Append(Advance());
        }

        var text = sb.ToString();
        var upper = text.ToUpperInvariant(); // ASCII-only per SQ-002

        return Keywords.TryGetKeyword(upper, out var keywordType)
            ? new Token(keywordType, text, start)
            : new Token(TokenType.Identifier, text, start);
    }

    private Token ReadNumber(SourceLocation start)
    {
        var sb = new StringBuilder();
        sb.Append(_source[_pos - 1]);

        bool hasDot = false;

        while (!IsAtEnd)
        {
            char c = Current;
            if (char.IsDigit(c))
            {
                sb.Append(Advance());
            }
            else if (c == '.' && !hasDot && char.IsDigit(Peek))
            {
                hasDot = true;
                sb.Append(Advance());
            }
            else
            {
                break;
            }
        }

        var text = sb.ToString();
        double value = double.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        return new Token(TokenType.NumberLiteral, text, start, numberValue: value);
    }

    private Token ReadString(SourceLocation start)
    {
        var sb = new StringBuilder();
        var raw = new StringBuilder();
        raw.Append('"');

        while (!IsAtEnd)
        {
            char c = Advance();
            raw.Append(c);

            if (c == '"')
            {
                // Check for escaped quote ""
                if (Current == '"')
                {
                    raw.Append(Advance());
                    sb.Append('"');
                }
                else
                {
                    break; // end of string
                }
            }
            else if (c == '\\')
            {
                // Escape sequences: \n \t \\ \"
                if (IsAtEnd) break;
                char esc = Advance();
                raw.Append(esc);
                sb.Append(esc switch
                {
                    'n' => '\n',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    _ => esc
                });
            }
            else
            {
                sb.Append(c);
            }
        }

        // Store UTF-8 bytes for runtime (SQ-001: code-point indexing at runtime)
        string value = sb.ToString();
        return new Token(TokenType.StringLiteral, raw.ToString(), start, stringValue: value);
    }

    private Token ReadDateOrTime(SourceLocation start)
    {
        var sb = new StringBuilder();
        sb.Append('#');

        while (!IsAtEnd && Current != '#')
        {
            sb.Append(Advance());
        }

        if (!IsAtEnd)
        {
            sb.Append(Advance()); // consume closing #
        }

        var text = sb.ToString();
        var inner = text.Substring(1, text.Length - 2); // without #

        // Try date: #YYYY-MM-DD#
        if (inner.Length == 10 && inner[4] == '-' && inner[7] == '-')
        {
            if (int.TryParse(inner.Substring(0, 4), out int year) &&
                int.TryParse(inner.Substring(5, 2), out int month) &&
                int.TryParse(inner.Substring(8, 2), out int day))
            {
                int days = DaysSinceEpoch(year, month, day);
                return new Token(TokenType.DateLiteral, text, start, intValue: days);
            }
        }

        // Try time: #HH:MM:SS#
        if (inner.Length >= 8 && inner[2] == ':' && inner[5] == ':')
        {
            if (int.TryParse(inner.Substring(0, 2), out int hour) &&
                int.TryParse(inner.Substring(3, 2), out int minute) &&
                int.TryParse(inner.Substring(6, 2), out int second))
            {
                long millis = ((long)hour * 3600 + minute * 60 + second) * 1000;
                return new Token(TokenType.TimeLiteral, text, start, longValue: millis);
            }
        }

        // Invalid format - treat as identifier
        return new Token(TokenType.Identifier, text, start);
    }

    private static int DaysSinceEpoch(int year, int month, int day)
    {
        // Days since 1970-01-01 (Unix epoch)
        if (month <= 2) { month += 12; year--; }
        int era = year / 400;
        int yoe = year - era * 400;
        int doy = (153 * (month - 3) + 2) / 5 + day - 1;
        int doe = yoe * 365 + yoe / 4 - yoe / 100 + doy;
        return era * 146097 + doe - 719468;
    }

    private Token ReadOperatorOrPunctuation(char c, SourceLocation start)
    {
        char next = _pos < _source.Length ? _source[_pos] : '\0';
        return c switch
        {
            '+' when next == '+' => Make(TokenType.PlusPlus, "++", start),
            '+' when next == '=' => Make(TokenType.PlusEqual, "+=", start),
            '+' => Make(TokenType.Plus, "+", start),

            '-' when next == '>' => Make(TokenType.DoubleArrow, "=>", start),
            '-' when next == '-' => Make(TokenType.MinusMinus, "--", start),
            '-' => Make(TokenType.Minus, "-", start),

            '*' when next == '*' => Make(TokenType.StarStar, "**", start),
            '*' => Make(TokenType.Star, "*", start),

            '/' => Make(TokenType.Slash, "/", start),
            '%' => Make(TokenType.Percent, "%", start),

            '=' when next == '=' => Make(TokenType.Equal, "==", start),
            '=' => Make(TokenType.Assign, "=", start),
            '<' when next == '=' => Make(TokenType.LessEqual, "<=", start),
            '<' when next == '>' => Make(TokenType.NotEqual, "<>", start),
            '<' => Make(TokenType.Less, "<", start),
            '>' when next == '=' => Make(TokenType.GreaterEqual, ">=", start),
            '>' => Make(TokenType.Greater, ">", start),

            '(' => Make(TokenType.LParen, "(", start),
            ')' => Make(TokenType.RParen, ")", start),
            '[' => Make(TokenType.LBracket, "[", start),
            ']' => Make(TokenType.RBracket, "]", start),
            '{' => Make(TokenType.LBrace, "{", start),
            '}' => Make(TokenType.RBrace, "}", start),

            ',' => Make(TokenType.Comma, ",", start),
            ':' => Make(TokenType.Colon, ":", start),
            '.' => Make(TokenType.Dot, ".", start),

            '|' => Make(TokenType.Pipe, "|", start),

            _ => throw new SyntaxException(start, $"Unexpected character '{c}' (0x{(int)c:X2})"),
        };
    }

    private Token Make(TokenType type, string text, SourceLocation start)
    {
        int count = text.Length;
        for (int i = 1; i < count; i++) Advance();
        return new Token(type, text, start);
    }
}

/// <summary>
/// Lexer syntax exception.
/// </summary>
public class SyntaxException : Exception
{
    public SourceLocation Location { get; }

    public SyntaxException(SourceLocation location, string message)
        : base($"Line {location.Line}, Column {location.Column}: {message}")
    {
        Location = location;
    }
}