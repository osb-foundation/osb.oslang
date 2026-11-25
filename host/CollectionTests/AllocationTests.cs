using System;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace CollectionTests;

internal static class AllocationTests
{
    private static readonly IntPtr TestBase = (IntPtr)0x2100000;

    internal static void RunAll()
    {
        Console.WriteLine("=== Allocation Failure Reliability Tests ===");
        SuccessfulAllocStringReturnsObject();
        NullFactoryThrowsOutOfMemory();
        NullFactoryDoesNotIncrementAllocCount();
        ProviderOversizedAllocReturnsZero();
        ProviderInvalidAlignmentThrows();
        ZeroSizeAllocReturnsZero();
        NoInvalidHeapReferenceEntersValue();
    }

    private static EngineHeap NewHeap(int regionSize = 1024)
    {
        var provider = new OsbMemoryProvider(TestBase, regionSize);
        return new EngineHeap(provider);
    }

    private static void SuccessfulAllocStringReturnsObject()
    {
        using var heap = NewHeap();
        var before = heap.AllocCount;

        var str = heap.AllocString("hello");

        Program.Check("Allocation.SuccessfulAllocStringReturnsObject",
            str is not null &&
            str.Utf8Bytes.Length == 5 &&
            heap.AllocCount == before + 1);
    }

    private static void NullFactoryThrowsOutOfMemory()
    {
        using var heap = NewHeap();

        Program.CheckThrows<OutOfMemoryException>(
            "Allocation.NullFactoryThrowsOutOfMemory",
            () => _ = heap.Allocate(() => null as HeapString));
    }

    private static void NullFactoryDoesNotIncrementAllocCount()
    {
        using var heap = NewHeap();
        var before = heap.AllocCount;

        try
        {
            _ = heap.Allocate(() => null as HeapString);
        }
        catch (OutOfMemoryException) { }

        Program.Check("Allocation.NullFactoryDoesNotIncrementAllocCount",
            heap.AllocCount == before,
            $"allocCount={heap.AllocCount} before={before}");
    }

    private static void ProviderOversizedAllocReturnsZero()
    {
        using var provider = new OsbMemoryProvider(TestBase, 64);

        Program.Check("Allocation.ProviderOversizedAllocReturnsZero",
            provider.Alloc(100, 8) == IntPtr.Zero);
    }

    private static void ProviderInvalidAlignmentThrows()
    {
        using var provider = new OsbMemoryProvider(TestBase, 64);

        Program.CheckThrows<ArgumentException>(
            "Allocation.ProviderInvalidAlignmentThrows",
            () => _ = provider.Alloc(8, 3));
    }

    private static void ZeroSizeAllocReturnsZero()
    {
        using var provider = new OsbMemoryProvider(TestBase, 64);

        Program.Check("Allocation.ZeroSizeAllocReturnsZero",
            provider.Alloc(0, 8) == IntPtr.Zero);
    }

    private static void NoInvalidHeapReferenceEntersValue()
    {
        using var heap = NewHeap();

        var str = heap.AllocString("abc");
        var value = Value.String(str);

        Program.Check("Allocation.NoInvalidHeapReferenceEntersValue",
            !value.IsNull &&
            value.Kind == ValueKind.String &&
            value.AsString is not null &&
            value.AsString.ByteLength == 3);
    }
}
