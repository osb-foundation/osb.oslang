using System.Runtime.InteropServices;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Hosted.Providers;

/// <summary>
/// Hosted implementation of IMemoryProvider using a pinned managed byte[].
/// For freestanding targets, replace with platform-specific memory (e.g., VirtualAlloc, mmap).
/// </summary>
public sealed class HostedMemoryProvider : IMemoryProvider
{
    internal readonly byte[] _region;
    private readonly GCHandle _handle;
    private int _bumpPtr;
    private readonly int _regionSize;

    public HostedMemoryProvider(int sizeBytes = 2 * 1024 * 1024)
    {
        _regionSize = sizeBytes;
        _region = new byte[sizeBytes];
        _handle = GCHandle.Alloc(_region, GCHandleType.Pinned);
        _bumpPtr = 0;
    }

    public IntPtr BaseAddress => _handle.AddrOfPinnedObject();

    public int RegionSize => _regionSize;

    public byte[]? ManagedRegion => _region;

    public IntPtr Alloc(int sizeBytes, int alignment = 8)
    {
        if (sizeBytes <= 0) return IntPtr.Zero;

        int alignedPtr = (_bumpPtr + alignment - 1) & ~(alignment - 1);
        int newPtr = alignedPtr + sizeBytes;

        if (newPtr > _regionSize)
        {
            return IntPtr.Zero;
        }

        IntPtr result = BaseAddress + alignedPtr;
        _bumpPtr = newPtr;
        return result;
    }

    public void Free(IntPtr ptr, int sizeBytes)
    {
    }

    public void Reset()
    {
        _bumpPtr = 0;
        Array.Clear(_region, 0, _region.Length);
    }

    public void Collect()
    {
    }

    public void Dispose()
    {
        if (_handle.IsAllocated)
            _handle.Free();
    }
}