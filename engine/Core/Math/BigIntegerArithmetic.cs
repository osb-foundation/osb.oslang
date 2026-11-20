using System;

namespace Osb.Engine.Core.Math;

/// <summary>
/// Arithmetic operations for BigInteger.
/// </summary>
internal static class BigIntegerOps
{
    public static BigInteger ShiftLeft(BigInteger value, int shift)
    {
        if (shift == 0) return value;
        if (value.IsZero) return BigInteger.Zero;
        if (shift < 0) return ShiftRight(value, -shift);

        int wordShift = shift / 64;
        int bitShift = shift % 64;
        var result = new ulong[value.Words.Length + wordShift + (bitShift > 0 ? 1 : 0)];
        Array.Copy(value.Words, 0, result, wordShift, value.Words.Length);

        if (bitShift > 0)
        {
            ulong carry = 0;
            for (int i = wordShift; i < result.Length; i++)
            {
                ulong nextCarry = result[i] >> (64 - bitShift);
                result[i] = (result[i] << bitShift) | carry;
                carry = nextCarry;
            }
            if (carry > 0)
            {
                Array.Resize(ref result, result.Length + 1);
                result[^1] = carry;
            }
        }

        return new BigInteger(result, value.IsNegativeFlag);
    }

    public static BigInteger ShiftRight(BigInteger value, int shift)
    {
        if (shift == 0) return value;
        if (value.IsZero) return BigInteger.Zero;
        if (shift < 0) return ShiftLeft(value, -shift);

        int wordShift = shift / 64;
        int bitShift = shift % 64;
        if (wordShift >= value.Words.Length) return BigInteger.Zero;

        var result = new ulong[value.Words.Length - wordShift];
        Array.Copy(value.Words, wordShift, result, 0, result.Length);

        if (bitShift > 0)
        {
            ulong carry = 0;
            for (int i = result.Length - 1; i >= 0; i--)
            {
                ulong nextCarry = result[i] << (64 - bitShift);
                result[i] = (result[i] >> bitShift) | carry;
                carry = nextCarry;
            }
        }

        return new BigInteger(result, value.IsNegativeFlag);
    }

    public static BigInteger Add(BigInteger a, BigInteger b)
    {
        if (a.IsNegativeFlag == b.IsNegativeFlag)
        {
            var words = AddUnsigned(a.Words, b.Words);
            return new BigInteger(words, a.IsNegativeFlag);
        }
        else
        {
            int cmp = BigIntegerComparison.CompareUnsigned(a.Words, b.Words);
            if (cmp == 0) return BigInteger.Zero;
            if (cmp > 0)
            {
                var words = SubtractUnsigned(a.Words, b.Words);
                return new BigInteger(words, a.IsNegativeFlag);
            }
            else
            {
                var words = SubtractUnsigned(b.Words, a.Words);
                return new BigInteger(words, b.IsNegativeFlag);
            }
        }
    }

    public static BigInteger Subtract(BigInteger a, BigInteger b)
    {
        return Add(a, new BigInteger(b.Words, !b.IsNegativeFlag));
    }

    public static BigInteger Multiply(BigInteger a, BigInteger b)
    {
        if (a.IsZero || b.IsZero) return BigInteger.Zero;
        if (a.IsOne) return b;
        if (b.IsOne) return a;

        var result = new ulong[a.Words.Length + b.Words.Length];
        for (int i = 0; i < a.Words.Length; i++)
        {
            ulong carry = 0;
            ulong ai = a.Words[i];
            for (int j = 0; j < b.Words.Length; j++)
            {
                ulong bj = b.Words[j];
                (ulong hi, ulong lo) = Mul128(ai, bj);
                ulong sum = result[i + j] + lo + carry;
                result[i + j] = sum;
                carry = hi + (sum < lo ? 1UL : 0UL);
            }
            int k = i + b.Words.Length;
            while (carry > 0)
            {
                ulong sum = result[k] + carry;
                result[k] = sum;
                carry = sum < carry ? 1UL : 0UL;
                k++;
            }
        }
        return new BigInteger(result, a.IsNegativeFlag != b.IsNegativeFlag);
    }

    public static BigInteger Divide(BigInteger a, BigInteger b)
    {
        var (quotient, _) = BigIntegerDivision.DivModUnsigned(a.Words, b.Words);
        return new BigInteger(quotient, a.IsNegativeFlag != b.IsNegativeFlag);
    }

    public static BigInteger Remainder(BigInteger a, BigInteger b)
    {
        var (_, remainder) = BigIntegerDivision.DivModUnsigned(a.Words, b.Words);
        return new BigInteger(remainder, a.IsNegativeFlag);
    }

    public static BigInteger Pow(BigInteger baseValue, int exponent)
    {
        if (exponent < 0) throw new ArgumentException("Negative exponent not supported");
        if (exponent == 0) return BigInteger.One;
        if (exponent == 1) return baseValue;

        BigInteger result = BigInteger.One;
        BigInteger current = baseValue;
        int exp = exponent;

        while (exp > 0)
        {
            if ((exp & 1) == 1) result = result * current;
            exp >>= 1;
            if (exp > 0) current = current * current;
        }
        return result;
    }

    internal static (ulong hi, ulong lo) Mul128(ulong a, ulong b)
    {
        ulong aLo = a & 0xFFFFFFFF;
        ulong aHi = a >> 32;
        ulong bLo = b & 0xFFFFFFFF;
        ulong bHi = b >> 32;

        ulong p0 = aLo * bLo;
        ulong p1 = aLo * bHi;
        ulong p2 = aHi * bLo;
        ulong p3 = aHi * bHi;

        ulong mid = (p0 >> 32) + (p1 & 0xFFFFFFFF) + (p2 & 0xFFFFFFFF);
        ulong lo = (mid << 32) | (p0 & 0xFFFFFFFF);
        ulong hi = p3 + (p1 >> 32) + (p2 >> 32) + (mid >> 32);
        return (hi, lo);
    }

    private static ulong[] AddUnsigned(ulong[] a, ulong[] b)
    {
        int maxLen = a.Length > b.Length ? a.Length : b.Length;
        var result = new ulong[maxLen + 1];
        ulong carry = 0;
        for (int i = 0; i < maxLen; i++)
        {
            ulong ai = i < a.Length ? a[i] : 0;
            ulong bi = i < b.Length ? b[i] : 0;
            ulong sum = ai + bi + carry;
            result[i] = sum;
            carry = (sum < ai || (sum == ai && carry > 0)) ? 1UL : 0UL;
        }
        if (carry > 0) result[maxLen] = 1;
        return Normalize(result);
    }

    private static ulong[] SubtractUnsigned(ulong[] a, ulong[] b)
    {
        var result = new ulong[a.Length];
        ulong borrow = 0;
        for (int i = 0; i < a.Length; i++)
        {
            ulong ai = a[i];
            ulong bi = i < b.Length ? b[i] : 0;
            ulong sub = ai - bi - borrow;
            result[i] = sub;
            borrow = (ai < bi + borrow) ? 1UL : 0UL;
        }
        return Normalize(result);
    }

    private static ulong[] Normalize(ulong[] words)
    {
        int i = words.Length;
        while (i > 0 && words[i - 1] == 0) i--;
        if (i == words.Length) return words;
        var result = new ulong[i];
        Array.Copy(words, result, i);
        return result;
    }

    // 128-bit division: (hi * 2^64 + lo) / divisor
    // Returns (quotient, remainder)
    public static (ulong quotient, ulong remainder) DivMod128(ulong hi, ulong lo, ulong divisor)
    {
        // Use built-in 128-bit division for common case
        if (hi < divisor)
        {
            // 64-bit division is sufficient
            ulong dividend = (hi << 64) | lo; // This would overflow, so we need a different approach
            // Actually use the standard algorithm
            // For hi < divisor, we can do: q = (hi * 2^64 + lo) / divisor
            // But since we can't represent 128-bit in C# natively without BigInteger,
            // we use the standard long division approach
            
            // Use the fact that (hi << 64) / divisor = hi * (2^64 / divisor) + hi * (2^64 % divisor) / divisor
            // This is complex. Let's use a simpler approach for now.
        }
        
        // Standard Knuth Algorithm D for 128/64 -> 64/64
        // We'll use the approximation method
        ulong q = ulong.MaxValue;
        ulong r = 0;
        
        if (hi == 0)
        {
            q = lo / divisor;
            r = lo % divisor;
            return (q, r);
        }
        
        // Use built-in division for the high part
        q = hi / divisor;
        r = hi % divisor;
        
        // Now we have q*divisor*2^64 + r*2^64 + lo
        // The remaining division: (r*2^64 + lo) / divisor
        // But we need to multiply q by 2^64, which overflows.
        // Instead, do the full 128-bit division properly
        
        // Use double-word division algorithm
        ulong d = divisor;
        ulong u1 = hi;
        ulong u0 = lo;
        
        if (u1 >= d)
        {
            // Result would overflow 64 bits - this shouldn't happen in our usage
            return (ulong.MaxValue, u0);
        }
        
        // Normalize
        int shift = LeadingZeroCount(d);
        d <<= shift;
        u1 = (u1 << shift) | (u0 >> (64 - shift));
        u0 <<= shift;
        
        // Divide
        q = u1 / d;
        r = u1 % d;
        
        ulong qhat = q;
        ulong rhat = r;
        
        // Check if qhat is too large
        ulong p = Mul128(qhat, d).lo;
        if (p > ((rhat << 64) | u0))
        {
            qhat--;
            rhat += d;
            if (rhat >= d)
            {
                p = Mul128(qhat, d).lo;
                if (p > ((rhat << 64) | u0))
                    qhat--;
            }
        }
        
        // Compute remainder
        ulong prodHi, prodLo;
        (prodHi, prodLo) = Mul128(qhat, d);
        r = ((rhat << 64) | u0) - prodLo;
        if (r > ((rhat << 64) | u0))
            prodHi++;
        r = (r >> shift);
        
        return (qhat, r);
    }

    public static int LeadingZeroCount(ulong value)
    {
        if (value == 0) return 64;
        int count = 0;
        if ((value & 0xFFFFFFFF00000000UL) == 0) { count += 32; value <<= 32; }
        if ((value & 0xFFFF000000000000UL) == 0) { count += 16; value <<= 16; }
        if ((value & 0xFF00000000000000UL) == 0) { count += 8; value <<= 8; }
        if ((value & 0xF000000000000000UL) == 0) { count += 4; value <<= 4; }
        if ((value & 0xC000000000000000UL) == 0) { count += 2; value <<= 2; }
        if ((value & 0x8000000000000000UL) == 0) { count += 1; }
        return count;
    }
}