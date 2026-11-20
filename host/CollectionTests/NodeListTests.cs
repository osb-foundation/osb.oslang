using System;
using Osb.Engine.Core.Collections;

namespace CollectionTests;

internal static class NodeListTests
{
    internal static void RunAll()
    {
        TestEmpty();
        TestAppend();
        TestIndexerGetSet();
        TestCount();
        TestNegativeIndex();
        TestOutOfRangeIndex();
        TestCapacityGrowth();
        TestPreservationDuringGrowth();
        TestIteration();
        TestEnumeratorCurrentBeforeMoveNext();
        TestEnumeratorCurrentAfterEnd();
        TestClear();
        TestClearReferenceRetention();
    }

    private static void TestEmpty()
    {
        var list = new NodeList<int>();
        Program.Check("NodeList.Empty.Count", list.Count == 0);
        Program.Check("NodeList.Empty.ToArray", list.ToArray().Length == 0);
    }

    private static void TestAppend()
    {
        var list = new NodeList<int>();
        list.Add(10);
        list.Add(20);
        list.Add(30);
        Program.Check("NodeList.Append.Count", list.Count == 3);
        Program.Check("NodeList.Append.Item0", list[0] == 10);
        Program.Check("NodeList.Append.Item1", list[1] == 20);
        Program.Check("NodeList.Append.Item2", list[2] == 30);
    }

    private static void TestIndexerGetSet()
    {
        var list = new NodeList<string> { };
        list.Add("a");
        list.Add("b");
        list[0] = "z";
        Program.Check("NodeList.IndexerSet", list[0] == "z");
        Program.Check("NodeList.IndexerGet", list[1] == "b");
    }

    private static void TestCount()
    {
        var list = new NodeList<double>();
        Program.Check("NodeList.Count.Zero", list.Count == 0);
        list.Add(1.0);
        Program.Check("NodeList.Count.One", list.Count == 1);
        list.Add(2.0);
        Program.Check("NodeList.Count.Two", list.Count == 2);
    }

    private static void TestNegativeIndex()
    {
        var list = new NodeList<int> { };
        list.Add(42);
        Program.CheckThrows<IndexOutOfRangeException>("NodeList.NegativeIndex", () => { var _ = list[-1]; });
        Program.CheckThrows<IndexOutOfRangeException>("NodeList.NegativeIndexSet", () => { list[-1] = 0; });
    }

    private static void TestOutOfRangeIndex()
    {
        var list = new NodeList<int> { };
        list.Add(42);
        Program.CheckThrows<IndexOutOfRangeException>("NodeList.OutOfRangeGet", () => { var _ = list[1]; });
        Program.CheckThrows<IndexOutOfRangeException>("NodeList.OutOfRangeSet", () => { list[1] = 0; });
        Program.CheckThrows<IndexOutOfRangeException>("NodeList.EmptyGet", () => { var _ = new NodeList<int>()[0]; });
    }

    private static void TestCapacityGrowth()
    {
        var list = new NodeList<int>(2);
        list.Add(1);
        list.Add(2);
        list.Add(3); // triggers growth from 2 to 4
        Program.Check("NodeList.Growth.Count", list.Count == 3);
        Program.Check("NodeList.Growth.Item0", list[0] == 1);
        Program.Check("NodeList.Growth.Item1", list[1] == 2);
        Program.Check("NodeList.Growth.Item2", list[2] == 3);
    }

    private static void TestPreservationDuringGrowth()
    {
        var list = new NodeList<int>(2);
        list.Add(100);
        list.Add(200);
        list.Add(300);
        list.Add(400);
        list.Add(500);
        Program.Check("NodeList.Preserve.Count", list.Count == 5);
        Program.Check("NodeList.Preserve.Item0", list[0] == 100);
        Program.Check("NodeList.Preserve.Item1", list[1] == 200);
        Program.Check("NodeList.Preserve.Item2", list[2] == 300);
        Program.Check("NodeList.Preserve.Item3", list[3] == 400);
        Program.Check("NodeList.Preserve.Item4", list[4] == 500);
    }

    private static void TestIteration()
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
        Program.Check("NodeList.Iteration.Count", n == 3);
        Program.Check("NodeList.Iteration.Sum", sum == 6);
    }

    private static void TestEnumeratorCurrentBeforeMoveNext()
    {
        var list = new NodeList<int> { };
        list.Add(1);
        var enumerator = list.GetEnumerator();
        Program.CheckThrows<InvalidOperationException>(
            "NodeList.Enumerator.CurrentBeforeMoveNext",
            () => { var _ = enumerator.Current; });
    }

    private static void TestEnumeratorCurrentAfterEnd()
    {
        var list = new NodeList<int> { };
        list.Add(1);
        var enumerator = list.GetEnumerator();
        Program.Check("NodeList.Enumerator.MoveNext.First", enumerator.MoveNext() == true);
        Program.Check("NodeList.Enumerator.Current.First", enumerator.Current == 1);
        Program.Check("NodeList.Enumerator.MoveNext.End", enumerator.MoveNext() == false);
        Program.CheckThrows<InvalidOperationException>(
            "NodeList.Enumerator.CurrentAfterEnd",
            () => { var _ = enumerator.Current; });
    }

    private static void TestClear()
    {
        var list = new NodeList<int> { };
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Clear();
        Program.Check("NodeList.Clear.Count", list.Count == 0);
        Program.CheckThrows<IndexOutOfRangeException>(
            "NodeList.Clear.AfterIndex",
            () => { var _ = list[0]; });
    }

    private static void TestClearReferenceRetention()
    {
        var container = new RefContainer { Value = 42 };
        var list = new NodeList<RefContainer> { };
        list.Add(container);
        list.Clear();
        Program.Check("NodeList.ClearRef.Count", list.Count == 0);
        Program.CheckThrows<IndexOutOfRangeException>(
            "NodeList.ClearRef.AfterIndex",
            () => { var _ = list[0]; });
        Program.Check("NodeList.ClearRef.OriginalAlive", container.Value == 42);
    }

    private sealed class RefContainer
    {
        public int Value { get; set; }
    }
}
