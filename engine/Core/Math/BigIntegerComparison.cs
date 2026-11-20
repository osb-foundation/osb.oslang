namespace Osb.Engine.Core.Math;

/// <summary>
/// Comparison operations for BigInteger.
/// </summary>
internal static class BigIntegerComparison
{
    public static int CompareUnsigned(ulong[] a, ulong[] b)
    {
        // Compare extra words in longer array (little-endian: higher indices = more significant)
        if (a.Length > b.Length)
        {
            for (int k = a.Length - 1; k >= b.Length; k--)
            {
                if (a[k] != 0) return 1;
            }
        }
        else if (b.Length > a.Length)
        {
            for (int k = b.Length - 1; k >= a.Length; k--)
            {
                if (b[k] != 0) return -1;
            }
        }

        // Compare overlapping words from most significant
        int minLen = a.Length < b.Length ? a.Length : b.Length;
        for (int k = minLen - 1; k >= 0; k--)
        {
            if (a[k] != b[k]) return a[k] > b[k] ? 1 : -1;
        }
        return 0;
    }
}