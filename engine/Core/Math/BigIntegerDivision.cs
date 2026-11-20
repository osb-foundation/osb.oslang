namespace Osb.Engine.Core.Math;

/// <summary>
/// Division operations for BigInteger.
/// </summary>
internal static class BigIntegerDivision
{
    public static (ulong[] quotient, ulong[] remainder) DivModUnsigned(ulong[] a, ulong[] b)
    {
        if (b.Length == 1 && b[0] == 0) throw new DivideByZeroException();
        
        // Special case: division by power of 2 (just right shift)
        if (b.Length == 1 && (b[0] & (b[0] - 1)) == 0)
        {
            int shift = 0;
            ulong v = b[0];
            while (v > 1) { v >>= 1; shift++; }
            return DivModByPowerOfTwo(a, shift);
        }
        
        int cmp = BigIntegerComparison.CompareUnsigned(a, b);
        if (cmp < 0) return (Array.Empty<ulong>(), a);
        if (cmp == 0) return (new ulong[] { 1 }, Array.Empty<ulong>());

        var remainder = new ulong[a.Length];
        Array.Copy(a, remainder, a.Length);
        var quotient = new ulong[a.Length];

        int n = b.Length;
        int m = a.Length - n;

        ulong divisorHi = b[n - 1];
        ulong divisorHi2 = n > 1 ? b[n - 2] : 0;

        for (int j = m; j >= 0; j--)
        {
            ulong remHi = j + n < remainder.Length ? remainder[j + n] : 0;
            ulong remHi2 = j + n - 1 < remainder.Length ? remainder[j + n - 1] : 0;
            ulong remHi3 = j + n - 2 < remainder.Length ? remainder[j + n - 2] : 0;

            ulong qhat = remHi == divisorHi ? ulong.MaxValue : (remHi << 64) / (divisorHi + 1);
            ulong qhat2 = ((remHi << 64) | remHi2) / divisorHi;
            if (qhat2 < qhat) qhat = qhat2;

            while (true)
            {
                (ulong prodHi, ulong prodLo) = BigIntegerOps.Mul128(qhat, divisorHi);
                ulong prodHi2 = 0;
                if (n > 1)
                {
                    (ulong _, ulong prodLo2) = BigIntegerOps.Mul128(qhat, divisorHi2);
                    prodHi2 = qhat * divisorHi2 + (prodLo2 >> 64);
                }

                if (prodHi > remHi || (prodHi == remHi && prodHi2 > remHi2) || (prodHi == remHi && prodHi2 == remHi2 && prodLo > remHi3))
                {
                    qhat--;
                }
                else break;
            }

            ulong borrow = 0;
            ulong carry = 0;
            for (int i = 0; i < n; i++)
            {
                (ulong prodHi, ulong prodLo) = BigIntegerOps.Mul128(qhat, b[i]);
                ulong sub = remainder[j + i] - prodLo - borrow;
                remainder[j + i] = sub;
                borrow = (remainder[j + i] > sub || borrow > 0 && remainder[j + i] == sub) ? 1UL : 0UL;
                carry += prodHi;
            }
            remainder[j + n] -= borrow + carry;
            quotient[j] = qhat;
        }

        return (Normalize(quotient), Normalize(remainder));
    }

    public static (ulong[] quotient, ulong[] remainder) DivModByPowerOfTwo(ulong[] a, int shift)
    {
        if (shift == 0) return (a, Array.Empty<ulong>());
        
        int wordShift = shift / 64;
        int bitShift = shift % 64;
        
        // Quotient = a >> shift
        int qLen = a.Length > wordShift ? a.Length - wordShift : 0;
        var quotient = new ulong[qLen];
        if (qLen > 0)
        {
            if (bitShift == 0)
            {
                Array.Copy(a, wordShift, quotient, 0, qLen);
            }
            else
            {
                for (int i = 0; i < qLen - 1; i++)
                {
                    quotient[i] = (a[wordShift + i] >> bitShift) | (a[wordShift + i + 1] << (64 - bitShift));
                }
                quotient[qLen - 1] = a[wordShift + qLen - 1] >> bitShift;
            }
        }
        
        // Remainder = a & ((1 << shift) - 1)
        int rWords = (shift + 63) / 64;
        var remainder = new ulong[rWords];
        if (bitShift == 0)
        {
            int copyLen = wordShift < a.Length ? wordShift : a.Length;
            Array.Copy(a, 0, remainder, 0, copyLen);
        }
        else
        {
            int copyLen = (wordShift + 1) < a.Length ? (wordShift + 1) : a.Length;
            Array.Copy(a, 0, remainder, 0, copyLen);
            ulong mask = (1UL << bitShift) - 1;
            if (wordShift < remainder.Length)
                remainder[wordShift] &= mask;
        }
        
        return (Normalize(quotient), Normalize(remainder));
    }

    // Division by 10 for ToString()
    public static (ulong[] quotient, ulong remainder) DivModByTen(ulong[] a)
    {
        if (a.Length == 0) return (Array.Empty<ulong>(), 0);
        
        var quotient = new ulong[a.Length];
        ulong rem = 0;
        
        // 2^64 / 10 = 1844674407370955161 (floor)
        // 2^64 % 10 = 6
        
        for (int i = a.Length - 1; i >= 0; i--)
        {
            // dividend = rem * 2^64 + a[i]
            // dividend / 10 = rem * floor(2^64/10) + a[i]/10 + (rem * (2^64%10) + a[i]%10)/10
            // dividend % 10 = (rem * (2^64%10) + a[i]) % 10
            
            ulong q = rem * 1844674407370955161UL + a[i] / 10;
            rem = (rem * 6UL + a[i]) % 10;
            quotient[i] = q;
        }
        
        return (Normalize(quotient), rem);
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