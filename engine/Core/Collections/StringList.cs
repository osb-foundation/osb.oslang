namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding dynamic array for strings.
/// No BCL dependencies.
/// </summary>
public sealed class StringList
{
    private string[] _items;
    private int _count;

    public StringList(int initialCapacity = 4)
    {
        _items = new string[initialCapacity];
        _count = 0;
    }

    public int Count => _count;
    public string this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new IndexOutOfRangeException();
            return _items[index];
        }
        set
        {
            if ((uint)index >= (uint)_count) throw new IndexOutOfRangeException();
            _items[index] = value;
        }
    }

    public void Add(string item)
    {
        if (_count == _items.Length) Grow();
        _items[_count++] = item;
    }

    public int IndexOf(string item)
    {
        for (int i = 0; i < _count; i++)
            if (_items[i] == item) return i;
        return -1;
    }

    public bool Contains(string item) => IndexOf(item) >= 0;

    public void Clear()
    {
        System.Array.Clear(_items, 0, _count);
        _count = 0;
    }

    public string[] ToArray()
    {
        var result = new string[_count];
        System.Array.Copy(_items, result, _count);
        return result;
    }

    private void Grow()
    {
        int newCapacity = _items.Length == 0 ? 4 : _items.Length * 2;
        var newItems = new string[newCapacity];
        System.Array.Copy(_items, newItems, _count);
        _items = newItems;
    }

    public struct Enumerator
    {
        private readonly StringList _list;
        private int _index;

        public Enumerator(StringList list) { _list = list; _index = 0; }

        public bool MoveNext() => _index < _list._count;
        public string Current => _list._items[_index++];
    }

    public Enumerator GetEnumerator() => new Enumerator(this);
}