using System;
using System.Runtime.CompilerServices;

using System;

namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding string-keyed map using open addressing with linear probing.
/// No BCL dependencies. Simple, deterministic, suitable for small-to-medium maps.
/// Supports both reference and value types.
/// </summary>
public sealed class StringMap<T>
{
    private string[] _keys;
    private T[] _values;
    private bool[] _occupied;
    private int _count;
    private int _deletedCount;
    private readonly StringComparer _comparer;

    // Load factor threshold for resizing
    private const float MaxLoadFactor = 0.75f;

    public StringMap(int initialCapacity = 8, StringComparer? comparer = null)
    {
        int capacity = RoundUpToPowerOfTwo(System.Math.Max(8, initialCapacity));
        _keys = new string[capacity];
        _values = new T[capacity];
        _occupied = new bool[capacity];
        _count = 0;
        _deletedCount = 0;
        _comparer = comparer ?? StringComparer.OrdinalIgnoreCase;
    }

    public int Count => _count;
    public int Capacity => _keys.Length;

    public T this[string key]
    {
        get
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            int index = FindIndex(key);
            if (index >= 0) return _values[index];
            throw new KeyNotFoundException();
        }
        set
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            Insert(key, value);
        }
    }

    public bool TryGetValue(string key, out T value)
    {
        if (key == null) { value = default!; return false; }
        int index = FindIndex(key);
        if (index >= 0)
        {
            value = _values[index];
            return true;
        }
        value = default!;
        return false;
    }

    public bool ContainsKey(string key)
    {
        if (key == null) return false;
        return FindIndex(key) >= 0;
    }

    public bool Remove(string key)
    {
        if (key == null) return false;
        int index = FindIndex(key);
        if (index < 0) return false;

        _keys[index] = null;
        _values[index] = default!;
        _occupied[index] = false;
        _count--;
        _deletedCount++;
        return true;
    }

    public void Clear()
    {
        if (_count > 0)
        {
            System.Array.Clear(_keys, 0, _keys.Length);
            System.Array.Clear(_values, 0, _values.Length);
            System.Array.Clear(_occupied, 0, _occupied.Length);
            _count = 0;
            _deletedCount = 0;
        }
    }

    public void Add(string key, T value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (TryGetValue(key, out _)) throw new ArgumentException("Key already exists");
        Insert(key, value);
    }

    public struct Enumerator
    {
        private readonly StringMap<T> _map;
        private int _index;

        public Enumerator(StringMap<T> map) { _map = map; _index = 0; }

        public bool MoveNext()
        {
            while (_index < _map._keys.Length)
            {
                if (_map._occupied[_index])
                    return true;
                _index++;
            }
            return false;
        }

        public (string Key, T Value) Current
        {
            get
            {
                var key = _map._keys[_index];
                var value = _map._values[_index];
                _index++;
                return (key!, value!);
            }
        }
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    private int FindIndex(string key)
    {
        int mask = _keys.Length - 1;
        int index = key.GetHashCode() & mask;

        for (int i = 0; i < _keys.Length; i++)
        {
            int probe = (index + i) & mask;

            if (!_occupied[probe])
                return -1; // Empty slot - not found

            string existingKey = _keys[probe];
            if (_comparer.Equals(existingKey, key))
                return probe; // Found
        }
        return -1; // Full table (shouldn't happen with resize)
    }

    private void Insert(string key, T value)
    {
        // Resize if needed
        if ((_count + _deletedCount + 1) > _keys.Length * MaxLoadFactor)
            Resize();

        int mask = _keys.Length - 1;
        int index = key.GetHashCode() & mask;

        for (int i = 0; i < _keys.Length; i++)
        {
            int probe = (index + i) & mask;

            if (!_occupied[probe] || _deletedCount > 0 && _keys[probe] == "\0")
            {
                // Empty or deleted slot
                _keys[probe] = key;
                _values[probe] = value;
                _occupied[probe] = true;
                _count++;
                if (_keys[probe] == "\0") _deletedCount--;
                return;
            }

            if (_comparer.Equals(_keys[probe], key))
            {
                _values[probe] = value;
                return;
            }
        }

        // Should never reach here if resize works correctly
        throw new InvalidOperationException("StringMap: Insertion failed - table full");
    }

    private void Resize()
    {
        int newCapacity = _keys.Length * 2;
        var oldKeys = _keys;
        var oldValues = _values;
        var oldOccupied = _occupied;

        _keys = new string[newCapacity];
        _values = new T[newCapacity];
        _occupied = new bool[newCapacity];
        _count = 0;
        _deletedCount = 0;

        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldOccupied[i] && oldKeys[i] != "\0")
            {
                Insert(oldKeys[i], oldValues[i]);
            }
        }
    }

    private static int RoundUpToPowerOfTwo(int v)
    {
        v--;
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        return v + 1;
    }
}