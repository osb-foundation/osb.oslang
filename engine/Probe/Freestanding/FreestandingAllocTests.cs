using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Math;
using Osb.Engine.Core.Providers;
using Osb.Engine.Core.Values;

namespace Osb.Engine.Probe.Freestanding;

/// <summary>
/// Additional allocation tests for freestanding Engine.Core probe.
/// </summary>
internal static class FreestandingAllocTests
{
    private const ulong HeapBase = 0x2108000;
    private const ulong HeapLimit = 0x2200000;
    private const int HeapSize = (int)(HeapLimit - HeapBase);

    public static void RunAll()
    {
        // Tests are integrated into FreestandingMain.Main()
        // This class exists for future expansion
    }
}