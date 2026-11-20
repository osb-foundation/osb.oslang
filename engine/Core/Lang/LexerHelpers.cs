using Osb.Engine.Core.Collections;

namespace Osb.Engine.Core.Lang;

/// <summary>
/// Freestanding helper methods for the Lexer.
/// Replaces BCL dependencies (string.ToUpperInvariant, double.Parse with CultureInfo)
/// with ASCII-only implementations suitable for NativeAOT / MiniCoreLib.
/// </summary>
public partial class Lexer
{
    /// <summary>
    /// ASCII-only uppercase conversion (SQ-002). Replaces string.ToUpperInvariant()
    /// for freestanding compatibility.
    /// </summary>
    private static string ToUpperInvariant(string s)
    {
        var result = new StringBuffer(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c >= 'a' && c <= 'z') c = (char)(c - 'a' + 'A');
            result.Append(c);
        }
        return result.ToString();
    }

    /// <summary>
    /// Simple ASCII double parser. Replaces double.Parse(text, CultureInfo.InvariantCulture)
    /// for freestanding compatibility. Handles integer and fractional parts only;
    /// exponent notation is not produced by the lexer's ReadNumber.
    /// </summary>
    private static double ParseDouble(string text)
    {
        double result = 0;
        int i = 0;

        // Integer part
        while (i < text.Length && text[i] >= '0' && text[i] <= '9')
        {
            result = result * 10 + (text[i] - '0');
            i++;
        }

        // Fractional part
        if (i < text.Length && text[i] == '.')
        {
            i++;
            double fraction = 0.1;
            while (i < text.Length && text[i] >= '0' && text[i] <= '9')
            {
                result += (text[i] - '0') * fraction;
                fraction *= 0.1;
                i++;
            }
        }

        return result;
    }
}
