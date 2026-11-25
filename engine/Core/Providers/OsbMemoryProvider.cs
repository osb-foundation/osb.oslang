using System;
using System.Runtime.InteropServices;

namespace Osb.Engine.Core.Providers;

/// <summary>
/// Freestanding memory provider for OSB Ring 3 using a bounded arena within the process's initial 2 MiB user block.
/// MEMGROW-allocated frames are tracked for accounting but not currently mapped
/// into the process address space (kernel support pending).
/// </summary>
public sealed class OsbMemoryProvider : IMemoryProvider
{
    [DllImport("osbnative")] private static extern ulong osb_tls_get_alloc();
    [DllImport("osbnative")] private static extern void osb_tls_set_alloc(ulong v);
    private readonly int _regionSize;
    private int _bumpPtr;
    private int _memGrowFrames;
    private bool _disposed;

    /// <summary>
    /// Creates a memory provider using a region within the existing user block.
    /// </summary>
    /// <param name="baseAddress">Base virtual address of the arena (must be within user block 0x2000000-0x2200000)</param>
    /// <param name="regionSize">Size of the arena in bytes</param>
    /// <param name="initialMemGrowFrames">Initial MEMGROW frames to track for accounting (multiple of 512, default 512 = 2 MiB)</param>
    public OsbMemoryProvider(IntPtr baseAddress, int regionSize, int initialMemGrowFrames = 512)
    {
        if (regionSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(regionSize), "Region size must be positive");
        if (baseAddress == IntPtr.Zero)
            throw new ArgumentException("Base address cannot be zero", nameof(baseAddress));

        // Verify the region is within the user block (0x2000000 - 0x2200000)
        ulong baseAddr = (ulong)baseAddress;
        ulong endAddr = baseAddr + (ulong)regionSize;
        const ulong UserBlockBase = 0x2000000;
        const ulong UserBlockLimit = 0x2200000;

        if (baseAddr < UserBlockBase || endAddr > UserBlockLimit)
            throw new ArgumentException($"Region must be within user block [{UserBlockBase:X}-{UserBlockLimit:X})", nameof(baseAddress));

        BaseAddress = baseAddress;
        _regionSize = regionSize;
        _bumpPtr = 0;
        _memGrowFrames = initialMemGrowFrames;
    }

    /// <summary>
    /// Base address of the memory region.
    /// </summary>
    public IntPtr BaseAddress { get; }

    /// <summary>
    /// Total size of the memory region in bytes.
    /// </summary>
    public int RegionSize => _regionSize;

    /// <summary>
    /// Returns null for freestanding implementation.
    /// </summary>
    public byte[]? ManagedRegion => null;

    /// <summary>
    /// Allocates a block of memory with specified alignment.
    /// Uses bump allocation within the provider's region.
    /// </summary>
    public IntPtr Alloc(int sizeBytes, int alignment = 8)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OsbMemoryProvider));
        if (sizeBytes <= 0) return IntPtr.Zero;
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            throw new ArgumentException("Alignment must be a power of two", nameof(alignment));

        // Sync bump pointer with the shared TLS allocation arena so that
        // managed allocations (RhNewString/RhpNewFast) and provider
        // allocations share one contiguous region without overlap.
        ulong tlsAlloc = osb_tls_get_alloc();
        ulong baseAddr = (ulong)BaseAddress;
        int current = (int)(tlsAlloc - baseAddr);
        if (current < 0 || current > _regionSize) current = _bumpPtr;

        // Align current pointer
        int alignedPtr = (current + alignment - 1) & ~(alignment - 1);
        int newPtr = alignedPtr + sizeBytes;

        if (newPtr > _regionSize)
        {
            return IntPtr.Zero; // Out of memory
        }

        IntPtr result = BaseAddress + alignedPtr;
        _bumpPtr = newPtr;
        osb_tls_set_alloc(baseAddr + (ulong)newPtr);
        return result;
    }

    /// <summary>
    /// Frees a previously allocated block.
    /// No-op for bump allocator (region reset reclaims all).
    /// </summary>
    public void Free(IntPtr ptr, int sizeBytes)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OsbMemoryProvider));
        // Bump allocator: individual frees not supported
    }

    /// <summary>
    /// Resets the memory region (reclaims all allocations).
    /// </summary>
    public void Reset()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(OsbMemoryProvider));
        _bumpPtr = 0;
    }

    /// <summary>
    /// Triggers garbage collection.
    /// No-op for this provider (EngineHeap handles its own GC).
    /// </summary>
    public void Collect()
    {
        // EngineHeap handles GC
    }

    /// <summary>
    /// Disposes the provider.
    /// MEMGROW frames are process-owned and released on process exit.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // MEMGROW frames cannot be individually returned in current ABI
    }

    /// <summary>
    /// Gets the number of MEMGROW frames currently tracked for this process.
    /// </summary>
    public int MemGrowFrames => _memGrowFrames;
}