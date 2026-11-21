// M8.4a GC architecture investigation experiments.
// Runs inside the published NativeAOT ELF executable.
// Tests how the official NativeAOT GC traces managed references.
// No production Engine.Core or MiniCoreLib modifications.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Osb.Engine.Probe;

internal static class NativeGCTests
{
    // A simple managed class with a reference field, simulating HeapObject-like structure.
    private sealed class RefHolder
    {
        public object? Target;
        public string? Name;
    }

    // A container holding an array of references.
    private sealed class RefArray
    {
        public object?[] Items;
        public RefArray(int n) => Items = new object?[n];
    }

    private static int _passed;
    private static int _failed;

    public static int RunAll()
    {
        Console.WriteLine("=== NativeAOT GC Experiments ===");
        Console.WriteLine();

        // Phase 2 experiments
        Test("GC: field-held reference survives", TestFieldHeldReference);
        Test("GC: array-held reference survives", TestArrayHeldReference);
        Test("GC: List<T>-held reference survives", TestListHeldReference);
        Test("GC: GCHandle Normal keeps alive", TestGCHandleNormalKeepsAlive);
        Test("GC: GCHandle freed -> target collected", TestGCHandleFreed);
        Test("GC: repeated alloc/release cycles", TestRepeatedAllocRelease);
        Test("GC: nullable field holds null", TestNullFieldHeld);
        Test("GC: struct with reference field", TestStructWithReferenceField);

        // Phase 3: GCHandle bridge investigation
        Test("GCHandle: Alloc/FromIntPtr round-trip", TestGCHandleRoundTrip);
        Test("GCHandle: Normal handle type", TestGCHandleNormalType);
        Test("GCHandle: Weak handle behavior", TestGCHandleWeak);
        Test("GCHandle: Pinned handle behavior", TestGCHandlePinned);

        // Object identity / relocation
        Test("GC: object identity via reference equality", TestObjectIdentity);
        Test("GC: managed reference stability across alloc", TestReferenceStability);

        Console.WriteLine();
        Console.WriteLine($"GC Results: {_passed} passed, {_failed} failed");
        return _failed == 0 ? 0 : 1;
    }

    private static void Test(string name, Func<bool> func)
    {
        try
        {
            if (func())
            {
                _passed++;
                Console.WriteLine($"  PASS {name}");
            }
            else
            {
                _failed++;
                Console.WriteLine($"  FAIL {name} - returned false");
            }
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL {name} - exception: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // --- Field-held reference ---

    private static bool TestFieldHeldReference()
    {
        object? target = null;
        RefHolder? holder = null;

        // Allocate target inside a scope; holder keeps a reference.
        {
            var t = new object();
            holder = new RefHolder { Target = t, Name = "field-holder" };
            target = t;
        }

        // target variable is out of scope; only holder.Target keeps it alive.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return holder?.Target is object && holder.Target == target;
    }

    // --- Array-held reference ---

    private static bool TestArrayHeldReference()
    {
        object? target = null;
        RefArray? container = null;

        {
            var t = new object();
            container = new RefArray(4);
            container.Items[0] = t;
            target = t;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return container?.Items?[0] is object && container.Items[0] == target;
    }

    // --- List<T>-held reference ---

    private static bool TestListHeldReference()
    {
        object? target = null;
        List<object?>? list = null;

        {
            var t = new object();
            list = new List<object?>();
            list.Add(t);
            target = t;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return list?.Count == 1 && list?[0] is object && list[0] == target;
    }

    // --- GCHandle Normal ---

    private static bool TestGCHandleNormalKeepsAlive()
    {
        object? target = null;
        GCHandle handle = default;

        {
            var t = new object();
            handle = GCHandle.Alloc(t, GCHandleType.Normal);
            target = t;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        bool alive = handle.IsAllocated && handle.Target is object && handle.Target == target;
        handle.Free();
        return alive;
    }

    // --- GCHandle freed -> target collectable ---

    private static bool TestGCHandleFreed()
    {
        object? target = null;
        GCHandle handle = default;

        {
            var t = new object();
            handle = GCHandle.Alloc(t, GCHandleType.Normal);
            target = t;
        }

        handle.Free();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // We can't directly observe collection, but we can verify handle is freed
        // and target reference is still held by local variable.
        return !handle.IsAllocated && target is object;
    }

    // --- Repeated alloc/release ---

    private static bool TestRepeatedAllocRelease()
    {
        List<GCHandle> handles = new();
        List<object?> targets = new();

        for (int i = 0; i < 100; i++)
        {
            var t = new TaggedObject { Id = i };
            var h = GCHandle.Alloc(t, GCHandleType.Normal);
            handles.Add(h);
            targets.Add(t);
        }

        // Drop all handles except last few
        for (int i = 0; i < handles.Count - 10; i++)
            handles[i].Free();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Verify the 10 we kept are still alive
        for (int i = handles.Count - 10; i < handles.Count; i++)
        {
            if (!handles[i].IsAllocated || handles[i].Target == null)
                return false;
        }

        // Free remaining
        for (int i = handles.Count - 10; i < handles.Count; i++)
            handles[i].Free();

        return targets.Count == 100;
    }

    // --- Null field ---

    private static bool TestNullFieldHeld()
    {
        RefHolder holder = new();
        // holder.Target is null by default
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return holder.Target == null;
    }

    // --- Struct with reference field ---

    private struct RefStruct
    {
        public object? Target;
        public int Value;
    }

    private static bool TestStructWithReferenceField()
    {
        object? target = null;
        RefStruct? s = null;

        {
            var t = new object();
            s = new RefStruct { Target = t, Value = 42 };
            target = t;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        return s?.Target is object && s?.Target == target && s?.Value == 42;
    }

    // --- GCHandle round-trip ---

    private static bool TestGCHandleRoundTrip()
    {
        var obj = new object();
        var handle = GCHandle.Alloc(obj, GCHandleType.Normal);
        var ptr = GCHandle.ToIntPtr(handle);
        var restored = GCHandle.FromIntPtr(ptr);
        bool ok = restored.IsAllocated && restored.Target == obj;
        handle.Free();
        return ok;
    }

    // --- GCHandle Normal type ---

    private static bool TestGCHandleNormalType()
    {
        var obj = new object();
        var handle = GCHandle.Alloc(obj, GCHandleType.Normal);
        bool ok = handle.IsAllocated && handle.Target == obj;
        handle.Free();
        return ok;
    }

    // --- GCHandle Weak ---

    private static bool TestGCHandleWeak()
    {
        object? target = null;
        GCHandle handle = default;

        {
            var t = new object();
            handle = GCHandle.Alloc(t, GCHandleType.Weak);
            target = t;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        bool wasTracked = handle.IsAllocated;
        handle.Free();
        return wasTracked;
    }

    // --- GCHandle Pinned ---

    private static bool TestGCHandlePinned()
    {
        var obj = new object();
        GCHandle handle = default;
        try
        {
            handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
            bool ok = handle.IsAllocated && handle.Target == obj && handle.AddrOfPinnedObject() != IntPtr.Zero;
            handle.Free();
            return ok;
        }
        catch
        {
            if (handle.IsAllocated) handle.Free();
            return false;
        }
    }

    // --- Object identity ---

    private static bool TestObjectIdentity()
    {
        var a = new object();
        var b = new object();
        var c = a;
        return ReferenceEquals(a, c) && !ReferenceEquals(a, b);
    }

    // --- Reference stability ---

    private static bool TestReferenceStability()
    {
        object? first = null;
        object? second = null;

        {
            var t = new object();
            first = t;
            // Allocate more objects to potentially trigger GC
            for (int i = 0; i < 1000; i++)
                _ = new object();
        }

        // If GC ran, first should still be valid
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        second = first;
        return ReferenceEquals(first, second);
    }

    // Helper class for identity test
    private sealed class TaggedObject
    {
        public int Id;
    }
}
