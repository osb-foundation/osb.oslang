namespace Osb.Engine.Core.Eval;

using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using Osb.Engine.Core.Heap;
using Osb.Engine.Core.Host;

/// <summary>
/// Interpreter control flow results.
/// </summary>
public enum ControlFlow
{
    Normal,
    Return,
    Break,
    Continue,
    Exception,
}

/// <summary>
/// Interpreter execution state.
/// </summary>
public sealed class ExecutionContext
{
    public readonly EngineHeap Heap;
    public readonly IHostApi Host;
    public readonly List<Frame> Frames = new();
    public readonly List<Value> Globals = new(); // GLOBAL variables
    public Value CurrentException;
    public string CurrentExceptionMessage;
    public SourceLocation ExceptionLocation;
    public ControlFlow CurrentFlow = ControlFlow.Normal;
    public Value ReturnValue;
    public int LoopDepth = 0;
    public bool InSwitch = false;

    public ExecutionContext(EngineHeap heap, IHostApi host)
    {
        Heap = heap;
        Host = host;
    }

    public void PushFrame(Frame frame) => Frames.Add(frame);
    public Frame PopFrame() { var f = Frames[^1]; Frames.RemoveAt(Frames.Count - 1); return f; }
    public Frame CurrentFrame => Frames.Count > 0 ? Frames[^1] : null!;

    public void Throw(string message, SourceLocation location)
    {
        CurrentException = Value.String(Heap.AllocString(message));
        CurrentExceptionMessage = message;
        ExceptionLocation = location;
        CurrentFlow = ControlFlow.Exception;
    }
}

/// <summary>
/// Stack frame for function/method execution.
/// </summary>
public sealed class Frame
{
    public readonly Lang.FunctionDecl Function;
    public readonly Value This; // ME for methods, null for functions
    public readonly Value Base; // BASE for constructors
    public readonly Dictionary<string, Value> Locals = new(StringComparer.OrdinalIgnoreCase);
    public int InstructionPointer = 0;
    public int LoopDepth = 0;
    public bool InTry = false;
    public int TryCatchFrameIndex = -1;

    public Frame(Lang.FunctionDecl function, Value thisRef = default, Value baseRef = default)
    {
        Function = function;
        This = thisRef;
        Base = baseRef;
    }
}