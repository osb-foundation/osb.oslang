namespace Osb.Engine.Core.Providers;

/// <summary>
/// Memory management provider. Implemented by hosted .NET GC or freestanding platform.
/// </summary>
public interface IMemoryProvider
{
    /// <summary>
    /// Allocates a string on the heap.
    /// </summary>
    object AllocString(string text);

    /// <summary>
    /// Allocates an array on the heap.
    /// </summary>
    object AllocArray(int length);

    /// <summary>
    /// Allocates an object instance on the heap.
    /// </summary>
    object AllocObjectInstance(object cls);

    /// <summary>
    /// Allocates a closure on the heap.
    /// </summary>
    object AllocClosure(object fn, object[] captured);

    /// <summary>
    /// Triggers garbage collection.
    /// </summary>
    void Collect();
}