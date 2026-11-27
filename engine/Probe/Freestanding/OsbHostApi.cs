using Osb.Engine.Core.Host;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using OsbKT;

namespace Osb.Engine.Probe.Freestanding;

/// <summary>
/// Freestanding OSB Ring3 host API implementation using osbkt syscalls.
/// No BCL dependencies - uses MiniCoreLib String and osbkt console I/O.
/// </summary>
internal sealed unsafe class OsbHostApi : IHostApi
{
    private const int InputBufferSize = 256;

    public void Write(string text)
    {
        if (text == null) return;
        // MiniCoreLib String is UTF-16; narrow to bytes for syscall
        int len = text.Length;
        var buffer = stackalloc byte[512];
        int n = len > 511 ? 511 : len;
        for (int i = 0; i < n; i++)
        {
            buffer[i] = (byte)text[i];
        }
        Syscall.Write(buffer, n);
    }

    public string? ReadLine()
    {
        var buffer = stackalloc char[InputBufferSize];
        // Use GetChar (cooked input, AH=6Eh) for line reading
        // Note: This is a simplified implementation; a full one would handle backspace, etc.
        int len = 0;
        while (len < InputBufferSize - 1)
        {
            char c = Syscall.GetChar();
            if (c == '\r' || c == '\n')
            {
                Syscall.PutChar('\r');
                Syscall.PutChar('\n');
                break;
            }
            else if (c == '\b' || c == 0x7F) // Backspace/DEL
            {
                if (len > 0)
                {
                    len--;
                    Syscall.PutChar('\b');
                    Syscall.PutChar(' ');
                    Syscall.PutChar('\b');
                }
            }
            else if (c >= ' ' && c <= '~') // Printable ASCII
            {
                buffer[len++] = c;
                Syscall.PutChar(c);
            }
        }
        if (len == 0) return null;
        return new string(buffer, 0, len);
    }

    public void Clear()
    {
        // ANSI escape sequence for clear screen
        var esc = stackalloc byte[8] { 0x1B, 0x5B, 0x32, 0x4A, 0x1B, 0x5B, 0x48 };
        Syscall.Write(esc, 7);
    }

    public void Exit(int code = 0)
    {
        Syscall.Exit(code);
    }

    public string? GetCwd()
    {
        var buf = stackalloc byte[160];
        int n = Syscall.GetCwd(buf, 160);
        if (n <= 0) return null;
        var chars = stackalloc char[n];
        for (int i = 0; i < n; i++) chars[i] = (char)buf[i];
        return new string(chars, n);
    }

    // Console (OSL.CONSOLE)
    public void ConsoleEnter() { }
    public void ConsoleAlternate(bool on) { }
    public void ConsoleHideCursor() => Syscall.ConsCursorHide();
    public void ConsoleShowCursor() => Syscall.ConsCursorShow();
    public void ConsoleExit() { }
    public void ConsoleGetKey(out int keyCode, out char character, out bool ctrl)
    {
        keyCode = 0; character = '\0'; ctrl = false;
        int ch = Syscall.GetChar();
        if (ch < 0) return;
        ctrl = Syscall.ConsMods();
        if (ch == 0x1B && Syscall.ConsKeyAvailable())
        {
            int ch2 = Syscall.GetChar();
            if (ch2 == '[' && Syscall.ConsKeyAvailable())
            {
                int ch3 = Syscall.GetChar();
                switch (ch3)
                {
                    case (byte)'A': keyCode = 101; return;
                    case (byte)'B': keyCode = 102; return;
                    case (byte)'C': keyCode = 103; return;
                    case (byte)'D': keyCode = 104; return;
                    case (byte)'H': keyCode = 105; return;
                    case (byte)'F': keyCode = 106; return;
                    case (byte)'3':
                        if (Syscall.ConsKeyAvailable())
                        {
                            int ch4 = Syscall.GetChar();
                            if (ch4 == '~') keyCode = 108;
                        }
                        return;
                }
            }
        }
        keyCode = ch;
        if (ch >= 32 && ch <= 126) character = (char)ch;
    }
    public void ConsoleSize(out int width, out int height)
    {
        width = 0; height = 0;
        if (!Syscall.ConsInfo(out ushort cols, out ushort rows)) return;
        width = cols; height = rows;
    }
    public void ConsoleBeginFrame() => Syscall.ConsBeginFrame();
    public void ConsoleEndFrame() => Syscall.ConsEndFrame();
    public void ConsoleWrite(int row, int col, string text)
    {
        if (text == null || text.Length == 0) return;
        int len = text.Length;
        var buf = stackalloc byte[512];
        int n = len > 511 ? 511 : len;
        for (int i = 0; i < n; i++) buf[i] = (byte)text[i];
        Syscall.ConsWriteAt(row, col, buf, n);
    }
    public void ConsoleSetCursor(int row, int col) => Syscall.ConsCursorSet(row, col);
    public void ConsoleShow(string text) => Write(text);
    public string ConsoleInput() => ReadLine() ?? string.Empty;

    // File (OSL.FILE)
    public bool FileExists(string path)
    {
        if (path == null) return false;
        var pbuf = stackalloc byte[260];
        int plen = Narrow(path, pbuf, 260);
        if (plen < 0) return false;
        uint ino; ushort mode, uid; ulong size;
        return Syscall.ResolvePath(pbuf, plen, out ino, out mode, out uid, out size);
    }

    public string[] FileReadLines(string path)
    {
        if (path == null) return new string[0];
        var pbuf = stackalloc byte[260];
        int plen = Narrow(path, pbuf, 260);
        if (plen < 0) return new string[0];
        int handle = Syscall.Open(pbuf, plen);
        if (handle < 0) return new string[0];

        const int ChunkSize = 4096;
        var readBuf = stackalloc byte[ChunkSize];
        var contentChars = new char[65536];
        int contentLen = 0;

        while (true)
        {
            int bytesRead = Syscall.Read(handle, readBuf, ChunkSize);
            if (bytesRead <= 0) break;
            for (int i = 0; i < bytesRead && contentLen < contentChars.Length - 1; i++)
                contentChars[contentLen++] = (char)readBuf[i];
        }
        Syscall.Close(handle);

        if (contentLen == 0) return new string[0];

        var lines = new string[64];
        int lineCount = 0;
        int lineStart = 0;
        for (int i = 0; i < contentLen; i++)
        {
            if (contentChars[i] == '\n')
            {
                int lineLen = i - lineStart;
                var lineChars = stackalloc char[lineLen];
                for (int j = 0; j < lineLen; j++) lineChars[j] = contentChars[lineStart + j];
                lines[lineCount++] = new string(lineChars, lineLen);
                lineStart = i + 1;
            }
        }
        if (lineStart < contentLen)
        {
            int lineLen = contentLen - lineStart;
            var lineChars = stackalloc char[lineLen];
            for (int j = 0; j < lineLen; j++) lineChars[j] = contentChars[lineStart + j];
            lines[lineCount++] = new string(lineChars, lineLen);
        }

        var result = new string[lineCount];
        for (int i = 0; i < lineCount; i++) result[i] = lines[i];
        return result;
    }

    public void FileWriteLines(string path, string[] lines)
    {
        if (path == null || lines == null) return;
        var pbuf = stackalloc byte[260];
        int plen = Narrow(path, pbuf, 260);
        if (plen < 0) return;
        int handle = Syscall.CreateFile(pbuf, plen);
        if (handle < 0) return;

        var lineBuf = stackalloc byte[512];
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i] ?? string.Empty;
            int len = line.Length;
            int n = len > 511 ? 511 : len;
            for (int k = 0; k < n; k++) lineBuf[k] = (byte)line[k];
            Syscall.WriteFile(handle, lineBuf, n);
            byte nl = (byte)'\n';
            Syscall.WriteFile(handle, &nl, 1);
        }
        Syscall.Close(handle);
    }

    private static int Narrow(string s, byte* dst, int cap)
    {
        if (s == null) return 0;
        if (s.Length > cap) return -1;
        fixed (char* p = s)
        {
            for (int i = 0; i < s.Length; i++) dst[i] = (byte)p[i];
        }
        return s.Length;
    }
}