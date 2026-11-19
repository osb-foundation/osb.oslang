namespace Osb.Engine.Host;

/// <summary>
/// Host abstraction layer for OSLANG engine.
/// Implemented by Host.Test (BCL) and Host.Osb (osbkt/INT 21h).
/// </summary>
public interface IHostApi
{
    // Console
    void Write(string text);
    string? ReadLine();
    void Clear();
    int GetKeyScancode();
    bool KeyAvailable();

    // File system
    int OpenRead(string path);
    int OpenWrite(string path);
    int OpenAppend(string path);
    int CreateFile(string path);
    int Read(int handle, byte[] buffer, int count);
    int Write(int handle, byte[] buffer, int count);
    void Close(int handle);
    bool Stat(string path, out FileStat stat);
    int ListDirectory(string path, out string[] entries);
    int MakeDirectory(string path);
    int DeleteFile(string path);
    int RenameFile(string oldPath, string newPath);
    string GetCurrentDirectory();
    int SetCurrentDirectory(string path);

    // Time
    DateTime Now();
    ulong Ticks();

    // Process
    void Exit(int code);
    string[] GetArgs();

    // Memory
    void HandleAllocationFailure();
}

public struct FileStat
{
    public bool Exists;
    public bool IsDirectory;
    public ulong Size;
    public DateTime ModifiedTime;
    public ushort Mode;
    public ushort Uid;
}