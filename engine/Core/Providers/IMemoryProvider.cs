using System;
namespace Osb.Engine.Core.Providers;

/// <summary>
/// Platform memory provider for freestanding OSLANG runtime.
/// The platform supplies a memory region or allocation primitives.
/// The Core never directly allocates managed arrays for heap storage.
/// </summary>
public interface IMemoryProvider : IDisposable
{
    /// <summary>
    /// Gets the base address of the memory region.
    /// For hosted: pinned byte[] address. For freestanding: physical/virtual address.
    /// </summary>
    IntPtr BaseAddress { get; }

    /// <summary>
    /// Total size of the memory region in bytes.
    /// </summary>
    int RegionSize { get; }

    /// <summary>
    /// Gets the managed byte[] backing the region (hosted only).
    /// Returns null for freestanding implementations.
    /// </summary>
    byte[]? ManagedRegion { get; }

    /// <summary>
    /// Allocates a block of memory with specified alignment.
    /// Returns IntPtr.Zero on failure (OOM).
    /// </summary>
    IntPtr Alloc(int sizeBytes, int alignment = 8);

    /// <summary>
    /// Frees a previously allocated block.
    /// For bump-allocator regions, this may be a no-op (region reset on shutdown).
    /// </summary>
    void Free(IntPtr ptr, int sizeBytes);

    /// <summary>
    /// Resets the memory region (reclaims all allocations).
    /// Used for test isolation or soft restart.
    /// </summary>
    void Reset();

    /// <summary>
    /// Triggers garbage collection.
    /// The provider may implement its own GC or delegate to EngineHeap.
    /// </summary>
    void Collect();
}