namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding string buffer for building strings incrementally.
/// No BCL dependencies. Simple char array with growth.
/// </summary>
public sealed class StringBuffer
{
    private char[] _chars;
    private int _length;

    public StringBuffer(int initialCapacity = 16)
    {
        _chars = new char[initialCapacity];
        _length = 0;
    }

    public int Length => _length;

    public StringBuffer Append(char c)
    {
        if (_length == _chars.Length) Grow();
        _chars[_length++] = c;
        return this;
    }

    public StringBuffer Append(char c, int count)
    {
        if (count <= 0) return this;
        EnsureCapacity(_length + count);
        for (int i = 0; i < count; i++)
            _chars[_length++] = c;
        return this;
    }

    public StringBuffer Append(string s)
    {
        if (s == null) return this;
        int needed = _length + s.Length;
        if (needed > _chars.Length) GrowTo(needed);
        for (int i = 0; i < s.Length; i++)
            _chars[_length++] = s[i];
        return this;
    }

    public StringBuffer Append(int value)
    {
        return Append(value.ToString());
    }

    public StringBuffer Append(long value)
    {
        return Append(value.ToString());
    }

    public void Clear()
    {
        _length = 0;
    }

    public override string ToString()
    {
        if (_length == 0) return string.Empty;
        return new string(_chars, 0, _length);
    }

    private void Grow()
    {
        int newCapacity = _chars.Length == 0 ? 16 : _chars.Length * 2;
        GrowTo(newCapacity);
    }

    private void GrowTo(int capacity)
    {
        var newChars = new char[capacity];
        System.Array.Copy(_chars, newChars, _length);
        _chars = newChars;
    }

    private void EnsureCapacity(int capacity)
    {
        if (capacity > _chars.Length) GrowTo(capacity);
    }
}