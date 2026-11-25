using Osb.Engine.Core.Collections;
using Osb.Engine.Core.Eval;
using Osb.Engine.Core.Lang;

namespace CollectionTests;

internal static class FrameStackTests
{
    internal static void RunAll()
    {
        Console.WriteLine("=== FrameStack Empty-State Contract Tests ===");
        CurrentOnEmptyStackThrows();
        CurrentAfterPush();
        CurrentAfterMultiplePush();
        CurrentAfterPop();
        RepeatedPushPop();
        PopOnEmptyStackThrows();
        CurrentAfterPopToEmptyThrows();
        CountTracksPushPop();
    }

    private static FrameStack NewStack() => new FrameStack(4);

    private static Frame TestFrame(string name = "f") =>
        new Frame(new FunctionDecl(name, Array.Empty<ParameterDecl>(), Array.Empty<Stmt>(), SourceLocation.Unknown));

    private static void CurrentOnEmptyStackThrows()
    {
        var stack = NewStack();
        Program.CheckThrows<InvalidOperationException>(
            "FrameStack.Current.EmptyThrows",
            () => { var _ = stack.Current; });
    }

    private static void CurrentAfterPush()
    {
        var stack = NewStack();
        var f1 = TestFrame("f1");
        stack.Push(f1);
        Program.Check("FrameStack.Current.PushOne", ReferenceEquals(stack.Current, f1));
        Program.Check("FrameStack.Current.Count", stack.Count == 1);
    }

    private static void CurrentAfterMultiplePush()
    {
        var stack = NewStack();
        var f1 = TestFrame("f1");
        var f2 = TestFrame("f2");
        stack.Push(f1);
        stack.Push(f2);
        Program.Check("FrameStack.Current.Top", ReferenceEquals(stack.Current, f2));
        Program.Check("FrameStack.Current.Count", stack.Count == 2);
    }

    private static void CurrentAfterPop()
    {
        var stack = NewStack();
        var f1 = TestFrame("f1");
        var f2 = TestFrame("f2");
        stack.Push(f1);
        stack.Push(f2);
        var popped = stack.Pop();
        Program.Check("FrameStack.Pop.ReturnsTop", ReferenceEquals(popped, f2));
        Program.Check("FrameStack.Current.AfterPop", ReferenceEquals(stack.Current, f1));
        Program.Check("FrameStack.Current.Count", stack.Count == 1);
    }

    private static void RepeatedPushPop()
    {
        var stack = NewStack();
        for (int i = 0; i < 10; i++)
        {
            stack.Push(TestFrame($"i{i}"));
            Program.Check($"FrameStack.Repeat.Push{i}.Count", stack.Count == i + 1);
        }
        for (int i = 9; i >= 0; i--)
        {
            stack.Pop();
            Program.Check($"FrameStack.Repeat.Pop{i}.Count", stack.Count == i);
        }
    }

    private static void PopOnEmptyStackThrows()
    {
        var stack = NewStack();
        Program.CheckThrows<InvalidOperationException>(
            "FrameStack.Pop.EmptyThrows",
            () => { stack.Pop(); });
    }

    private static void CurrentAfterPopToEmptyThrows()
    {
        var stack = NewStack();
        stack.Push(TestFrame("only"));
        stack.Pop();
        Program.CheckThrows<InvalidOperationException>(
            "FrameStack.Current.EmptyAfterPopThrows",
            () => { var _ = stack.Current; });
    }

    private static void CountTracksPushPop()
    {
        var stack = NewStack();
        Program.Check("FrameStack.Count.Zero", stack.Count == 0);
        stack.Push(TestFrame("a"));
        Program.Check("FrameStack.Count.One", stack.Count == 1);
        stack.Push(TestFrame("b"));
        stack.Push(TestFrame("c"));
        Program.Check("FrameStack.Count.Three", stack.Count == 3);
        stack.Pop();
        stack.Pop();
        Program.Check("FrameStack.Count.OneAgain", stack.Count == 1);
    }
}
