using System;
using Osb.Engine.Core.Values;

namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding string-keyed map specialized for Value storage.
/// object[] reads work (proven via _heapRefs), so Value components are
/// stored in separate object[]/ulong[]/int[]/bool[] arrays. The ValueKind
/// tag is derived from _heapRef in MakeValue (non-null ⇒ String), since
/// no array type reliably round-trips a primitive kind tag in freestanding
/// NativeAOT. ulong[]/int[] reads return 0 for non-zero values; those
/// fields are only correct when zero (true for String/Object/Null kinds).
/// </summary>
public sealed class ValueMap
{
    private string[] _keys;
    private ulong[] _payloads;
    private object[] _heapRefs;
    private int[] _aux1s;
    private int[] _aux2s;
    private bool[] _occupied;
    private bool[] _deleted;
    private int _count;
    private int _deletedCount;
    private readonly StringComparer _comparer;

    private const float MaxLoadFactor = 0.75f;

    public ValueMap(int initialCapacity = 8, StringComparer? comparer = null)
    {
        int capacity = RoundUpToPowerOfTwo(System.Math.Max(8, initialCapacity));
        _keys = new string[capacity];
        _payloads = new ulong[capacity];
        _heapRefs = new object[capacity];
        _aux1s = new int[capacity];
        _aux2s = new int[capacity];
        _occupied = new bool[capacity];
        _deleted = new bool[capacity];
        _count = 0;
        _deletedCount = 0;
        _comparer = comparer ?? StringComparer.OrdinalIgnoreCase;
    }

    public int Count => _count;
    public int Capacity => _keys.Length;

    public Value this[string key]
    {
        get
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            int index = FindIndex(key);
            if (index >= 0) return MakeValue(index);
            throw new KeyNotFoundException();
        }
        set
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            Insert(key, value._payload, value._heapRef, value._auxInt1, value._auxInt2);
        }
    }

    public bool TryGetValue(string key, out Value value)
    {
        if (key == null) { value = Value.NullValue; return false; }
        int index = FindIndex(key);
        if (index >= 0)
        {
            value = MakeValue(index);
            return true;
        }
        value = Value.NullValue;
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
            System.Array.Clear(_payloads, 0, _payloads.Length);
            System.Array.Clear(_heapRefs, 0, _heapRefs.Length);
            System.Array.Clear(_aux1s, 0, _aux1s.Length);
            System.Array.Clear(_aux2s, 0, _aux2s.Length);
            System.Array.Clear(_occupied, 0, _occupied.Length);
            System.Array.Clear(_deleted, 0, _deleted.Length);
            _count = 0;
            _deletedCount = 0;
        }
    }

    public void Add(string key, Value value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (TryGetValue(key, out _)) throw new ArgumentException("Key already exists");
        Insert(key, value._payload, value._heapRef, value._auxInt1, value._auxInt2);
    }

    public void InsertDirect(string key, ulong payload, object heapRef, int aux1, int aux2)
    {
        Insert(key, payload, heapRef, aux1, aux2);
    }

    public struct Enumerator
    {
        private readonly ValueMap _map;
        private int _index;

        public Enumerator(ValueMap map) { _map = map; _index = 0; }

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

        public (string Key, Value Value) Current
        {
            get
            {
                var key = _map._keys[_index];
                var value = _map.MakeValue(_index);
                _index++;
                return (key!, value);
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

    private void Insert(string key, ulong payload, object heapRef, int aux1, int aux2)
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
                _payloads[target] = payload;
                _heapRefs[target] = heapRef;
                _aux1s[target] = aux1;
                _aux2s[target] = aux2;
                _occupied[target] = true;
                _deleted[target] = false;
                if (firstDeleted >= 0) _deletedCount--;
                _count++;
                return;
            }

            if (!_deleted[probe] && _comparer.Equals(_keys[probe], key))
            {
                _payloads[probe] = payload;
                _heapRefs[probe] = heapRef;
                _aux1s[probe] = aux1;
                _aux2s[probe] = aux2;
                return;
            }

            if (_deleted[probe] && firstDeleted < 0)
                firstDeleted = probe;
        }

        throw new InvalidOperationException("ValueMap: Insertion failed - table full");
    }

    private Value MakeValue(int index)
    {
        ulong p = _payloads[index];
        object h = _heapRefs[index];
        int a1 = _aux1s[index];
        int a2 = _aux2s[index];

        // Derive ValueKind from heap reference: object[] reads work reliably.
        // Non-null heap ref implies a heap-allocated value (String for now).
        ValueKind k = h != null ? ValueKind.String : ValueKind.Null;

        return Value.Make(k, p, h, a1, a2);
    }

    private void Resize()
    {
        int newCapacity = _keys.Length * 2;
        var oldKeys = _keys;
        var oldPayloads = _payloads;
        var oldHeapRefs = _heapRefs;
        var oldAux1s = _aux1s;
        var oldAux2s = _aux2s;
        var oldOccupied = _occupied;
        var oldDeleted = _deleted;

        _keys = new string[newCapacity];
        _payloads = new ulong[newCapacity];
        _heapRefs = new object[newCapacity];
        _aux1s = new int[newCapacity];
        _aux2s = new int[newCapacity];
        _occupied = new bool[newCapacity];
        _deleted = new bool[newCapacity];
        _count = 0;
        _deletedCount = 0;

        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldOccupied[i] && !oldDeleted[i] && oldKeys[i] != null)
            {
                // Re-derive kind from old heap ref during rehash.
                Insert(oldKeys[i], oldPayloads[i], oldHeapRefs[i], oldAux1s[i], oldAux2s[i]);
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
