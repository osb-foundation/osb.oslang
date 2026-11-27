using System;
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

    public string? GetCwd() => Environment.CurrentDirectory;

    // Console (OSL.CONSOLE)
    public void ConsoleEnter() { }
    public void ConsoleAlternate(bool on) { }
    public void ConsoleHideCursor() { }
    public void ConsoleShowCursor() { }
    public void ConsoleExit() { }
    public void ConsoleGetKey(out int keyCode, out char character, out bool ctrl)
    {
        keyCode = 0;
        character = '\0';
        ctrl = false;
        var line = _input.ReadLine();
        if (string.IsNullOrEmpty(line)) return;
        keyCode = line[0];
        character = line[0];
    }
    public void ConsoleSize(out int width, out int height) { width = 80; height = 25; }
    public void ConsoleBeginFrame() { }
    public void ConsoleEndFrame() { }
    public void ConsoleWrite(int row, int col, string text)
    {
        _output.Write($"[{row},{col}]{text}");
    }
    public void ConsoleSetCursor(int row, int col) { }
    public void ConsoleShow(string text) => _output.Write(text);
    public string ConsoleInput() => _input.ReadLine() ?? string.Empty;

    // File (OSL.FILE)
    public bool FileExists(string path) => File.Exists(path);
    public string[] FileReadLines(string path)
    {
        try { return File.ReadAllLines(path); }
        catch { return Array.Empty<string>(); }
    }
    public void FileWriteLines(string path, string[] lines)
    {
        File.WriteAllLines(path, lines);
    }
}
