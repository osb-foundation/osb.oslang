using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;

namespace Osb.Engine.Core.Host;

/// <summary>
/// Host API interface for platform services (console, files, time, etc.).
/// Implemented by hosted BCL or freestanding platform.
/// </summary>
public interface IHostApi
{
    void Write(string text);
    string? ReadLine();
    void Clear();
    void Exit(int code = 0);
    string? GetCwd();

    // Console services (OSL.CONSOLE namespace)
    void ConsoleEnter();
    void ConsoleAlternate(bool on);
    void ConsoleHideCursor();
    void ConsoleShowCursor();
    void ConsoleExit();
    void ConsoleGetKey(out int keyCode, out char character, out bool ctrl);
    void ConsoleSize(out int width, out int height);
    void ConsoleBeginFrame();
    void ConsoleEndFrame();
    void ConsoleWrite(int row, int col, string text);
    void ConsoleSetCursor(int row, int col);
    void ConsoleShow(string text);
    string ConsoleInput();

    // File services (OSL.FILE namespace)
    bool FileExists(string path);
    string[] FileReadLines(string path);
    void FileWriteLines(string path, string[] lines);
}
