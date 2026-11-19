using System.IO;
using Osb.Engine.Core.Host;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;

namespace Osb.Engine.Hosted;

/// <summary>
/// BCL-based host implementation for testing and conformance running.
/// </summary>
public sealed class HostTest : IHostApi
{
    private readonly TextWriter _output;
    private readonly TextReader _input;

    public HostTest(TextWriter? output = null, TextReader? input = null)
    {
        _output = output ?? Console.Out;
        _input = input ?? Console.In;
    }

    public void Write(string text) => _output.Write(text);

    public string? ReadLine() => _input.ReadLine();

    public void Clear()
    {
        if (OperatingSystem.IsWindows())
            Console.Clear();
        else
            _output.Write("\x1b[2J\x1b[H");
    }

    public void Exit(int code = 0) => Environment.Exit(code);
}