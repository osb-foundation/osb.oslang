# OSLANG Engine Core NativeAOT Compatibility Spike — Analysis Report

## Executive Summary

**Result: Major architectural blockers prevent NativeAOT compilation of the current Engine Core.**

The probe build fails with 119+ errors spanning 5 categories of missing MiniCoreLib types. While the freestanding collections (O3.5.2) and BigInteger (O3.5.3) compile against standard .NET, they cannot compile against the current MiniCoreLib without substantial additions.

---

## Phase 0 — Environment Inspection

| Item | Status |
|------|--------|
| OSLANG Engine Core | O3.5.3 (3 commits ahead of origin/master) |
| Target Framework | net10.0 (standard) / freestanding NativeAOT (probe) |
| MiniCoreLib | osb.core/src/MiniCoreLib.cs (407 lines) |
| Kernel NativeAOT | Working (osb.kernel, osb.shell, osbkt) |
| Conformance Baseline | 38 passed, 0 failed, 1 skipped |

---

## Phase 1 — Dependency Audit

### 1.1 Engine Core Dependency Graph (Explicit Imports)

| Namespace | Files | Purpose |
|-----------|-------|---------|
| `System.Collections.Generic` | 8 files | List<T>, Dictionary<K,V>, HashSet<T>, IReadOnlyList<T> |
| `System.Collections` | 1 file | IEnumerable, IEnumerator |
| `System.Runtime.CompilerServices` | 5 files | IsExternalInit, TupleElementNamesAttribute, Intrinsic, Unsafe |
| `System.Runtime.InteropServices` | 2 files | StructLayout, Marshal, IntPtr, GCHandle |
| `System.Text` | 3 files | StringBuilder |
| `System` | 15 files | Array, Math, Exception, IntPtr, IComparable, IEquatable, ValueTuple |
| `Osb.Engine.Core.Lang` | 20+ files | AST nodes, Parser, Lexer, tokens |

### 1.2 Dependency Classification

| Category | Examples | MiniCoreLib Status |
|----------|----------|-------------------|
| **Already supported** | `Object`, `Array`, `String`, primitives, `ValueType`, `Enum`, `Delegate`, `Attribute`, `Type`, `RuntimeTypeHandle`, `MethodImplAttribute`, `IntrinsicAttribute`, `Unsafe`, `SpanHelpers`, `Buffer`, `MethodTable`, `RuntimeExport/Import`, `Monitor`, `StartupCodeHelpers`, `ThrowHelpers` | ✅ Present |
| **Supported by NativeAOT but missing in MiniCoreLib** | `IReadOnlyList<T>`, `IEnumerable<T>`, `IEnumerator<T>`, `IEquatable<T>`, `IComparable<T>`, `StringComparer`, `StringBuilder`, `ValueTuple`, `TupleElementNamesAttribute`, `IsExternalInit`, `DefaultMemberAttribute`, `ParamArrayAttribute`, `Func<T>`, `IDisposable`, `ReadOnlySpan<T>`, `DateTime`, `HashSet<T>`, `Dictionary<K,V>` | ❌ Missing |
| **Compiler-generated runtime support** | Record types (`IsExternalInit`), tuple deconstruction (`ValueTuple`), indexers (`DefaultMemberAttribute`), params arrays (`ParamArrayAttribute`), async (`AsyncHelpers`) | ❌ Missing |
| **Additional MiniCoreLib implementation needed** | All above + `IntPtr` (partial), `Math` functions, `Exception` hierarchy | ❌ Missing |
| **Fundamental incompatibilities** | None identified — all are implementable in MiniCoreLib | — |

---

## Phase 2 — Compatibility Probe Results

### 2.1 Full Engine Core Probe (All Core/**/*.cs)

**Errors: 405** — Dominated by Lang/Parser/AstNodes dependencies on:
- `System.Collections.Generic.List<T>` (28 allocations in Parser.cs, 1 in Lexer.cs)
- `System.Collections.Generic.Dictionary<K,V>` (Keywords.cs)
- `System.Collections.Generic.HashSet<T>` (Parser.cs)
- `System.Text.StringBuilder` (12+ usages in Lexer.cs, AstNodes.cs)
- Record syntax requiring `IsExternalInit` (50+ record structs in AstNodes.cs)
- `IReadOnlyList<T>` interfaces on AST node collections

### 2.2 Minimal Runtime Probe (Collections, Values, Math, Heap, ExecutionContext)

**Errors: 119** — Missing MiniCoreLib types:

| Missing Type | Required By | Blocker Level |
|--------------|-------------|---------------|
| `IReadOnlyList<T>` | ValueArray, HeapObjects, ExecutionContext | Medium |
| `IEnumerable<T>` / `IEnumerator<T>` | ValueArray (explicit interface impl) | Medium |
| `IEquatable<T>` / `IComparable<T>` | BigInteger | Medium |
| `ValueTuple` / `TupleElementNamesAttribute` | BigInteger, Value, StringMap, BigIntegerDivision, BigIntegerArithmetic | Medium |
| `IsExternalInit` | Record types (if any used) | Medium |
| `StringComparer` | StringMap | Simple |
| `StringBuilder` | Not in minimal probe | — |
| `IntPtr` | EngineHeap, IMemoryProvider | Simple (partial exists) |
| `Func<T>` | EngineHeap (callback) | Medium |
| `IDisposable` | EngineHeap | Simple |
| `DefaultMemberAttribute` | StringMap, ValueArray, IntList, StringList, UsingList | Simple |
| `ParamArrayAttribute` | Parser (params arrays) | Simple |
| `ReadOnlySpan<T>` | IUtf8Provider, FreestandingUtf8Provider | Medium |
| `DateTime` | ITimeProvider | Medium |
| `HashSet<T>` | Parser | Medium |
| `Dictionary<K,V>` | Keywords, Parser | Medium |

### 2.3 Missing Types in MiniCoreLib (Complete Inventory)

| Type | Namespace | Complexity | Notes |
|------|-----------|------------|-------|
| `IReadOnlyList<T>` | System.Collections.Generic | Medium | Required by ValueArray, ExecutionContext |
| `IReadOnlyCollection<T>` | System.Collections.Generic | Medium | Base of IReadOnlyList |
| `IEnumerable<T>` | System.Collections.Generic | Medium | Required for foreach on ValueArray |
| `IEnumerator<T>` | System.Collections.Generic | Medium | Required for ValueArray enumerator |
| `IEnumerable` | System.Collections | Medium | Non-generic IEnumerable |
| `IEnumerator` | System.Collections | Medium | Non-generic IEnumerator |
| `IEquatable<T>` | System | Medium | BigInteger, Token |
| `IComparable<T>` | System | Medium | BigInteger |
| `ValueTuple<T1,T2>` | System | Medium | Tuples in BigInteger, Value, StringMap |
| `ValueTuple<T1,T2,T3>` | System | Medium | Tuples in ITimeProvider |
| `ValueTuple<T1,T2,T3,T4>` | System | Medium | Tuples in ITimeProvider |
| `TupleElementNamesAttribute` | System.Runtime.CompilerServices | Medium | Required for named tuples |
| `IsExternalInit` | System.Runtime.CompilerServices | Medium | Required for record types (init-only properties) |
| `StringComparer` | System | Simple | StringMap ordinal ignore case |
| `StringBuilder` | System.Text | Medium | Lexer, AstNodes (12+ usages) |
| `IntPtr` | System | Simple | Partial exists; needs full implementation |
| `UIntPtr` | System | Simple | Already in MiniCoreLib |
| `Func<T>` / `Func<T,TResult>` | System | Medium | EngineHeap callbacks |
| `Action` / `Action<T>` | System | Medium | Potential future use |
| `IDisposable` | System | Simple | EngineHeap |
| `DefaultMemberAttribute` | System.Reflection | Simple | Indexer support (4 collection types) |
| `ParamArrayAttribute` | System | Simple | Parser params arrays |
| `ReadOnlySpan<T>` | System | Medium | UTF-8 provider interface |
| `Span<T>` | System | Medium | Potential UTF-8 optimization |
| `DateTime` | System | Medium | ITimeProvider |
| `TimeSpan` | System | Medium | ITimeProvider |
| `HashSet<T>` | System.Collections.Generic | Medium | Parser duplicate detection |
| `Dictionary<K,V>` | System.Collections.Generic | Medium | Keywords, Parser symbol tables |
| `List<T>` | System.Collections.Generic | **Major** | 30+ allocations in Parser/Lexer |
| `Math` (static methods) | System | Medium | MathProvider hosted impl |
| `Exception` hierarchy | System | Medium | ArgumentOutOfRangeException, InvalidOperationException |

---

## Phase 3 — NativeAOT Build Attempt

### 3.1 Build Configuration Used

```xml
<NoStdLib>true</NoStdLib>
<NoConfig>true</NoConfig>
<DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
<DisableImplicitUsings>true</DisableImplicitUsings>
<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<GenerateRuntimeConfigurationFiles>false</GenerateRuntimeConfigurationFiles>
<UseAppHost>false</UseAppHost>
<RuntimeMetadataVersion>v4.0.30319</RuntimeMetadataVersion>
```

### 3.2 Toolchain
- dotnet SDK 10.x
- ilc (NativeAOT compiler) from NuGet cache
- clang/lld for assembly and linking

### 3.3 Result
**Compilation fails at C# level (csc)** — missing predefined types before ilc runs. The MiniCoreLib is incomplete for the Engine Core's dependency surface.

---

## Phase 4 — Minimal Runtime Gap Analysis

### 4.1 Simple Compatibility Additions (1-2 days each)

| Blocker | File | Solution |
|---------|------|----------|
| `StringComparer` | StringMap.cs:20,25 | Add minimal `StringComparer` with `OrdinalIgnoreCase` static property |
| `DefaultMemberAttribute` | StringMap.cs:39, ValueArray.cs:25, IntList.cs:19, StringList.cs:19, UsingList.cs:21 | Add attribute stub |
| `ParamArrayAttribute` | Parser.cs params arrays | Add attribute stub |
| `IDisposable` | EngineHeap.cs:14 | Add interface stub |
| `IntPtr` (complete) | EngineHeap, IMemoryProvider | Extend existing partial |

### 4.2 Medium Runtime Additions (3-5 days each)

| Blocker | Files | Solution |
|---------|-------|----------|
| `IReadOnlyList<T>`, `IReadOnlyCollection<T>` | ValueArray, ExecutionContext, HeapObjects | Add interfaces + `IEnumerable<T>`/`IEnumerator<T>` |
| `IEquatable<T>`, `IComparable<T>` | BigInteger, Token | Add interfaces |
| `ValueTuple` + `TupleElementNamesAttribute` | BigInteger, Value, StringMap, Division, Arithmetic | Add structs + attribute |
| `IsExternalInit` | AstNodes (50+ records) | Add attribute |
| `Func<T>` / `Action` | EngineHeap callbacks | Add delegate types |
| `ReadOnlySpan<T>` | IUtf8Provider, FreestandingUtf8Provider | Add struct (intrinsic) |
| `StringBuilder` | Lexer, AstNodes | Add minimal implementation |

### 4.3 Major Architectural Blockers (Weeks)

| Blocker | Files | Solution |
|---------|-------|----------|
| `List<T>` / `Dictionary<K,V>` / `HashSet<T>` | Parser.cs (28+), Lexer.cs, Keywords.cs | **Replace with freestanding equivalents** — Parser/Lexer are the largest BCL consumers |
| `DateTime` / `TimeSpan` | ITimeProvider | Implement in MiniCoreLib or make provider freestanding-only |
| `Math` static methods | MathProvider hosted | Already have freestanding MathProvider; remove hosted dependency |
| Record types in AstNodes | 50+ record structs | Convert to `readonly struct` with manual constructors |

---

## Phase 5 — Runtime Proof

### 5.1 What Was Proven

| Stage | Result |
|-------|--------|
| NativeAOT compilation (csc) | ❌ Fails — missing predefined types |
| Linking (ilc) | ❌ Not reached |
| Executable start | ❌ Not reached |
| Engine initialization | ❌ Not reached |
| Script parsing | ❌ Not reached |
| Expression evaluation | ❌ Not reached |
| Output production | ❌ Not reached |

### 5.2 Minimal Working Subset

The **freestanding collections + BigInteger + Value + EngineHeap** can compile if MiniCoreLib adds:
1. `IReadOnlyList<T>`, `IEnumerable<T>`, `IEnumerator<T>` + non-generic variants
2. `IEquatable<T>`, `IComparable<T>`
3. `ValueTuple` + `TupleElementNamesAttribute`
4. `StringComparer`, `DefaultMemberAttribute`, `IntPtr` (complete)
5. `Func<T>`, `IDisposable`

**Estimated effort: 1-2 weeks** for MiniCoreLib additions to enable minimal runtime probe.

---

## Phase 6 — GC Root Scanning Validation (M3.2)

### 6.1 Finding: Non-HeapObject Containers Are GC-Invisible

EngineHeap's precise mark-and-sweep collector (`EngineHeap.cs`) only traverses:
- Objects in the EngineHeap roots table (registered via `AddRoot`)
- Fields of known `HeapObject` subtypes (String, Array, Object, EnumSet, Closure, KeyEvent, NativeFunction)

**`NodeList<T>` and its backing `T[]` are NOT scanned by EngineHeap's mark phase.** References stored exclusively in a `NodeList<T>` are invisible to EngineHeap's GC. If a `HeapObject` is referenced only by a `NodeList<Value>` (not added as an EngineHeap root), EngineHeap will collect it despite the `NodeList<T>` still holding the reference.

This limitation applies equally to all non-HeapObject containers (StringList, IntList, ValueArray, FrameStack, UsingList, StringBuffer).

### 6.2 Validation Evidence

CollectionTests (`host/CollectionTests/`) directly verifies:

| Test | Result | Meaning |
|------|--------|---------|
| `GC.UnrootedObjectCollected` | PASS | EngineHeap collects unrooted objects |
| `GC.NodeListReferencedCollectedByEngineHeap` | PASS | EngineHeap collects objects referenced only by NodeList<T> — proving no array scan |
| `GC.RootedObjectNotCollected` | PASS | EngineHeap preserves EngineHeap-rooted objects |

### 6.3 NativeAOT Implications

In NativeAOT there is no Hosted CLR GC — only EngineHeap (or a future freestanding collector). References stored in `NodeList<T>` backing arrays would be invisible to the NativeAOT collector, potentially causing premature collection of live heap objects.

### 6.4 Recommendation

A dedicated GC root/container integration milestone is required before claiming full NativeAOT runtime readiness. Containers that hold `Value` references to `HeapObject` instances must either:
1. Register their contents as EngineHeap roots, or
2. Be scanned by EngineHeap's mark phase, or
3. Be converted to HeapObject subclasses

---

## Recommendations

### Immediate (This Milestone)
1. **Do not modify Engine Core** — the probe correctly identifies MiniCoreLib gaps
2. **Document all blockers** in this report (complete)
3. **Decide strategy**: Extend MiniCoreLib vs. refactor Engine Core

### Short Term (Next Milestone)
**Option A: Extend MiniCoreLib** (Recommended)
- Add 20+ missing types to MiniCoreLib
- Estimated: 2-3 weeks
- Preserves Engine Core architecture
- Enables full Engine Core NativeAOT compilation

**Option B: Refactor Engine Core for MiniCoreLib**
- Replace Parser/Lexer `List<T>`/`Dictionary<K,V>` with freestanding collections
- Convert record structs to `readonly struct`
- Remove `StringBuilder` dependency from Lexer/AstNodes
- Estimated: 3-4 weeks
- Reduces MiniCoreLib surface area

**Option C: Hybrid**
- Extend MiniCoreLib for runtime types (ValueTuple, interfaces, IntPtr)
- Refactor Parser/Lexer to use freestanding collections
- Best of both: smaller MiniCoreLib, cleaner Engine Core

### Long Term
- Implement OSLANG.EXE as freestanding NativeAOT userspace program (like SHELL.EXE)
- Use osbkt + MiniCoreLib linked sources (proven pattern)
- Kernel integration not required for language execution

---

## Appendix — Probe Artifacts

```
osb.oslang/engine/Probe/
├── Osb.Engine.Probe.csproj      # NativeAOT project (kernel-style)
├── ProbeMain.cs                  # Full engine probe (405 errors)
└── ProbeMainMinimal.cs           # Minimal runtime probe (119 errors)
```

Run with:
```bash
cd osb.oslang/engine/Probe
dotnet build Osb.Engine.Probe.csproj
```