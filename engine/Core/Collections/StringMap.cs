using System;

namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding string-keyed map using open addressing with linear probing.
/// No BCL dependencies. Simple, deterministic, suitable for small-to-medium maps.
/// Supports both reference and value types.
/// Values are stored in Slot wrapper objects on the heap so that struct types
/// (e.g. Value) survive array element read/write in freestanding NativeAOT,
/// where T[] element access for structs is unreliable.
/// </summary>
public sealed class StringMap<T>
{
    private sealed class Slot
    {
        public T Value;
    }

    private string[] _keys;
    private Slot[] _slots;
    private bool[] _occupied;
    private int _count;
    private int _deletedCount;
    private bool[] _deleted;
    private readonly StringComparer _comparer;

    private const float MaxLoadFactor = 0.75f;

    public StringMap(int initialCapacity = 8, StringComparer? comparer = null)
    {
        int capacity = RoundUpToPowerOfTwo(System.Math.Max(8, initialCapacity));
        _keys = new string[capacity];
        _slots = new Slot[capacity];
        _occupied = new bool[capacity];
        _deleted = new bool[capacity];
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
            if (index >= 0) return _slots[index].Value;
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
            value = _slots[index].Value;
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
        _slots[index] = null!;
        _occupied[index] = true;
        _deleted[index] = true;
        _count--;
        _deletedCount++;
        return true;
    }

    public void Clear()
    {
        if (_count > 0 || _deletedCount > 0)
        {
            System.Array.Clear(_keys, 0, _keys.Length);
            System.Array.Clear(_slots, 0, _slots.Length);
            System.Array.Clear(_occupied, 0, _occupied.Length);
            System.Array.Clear(_deleted, 0, _deleted.Length);
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
                if (_map._occupied[_index] && !_map._deleted[_index])
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
                var value = _map._slots[_index].Value;
                _index++;
                return (key!, value!);
            }
        }
    }

    public Enumerator GetEnumerator() => new Enumerator(this);

    private int FindIndex(string key)
    {
        int mask = _keys.Length - 1;
        int index = StringHash(key) & mask;

        for (int i = 0; i < _keys.Length; i++)
        {
            int probe = (index + i) & mask;

            if (!_occupied[probe])
                return -1;

            if (!_deleted[probe] && _comparer.Equals(_keys[probe], key))
                return probe;
        }
        return -1;
    }

    private void Insert(string key, T value)
    {
        if ((_count + _deletedCount + 1) > _keys.Length * MaxLoadFactor)
        {
            Resize();
        }

        int mask = _keys.Length - 1;
        int index = StringHash(key) & mask;
        int firstDeleted = -1;

        for (int i = 0; i < _keys.Length; i++)
        {
            int probe = (index + i) & mask;

            if (!_occupied[probe])
            {
                int target = firstDeleted >= 0 ? firstDeleted : probe;
                _keys[target] = key;
                var slot = new Slot();
                slot.Value = value;
                _slots[target] = slot;
                _occupied[target] = true;
                _deleted[target] = false;
                if (firstDeleted >= 0) _deletedCount--;
                _count++;
                return;
            }

            if (!_deleted[probe] && _comparer.Equals(_keys[probe], key))
            {
                _slots[probe].Value = value;
                return;
            }

            if (_deleted[probe] && firstDeleted < 0)
                firstDeleted = probe;
        }

        throw new InvalidOperationException("StringMap: Insertion failed - table full");
    }

    private void Resize()
    {
        int newCapacity = _keys.Length * 2;
        var oldKeys = _keys;
        var oldSlots = _slots;
        var oldOccupied = _occupied;
        var oldDeleted = _deleted;

        _keys = new string[newCapacity];
        _slots = new Slot[newCapacity];
        _occupied = new bool[newCapacity];
        _deleted = new bool[newCapacity];
        _count = 0;
        _deletedCount = 0;

        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldOccupied[i] && !oldDeleted[i] && oldKeys[i] != null)
            {
                Insert(oldKeys[i], oldSlots[i].Value);
            }
        }
    }

    private static int StringHash(string s)
    {
        int hash = 0;
        for (int i = 0; i < s.Length; i++)
            hash = hash * 31 + s[i];
        return hash;
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
