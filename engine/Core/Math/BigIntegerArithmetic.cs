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
}