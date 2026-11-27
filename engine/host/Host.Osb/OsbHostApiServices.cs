using OsbKT;

/// <summary>
/// Additional freestanding host API methods for OSL.CONSOLE and OSL.FILE
/// services used by KISS (Card 2). Implemented via osbkt INT 21h syscalls.
/// </summary>
internal sealed unsafe partial class OsbHostApi
{
    public string? GetCwd()
        {
            var buf = stackalloc byte[160];
            int n = Syscall.GetCwd(buf, 160);
            if (n <= 0) return null;
            var chars = stackalloc char[n];
            for (int i = 0; i < n; i++) chars[i] = (char)buf[i];
            return new string(chars, n);
        }

        // ---- Console (OSL.CONSOLE) ----

        public void ConsoleEnter() { /* alternate-screen enter: no-op on VGA */ }
        public void ConsoleAlternate(bool on) { /* no-op on VGA */ }
        public void ConsoleHideCursor() => Syscall.ConsCursorHide();
        public void ConsoleShowCursor() => Syscall.ConsCursorShow();
        public void ConsoleExit() { /* alternate-screen exit: no-op on VGA */ }

        public void ConsoleGetKey(out int keyCode, out char character, out bool ctrl)
        {
            keyCode = 0;
            character = '\0';
            ctrl = false;

            // Blocking read of the next cooked byte
            int ch = Syscall.GetChar();
            if (ch < 0) return;

            ctrl = Syscall.ConsMods();

            // ESC (0x1B) may start an ANSI escape sequence from the kernel's
            // cooked input (arrow keys, home, end, delete, etc.).
            if (ch == 0x1B)
            {
                if (Syscall.ConsKeyAvailable())
                {
                    int ch2 = Syscall.GetChar();
                    if (ch2 == '[' && Syscall.ConsKeyAvailable())
                    {
                        int ch3 = Syscall.GetChar();
                        switch (ch3)
                        {
                            case (byte)'A': keyCode = 101; return; // KEYCODE.UP
                            case (byte)'B': keyCode = 102; return; // KEYCODE.DOWN
                            case (byte)'C': keyCode = 103; return; // KEYCODE.RIGHT
                            case (byte)'D': keyCode = 104; return; // KEYCODE.LEFT
                            case (byte)'H': keyCode = 105; return; // KEYCODE.HOME
                            case (byte)'F': keyCode = 106; return; // KEYCODE.END
                            case (byte)'3':
                                if (Syscall.ConsKeyAvailable())
                                {
                                    int ch4 = Syscall.GetChar();
                                    if (ch4 == '~') keyCode = 108; // KEYCODE.DELETE
                                }
                                return;
                        }
                    }
                }
                keyCode = 27; // KEYCODE.ESC
                return;
            }

            // Regular key
            keyCode = ch;
            if (ch >= 32 && ch <= 126) character = (char)ch;
        }

        public void ConsoleSize(out int width, out int height)
        {
            width = 0;
            height = 0;
            if (!Syscall.ConsInfo(out ushort cols, out ushort rows)) return;
            width = cols;
            height = rows;
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

        public string ConsoleInput()
        {
            var line = ReadLine();
            return line ?? string.Empty;
        }

        // ---- File (OSL.FILE) ----

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
                // Append newline (except we always include it for line-based files)
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
