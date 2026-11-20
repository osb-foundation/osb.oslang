using System.Collections;
using System.Collections.Generic;

namespace Osb.Engine.Core.Lang;

/// <summary>
/// Fixed-size set for TokenType using linear search.
/// No BCL dependencies. Used by Parser for BuiltinCallKeywords.
///
/// Duplicate behavior: duplicates are stored as-is; Contains() returns true on first match.
/// Invalid enum values: any TokenType value is comparable via == and may be stored.
/// Membership is deterministic: linear scan in insertion order.
/// </summary>
public sealed class TokenTypeSet : IEnumerable<TokenType>
{
    private readonly TokenType[] _items;

    public TokenTypeSet(params TokenType[] items)
    {
        _items = items ?? Array.Empty<TokenType>();
    }

    public int Count => _items.Length;

    public bool Contains(TokenType type)
    {
        for (int i = 0; i < _items.Length; i++)
        {
            if (_items[i] == type) return true;
        }
        return false;
    }

    public Enumerator GetEnumerator() => new Enumerator(_items);

    public struct Enumerator : IEnumerator<TokenType>
    {
        private readonly TokenType[] _items;
        private int _index;

        public Enumerator(TokenType[] items) { _items = items; _index = 0; }

        public bool MoveNext()
        {
            if (_index < _items.Length)
            {
                _index++;
                return true;
            }
            _index = _items.Length + 1;
            return false;
        }

        public TokenType Current
        {
            get
            {
                if (_index == 0 || _index > _items.Length) throw new InvalidOperationException();
                return _items[_index - 1];
            }
        }

        object? IEnumerator.Current => Current;

        public void Reset() => _index = 0;

        public void Dispose() { }
    }

    IEnumerator<TokenType> IEnumerable<TokenType>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
