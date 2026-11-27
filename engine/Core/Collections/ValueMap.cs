using System;
using Osb.Engine.Core.Values;

namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding string-keyed map specialized for Value storage.
/// Value components are stored in separate object[]/ulong[]/bool[] arrays.
/// The ValueKind tag is preserved via bool[] kind-bit arrays for
/// immediate types (heapRef == null) and derived from heap object type for
/// heap-allocated values. ulong[]/int[] reads return 0 for non-zero values
/// in freestanding NativeAOT, so kind bits use bool[] which round-trips
/// reliably. Enum type/member indices are packed into _payload (ulong).
/// </summary>
public sealed class ValueMap
{
    private string[] _keys;
    private ulong[] _payloads;
    private object[] _heapRefs;
    // Kind tag bits (bit0=isBoolean, bit1=isNumber) for immediate types where
    // heapRef is null. ulong[]/int[] reads return 0 in freestanding NativeAOT,
    // so kind bits are stored in bool[] arrays which round-trip reliably.
    private bool[] _kindBit0;
    private bool[] _kindBit1;
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
        // Kind bits: only meaningful for immediate types (heapRef == null).
        _kindBit0 = new bool[capacity];
        _kindBit1 = new bool[capacity];
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
            Insert(key, value._payload, value._heapRef, value._kind);
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

    /// <summary>
    /// Return-buffer variant of TryGetValue. The Value is returned through the
    /// caller's return buffer (like the indexer) rather than an out parameter.
    /// This avoids the freestanding NativeAOT ILC out-parameter struct-copy
    /// defect that zeroes the Value when the call immediately follows the
    /// indexer (get_Item). The map logic and storage are unchanged.
    /// </summary>
    public Value GetValue(string key, out bool found)
    {
        if (key == null) { found = false; return Value.NullValue; }
        int index = FindIndex(key);
        if (index >= 0) { found = true; return MakeValue(index); }
        found = false;
        return Value.NullValue;
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
            System.Array.Clear(_kindBit0, 0, _kindBit0.Length);
            System.Array.Clear(_kindBit1, 0, _kindBit1.Length);
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
        Insert(key, value._payload, value._heapRef, value._kind);
    }

    public void InsertDirect(string key, ulong payload, object heapRef, ValueKind kind)
    {
        Insert(key, payload, heapRef, kind);
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

    private void Insert(string key, ulong payload, object heapRef, ValueKind kind)
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
                // Store kind tag bits for immediate types (heapRef == null).
                // bit0 = isBoolean, bit1 = isNumber. Heap-derived kinds are
                // resolved from heapRef type on read, so bits stay false.
                _kindBit0[target] = heapRef == null && kind == ValueKind.Boolean;
                _kindBit1[target] = heapRef == null && kind == ValueKind.Number;
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
                // Update kind tag bits on overwrite.
                _kindBit0[probe] = heapRef == null && kind == ValueKind.Boolean;
                _kindBit1[probe] = heapRef == null && kind == ValueKind.Number;
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

        // Derive ValueKind: heap-based types from object type, immediate types
        // from stored kind tag bits (bool[] round-trips reliably in freestanding).
        ValueKind k;
        if (h != null)
        {
            if (h is HeapString) k = ValueKind.String;
            else if (h is HeapArray) k = ValueKind.Array;
            else if (h is HeapObjectInstance) k = ValueKind.Object;
            else if (h is HeapClass) k = ValueKind.Object;
            else if (h is HeapClosure) k = ValueKind.Closure;
            else if (h is HeapKeyEvent) k = ValueKind.KeyEvent;
            else if (h is HeapEnumSet) k = ValueKind.EnumSet;
            else if (h is HeapNativeFunction) k = ValueKind.NativeFunction;
            else k = ValueKind.String;
        }
        else if (_kindBit0[index])
        {
            k = ValueKind.Boolean;
        }
        else if (_kindBit1[index])
        {
            k = ValueKind.Number;
        }
        else
        {
            k = ValueKind.Null;
        }

        return Value.Make(k, p, h);
    }

    private void Resize()
    {
        int newCapacity = _keys.Length * 2;
        var oldKeys = _keys;
        var oldPayloads = _payloads;
        var oldHeapRefs = _heapRefs;
        var oldKindBit0 = _kindBit0;
        var oldKindBit1 = _kindBit1;
        var oldOccupied = _occupied;
        var oldDeleted = _deleted;

        _keys = new string[newCapacity];
        _payloads = new ulong[newCapacity];
        _heapRefs = new object[newCapacity];
        _kindBit0 = new bool[newCapacity];
        _kindBit1 = new bool[newCapacity];
        _occupied = new bool[newCapacity];
        _deleted = new bool[newCapacity];
        _count = 0;
        _deletedCount = 0;

        for (int i = 0; i < oldKeys.Length; i++)
        {
            if (oldOccupied[i] && !oldDeleted[i] && oldKeys[i] != null)
            {
                // Re-derive kind from old heap ref / kind bits during rehash.
                object h = oldHeapRefs[i];
                ValueKind k;
                if (h != null)
                {
                    if (h is HeapString) k = ValueKind.String;
                    else if (h is HeapArray) k = ValueKind.Array;
                    else if (h is HeapObjectInstance) k = ValueKind.Object;
                    else if (h is HeapClass) k = ValueKind.Object;
                    else if (h is HeapClosure) k = ValueKind.Closure;
                    else if (h is HeapKeyEvent) k = ValueKind.KeyEvent;
                    else if (h is HeapEnumSet) k = ValueKind.EnumSet;
                    else if (h is HeapNativeFunction) k = ValueKind.NativeFunction;
                    else k = ValueKind.String;
                }
                else if (oldKindBit0[i])
                {
                    k = ValueKind.Boolean;
                }
                else if (oldKindBit1[i])
                {
                    k = ValueKind.Number;
                }
                else
                {
                    k = ValueKind.Null;
                }
                Insert(oldKeys[i], oldPayloads[i], oldHeapRefs[i], k);
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
