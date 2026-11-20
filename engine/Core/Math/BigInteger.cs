namespace Osb.Engine.Core.Math;

/// <summary>
/// Freestanding BigInteger implementation for number formatting.
/// Supports only operations needed by the deterministic number formatter.
/// No BCL dependencies.
/// </summary>
public readonly struct BigInteger : IEquatable<BigInteger>, IComparable<BigInteger>
{
    private readonly ulong[] _words;
    private readonly bool _isNegative;

    public BigInteger(ulong[] words, bool isNegative = false)
    {
        _words = Normalize(words);
        _isNegative = isNegative && !IsZeroWords(words);
    }

    public BigInteger(ulong value)
    {
        _words = value == 0 ? Array.Empty<ulong>() : new ulong[] { value };
        _isNegative = false;
    }

    public BigInteger(long value)
    {
        if (value >= 0)
        {
            _words = value == 0 ? Array.Empty<ulong>() : new ulong[] { (ulong)value };
            _isNegative = false;
        }
        else
        {
            _words = new ulong[] { (ulong)(-value) };
            _isNegative = true;
        }
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

    private static bool IsZeroWords(ulong[] words)
    {
        foreach (var w in words) if (w != 0) return false;
        return true;
    }

    public static BigInteger Zero => new(Array.Empty<ulong>());
    public static BigInteger One => new(new ulong[] { 1 });
    public static BigInteger MinusOne => new(new ulong[] { 1 }, true);

    public bool IsZero => _words.Length == 0 || (_words.Length == 1 && _words[0] == 0);
    public bool IsOne => _words.Length == 1 && _words[0] == 1 && !_isNegative;
    public int BitLength => _words.Length == 0 ? 0 : (int)(64 * (_words.Length - 1) + BitOperations.Log2(_words[^1]) + 1);

    public static BigInteger operator <<(BigInteger value, int shift) => BigIntegerOps.ShiftLeft(value, shift);
    public static BigInteger operator >>(BigInteger value, int shift) => BigIntegerOps.ShiftRight(value, shift);
    public static BigInteger operator +(BigInteger a, BigInteger b) => BigIntegerOps.Add(a, b);
    public static BigInteger operator -(BigInteger a, BigInteger b) => BigIntegerOps.Subtract(a, b);
    public static BigInteger operator *(BigInteger a, BigInteger b) => BigIntegerOps.Multiply(a, b);
    public static BigInteger operator /(BigInteger a, BigInteger b) => BigIntegerOps.Divide(a, b);
    public static BigInteger operator %(BigInteger a, BigInteger b) => BigIntegerOps.Remainder(a, b);

    public static bool operator ==(BigInteger a, BigInteger b) => a.CompareTo(b) == 0;
    public static bool operator !=(BigInteger a, BigInteger b) => a.CompareTo(b) != 0;
    public static bool operator <(BigInteger a, BigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BigInteger a, BigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BigInteger a, BigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BigInteger a, BigInteger b) => a.CompareTo(b) >= 0;

    public int CompareTo(BigInteger other)
    {
        if (_isNegative != other._isNegative)
            return _isNegative ? -1 : 1;
        if (_isNegative)
            return -BigIntegerComparison.CompareUnsigned(_words, other._words);
        return BigIntegerComparison.CompareUnsigned(_words, other._words);
    }

    public bool Equals(BigInteger other) => CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is BigInteger bi && Equals(bi);
    public override int GetHashCode() => _words.Length > 0 ? _words[0].GetHashCode() : 0;

    public static BigInteger Pow(BigInteger baseValue, int exponent) => BigIntegerOps.Pow(baseValue, exponent);

    public override string ToString()
    {
        if (IsZero) return "0";
        if (_words.Length == 1) return (_isNegative ? "-" : "") + _words[0].ToString();

        var temp = this;
        var digits = new List<char>();

        while (!temp.IsZero)
        {
            var (quotient, remainder) = BigIntegerDivision.DivModByTen(temp._words);
            digits.Add((char)('0' + remainder));
            temp = new BigInteger(quotient, temp._isNegative);
        }

        if (_isNegative) digits.Add('-');
        digits.Reverse();
        return new string(digits.ToArray());
    }

    public static implicit operator BigInteger(ulong value) => new(value);
    public static implicit operator BigInteger(long value) => new(value);

    internal ulong[] Words => _words;
    internal bool IsNegativeFlag => _isNegative;
}