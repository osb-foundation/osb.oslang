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
}