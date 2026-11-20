namespace Osb.Engine.Core.Math;

/// <summary>
/// Bit operations for BigInteger.
/// </summary>
internal static class BitOperations
{
    public static int Log2(ulong value)
    {
        if (value == 0) return 0;
        int log = 0;
        if ((value & 0xFFFFFFFF00000000) != 0) { value >>= 32; log += 32; }
        if ((value & 0xFFFF0000) != 0) { value >>= 16; log += 16; }
        if ((value & 0xFF00) != 0) { value >>= 8; log += 8; }
        if ((value & 0xF0) != 0) { value >>= 4; log += 4; }
        if ((value & 0xC) != 0) { value >>= 2; log += 2; }
        if ((value & 0x2) != 0) log += 1;
        return log;
    }
}