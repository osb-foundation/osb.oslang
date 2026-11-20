using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Eval;

namespace Osb.Engine.Core.Collections;

/// <summary>
/// Freestanding call frame stack.
/// No BCL dependencies. Simple array-based stack for interpreter frames.
/// </summary>
public sealed class FrameStack
{
    private Frame[] _frames;
    private int _count;

    public FrameStack(int initialCapacity = 8)
    {
        _frames = new Frame[initialCapacity];
        _count = 0;
    }

    public int Count => _count;
    public Frame Current => _count > 0 ? _frames[_count - 1] : null!;

    public void Push(Frame frame)
    {
        if (_count == _frames.Length) Grow();
        _frames[_count++] = frame;
    }

    public Frame Pop()
    {
        if (_count == 0) throw new InvalidOperationException("Empty frame stack");
        var frame = _frames[--_count];
        _frames[_count] = null!;
        return frame;
    }

    public Frame Peek(int offset = 0)
    {
        int index = _count - 1 - offset;
        if (index < 0) return null!;
        return _frames[index];
    }

    public void Clear()
    {
        for (int i = 0; i < _count; i++)
            _frames[i] = null!;
        _count = 0;
    }

    private void Grow()
    {
        int newCapacity = _frames.Length * 2;
        var newFrames = new Frame[newCapacity];
        System.Array.Copy(_frames, newFrames, _count);
        _frames = newFrames;
    }
}