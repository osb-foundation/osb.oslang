using Osb.Engine.Core.Lang;

namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding list for UsingDecl.
/// No BCL dependencies.
/// </summary>
public sealed class UsingList
{
    private UsingDecl[] _items;
    private int _count;

    public UsingList(int initialCapacity = 4)
    {
        _items = new UsingDecl[initialCapacity];
        _count = 0;
    }

    public int Count => _count;
    public UsingDecl this[int index]
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

    public void Add(UsingDecl item)
    {
        if (_count == _items.Length) Grow();
        _items[_count++] = item;
    }

    public void Clear()
    {
        System.Array.Clear(_items, 0, _count);
        _count = 0;
    }

    public UsingDecl[] ToArray()
    {
        var result = new UsingDecl[_count];
        System.Array.Copy(_items, result, _count);
        return result;
    }

    private void Grow()
    {
        int newCapacity = _items.Length == 0 ? 4 : _items.Length * 2;
        var newItems = new UsingDecl[newCapacity];
        System.Array.Copy(_items, newItems, _count);
        _items = newItems;
    }

    public struct Enumerator
    {
        private readonly UsingList _list;
        private int _index;

        public Enumerator(UsingList list) { _list = list; _index = 0; }

        public bool MoveNext() => _index < _list._count;
        public UsingDecl Current => _list._items[_index++];
    }

    public Enumerator GetEnumerator() => new Enumerator(this);
}