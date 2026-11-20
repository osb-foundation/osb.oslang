using System;
using System.Collections;
using System.Collections.Generic;

namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding generic dynamic array implementing IReadOnlyList<T>.
/// No BCL dependencies beyond System.Array intrinsics.
/// </summary>
public sealed class NodeList<T> : IReadOnlyList<T>
{
    private T[] _items;
    private int _count;

    public NodeList(int initialCapacity = 4)
    {
        if (initialCapacity < 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        _items = new T[initialCapacity];
        _count = 0;
    }

    public int Count => _count;

    public T this[int index]
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

    public void Add(T item)
    {
        if (_count == _items.Length) Grow();
        _items[_count++] = item;
    }

    public void Clear()
    {
        Array.Clear(_items, 0, _count);
        _count = 0;
    }

    public T[] ToArray()
    {
        var result = new T[_count];
        Array.Copy(_items, result, _count);
        return result;
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity > _items.Length) GrowTo(capacity);
    }

    private void Grow()
    {
        int newCapacity = _items.Length == 0 ? 4 : GrowCapacity(_items.Length);
        GrowTo(newCapacity);
    }

    private static int GrowCapacity(int current)
    {
        if (current > int.MaxValue / 2) throw new OverflowException();
        return current * 2;
    }

    private void GrowTo(int capacity)
    {
        var newItems = new T[capacity];
        Array.Copy(_items, newItems, _count);
        _items = newItems;
    }

    public struct Enumerator : IEnumerator<T>
    {
        private readonly NodeList<T> _list;
        private int _index;

        public Enumerator(NodeList<T> list) { _list = list; _index = 0; }

        public bool MoveNext()
        {
            if (_index < _list._count)
            {
                _index++;
                return true;
            }
            _index = _list._count + 1;
            return false;
        }

        public T Current
        {
            get
            {
                if (_index == 0 || _index > _list._count) throw new InvalidOperationException();
                return _list._items[_index - 1];
            }
        }

        object IEnumerator.Current => Current!;

        public void Reset() => _index = 0;

        public void Dispose() { }
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
