using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Math;

namespace Osb.Engine.Core.Values;

/// <summary>
/// Number formatting utilities - freestanding implementation using custom BigInteger.
/// No BCL dependencies (System.Math, CultureInfo, System.Numerics.BigInteger, decimal).
/// </summary>
internal static class NumberFormatting
{
    public static string FormatNumber(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";

        // Handle negative zero
        if (value == 0 && BitConverter.DoubleToUInt64Bits(value) == 0x8000000000000000)
            return "-0";

        // Extract IEEE 754 components
        ulong bits = BitConverter.DoubleToUInt64Bits(value);
        bool isNegative = (bits & 0x8000000000000000) != 0;
        double absValue = isNegative ? -value : value;

        // Check if integer-valued (no fractional part) and within safe integer range
        // For doubles, integers up to 2^53 can be represented exactly
        if (absValue < 9007199254740992.0) // 2^53
        {
            double truncated = (long)absValue;
            if (absValue == truncated)
            {
                // Integer value - format without decimal point
                return FormatInteger((long)value);
            }
        }

        // Fractional value - use exact 15-significant-digit formatting via BigInteger
        return FormatDoubleExact(bits, isNegative, absValue);
    }

    private static string FormatInteger(long value)
    {
        if (value == 0) return "0";

        bool negative = value < 0;
        ulong abs = negative ? (ulong)(-value) : (ulong)value;

        // Count digits
        int digits = 0;
        ulong temp = abs;
        while (temp > 0)
        {
            temp /= 10;
            digits++;
        }

        char[] chars = new char[digits + (negative ? 1 : 0)];
        int pos = chars.Length - 1;

        while (abs > 0)
        {
            chars[pos--] = (char)('0' + (abs % 10));
            abs /= 10;
        }

        if (negative) chars[0] = '-';

        return new string(chars);
    }

    // Format a double using exact bit manipulation for 15 significant digits
    private static string FormatDoubleExact(ulong bits, bool isNegative, double absValue)
    {
        // Extract IEEE 754 components
        int exponent = (int)((bits >> 52) & 0x7FF);
        ulong mantissa = bits & 0xFFFFFFFFFFFFFUL;

        // Handle subnormal numbers
        if (exponent == 0)
        {
            if (mantissa == 0) return isNegative ? "-0" : "0";
            return FormatSubnormal(mantissa, isNegative);
        }

        // Normal number: value = (1 + mantissa/2^52) * 2^(exponent-1023)
        // Exact significand = 2^52 + mantissa
        ulong significand = (1UL << 52) | mantissa;
        int binaryExp = exponent - 1023;

        // Compute decimal exponent: floor(log10(value))
        int decExp = ComputeDecimalExponent(significand, binaryExp);

        // Format using exact BigInteger arithmetic
        return FormatUsingBigInteger(significand, binaryExp, decExp, isNegative);
    }

    // Compute decimal exponent for normal double using integer arithmetic
    private static int ComputeDecimalExponent(ulong significand, int binaryExp)
    {
        // value = significand * 2^(binaryExp - 52)
        // We want floor(log10(value)) = largest k such that 10^k <= value
        // For k >= 0: 10^k <= value  <=>  10^k * 2^(52 - binaryExp) <= significand
        // For k < 0:  10^k <= value  <=>  1 <= value * 10^(-k)
        //                              <=>  2^52 <= significand * 2^binaryExp * 10^(-k)

        // Binary search on k
        int low = -400;
        int high = 400;

        while (low <= high)
        {
            int mid = (low + high) / 2;
            bool condition;

            if (mid >= 0)
            {
                // Check: 10^mid * 2^(52 - binaryExp) <= significand
                BigInteger tenPow = BigInteger.Pow(new BigInteger(10), mid);
                int pow2 = 52 - binaryExp;
                BigInteger lhs;
                if (pow2 >= 0) lhs = tenPow << pow2; else lhs = tenPow >> -pow2;
                condition = lhs <= new BigInteger(significand);
            }
            else
            {
                // Check: 2^52 <= significand * 2^binaryExp * 10^(-mid)
                BigInteger tenPow = BigInteger.Pow(new BigInteger(10), -mid);
                BigInteger lhs = new BigInteger(significand);
                if (binaryExp >= 0) lhs <<= binaryExp; else lhs >>= -binaryExp;
                lhs *= tenPow;
                BigInteger rhs = BigInteger.One << 52;
                condition = lhs >= rhs;
            }

            if (condition) low = mid + 1; else high = mid - 1;
        }

        return high; // high is the largest k where 10^k <= value
    }

    // Main formatting using exact BigInteger arithmetic
    // Matches C# double-to-decimal conversion: truncates to 15 significant digits (round toward zero)
    private static string FormatUsingBigInteger(ulong significand, int binaryExp, int decExp, bool isNegative)
    {
        // We want 15 significant digits with truncation (round toward zero)
        // value = significand * 2^(binaryExp - 52)
        // For value in [10^decExp, 10^(decExp+1)), we need 15 significant digits
        // Scale = 10^(14 - decExp) to get 15 digits (1 before decimal + 14 after)
        // Then truncate: floor(value * 10^(14 - decExp))

        int totalDecScale = 14 - decExp; // 10^(14 - decExp) = 15 digits, no guard

        BigInteger numerator;
        BigInteger denominator;

        if (binaryExp >= 0)
        {
            // value * scale = significand * 2^binaryExp * 10^scale / 2^52
            numerator = new BigInteger(significand) << binaryExp;
            if (totalDecScale >= 0)
                numerator *= BigInteger.Pow(new BigInteger(10), totalDecScale);
            else
                numerator /= BigInteger.Pow(new BigInteger(10), -totalDecScale);
            denominator = BigInteger.One << 52;
        }
        else
        {
            // value * scale = significand * 10^scale / 2^(52 - binaryExp)
            // where -binaryExp > 0
            int b = -binaryExp;
            numerator = new BigInteger(significand);
            if (totalDecScale >= 0)
                numerator *= BigInteger.Pow(new BigInteger(10), totalDecScale);
            else
                numerator /= BigInteger.Pow(new BigInteger(10), -totalDecScale);
            denominator = BigInteger.One << (52 + b);
        }

        // Truncate: floor(value * scale) = numerator / denominator (integer division)
        // This matches C# double-to-decimal conversion which truncates to 15 significant digits
        BigInteger truncated = numerator / denominator;

        // Now we have 15 significant digits in 'truncated'
        string digits = truncated.ToString();

        // Ensure we have exactly 15 digits (pad with leading zeros if needed)
        if (digits.Length < 15)
        {
            digits = digits.PadLeft(15, '0');
        }
        else if (digits.Length > 15)
        {
            // Should not happen with truncation, but handle just in case
            decExp++;
            digits = digits.Substring(0, 15);
        }

        // Determine format: scientific if decExp >= 15 or decExp < -4
        bool useScientific = decExp >= 15 || decExp < -4;

        var sb = new StringBuffer();
        if (isNegative) sb.Append('-');

        if (useScientific)
        {
            // Scientific: d.ddddddddddddddE+xxx
            sb.Append(digits[0]);
            sb.Append('.');
            sb.Append(digits.Substring(1));
            sb.Append('E');
            sb.Append(decExp >= 0 ? '+' : '-');
            int absExp = decExp >= 0 ? decExp : -decExp;
            if (absExp >= 100) sb.Append(absExp);
            else if (absExp >= 10) sb.Append('0').Append(absExp);
            else sb.Append("00").Append(absExp);
        }
        else
        {
            // Fixed-point: integer part + fractional part
            if (decExp >= 0)
            {
                // Integer part has decExp+1 digits
                int intDigits = decExp + 1;
                if (intDigits >= digits.Length)
                {
                    sb.Append(digits);
                    sb.Append('0', intDigits - digits.Length);
                }
                else
                {
                    sb.Append(digits.Substring(0, intDigits));
                    sb.Append('.');
                    string frac = digits.Substring(intDigits).TrimEnd('0');
                    if (frac.Length > 0) sb.Append(frac);
                }
            }
            else
            {
                // Pure fraction: 0.00...digits
                sb.Append("0.");
                sb.Append('0', -decExp - 1);
                string frac = digits.TrimEnd('0');
                if (frac.Length > 0) sb.Append(frac);
            }
        }

        return sb.ToString();
    }

    // Handle subnormal numbers
    private static string FormatSubnormal(ulong mantissa, bool isNegative)
    {
        // Subnormal: value = mantissa * 2^-1074
        // Use double for subnormals (they're tiny, < 1e-300)
        double val = BitConverter.UInt64BitsToDouble(mantissa); // exponent=0
        return FormatSmallDouble(val, isNegative);
    }

    private static string FormatSmallDouble(double val, bool isNegative)
    {
        // For very small values, use a simpler approach
        var sb = new StringBuffer();
        if (isNegative) sb.Append('-');

        // Convert to string with enough precision
        // Since val < 1e-300, use scientific notation
        // Find decimal exponent
        int decExp = 0;
        double temp = val;
        while (temp < 1.0 && temp > 0)
        {
            temp *= 10.0;
            decExp--;
        }

        // Now temp in [1, 10)
        // Get 15 digits
        long scaled = (long)(temp * 1e14 + 0.5);
        string digits = scaled.ToString().PadLeft(15, '0');

        sb.Append(digits[0]);
        sb.Append('.');
        sb.Append(digits.Substring(1));
        sb.Append('E');
        sb.Append(decExp >= 0 ? '+' : '-');
        int absExp = decExp >= 0 ? decExp : -decExp;
        if (absExp >= 100) sb.Append(absExp);
        else if (absExp >= 10) sb.Append('0').Append(absExp);
        else sb.Append("00").Append(absExp);

        return sb.ToString();
    }
}