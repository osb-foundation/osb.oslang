using OsbKT;

/// <summary>
/// Freestanding OSB Ring3 host API (osbkt syscalls); no BCL, MiniCoreLib String.
/// </summary>
internal sealed unsafe class OsbHostApi : Osb.Engine.Core.Host.IHostApi
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
        int len = 0;
        while (len < InputBufferSize - 1)
        {
            char c = (char)Syscall.GetChar();
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
        return new string(buffer, len);
    }

    public void Clear()
    {
        // ANSI escape sequence for clear screen: ESC[2J ESC[H
        var esc = stackalloc byte[7] { 0x1B, 0x5B, 0x32, 0x4A, 0x1B, 0x5B, 0x48 };
        Syscall.Write(esc, 7);
    }

    public void Exit(int code = 0)
    {
        Syscall.Exit(code);
    }
}

/// <summary>
/// Freestanding math provider with no BCL dependencies.
/// Uses MiniCoreLib Math for all operations.
/// </summary>
internal sealed unsafe class FreestandingMathProvider : Osb.Engine.Core.Providers.IMathProvider
{
    public double PI => Math.PI;
    public double E => Math.E;

    public double Sqrt(double x) => Math.Sqrt(x);
    public double Abs(double x) => Math.Abs(x);
    public double Pow(double x, double y) => Math.Pow(x, y);
    public double Floor(double x) => Math.Floor(x);
    public double Ceil(double x) => Math.Ceiling(x);
    public double Round(double x) => Math.Round(x);
    public double Trunc(double x) => Math.Truncate(x);
    public double Sin(double x) => Math.Sin(x);
    public double Cos(double x) => Math.Cos(x);
    public double Tan(double x) => Math.Tan(x);
    public double Asin(double x) => Math.Asin(x);
    public double Acos(double x) => Math.Acos(x);
    public double Atan(double x) => Math.Atan(x);
    public double Log(double x) => Math.Log(x);
    public double Log10(double x) => Math.Log10(x);
    public double Exp(double x) => Math.Exp(x);
    public double Sign(double x) => Math.Sign(x);
    public double Min(double x, double y) => Math.Min(x, y);
    public double Max(double x, double y) => Math.Max(x, y);
    public double Clamp(double x, double min, double max) => Math.Clamp(x, min, max);
}

/// <summary>
/// Freestanding time provider with no BCL dependencies.
/// Uses osbkt DATETIME syscall (AH=72h) when available.
/// </summary>
internal sealed unsafe class FreestandingTimeProvider : Osb.Engine.Core.Providers.ITimeProvider
{
    public DateTime UtcNow
    {
        get
        {
            Syscall.DateTime(out int y, out int mo, out int d, out int h, out int mi, out int s);
            return new DateTime(y, mo, d);
        }
    }

    public ulong Ticks => 0; // Not implemented in freestanding

    public void Sleep(ulong ticks)
    {
        Syscall.Sleep(ticks);
    }

    public DateTime? CreateDate(int year, int month, int day)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > 31) return null;
        return new DateTime(year, month, day);
    }

    public DateTime? CreateTime(int hour, int minute, int second, int millisecond = 0)
    {
        // MiniCoreLib DateTime only supports date components
        if (hour < 0 || hour > 23 || minute < 0 || minute > 59 || second < 0 || second > 59 || millisecond < 0 || millisecond > 999) return null;
        // Return date-only (today's date)
        Syscall.DateTime(out int y, out int mo, out int d, out _, out _, out _);
        return new DateTime(y, mo, d);
    }

    public (int Year, int Month, int Day, int DayOfWeek)? DaysToDate(long daysSinceEpoch)
    {
        // Simplified: not implemented in freestanding
        return null;
    }

    public (int Hour, int Minute, int Second)? MillisToTime(long millisSinceMidnight)
    {
        if (millisSinceMidnight < 0 || millisSinceMidnight >= 24 * 60 * 60 * 1000) return null;
        int hour = (int)(millisSinceMidnight / (60 * 60 * 1000));
        int minute = (int)((millisSinceMidnight % (60 * 60 * 1000)) / (60 * 1000));
        int second = (int)((millisSinceMidnight % (60 * 1000)) / 1000);
        return (hour, minute, second);
    }
}

/// <summary>
/// Freestanding random provider with no BCL dependencies.
/// Uses a simple LCG (Linear Congruential Generator).
/// </summary>
internal sealed unsafe class FreestandingRandomProvider : Osb.Engine.Core.Providers.IRandomProvider
{
    private ulong _seed = 1;

    public FreestandingRandomProvider()
    {
        // Try to seed from Ticks syscall
        _seed = Syscall.Ticks() & 0xFFFFFFFF;
        if (_seed == 0) _seed = 1;
    }

    public int Next() => (int)(NextUlong() >> 32);

    public int Next(int maxValue)
    {
        if (maxValue <= 0) return 0;
        return (int)(NextUlong() % (ulong)maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue) return minValue;
        int range = maxValue - minValue;
        return minValue + Next(range);
    }

    public double NextDouble() => (double)(NextUlong() >> 11) * (1.0 / (1UL << 53));

    private ulong NextUlong()
    {
        // xorshift64*
        _seed ^= _seed >> 12;
        _seed ^= _seed << 25;
        _seed ^= _seed >> 27;
        return _seed * 0x2545F4914F6CDD1D;
    }
}

/// <summary>
/// Freestanding UTF-8 provider alias for the existing implementation.
/// </summary>
internal sealed unsafe class FreestandingUtf8ProviderAlias : Osb.Engine.Core.Providers.IUtf8Provider
{
    private readonly Osb.Engine.Core.Providers.FreestandingUtf8Provider _inner = new();

    public byte[] GetBytes(string s) => _inner.GetBytes(s);
    public string GetString(byte[] bytes) => _inner.GetString(bytes);
    public string GetString(ReadOnlySpan<byte> bytes) => _inner.GetString(bytes);
    public bool IsValidUtf8(ReadOnlySpan<byte> bytes) => _inner.IsValidUtf8(bytes);
}

/// <summary>
/// Freestanding OSLANG.EXE entry: read args, load source, execute via Engine.Core.
/// </summary>
internal static unsafe class OsbMain
{
    // Ring 3 heap = the kernel-designated process heap [UserTlsEnd, UserTlsHeapEnd)
    // (Scheduler.InitTaskTls); the former M2 region overlapped the TLS area (M3).
    private const ulong HeapBase = 0x2180000;
    private const ulong HeapLimit = 0x21FFFF0;
    private const int HeapSize = (int)(HeapLimit - HeapBase);

    public static void Main()
    {
        // Initialize TLS heap pointers for NativeAOT allocation helpers
        OsbNative.osb_tls_set_alloc(HeapBase);
        OsbNative.osb_tls_set_limit(HeapLimit);

        // Establish NativeAOT GC-static base pointers. ILC emits
        // __GCSTATICS_<Type> placeholders that the normal runtime patches
        // to point at zeroed GC-static storage; the freestanding path never
        // runs that runtime, so we establish them here before any reference
        // type static field (e.g. Keywords._map) is accessed.
        OsbNative.osb_gcstatics_init();

        // Get command-line arguments
        var argsBuffer = stackalloc byte[260];
        int argsLen = Syscall.GetArgs(argsBuffer, 260);
        if (argsLen <= 0)
        {
            PrintUsage();
            Syscall.Exit(1);
        }

        // Parse arguments: first token is the script source path; anything after
        // the first space is passed to the script as application arguments.
        string argsStr = BytesToString(argsBuffer, argsLen);
        string scriptPath = ParseScriptPath(argsStr);
        
        if (scriptPath == null || scriptPath.Length == 0)
        {
            PrintUsage();
            Syscall.Exit(1);
        }

        // Read source file
        string source = ReadSourceFile(scriptPath);
        if (source == null)
        {
            Syscall.PrintString("Error: Cannot read source file '");
            Syscall.PrintString(scriptPath);
            Syscall.PrintString("'\r\n");
            Syscall.Exit(1);
        }

        // Set up Engine.Core
        var provider = new Osb.Engine.Core.Providers.OsbMemoryProvider((IntPtr)HeapBase, HeapSize);
        var heap = new Osb.Engine.Core.Heap.EngineHeap(provider);
        var host = new OsbHostApi();
        var math = new FreestandingMathProvider();
        var time = new FreestandingTimeProvider();
        var random = new FreestandingRandomProvider();
        var utf8 = new FreestandingUtf8ProviderAlias();

        try
        {
            var lexer = new Osb.Engine.Core.Lang.Lexer(source, utf8);
            var tokens = new Osb.Engine.Core.Collections.NodeList<Osb.Engine.Core.Lang.Token>();
            bool atEnd = lexer.IsAtEnd;
            while (!atEnd)
            {
                tokens.Add(lexer.NextToken());
                atEnd = lexer.IsAtEnd;
            }
            tokens.Add(new Osb.Engine.Core.Lang.Token(Osb.Engine.Core.Lang.TokenType.Eof, "", lexer.CurrentLocation));

            var parser = new Osb.Engine.Core.Lang.Parser(tokens);
            var program = parser.Parse();

            var interpreter = new Osb.Engine.Core.Eval.Interpreter(heap, host, math, time, random, utf8);
            interpreter.Execute(program);
            if (interpreter.CurrentFlow == Osb.Engine.Core.Eval.ControlFlow.Exception)
            {
                Syscall.PrintString("Runtime error: ");
                Syscall.PrintString(interpreter.CurrentExceptionMessage ?? "unknown");
                Syscall.PrintString("\r\n");
                Syscall.Exit(1);
            }
        }
        catch (Exception ex)
        {
            Syscall.PrintString("Runtime error: ");
            Syscall.PrintString(ex.GetType().Name);
            Syscall.PrintString(": ");
            Syscall.PrintString(ex.Message ?? "unknown");
            Syscall.PrintString("\r\n");
            Syscall.Exit(1);
        }
        finally
        {
            heap.Dispose();
            provider.Dispose();
        }

        Syscall.Exit(0);
    }

    private static void PrintUsage()
    {
        Syscall.PrintString("OSLANG - OSB Scripting Language\r\n");
        Syscall.PrintString("Usage: OSLANG <script.osl>\r\n");
    }

    private static string ParseScriptPath(string args)
    {
        // The kernel passes "<sourcePath> <appArgs>" (see osb.shell External.Run):
        // the first token is the script source path, the remainder is the app's ARGS.
        int i = 0;
        while (i < args.Length && args[i] == ' ') i++;
        int start = i;
        while (i < args.Length && args[i] != ' ' && args[i] != '\r' && args[i] != '\n') i++;
        if (start >= i) return null;

        // Build the script-path string directly from the argument bytes. Using
        // args.Substring(start, i - start) routes through System.String.Substring,
        // which in this freestanding MiniCoreLib goes through FastAllocateString ->
        // RhNewString (the O8.14 RuntimeExport) and returns an empty string, while
        // BytesToString -> new string(char*, int) works correctly.
        var buf = stackalloc byte[260];
        int n = i - start;
        for (int k = 0; k < n; k++) buf[k] = (byte)args[start + k];
        return BytesToString(buf, n);
    }

    private static string ReadSourceFile(string path)
    {
        int handle = Syscall.Open(path);
        if (handle < 0)
        {
            return null;
        }

        // Read file in chunks
        const int ChunkSize = 4096;
        var buffer = stackalloc byte[ChunkSize];
        var contentChars = new char[65536]; // Max 64KB source
        int contentLen = 0;

        while (true)
        {
            int bytesRead = Syscall.Read(handle, buffer, ChunkSize);
            if (bytesRead <= 0) break;

            // Narrow bytes to chars (ASCII/UTF-8)
            for (int i = 0; i < bytesRead && contentLen < contentChars.Length - 1; i++)
            {
                contentChars[contentLen++] = (char)buffer[i];
            }
        }

        Syscall.Close(handle);

        if (contentLen == 0) return string.Empty;
        var chars = stackalloc char[contentLen];
        for (int i = 0; i < contentLen; i++) chars[i] = contentChars[i];
        var result = new string(chars, contentLen);
        return result;
    }

    private static string BytesToString(byte* buf, int len)
    {
        var chars = stackalloc char[len];
        for (int i = 0; i < len; i++)
        {
            chars[i] = (char)buf[i];
        }
        return new string(chars, len);
    }

    /// <summary>
    /// TLS and NativeAOT helper stubs for freestanding OSB Ring3.
    /// </summary>
    internal static class OsbNative
    {
        [System.Runtime.InteropServices.DllImport("osbnative")]
        public static extern ulong osb_tls_tp();

        [System.Runtime.InteropServices.DllImport("osbnative")]
        public static extern ulong osb_tls_get_alloc();

        [System.Runtime.InteropServices.DllImport("osbnative")]
        public static extern ulong osb_tls_get_limit();

        [System.Runtime.InteropServices.DllImport("osbnative")]
        public static extern void osb_tls_set_alloc(ulong v);

        [System.Runtime.InteropServices.DllImport("osbnative")]
        public static extern void osb_tls_set_limit(ulong v);

        [System.Runtime.InteropServices.DllImport("osbnative")]
        public static extern void osb_gcstatics_init();
    }
}