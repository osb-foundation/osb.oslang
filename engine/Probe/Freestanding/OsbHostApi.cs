using Osb.Engine.Core.Host;
using Osb.Engine.Core.Lang;
using Osb.Engine.Core.Values;
using OsbKT;

namespace Osb.Engine.Probe.Freestanding;

/// <summary>
/// Freestanding OSB Ring3 host API implementation using osbkt syscalls.
/// No BCL dependencies - uses MiniCoreLib String and osbkt console I/O.
/// </summary>
internal sealed class OsbHostApi : IHostApi
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
}