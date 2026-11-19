using System.IO;

namespace Osb.Engine.Host;

/// <summary>
/// BCL-based host implementation for testing and conformance running.
/// </summary>
public sealed class HostTest : IHostApi
{
    private readonly TextWriter _output;
    private readonly TextReader _input;
    private readonly string _basePath;

    public HostTest(TextWriter? output = null, TextReader? input = null, string? basePath = null)
    {
        _output = output ?? Console.Out;
        _input = input ?? Console.In;
        _basePath = basePath ?? Directory.GetCurrentDirectory();
    }

    public void Write(string text) => _output.Write(text);

    public string? ReadLine() => _input.ReadLine();

    public void Clear()
    {
        if (OperatingSystem.IsWindows())
        {
            Console.Clear();
        }
        else
        {
            _output.Write("\x1b[2J\x1b[H");
        }
    }

    public int GetKeyScancode()
    {
        if (Console.KeyAvailable)
        {
            var key = Console.ReadKey(true);
            // Simplified mapping
            return key.Key switch
            {
                ConsoleKey.Enter => 0x1C,
                ConsoleKey.Escape => 0x01,
                ConsoleKey.UpArrow => 0x48,
                ConsoleKey.DownArrow => 0x50,
                ConsoleKey.LeftArrow => 0x4B,
                ConsoleKey.RightArrow => 0x4D,
                _ => key.KeyChar != 0 ? key.KeyChar : 0,
            };
        }
        return -1;
    }

    public bool KeyAvailable() => Console.KeyAvailable;

    public int OpenRead(string path)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, path));
            var fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            return FileHandleTable.Register(fs);
        }
        catch { return -1; }
    }

    public int OpenWrite(string path)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, path));
            var fs = new FileStream(full, FileMode.Open, FileAccess.Write, FileShare.Read);
            return FileHandleTable.Register(fs);
        }
        catch { return -1; }
    }

    public int OpenAppend(string path)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, path));
            var fs = new FileStream(full, FileMode.Append, FileAccess.Write, FileShare.Read);
            return FileHandleTable.Register(fs);
        }
        catch { return -1; }
    }

    public int CreateFile(string path)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, path));
            var fs = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None);
            return FileHandleTable.Register(fs);
        }
        catch { return -1; }
    }

    public int Read(int handle, byte[] buffer, int count)
    {
        if (!FileHandleTable.TryGet(handle, out var fs)) return -1;
        try { return fs.Read(buffer, 0, count); }
        catch { return -1; }
    }

    public int Write(int handle, byte[] buffer, int count)
    {
        if (!FileHandleTable.TryGet(handle, out var fs)) return -1;
        try { fs.Write(buffer, 0, count); return count; }
        catch { return -1; }
    }

    public void Close(int handle) => FileHandleTable.Unregister(handle);

    public bool Stat(string path, out FileStat stat)
    {
        stat = default;
        try
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, path));
            var info = new FileInfo(full);
            if (!info.Exists)
            {
                var dirInfo = new DirectoryInfo(full);
                if (!dirInfo.Exists) return false;
                stat = new FileStat
                {
                    Exists = true,
                    IsDirectory = true,
                    Size = 0,
                    ModifiedTime = dirInfo.LastWriteTimeUtc,
                };
                return true;
            }
            stat = new FileStat
            {
                Exists = true,
                IsDirectory = false,
                Size = (ulong)info.Length,
                ModifiedTime = info.LastWriteTimeUtc,
            };
            return true;
        }
        catch { return false; }
    }

    public int ListDirectory(string path, out string[] entries)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, path));
            entries = Directory.GetFileSystemEntries(full);
            return entries.Length;
        }
        catch
        {
            entries = [];
            return -1;
        }
    }

    public int MakeDirectory(string path)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, path));
            Directory.CreateDirectory(full);
            return 0;
        }
        catch { return -1; }
    }

    public int DeleteFile(string path)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, path));
            if (File.Exists(full)) File.Delete(full);
            else if (Directory.Exists(full)) Directory.Delete(full, true);
            else return -1;
            return 0;
        }
        catch { return -1; }
    }

    public int RenameFile(string oldPath, string newPath)
    {
        try
        {
            var fullOld = Path.GetFullPath(Path.Combine(_basePath, oldPath));
            var fullNew = Path.GetFullPath(Path.Combine(_basePath, newPath));
            File.Move(fullOld, fullNew);
            return 0;
        }
        catch { return -1; }
    }

    public string GetCurrentDirectory() => _basePath;

    public int SetCurrentDirectory(string path)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(_basePath, path));
            Directory.SetCurrentDirectory(full);
            return 0;
        }
        catch { return -1; }
    }

    public DateTime Now() => DateTime.Now;

    public ulong Ticks() => (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public void Exit(int code) => Environment.Exit(code);

    public string[] GetArgs() => Environment.GetCommandLineArgs()[1..];

    public void HandleAllocationFailure()
    {
        // In test host, we can't really handle OOM gracefully
        throw new OutOfMemoryException("Engine heap exhausted");
    }

    private static class FileHandleTable
    {
        private static readonly Dictionary<int, FileStream> _handles = new();
        private static int _nextHandle = 100; // avoid stdio handles

        public static int Register(FileStream fs)
        {
            int handle = _nextHandle++;
            _handles[handle] = fs;
            return handle;
        }

        public static bool TryGet(int handle, out FileStream fs) => _handles.TryGetValue(handle, out fs);

        public static void Unregister(int handle) => _handles.Remove(handle);
    }
}