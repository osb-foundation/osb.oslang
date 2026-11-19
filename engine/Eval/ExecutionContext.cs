namespace Osb.Engine.Eval;

using Osb.Engine.Lang;

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
    public readonly Heap.EngineHeap Heap;
    public readonly Host.IHostApi Host;
    public readonly List<Frame> Frames = new();
    public readonly List<Values.Value> Globals = new(); // GLOBAL variables
    public Values.Value CurrentException;
    public string CurrentExceptionMessage;
    public SourceLocation ExceptionLocation;
    public ControlFlow CurrentFlow = ControlFlow.Normal;
    public Values.Value ReturnValue;
    public int LoopDepth = 0;
    public bool InSwitch = false;

    public ExecutionContext(Heap.EngineHeap heap, Host.IHostApi host)
    {
        Heap = heap;
        Host = host;
    }

    public void PushFrame(Frame frame) => Frames.Add(frame);
    public Frame PopFrame() { var f = Frames[^1]; Frames.RemoveAt(Frames.Count - 1); return f; }
    public Frame CurrentFrame => Frames.Count > 0 ? Frames[^1] : null!;

    public void Throw(string message, SourceLocation location)
    {
        CurrentException = Values.Value.String(Heap.AllocString(message));
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
    public readonly Values.Value This; // ME for methods, null for functions
    public readonly Values.Value Base; // BASE for constructors
    public readonly Dictionary<string, Values.Value> Locals = new(StringComparer.OrdinalIgnoreCase);
    public int InstructionPointer = 0;
    public int LoopDepth = 0;
    public bool InTry = false;
    public int TryCatchFrameIndex = -1;

    public Frame(Lang.FunctionDecl function, Values.Value thisRef = default, Values.Value baseRef = default)
    {
        Function = function;
        This = thisRef;
        Base = baseRef;
    }
}