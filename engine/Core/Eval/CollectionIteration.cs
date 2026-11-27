using Osb.Engine.Core.Collections;

namespace Osb.Engine.Core.Eval;

/// <summary>
/// Freestanding-safe iteration helpers for AST collections.
///
/// The OSLANG parser backs every AST collection with NodeList&lt;T&gt;, exposed
/// through IReadOnlyList&lt;T&gt;. In the freestanding NativeAOT runtime, interface
/// dispatch on these collections is broken (IReadOnlyList&lt;T&gt;.Count returns 0
/// and GetEnumerator yields nothing), so iterating through the interface silently
/// skips every element. When the runtime type is NodeList&lt;T&gt;, these helpers use
/// its concrete Count/indexer (which read the backing fields directly).
///
/// For any other IReadOnlyList&lt;T&gt; implementation (e.g. a plain array used by
/// host-side tests), the helpers fall back to interface enumeration, which is
/// correct in a normal runtime. In freestanding the parser only ever produces
/// NodeList&lt;T&gt;, so the fallback is never exercised there.
/// </summary>
internal static class CollectionIteration
{
    public static int Count<T>(IReadOnlyList<T> list)
    {
        if (list is NodeList<T> nl) return nl.Count;
        return list.Count;
    }

    public static T Get<T>(IReadOnlyList<T> list, int index)
    {
        if (list is NodeList<T> nl) return nl[index];
        return list[index];
    }
}