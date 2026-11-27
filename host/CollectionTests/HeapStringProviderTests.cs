using System;
using System.Text;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace CollectionTests;

/// <summary>
/// Regression coverage for HeapString construction and ToString() using the
/// concrete FreestandingUtf8Provider path.
///
/// In the freestanding NativeAOT runtime, interface dispatch on IUtf8Provider
/// is broken (dispatch cells are not initialized), so calling GetBytes/GetString
/// through an IUtf8Provider interface reference yields a null/garbage target.
/// HeapString must use the concrete FreestandingUtf8Provider for its default
/// (no-provider-supplied) path so that construction and ToString() work without
/// interface dispatch.
///
/// These tests construct HeapString via EngineHeap.AllocString (the default
/// path, no provider) and via an explicit FreestandingUtf8Provider, and assert
/// correct UTF-8 byte storage and round-trip ToString(). They protect against a
/// regression that reintroduces interface-dispatched provider calls in
/// HeapString.
/// </summary>
internal static class HeapStringProviderTests
{
    internal static void RunAll()
    {
        Console.WriteLine("=== HeapString / Provider Tests (freestanding concrete path) ===");
        AllocStringStoresCorrectUtf8Bytes();
        AllocStringRoundTripsToString();
        ExplicitFreestandingProviderStoresBytes();
        UnicodeRoundTrips();
    }

    private static EngineHeap NewHeap()
    {
        var provider = new BumpMemoryProvider();
        return new EngineHeap(provider);
    }

    // ── 1. AllocString (default path) stores correct UTF-8 bytes ─────────

    private static void AllocStringStoresCorrectUtf8Bytes()
    {
        var heap = NewHeap();
        var s = heap.AllocString("Hello, World!");
        byte[] expected = Encoding.UTF8.GetBytes("Hello, World!");
        bool ok = s.Utf8Bytes.Length == expected.Length;
        for (int i = 0; ok && i < expected.Length; i++)
            ok = s.Utf8Bytes[i] == expected[i];
        Program.Check("HeapString.AllocStringStoresCorrectUtf8Bytes", ok,
            $"len={s.Utf8Bytes.Length} expected={expected.Length}");
    }

    // ── 2. AllocString round-trips via ToString() ────────────────────────

    private static void AllocStringRoundTripsToString()
    {
        var heap = NewHeap();
        var s = heap.AllocString("Hello, World!");
        Program.Check("HeapString.AllocStringRoundTripsToString",
            s.ToString() == "Hello, World!",
            $"got='{s.ToString()}'");
    }

    // ── 3. Explicit FreestandingUtf8Provider stores bytes correctly ──────

    private static void ExplicitFreestandingProviderStoresBytes()
    {
        var provider = new FreestandingUtf8Provider();
        var s = new HeapString("ABC", provider);
        bool ok = s.Utf8Bytes.Length == 3 &&
                  s.Utf8Bytes[0] == (byte)'A' &&
                  s.Utf8Bytes[1] == (byte)'B' &&
                  s.Utf8Bytes[2] == (byte)'C';
        Program.Check("HeapString.ExplicitFreestandingProviderStoresBytes", ok,
            $"bytes={s.Utf8Bytes.Length}");
    }

    // ── 4. Unicode round-trips (multi-byte UTF-8) ────────────────────────

    private static void UnicodeRoundTrips()
    {
        var heap = NewHeap();
        const string text = "Héllo, 世界! 🌍";
        var s = heap.AllocString(text);
        byte[] expected = Encoding.UTF8.GetBytes(text);
        bool bytesOk = s.Utf8Bytes.Length == expected.Length;
        for (int i = 0; bytesOk && i < expected.Length; i++)
            bytesOk = s.Utf8Bytes[i] == expected[i];
        bool roundTrip = s.ToString() == text;
        Program.Check("HeapString.UnicodeRoundTrips", bytesOk && roundTrip,
            $"bytesOk={bytesOk} roundTrip={roundTrip} len={s.Utf8Bytes.Length}/{expected.Length}");
    }

    // ── stub ──────────────────────────────────────────────────────────

    private sealed class BumpMemoryProvider : IMemoryProvider
    {
        private readonly byte[] _region = new byte[1024 * 1024];
        private int _bumpPtr;
        private readonly System.Runtime.InteropServices.GCHandle _handle;

        public BumpMemoryProvider()
        {
            _handle = System.Runtime.InteropServices.GCHandle.Alloc(_region,
                System.Runtime.InteropServices.GCHandleType.Pinned);
        }

        public IntPtr BaseAddress => _handle.AddrOfPinnedObject();
        public int RegionSize => _region.Length;
        public byte[]? ManagedRegion => _region;

        public IntPtr Alloc(int sizeBytes, int alignment = 8)
        {
            if (sizeBytes <= 0) return IntPtr.Zero;
            int aligned = (_bumpPtr + alignment - 1) & ~(alignment - 1);
            int newPtr = aligned + sizeBytes;
            if (newPtr > _region.Length) return IntPtr.Zero;
            IntPtr result = BaseAddress + aligned;
            _bumpPtr = newPtr;
            return result;
        }

        public void Free(IntPtr ptr, int sizeBytes) { }
        public void Reset() { _bumpPtr = 0; }
        public void Collect() { }
        public void Dispose() { if (_handle.IsAllocated) _handle.Free(); }
    }
}