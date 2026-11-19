using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace Osb.Engine.Core.Heap;

/// <summary>
/// Engine heap with bump allocator and precise mark-sweep GC.
/// Uses platform memory via IMemoryProvider — no managed collections in GC paths.
/// Compatible with freestanding NativeAOT.
/// </summary>
public sealed class EngineHeap : IDisposable
{
    private readonly IMemoryProvider _memoryProvider;
    private readonly byte[]? _managedRegion;

    // Allocation tracking via intrusive linked list in platform memory
    // Each allocation has a HeapObjHeader in platform memory
    private IntPtr _firstAlloc;
    private IntPtr _lastAlloc;

    // GC roots: fixed-size table in platform memory
    private IntPtr _rootsTable;
    private int _rootsCapacity;
    private int _rootsCount;

    // Mark stack: fixed-size array in platform memory
    private IntPtr _markStack;
    private int _markStackCapacity;
    private int _markStackTop;

    // Bump allocator state
    private int _bumpPtr;
    private readonly int _regionSize;

    // Statistics
    private int _allocCount;
    private int _freedCount;
    private int _gcCycles;

    public EngineHeap(IMemoryProvider memoryProvider)
    {
        _memoryProvider = memoryProvider;
        _managedRegion = memoryProvider.ManagedRegion;
        _regionSize = memoryProvider.RegionSize;

        // Reserve space for roots table (256 entries default)
        _rootsCapacity = 256;
        int rootsTableSize = _rootsCapacity * RootEntry.EntrySize;
        _rootsTable = AllocRaw(rootsTableSize, 8);
        if (_rootsTable == IntPtr.Zero) ThrowOOM();

        // Reserve space for mark stack (1024 entries default)
        _markStackCapacity = 1024;
        int markStackSize = _markStackCapacity * IntPtr.Size;
        _markStack = AllocRaw(markStackSize, 8);
        if (_markStack == IntPtr.Zero) ThrowOOM();

        _bumpPtr = rootsTableSize + markStackSize;
        _firstAlloc = IntPtr.Zero;
        _lastAlloc = IntPtr.Zero;
    }

    public int UsedBytes => _bumpPtr;
    public int CapacityBytes => _regionSize;
    public int FreeBytes => _regionSize - _bumpPtr;
    public int AllocCount => _allocCount;
    public int FreedCount => _freedCount;
    public int GcCycles => _gcCycles;

    /// <summary>
    /// Low-level raw allocation from platform memory. No GC tracking.
    /// Used for internal structures (roots table, mark stack, headers).
    /// </summary>
    private IntPtr AllocRaw(int sizeBytes, int alignment)
    {
        IntPtr ptr = _memoryProvider.Alloc(sizeBytes, alignment);
        if (ptr == IntPtr.Zero) return IntPtr.Zero;
        _bumpPtr += sizeBytes;
        return ptr;
    }

    /// <summary>
    /// Allocates a managed heap object with GC tracking.
    /// The object is allocated on the managed heap but tracked via intrusive list in platform memory.
    /// </summary>
    public T Allocate<T>(Func<T> factory) where T : HeapObject
    {
        T obj = factory();
        if (obj == null) return null!;

        // Create header in platform memory
        int headerSize = HeapObjHeader.HeaderSize;
        IntPtr headerPtr = _memoryProvider.Alloc(headerSize, 8);
        if (headerPtr == IntPtr.Zero)
        {
            Collect();
            headerPtr = _memoryProvider.Alloc(headerSize, 8);
            if (headerPtr == IntPtr.Zero) ThrowOOM();
        }

        // Initialize header with GCHandle to managed object
        var handle = GCHandle.Alloc(obj, GCHandleType.Normal);
        var header = new HeapObjHeader
        {
            Kind = obj.Kind,
            ObjectSize = 0,
            NextAlloc = IntPtr.Zero,
            PrevAlloc = _lastAlloc,
            Marked = false,
            Handle = GCHandle.ToIntPtr(handle)
        };
        header.WriteTo(headerPtr, _managedRegion);

        // Link into allocation list
        if (_lastAlloc != IntPtr.Zero)
        {
            var prevHeader = HeapObjHeader.ReadFrom(_lastAlloc, _managedRegion);
            prevHeader.NextAlloc = headerPtr;
            prevHeader.WriteTo(_lastAlloc, _managedRegion);
        }
        else
        {
            _firstAlloc = headerPtr;
        }
        _lastAlloc = headerPtr;
        _allocCount++;

        return obj;
    }

    /// <summary>
    /// Allocates a string on the heap.
    /// </summary>
    public HeapString AllocString(string text)
    {
        return Allocate(() => new HeapString(text));
    }

    /// <summary>
    /// Allocates an array on the heap.
    /// </summary>
    public HeapArray AllocArray(int capacity)
    {
        return Allocate(() => new HeapArray(capacity));
    }

    /// <summary>
    /// Allocates a class definition on the heap.
    /// </summary>
    public HeapClass AllocClass(string name, HeapClass? baseClass, string[] fieldNames, Lang.MethodDecl[] methods)
    {
        return Allocate(() => new HeapClass(name, baseClass, fieldNames, methods));
    }

    /// <summary>
    /// Allocates a closure on the heap.
    /// </summary>
    public HeapClosure AllocClosure(Lang.FunctionDecl function, Value[] captured)
    {
        return Allocate(() => new HeapClosure(function, captured));
    }

    /// <summary>
    /// Allocates an object instance on the heap.
    /// </summary>
    public HeapObjectInstance AllocObject(HeapClass classDef)
    {
        return Allocate(() => new HeapObjectInstance(classDef));
    }

    /// <summary>
    /// Registers a root reference for GC.
    /// </summary>
    public void AddRoot(Value value)
    {
        if (value.IsNull || value.IsImmediate) return;

        if (_rootsCount >= _rootsCapacity) GrowRootsTable();

        IntPtr objPtr = GetObjectPointer(value);
        if (objPtr != IntPtr.Zero)
        {
            var entry = new RootEntry { ObjectPtr = objPtr };
            entry.WriteTo(_rootsTable + _rootsCount * RootEntry.EntrySize, _managedRegion);
            _rootsCount++;
        }
    }

    /// <summary>
    /// Removes a root reference.
    /// </summary>
    public void RemoveRoot(Value value)
    {
        if (value.IsNull || value.IsImmediate) return;

        IntPtr objPtr = GetObjectPointer(value);
        if (objPtr == IntPtr.Zero) return;

        for (int i = 0; i < _rootsCount; i++)
        {
            var entry = RootEntry.ReadFrom(_rootsTable + i * RootEntry.EntrySize, _managedRegion);
            if (entry.ObjectPtr == objPtr)
            {
                if (i < _rootsCount - 1)
                {
                    var lastEntry = RootEntry.ReadFrom(_rootsTable + (_rootsCount - 1) * RootEntry.EntrySize, _managedRegion);
                    lastEntry.WriteTo(_rootsTable + i * RootEntry.EntrySize, _managedRegion);
                }
                _rootsCount--;
                break;
            }
        }
    }

    /// <summary>
    /// Performs a full mark-sweep collection.
    /// </summary>
    public int Collect()
    {
        _gcCycles++;
        _freedCount = 0;

        // Mark phase
        _markStackTop = 0;

        // Mark from roots
        for (int i = 0; i < _rootsCount; i++)
        {
            var entry = RootEntry.ReadFrom(_rootsTable + i * RootEntry.EntrySize, _managedRegion);
            MarkObject(entry.ObjectPtr);
        }

        // Process mark stack
        while (_markStackTop > 0)
        {
            _markStackTop--;
            IntPtr objPtr = ReadMarkStack(_markStackTop);
            var header = HeapObjHeader.ReadFrom(objPtr, _managedRegion);
            if (header.Marked) continue;
            header.Marked = true;
            header.WriteTo(objPtr, _managedRegion);
            ScanObject(objPtr, header.Kind, header.GetManagedObject(_managedRegion));
        }

        // Sweep phase
        IntPtr current = _firstAlloc;
        IntPtr prev = IntPtr.Zero;
        _lastAlloc = IntPtr.Zero;

        while (current != IntPtr.Zero)
        {
            var header = HeapObjHeader.ReadFrom(current, _managedRegion);
            IntPtr next = header.NextAlloc;

            if (!header.Marked)
            {
                _freedCount++;
                if (prev != IntPtr.Zero)
                {
                    var prevHeader = HeapObjHeader.ReadFrom(prev, _managedRegion);
                    prevHeader.NextAlloc = next;
                    prevHeader.WriteTo(prev, _managedRegion);
                }
                else
                {
                    _firstAlloc = next;
                }
                // Free the GCHandle
                if (header.Handle != IntPtr.Zero)
                {
                    GCHandle.FromIntPtr(header.Handle).Free();
                }
            }
            else
            {
                header.Marked = false;
                header.WriteTo(current, _managedRegion);
                prev = current;
                _lastAlloc = current;
            }

            current = next;
        }

        return _freedCount;
    }

    private void MarkObject(IntPtr objPtr)
    {
        if (objPtr == IntPtr.Zero) return;

        var header = HeapObjHeader.ReadFrom(objPtr, _managedRegion);
        if (header.Marked) return;

        if (_markStackTop >= _markStackCapacity) GrowMarkStack();
        WriteMarkStack(_markStackTop, objPtr);
        _markStackTop++;
    }

    private void ScanObject(IntPtr objPtr, ValueKind kind, HeapObject? managedObj)
    {
        if (managedObj == null) return;

        switch (kind)
        {
            case ValueKind.String:
                break;
            case ValueKind.Array:
                if (managedObj is HeapArray arr)
                {
                    foreach (var item in arr.Items)
                        if (!item.IsNull && !item.IsImmediate)
                            MarkObject(GetObjectPointer(item));
                }
                break;
            case ValueKind.Object:
                if (managedObj is HeapObjectInstance inst)
                {
                    foreach (var field in inst.Fields)
                        if (!field.IsNull && !field.IsImmediate)
                            MarkObject(GetObjectPointer(field));
                }
                break;
            case ValueKind.EnumSet:
                if (managedObj is HeapEnumSet eset)
                {
                    foreach (var item in eset.Values)
                        if (!item.IsNull && !item.IsImmediate)
                            MarkObject(GetObjectPointer(item));
                }
                break;
            case ValueKind.Closure:
                if (managedObj is HeapClosure closure)
                {
                    foreach (var captured in closure.Captured)
                        if (!captured.IsNull && !captured.IsImmediate)
                            MarkObject(GetObjectPointer(captured));
                }
                break;
            case ValueKind.KeyEvent:
            case ValueKind.NativeFunction:
                break;
        }
    }

    private IntPtr GetObjectPointer(Value value)
    {
        // Scan allocation list to find matching managed object
        IntPtr current = _firstAlloc;
        while (current != IntPtr.Zero)
        {
            var header = HeapObjHeader.ReadFrom(current, _managedRegion);
            if (header.GetManagedObject(_managedRegion) is HeapObject managedObj && ObjectMatches(managedObj, value))
                return current;
            current = header.NextAlloc;
        }
        return IntPtr.Zero;
    }

    private bool ObjectMatches(HeapObject managedObj, Value value)
    {
        return value.Kind switch
        {
            ValueKind.String => managedObj is HeapString s && value.AsString == s,
            ValueKind.Array => managedObj is HeapArray a && value.AsArray == a,
            ValueKind.Object => managedObj is HeapObjectInstance o && value.AsObject == o,
            ValueKind.EnumSet => managedObj is HeapEnumSet e && value.AsEnumSet == e,
            ValueKind.Closure => managedObj is HeapClosure c && value.AsClosure == c,
            ValueKind.KeyEvent => managedObj is HeapKeyEvent k && value.AsKeyEvent == k,
            ValueKind.NativeFunction => managedObj is HeapNativeFunction n && value.AsNativeFunctionObj == n,
            _ => false
        };
    }

    private void GrowRootsTable()
    {
        int newCapacity = _rootsCapacity * 2;
        int newSize = newCapacity * RootEntry.EntrySize;
        IntPtr newTable = _memoryProvider.Alloc(newSize, 8);
        if (newTable == IntPtr.Zero) ThrowOOM();

        for (int i = 0; i < _rootsCount; i++)
        {
            var entry = RootEntry.ReadFrom(_rootsTable + i * RootEntry.EntrySize, _managedRegion);
            entry.WriteTo(newTable + i * RootEntry.EntrySize, _managedRegion);
        }

        _rootsTable = newTable;
        _rootsCapacity = newCapacity;
    }

    private void GrowMarkStack()
    {
        int newCapacity = _markStackCapacity * 2;
        int newSize = newCapacity * IntPtr.Size;
        IntPtr newStack = _memoryProvider.Alloc(newSize, 8);
        if (newStack == IntPtr.Zero) ThrowOOM();

        unsafe
        {
            byte* src = (byte*)_markStack.ToPointer();
            byte* dst = (byte*)newStack.ToPointer();
            Buffer.MemoryCopy(src, dst, newSize, _markStackTop * IntPtr.Size);
        }

        _markStack = newStack;
        _markStackCapacity = newCapacity;
    }

    private void WriteMarkStack(int index, IntPtr value)
    {
        unsafe { ((IntPtr*)_markStack.ToPointer())[index] = value; }
    }

    private IntPtr ReadMarkStack(int index)
    {
        unsafe { return ((IntPtr*)_markStack.ToPointer())[index]; }
    }

    private static void ThrowOOM() => throw new OutOfMemoryException("EngineHeap: Out of memory");

    public void Dispose()
    {
        // Free all GCHHandles
        IntPtr current = _firstAlloc;
        while (current != IntPtr.Zero)
        {
            var header = HeapObjHeader.ReadFrom(current, _managedRegion);
            if (header.Handle != IntPtr.Zero)
            {
                GCHandle.FromIntPtr(header.Handle).Free();
            }
            current = header.NextAlloc;
        }

        _memoryProvider.Reset();
        _firstAlloc = IntPtr.Zero;
        _lastAlloc = IntPtr.Zero;
        _rootsCount = 0;
        _markStackTop = 0;
        _bumpPtr = 0;
    }
}

/// <summary>
/// Header for each heap allocation. Stored in platform memory.
/// </summary>
internal struct HeapObjHeader
{
    public ValueKind Kind;
    public int ObjectSize;
    public IntPtr NextAlloc;
    public IntPtr PrevAlloc;
    public bool Marked;
    public IntPtr Handle; // GCHandle to managed object

    public static int HeaderSize => 1 + 3 + 4 + IntPtr.Size + IntPtr.Size + IntPtr.Size;

    public void WriteTo(IntPtr ptr, byte[]? managedRegion)
    {
        unsafe
        {
            byte* p = (byte*)ptr.ToPointer();
            p[0] = (byte)Kind;
            *(int*)(p + 4) = ObjectSize;
            *(IntPtr*)(p + 8) = NextAlloc;
            *(IntPtr*)(p + 8 + IntPtr.Size) = PrevAlloc;
            p[8 + 2 * IntPtr.Size] = Marked ? (byte)1 : (byte)0;
            *(IntPtr*)(p + 8 + 2 * IntPtr.Size + 1) = Handle;
        }
    }

    public static HeapObjHeader ReadFrom(IntPtr ptr, byte[]? managedRegion)
    {
        unsafe
        {
            byte* p = (byte*)ptr.ToPointer();
            return new HeapObjHeader
            {
                Kind = (ValueKind)p[0],
                ObjectSize = *(int*)(p + 4),
                NextAlloc = *(IntPtr*)(p + 8),
                PrevAlloc = *(IntPtr*)(p + 8 + IntPtr.Size),
                Marked = p[8 + 2 * IntPtr.Size] != 0,
                Handle = *(IntPtr*)(p + 8 + 2 * IntPtr.Size + 1)
            };
        }
    }

    public HeapObject? GetManagedObject(byte[]? managedRegion)
    {
        if (Handle == IntPtr.Zero) return null;
        try
        {
            var handle = GCHandle.FromIntPtr(Handle);
            return handle.Target as HeapObject;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Root table entry.
/// </summary>
internal struct RootEntry
{
    public IntPtr ObjectPtr;
    public static int EntrySize => IntPtr.Size;

    public void WriteTo(IntPtr ptr, byte[]? managedRegion)
    {
        unsafe { *(IntPtr*)ptr.ToPointer() = ObjectPtr; }
    }

    public static RootEntry ReadFrom(IntPtr ptr, byte[]? managedRegion)
    {
        unsafe { return new RootEntry { ObjectPtr = *(IntPtr*)ptr.ToPointer() }; }
    }
}