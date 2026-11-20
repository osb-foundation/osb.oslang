namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding dynamic array for integers.
/// No BCL dependencies. Used for code point offsets in HeapString.
/// </summary>
public sealed class IntList
{
    private int[] _items;
    private int _count;

    public IntList(int initialCapacity = 4)
    {
        _items = new int[initialCapacity];
        _count = 0;
    }

    public int Count => _count;
    public int this[int index]
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

    public void Add(int item)
    {
        if (_count == _items.Length) Grow();
        _items[_count++] = item;
    }

    public void Clear()
    {
        _count = 0;
    }

    public int[] ToArray()
    {
        var result = new int[_count];
        System.Array.Copy(_items, result, _count);
        return result;
    }

    private void Grow()
    {
        int newCapacity = _items.Length == 0 ? 4 : _items.Length * 2;
        var newItems = new int[newCapacity];
        System.Array.Copy(_items, newItems, _count);
        _items = newItems;
    }
}