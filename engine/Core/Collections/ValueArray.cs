using Osb.Engine.Core.Values;

using Osb.Engine.Core.Eval;
using System.Collections;
using System.Collections.Generic;

namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding dynamic array for Value objects.
/// No BCL dependencies. Stores values in platform memory via EngineHeap.
/// </summary>
public sealed class ValueArray : IReadOnlyList<Value>
{
    private Value[] _items;
    private int _count;

    public ValueArray(int initialCapacity = 4)
    {
        _items = new Value[initialCapacity];
        _count = 0;
    }

    public int Count => _count;
    public Value this[int index]
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

    public void Add(Value item)
    {
        if (_count == _items.Length) Grow();
        _items[_count++] = item;
    }

    public void AddRange(ValueArray other)
    {
        EnsureCapacity(_count + other._count);
        System.Array.Copy(other._items, 0, _items, _count, other._count);
        _count += other._count;
    }

    public void Clear()
    {
        // Don't zero out for performance - count is authoritative
        _count = 0;
    }

    public bool RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count) return false;
        _count--;
        if (index < _count)
            System.Array.Copy(_items, index + 1, _items, index, _count - index);
        _items[_count] = default;
        return true;
    }

    public int IndexOf(Value item)
    {
        for (int i = 0; i < _count; i++)
            if (ValueUtilities.ValuesEqual(_items[i], item))
                return i;
        return -1;
    }

    public bool Contains(Value item) => IndexOf(item) >= 0;

    public Value[] ToArray()
    {
        var result = new Value[_count];
        System.Array.Copy(_items, result, _count);
        return result;
    }

    public void EnsureCapacity(int capacity)
    {
        if (capacity > _items.Length) GrowTo(capacity);
    }

    private void Grow()
    {
        int newCapacity = _items.Length == 0 ? 4 : _items.Length * 2;
        GrowTo(newCapacity);
    }

    private void GrowTo(int capacity)
    {
        var newItems = new Value[capacity];
        System.Array.Copy(_items, newItems, _count);
        _items = newItems;
    }

    public struct Enumerator : IEnumerator<Value>
    {
        private readonly ValueArray _array;
        private int _index;

        public Enumerator(ValueArray array) { _array = array; _index = 0; }

        public bool MoveNext() => _index < _array._count;
        public Value Current => _array._items[_index];
        object IEnumerator.Current => Current;
        public void Reset() => _index = 0;
        public void Dispose() { }
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    IEnumerator<Value> IEnumerable<Value>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}