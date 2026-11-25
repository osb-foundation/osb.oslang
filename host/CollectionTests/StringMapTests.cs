using System;
using Osb.Engine.Core.Collections;

namespace CollectionTests;

internal static class StringMapTests
{
    internal static void RunAll()
    {
        Console.WriteLine("=== StringMap Deletion Integrity Tests ===");
        InsertAndFindLiveKeys();
        RemoveMiddleOfProbeChainKeepsChainIntact();
        RemoveAbsentKeyReturnsFalse();
        RepeatedRemoveIsIdempotent();
        ReinsertRemovedKey();
        ContainsKeyAfterRemoval();
        TryGetValueAfterRemoval();
        IndexerThrowsOnRemovedKey();
        ResizeAfterDeletionsPreservesAllKeys();
        EnumerationSkipsTombstones();
    }

    private static StringMap<int> NewMap() => new StringMap<int>(8);

    private static void InsertAndFindLiveKeys()
    {
        var map = NewMap();
        map.Add("a", 1);
        map.Add("b", 2);
        map.Add("c", 3);
        map.Add("d", 4);

        Program.Check("StringMap.Insert.Count", map.Count == 4, $"count={map.Count}");
        Program.Check("StringMap.Insert.TryGetA", map.TryGetValue("a", out var va) && va == 1);
        Program.Check("StringMap.Insert.TryGetD", map.TryGetValue("d", out var vd) && vd == 4);
        Program.Check("StringMap.Insert.ContainsAll",
            map.ContainsKey("a") && map.ContainsKey("b") && map.ContainsKey("c") && map.ContainsKey("d"));
        Program.Check("StringMap.Insert.IndexerD", map["d"] == 4);
    }

    private static void RemoveMiddleOfProbeChainKeepsChainIntact()
    {
        // Insert enough keys that collision chains form across buckets, then remove
        // keys and verify every remaining key stays reachable through any tombstones.
        var map = NewMap();
        const int Count = 200;
        for (int i = 0; i < Count; i++) map.Add($"k{i}", i);

        int removed = 0;
        for (int i = 0; i < Count; i += 3)
        {
            if (map.Remove($"k{i}")) removed++;
        }

        int expected = Count - removed;
        Program.Check("StringMap.RemoveChain.Count", map.Count == expected, $"count={map.Count} expected={expected}");

        for (int i = 0; i < Count; i++)
        {
            bool shouldExist = i % 3 != 0;
            Program.Check($"StringMap.RemoveChain.k{i}",
                shouldExist == map.ContainsKey($"k{i}"),
                $"i={i} exists={map.ContainsKey($"k{i}")}");
            if (shouldExist)
            {
                Program.Check($"StringMap.RemoveChain.k{i}.Value",
                    map.TryGetValue($"k{i}", out var v) && v == i,
                    $"i={i}");
            }
        }
    }

    private static void RemoveAbsentKeyReturnsFalse()
    {
        var map = NewMap();
        map.Add("a", 1);
        Program.Check("StringMap.RemoveAbsent", !map.Remove("zzz"));
        Program.Check("StringMap.RemoveAbsent.Count", map.Count == 1);
    }

    private static void RepeatedRemoveIsIdempotent()
    {
        var map = NewMap();
        map.Add("a", 1);
        Program.Check("StringMap.Idempotent.First", map.Remove("a"));
        Program.Check("StringMap.Idempotent.Second", !map.Remove("a"));
        Program.Check("StringMap.Idempotent.Count", map.Count == 0);
    }

    private static void ReinsertRemovedKey()
    {
        var map = NewMap();
        map.Add("a", 1);
        map.Add("b", 2);
        map.Remove("b");
        map.Add("b", 20);

        Program.Check("StringMap.Reinsert.Count", map.Count == 2, $"count={map.Count}");
        Program.Check("StringMap.Reinsert.Value", map.TryGetValue("b", out var vb) && vb == 20);
        Program.Check("StringMap.Reinsert.A", map.TryGetValue("a", out var va) && va == 1);
    }

    private static void ContainsKeyAfterRemoval()
    {
        var map = NewMap();
        map.Add("a", 1);
        map.Add("b", 2);
        map.Add("c", 3);
        map.Remove("b");

        Program.Check("StringMap.Contains.AfterRemoveB", !map.ContainsKey("b"));
        Program.Check("StringMap.Contains.AfterRemoveA", map.ContainsKey("a"));
        Program.Check("StringMap.Contains.AfterRemoveC", map.ContainsKey("c"));
    }

    private static void TryGetValueAfterRemoval()
    {
        var map = NewMap();
        map.Add("a", 1);
        map.Add("b", 2);
        map.Remove("b");

        Program.Check("StringMap.TryGet.BDefault",
            !map.TryGetValue("b", out var vb) && vb == 0);
        Program.Check("StringMap.TryGet.AFound",
            map.TryGetValue("a", out var va) && va == 1);
    }

    private static void IndexerThrowsOnRemovedKey()
    {
        var map = NewMap();
        map.Add("a", 1);
        map.Remove("a");

        Program.CheckThrows<KeyNotFoundException>(
            "StringMap.Indexer.RemovedKeyThrows",
            () => { var _ = map["a"]; });
    }

    private static void ResizeAfterDeletionsPreservesAllKeys()
    {
        var map = NewMap();
        // Insert enough keys to force multiple resizes (capacity starts at 8).
        for (int i = 0; i < 40; i++) map.Add($"k{i}", i);
        // Remove some from the middle of the probe chains.
        for (int i = 0; i < 40; i += 3) map.Remove($"k{i}");

        int expected = 40 - 14; // 40 inserted, indices 0,3,...,39 removed = 14 removed
        Program.Check("StringMap.ResizeAfterDelete.Count", map.Count == expected, $"count={map.Count} expected={expected}");

        for (int i = 0; i < 40; i++)
        {
            bool shouldExist = i % 3 != 0;
            Program.Check($"StringMap.ResizeAfterDelete.k{i}",
                shouldExist == map.ContainsKey($"k{i}"),
                $"i={i} exists={map.ContainsKey($"k{i}")}");
            if (shouldExist)
            {
                Program.Check($"StringMap.ResizeAfterDelete.k{i}.Value",
                    map.TryGetValue($"k{i}", out var v) && v == i);
            }
        }
    }

    private static void EnumerationSkipsTombstones()
    {
        var map = NewMap();
        map.Add("a", 1);
        map.Add("b", 2);
        map.Add("c", 3);
        map.Remove("b");

        int n = 0;
        foreach (var kv in map)
        {
            n++;
            Program.Check($"StringMap.Enum.Key{kv.Key}", kv.Key == "a" || kv.Key == "c");
        }
        Program.Check("StringMap.Enum.Count", n == 2, $"n={n}");
    }
}
