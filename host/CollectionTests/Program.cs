using System;
using System.Collections.Generic;
using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Lang;

namespace CollectionTests;

internal static class Program
{
    private static int _passed = 0;
    private static int _failed = 0;

    private static void Main(string[] args)
    {
        Console.WriteLine("=== NodeList<T> Tests ===");
        TestNodeListEmpty();
        TestNodeListAppend();
        TestNodeListIndexerGetSet();
        TestNodeListCount();
        TestNodeListNegativeIndex();
        TestNodeListOutOfRangeIndex();
        TestNodeListCapacityGrowth();
        TestNodeListPreservationDuringGrowth();
        TestNodeListIteration();
        TestNodeListEnumeratorCurrentBeforeMoveNext();
        TestNodeListEnumeratorCurrentAfterEnd();
        TestNodeListClear();
        TestNodeListClearReferenceRetention();

        Console.WriteLine();
        Console.WriteLine("=== TokenTypeSet Tests ===");
        TestTokenTypeSetEmpty();
        TestTokenTypeSetMembership();
        TestTokenTypeSetNonMembership();
        TestTokenTypeSetDuplicateEntries();
        TestTokenTypeSetInvalidEnumValues();
        TestTokenTypeSetAllNineKeywords();
        TestTokenTypeSetValuesOutsideKeywords();

        Console.WriteLine();
        Console.WriteLine($"Results: {_passed} passed, {_failed} failed");
        if (_failed > 0) Environment.Exit(1);
    }

    private static void Check(string name, bool condition, string? detail = null)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine($"  PASS {name}");
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL {name}" + (detail != null ? $" - {detail}" : ""));
        }
    }

    private static void CheckThrows<T>(string name, Action action) where T : Exception
    {
        try
        {
            action();
            _failed++;
            Console.WriteLine($"  FAIL {name} - no exception thrown");
        }
        catch (T)
        {
            _passed++;
            Console.WriteLine($"  PASS {name}");
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL {name} - wrong exception type: {ex.GetType().Name}");
        }
    }

    // ── NodeList<T> tests ──────────────────────────────────────────

    private static void TestNodeListEmpty()
    {
        var list = new NodeList<int>();
        Check("NodeList.Empty.Count", list.Count == 0);
        Check("NodeList.Empty.ToArray", list.ToArray().Length == 0);
    }

    private static void TestNodeListAppend()
    {
        var list = new NodeList<int>();
        list.Add(10);
        list.Add(20);
        list.Add(30);
        Check("NodeList.Append.Count", list.Count == 3);
        Check("NodeList.Append.Item0", list[0] == 10);
        Check("NodeList.Append.Item1", list[1] == 20);
        Check("NodeList.Append.Item2", list[2] == 30);
    }

    private static void TestNodeListIndexerGetSet()
    {
        var list = new NodeList<string> { };
        list.Add("a");
        list.Add("b");
        list[0] = "z";
        Check("NodeList.IndexerSet", list[0] == "z");
        Check("NodeList.IndexerGet", list[1] == "b");
    }

    private static void TestNodeListCount()
    {
        var list = new NodeList<double>();
        Check("NodeList.Count.Zero", list.Count == 0);
        list.Add(1.0);
        Check("NodeList.Count.One", list.Count == 1);
        list.Add(2.0);
        Check("NodeList.Count.Two", list.Count == 2);
    }

    private static void TestNodeListNegativeIndex()
    {
        var list = new NodeList<int> { };
        list.Add(42);
        CheckThrows<IndexOutOfRangeException>("NodeList.NegativeIndex", () => { var _ = list[-1]; });
        CheckThrows<IndexOutOfRangeException>("NodeList.NegativeIndexSet", () => { list[-1] = 0; });
    }

    private static void TestNodeListOutOfRangeIndex()
    {
        var list = new NodeList<int> { };
        list.Add(42);
        CheckThrows<IndexOutOfRangeException>("NodeList.OutOfRangeGet", () => { var _ = list[1]; });
        CheckThrows<IndexOutOfRangeException>("NodeList.OutOfRangeSet", () => { list[1] = 0; });
        CheckThrows<IndexOutOfRangeException>("NodeList.EmptyGet", () => { var _ = new NodeList<int>()[0]; });
    }

    private static void TestNodeListCapacityGrowth()
    {
        var list = new NodeList<int>(2);
        list.Add(1);
        list.Add(2);
        list.Add(3); // triggers growth from 2 to 4
        Check("NodeList.Growth.Count", list.Count == 3);
        Check("NodeList.Growth.Item0", list[0] == 1);
        Check("NodeList.Growth.Item1", list[1] == 2);
        Check("NodeList.Growth.Item2", list[2] == 3);
    }

    private static void TestNodeListPreservationDuringGrowth()
    {
        var list = new NodeList<int>(2);
        list.Add(100);
        list.Add(200);
        list.Add(300);
        list.Add(400);
        list.Add(500);
        Check("NodeList.Preserve.Count", list.Count == 5);
        Check("NodeList.Preserve.Item0", list[0] == 100);
        Check("NodeList.Preserve.Item1", list[1] == 200);
        Check("NodeList.Preserve.Item2", list[2] == 300);
        Check("NodeList.Preserve.Item3", list[3] == 400);
        Check("NodeList.Preserve.Item4", list[4] == 500);
    }

    private static void TestNodeListIteration()
    {
        var list = new NodeList<int> { };
        list.Add(1);
        list.Add(2);
        list.Add(3);
        var sum = 0;
        var n = 0;
        foreach (var item in list)
        {
            sum += item;
            n++;
        }
        Check("NodeList.Iteration.Count", n == 3);
        Check("NodeList.Iteration.Sum", sum == 6);
    }

    private static void TestNodeListEnumeratorCurrentBeforeMoveNext()
    {
        var list = new NodeList<int> { };
        list.Add(1);
        var enumerator = list.GetEnumerator();
        CheckThrows<InvalidOperationException>(
            "NodeList.Enumerator.CurrentBeforeMoveNext",
            () => { var _ = enumerator.Current; });
    }

    private static void TestNodeListEnumeratorCurrentAfterEnd()
    {
        var list = new NodeList<int> { };
        list.Add(1);
        var enumerator = list.GetEnumerator();
        Check("NodeList.Enumerator.MoveNext.First", enumerator.MoveNext() == true);
        Check("NodeList.Enumerator.Current.First", enumerator.Current == 1);
        Check("NodeList.Enumerator.MoveNext.End", enumerator.MoveNext() == false);
        CheckThrows<InvalidOperationException>(
            "NodeList.Enumerator.CurrentAfterEnd",
            () => { var _ = enumerator.Current; });
    }

    private static void TestNodeListClear()
    {
        var list = new NodeList<int> { };
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Clear();
        Check("NodeList.Clear.Count", list.Count == 0);
        CheckThrows<IndexOutOfRangeException>(
            "NodeList.Clear.AfterIndex",
            () => { var _ = list[0]; });
    }

    private static void TestNodeListClearReferenceRetention()
    {
        var container = new RefContainer { Value = 42 };
        var list = new NodeList<RefContainer> { };
        list.Add(container);
        list.Clear();
        // After Clear, the list should not retain the reference.
        // We verify Count is 0 and the item at index 0 would throw.
        Check("NodeList.ClearRef.Count", list.Count == 0);
        CheckThrows<IndexOutOfRangeException>(
            "NodeList.ClearRef.AfterIndex",
            () => { var _ = list[0]; });
        // The original container is still alive (held by local variable)
        Check("NodeList.ClearRef.OriginalAlive", container.Value == 42);
    }

    // ── TokenTypeSet tests ─────────────────────────────────────────

    private static void TestTokenTypeSetEmpty()
    {
        var set = new TokenTypeSet();
        Check("TokenTypeSet.Empty.Count", set.Count == 0);
        Check("TokenTypeSet.Empty.Contains", !set.Contains(TokenType.Sqrt));
    }

    private static void TestTokenTypeSetMembership()
    {
        var set = new TokenTypeSet(TokenType.Sqrt, TokenType.Pow);
        Check("TokenTypeSet.Membership.Sqrt", set.Contains(TokenType.Sqrt));
        Check("TokenTypeSet.Membership.Pow", set.Contains(TokenType.Pow));
    }

    private static void TestTokenTypeSetNonMembership()
    {
        var set = new TokenTypeSet(TokenType.Sqrt);
        Check("TokenTypeSet.NonMembership", !set.Contains(TokenType.Pow));
        Check("TokenTypeSet.NonMembership2", !set.Contains(TokenType.Str));
    }

    private static void TestTokenTypeSetDuplicateEntries()
    {
        var set = new TokenTypeSet(TokenType.Sqrt, TokenType.Sqrt, TokenType.Pow);
        Check("TokenTypeSet.Duplicate.Count", set.Count == 3);
        Check("TokenTypeSet.Duplicate.Contains", set.Contains(TokenType.Sqrt));
        Check("TokenTypeSet.Duplicate.Contains2", set.Contains(TokenType.Pow));
    }

    private static void TestTokenTypeSetInvalidEnumValues()
    {
        // Any TokenType value should be comparable; invalid/unused enum values work fine
        var set = new TokenTypeSet((TokenType)999);
        Check("TokenTypeSet.InvalidEnum.Contains", set.Contains((TokenType)999));
        Check("TokenTypeSet.InvalidEnum.NonContains", !set.Contains(TokenType.Sqrt));
    }

    private static void TestTokenTypeSetAllNineKeywords()
    {
        var set = new TokenTypeSet(
            TokenType.Sqrt, TokenType.Pow, TokenType.Floor, TokenType.Ceil,
            TokenType.Str, TokenType.Number, TokenType.Bool, TokenType.Count, TokenType.TypeOf
        );
        Check("TokenTypeSet.Nine.Count", set.Count == 9);
        Check("TokenTypeSet.Nine.Sqrt", set.Contains(TokenType.Sqrt));
        Check("TokenTypeSet.Nine.Pow", set.Contains(TokenType.Pow));
        Check("TokenTypeSet.Nine.Floor", set.Contains(TokenType.Floor));
        Check("TokenTypeSet.Nine.Ceil", set.Contains(TokenType.Ceil));
        Check("TokenTypeSet.Nine.Str", set.Contains(TokenType.Str));
        Check("TokenTypeSet.Nine.Number", set.Contains(TokenType.Number));
        Check("TokenTypeSet.Nine.Bool", set.Contains(TokenType.Bool));
        Check("TokenTypeSet.Nine.CountTok", set.Contains(TokenType.Count));
        Check("TokenTypeSet.Nine.TypeOf", set.Contains(TokenType.TypeOf));
    }

    private static void TestTokenTypeSetValuesOutsideKeywords()
    {
        var set = new TokenTypeSet(
            TokenType.Sqrt, TokenType.Pow, TokenType.Floor, TokenType.Ceil,
            TokenType.Str, TokenType.Number, TokenType.Bool, TokenType.Count, TokenType.TypeOf
        );
        Check("TokenTypeSet.Outside.LParen", !set.Contains(TokenType.LParen));
        Check("TokenTypeSet.Outside.RParen", !set.Contains(TokenType.RParen));
        Check("TokenTypeSet.Outside.Plus", !set.Contains(TokenType.Plus));
        Check("TokenTypeSet.Outside.Identifier", !set.Contains(TokenType.Identifier));
    }

    private sealed class RefContainer
    {
        public int Value { get; set; }
    }
}
