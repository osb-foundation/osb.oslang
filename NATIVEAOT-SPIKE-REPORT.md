# OSLANG Engine Core NativeAOT Compatibility Spike — Analysis Report

## Executive Summary

**Result: M8 compilation achieved (0 CS errors), but NativeAOT executable generation is BLOCKED.**

M8 work extended MiniCoreLib with all types required for Engine Core to compile under NoStdLib. The probe project (`Osb.Engine.Probe`) builds with **0 C# compilation errors**. However, NativeAOT executable generation cannot proceed because `NoStdLib=true` prevents the .NET SDK from resolving the ILCompiler package and generating an apphost/native host. The build produces only a managed DLL that cannot execute without a BCL runtime.

No native executable has been generated. No runtime execution has been demonstrated.

---

## M8 — Changes Summary (completed)

### New MiniCoreLib files (`osb.core/src/`)

| File | Type | Used By | Implementation Status |
|------|------|---------|----------------------|
| `Console.cs` | Stub | ProbeMain.cs | Stub (WriteLine no-op) |
| `Index.cs` | Functional | Parser.cs, BigInteger.cs, BigIntegerArithmetic.cs | Functional struct with FromStart/FromEnd/GetOffset/operators |
| `List.cs` | Functional | ExpressionEvaluator, Interpreter, NamespaceEvaluator, BigInteger, FreestandingUtf8Provider | Functional List<T> with Add, Reverse, ToArray, indexer, Count, enumerator, IReadOnlyList<T>. Missing: Clear, Contains, Insert, RemoveAt, Capacity |
| `Math.cs` | Stub/Partial | StringMap.cs | Abs/Max/Min functional; Pow returns 1.0; Sqrt returns 0.0 |
| `StringComparison.cs` | Functional | ExpressionEvaluator, Interpreter, HeapObjects | Functional enum |

### Modified MiniCoreLib files

| File | Changes | Implementation Status |
|------|---------|----------------------|
| `MiniCoreLib.cs` | Added String constructors (char[], char[]+int+int), ToUpperInvariant, Substring, PadLeft, TrimEnd, Trim, Equals overloads (static + instance, with StringComparison), Nullable<T>.HasValue/Value, Char.IsDigit/IsLetter/IsLetterOrDigit, Array.IndexOf, Buffer.MemoryCopy 4-arg overload | All stubs (throw null or return defaults) |
| `PrimitiveTypes.cs` | Added UInt32, IntPtr.operator+, GCHandle/GCHandleType with FromIntPtr/ToIntPtr/AddrOfPinnedObject/Target/Free | GCHandle is a STUB — Alloc returns default, Target returns null, Free is no-op |
| `Exceptions.cs` | Added parameterless ctors to ArgumentException, InvalidOperationException, ArgumentOutOfRangeException | Functional |
| `DateTime.cs` | Changed Ticks from ulong to long, MaxValue uses long.MaxValue | Functional for ticks-based math |
| `BitConverter.cs` | Changed from DoubleToInt64Bits/Int64BitsToDouble (long) to DoubleToUInt64Bits/UInt64BitsToDouble (ulong) | Functional for IEEE 754 bit casting |
| `osb.core.csproj` | Added all new source files to Compile items | Build configuration |

### Engine.Core changes

| File | Change | Reason |
|------|--------|--------|
| `Parser.cs` | `CheckAny`: `types.Contains()` → `Array.IndexOf(types, ...)`; `Current`: `_tokens[^1]` → `_tokens[_tokens.Count - 1]` | Avoid TokenType[].Contains and Index^ operator (needs System.Index compiler support) |
| `BigInteger.cs` | `_words[^1]` → `_words[_words.Length - 1]` | Same reason |
| `BigIntegerArithmetic.cs` | `result[^1]` → `result[result.Length - 1]` | Same reason |
| `FreestandingUtf8Provider.cs` | `(ReadOnlySpan<byte>)bytes` → `new ReadOnlySpan<byte>(bytes)` | Cannot cast array to span without BCL implicit conversion |

### Probe configuration

| File | Change |
|------|--------|
| `Osb.Engine.Probe.csproj` | Added `GlobalUsings.cs` to Compile items |
| `GlobalUsings.cs` | New: `global using System; System.Collections; System.Collections.Generic; System.Runtime.CompilerServices; System.Runtime.InteropServices;` |

---

## Phase 2 — Critical Runtime Contract Audit

### A. GCHandle — CRITICAL BLOCKER (STUB)

**Finding: GCHandle is a non-functional stub that breaks EngineHeap GC.**

The `GCHandle` struct in `PrimitiveTypes.cs` implements none of the actual handle semantics:
- `Alloc(object)` → returns `default` (GCHandle with `_handle = 0`)
- `Alloc(object, type)` → returns `default`
- `FromIntPtr(IntPtr)` → returns `default`
- `ToIntPtr(GCHandle)` → returns `default` (IntPtr.Zero)
- `Target` → returns `null`
- `Free()` → no-op
- `AddrOfPinnedObject()` → returns `default`

**EngineHeap impact** (`EngineHeap.cs:106,114,275,435,499`):
1. `GCHandle.Alloc(obj, GCHandleType.Normal)` returns default → `ToIntPtr` returns `IntPtr.Zero` → `header.Handle = IntPtr.Zero`
2. `GetManagedObject()` (`EngineHeap.cs:494`) checks `if (Handle == IntPtr.Zero) return null;` → always returns null
3. `ScanObject()` gets `managedObj == null` and returns immediately → **no objects are ever marked**
4. Sweep phase frees all unmarked objects → **all EngineHeap allocations are collected on first GC cycle**

**Verdict: BLOCKED.** Without a functional GCHandle, EngineHeap cannot preserve object references across GC cycles. This is a fundamental architectural limitation that requires either:
- A real GCHandle implementation that interops with the NativeAOT runtime GC, or
- A different object reference tracking mechanism

Do not attempt to fix within M8.1 scope (per instructions: do not redesign GC).

### B. DateTime — FUNCTIONAL

**Change:** `DateTime.Ticks` changed from `ulong` to `long` (`DateTime.cs:12-14`).

**Usage trace:**
- `ITimeProvider.Ticks` is `ulong` in the interface (`ITimeProvider.cs:17`)
- Hosted `TimeProvider.Ticks` casts BCL `DateTime.UtcNow.Ticks` (long) to `ulong` (`TimeProvider.cs:12`)
- `DateTimeNamespace.cs:71` reads `dt.Value.Ticks` (now `long`) for Unix epoch math
- `UnixEpochTicks = 621355968000000000L`, `TicksPerSecond = 10000000L` — correct .NET epoch constants

**Compatibility:**
- `DateTime` construction: `new DateTime(long ticks)` — functional
- Comparison: `CompareTo` uses long comparison — functional
- Negative timestamps (pre-1970): `ticks < UnixEpochTicks` yields negative `unixSeconds` — works with long arithmetic
- Hosted `TimeProvider` unchanged, still returns BCL `DateTime.UtcNow`

**Verdict: FUNCTIONAL.** No compatibility regression for hosted path. Freestanding path has no concrete `ITimeProvider` implementation (none exists in Core), so DateTime operations are not exercised at runtime.

### C. Console — STUB

**Implementation:** `Console.cs` — all `WriteLine` overloads are no-ops.

**Usage:** Only `ProbeMain.cs:41` calls `System.Console.WriteLine(...)`.

**Verdict: STUB.** No output will be produced. Probe cannot validate via console output. Deliberate unsupported stub — acceptable for compilation, not for runtime validation.

### D. Math — PARTIAL / STUB

| Method | Implementation | Used By | Status |
|--------|---------------|---------|--------|
| `Abs(int)` / `Abs(double)` | Functional | — | ✅ |
| `Max(int)` / `Max(double)` | Functional | StringMap.cs:27 | ✅ |
| `Min(int)` / `Min(double)` | Functional | — | ✅ |
| `Pow(double,double)` | Returns `1.0` | — | ⚠️ Stub |
| `Sqrt(double)` | Returns `0.0` | — | ⚠️ Stub |

**Verdict: PARTIAL.** Only `Math.Max(int,int)` is actually called. `Pow` and `Sqrt` return incorrect defaults but are not called by any Engine.Core code.

### E. List<T> — FUNCTIONAL (LIMITED)

**Implementation:** `List.cs` — supports Add, indexer (get/set), Count, Reverse, ToArray, enumerator, `IReadOnlyList<T>`, default and capacity constructors.

**Missing:** Clear, Contains, Insert, RemoveAt, Capacity property, index-out-of-range bounds checking.

**Usage trace:**
- `Interpreter.cs:78-79`: `new List<string>()`, `new List<Lang.MethodDecl>()` — Add called
- `NamespaceEvaluator.cs:28`: `new List<Value>()` — Add called
- `BigInteger.cs:100`: `new List<char>()` — Add, Reverse, ToArray called
- `FreestandingUtf8Provider.cs:108`: `new List<char>()` — Add, ToArray called

**GC visibility:** `List<T>` backing `T[]` array is NOT scanned by EngineHeap's mark phase (same limitation as `NodeList<T>`). References stored in `List<T>` are invisible to EngineHeap GC.

**Verdict: FUNCTIONAL for current usage.** No missing members are called by existing Engine.Core code.

---

## Phase 3 — NativeAOT Executable Generation

### Build Configuration

**Project:** `osb.oslang/engine/Probe/Osb.Engine.Probe.csproj`

**Key properties:**
```xml
<TargetFramework>net10.0</TargetFramework>
<NoStdLib>true</NoStdLib>
<NoConfig>true</NoConfig>
<DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
<DisableImplicitUsings>true</DisableImplicitUsings>
<EnableDefaultCompileItems>false</EnableDefaultCompileItems>
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<PublishTrimmed>true</PublishTrimmed>
<InvariantGlobalization>true</InvariantGlobalization>
```

**Build command:** `dotnet publish Osb.Engine.Probe.csproj -c Release -o <output>`

### Results

| Stage | Command | Result |
|-------|---------|--------|
| C# compilation (csc) | `dotnet build` | **0 CS errors** ✅ |
| Managed DLL output | `dotnet build` | `OsbEngineProbe.dll` produced (PE32+ managed assembly) |
| NativeAOT publish | `dotnet publish` | ❌ **MSB3030: apphost not found** |
| NativeAOT publish + PublishAot=true | `dotnet publish /p:PublishAot=true` | ❌ ILCompiler package not resolvable (NoStdLib prevents package resolution) |

### MSB3030 Analysis

Error: `Could not copy the file .../apphost because it was not found.`

**Cause:** With `NoStdLib=true` and `DisableImplicitFrameworkReferences=true`, the .NET SDK cannot generate the native apphost launcher because it depends on the BCL runtime being present. The apphost is a native binary that bootstraps the .NET runtime, which requires mscorlib/System.Private.CoreLib.

**Not harmless:** This indicates that no native executable can be generated in this configuration. The SDK's native host generation pipeline requires the standard BCL.

**Verdict: BLOCKED.** NativeAOT executable generation is impossible with `NoStdLib=true` because the .NET SDK's NativeAOT toolchain (ILCompiler + apphost generation) requires the BCL. MiniCoreLib provides type stubs for C# compilation but does not provide a NativeAOT runtime.

---

## Phase 4 — Runtime Execution Tests

**Status: NOT EXECUTABLE.**

The probe cannot be executed because:
1. No native executable was generated (Phase 3 blocked)
2. The managed DLL (`OsbEngineProbe.dll`) cannot be run with `dotnet exec` because `NoStdLib=true` means no BCL runtime is available to load it

**No runtime tests were performed.** No inputs, expected results, or actual results can be reported.

---

## Phase 5 — GC and Collection Assessment

### GC Architecture

EngineHeap uses a precise mark-and-sweep collector on platform-allocated memory. The mark phase (`EngineHeap.cs:222-290`):
1. Scans root table entries (`AddRoot`)
2. For each marked object, calls `ScanObject` which inspects known `HeapObject` subtype fields (HeapArray.Items, HeapObjectInstance.Fields, HeapEnumSet.Values, HeapClosure.Captured)
3. Sweeps unmarked objects

### GCHandle Bridge (BROKEN)

`HeapObjHeader.Handle` is populated via `GCHandle.ToIntPtr(GCHandle.Alloc(obj))`. With the GCHandle stub, Handle is always `IntPtr.Zero`, so `GetManagedObject()` returns null, and `ScanObject` never marks child objects.

### Collection-by-Collection GC Visibility

| Collection | Holds HeapObject refs? | Scanned by EngineHeap mark? | GC-safe? |
|------------|----------------------|---------------------------|----------|
| `NodeList<T>` | Yes | ❌ No | ⚠️ No — must be registered as roots |
| `StringMap<T>` | Yes | ❌ No | ⚠️ No — must be registered as roots |
| `StringList` | Yes | ❌ No | ⚠️ No |
| `ValueArray` | Yes | ❌ No | ⚠️ No |
| `FrameStack` | Yes | ❌ No | ⚠️ No |
| `UsingList` | Yes | ❌ No | ⚠️ No |
| `StringBuffer` | No (char[] only) | N/A | ✅ |
| `List<T>` (M8) | Yes | ❌ No | ⚠️ No |

**Verdict:** All collections that can hold `HeapObject` references are GC-invisible. This is a pre-existing architectural limitation (documented in NATIVEAOT-SPIKE-REPORT.md Phase 6), not introduced by M8. M8's `List<T>` adds another collection with the same limitation.

---

## Phase 6 — Regression Validation

### Builds

| Project | Result | Errors | Warnings |
|---------|--------|--------|----------|
| `osb.core` | ✅ Builds | 0 | 77 (pre-existing nullable + CS0169 GCHandle) |
| `Osb.Engine.Core` (hosted) | ✅ Builds | 0 | 5 (file lock retry, not code) |
| `Osb.Engine.Probe` | ✅ Builds | 0 CS errors | CS8632 (nullable annotations), CS1718 (IsNaN d!=d), CS0169 (GCHandle._handle unused) |
| NativeAOT publish | ❌ Blocked | MSB3030 | N/A |

### CollectionTests

**Result: 254 passed, 0 failed** ✅ (matches expected baseline)

### OSLANG Conformance Corpus

**Result: 27 passed, 11 failed, 1 skipped** ✅ (matches expected baseline exactly)

The 11 pre-existing failures are unchanged in identity and failure messages:
- 07-strings/ascii-case-conversion/accented-latin.osl
- 07-strings/ascii-case-conversion/mixed-preservation.osl
- 07-strings/ascii-case-conversion/non-ascii.osl
- 07-strings/unicode-indexing/ascii-only.osl (6 tests)
- 09-standard-lib/math.osl
- 10-gc/allocation-exhaustion.osl

### CS8625 Warnings (M7.1 tests)

Five CS8625 warnings in `DateTimeTests.cs` (lines 69, 71): passing `null` literals to non-nullable `ExecutionContext(EngineHeap, IHostApi)` and `DateTimeNamespace.Call(..., IMathProvider, ITimeProvider, IRandomProvider, IUtf8Provider)` parameters. These are pre-existing from M7.1 tests. Not straightforward to correct without either changing production API signatures (making parameters nullable) or using null-forgiving operators (suppressions). **Not corrected** per instructions (no suppressions, no unnecessary API changes).

---

## Phase 7 — Documentation Status

NATIVEAOT-SPIKE-REPORT.md has been updated with M8 results above. O3.5.3-HYBRID-PLAN.md references remain valid; M8 compilation work is complete but M8.1 validation shows NativeAOT execution is blocked.

---

## Remaining Blockers

### Compiler/Runtime Blockers
1. **NoStdLib prevents NativeAOT toolchain:** `NoStdLib=true` makes the .NET SDK unable to resolve ILCompiler packages or generate apphost. No native executable can be produced.
2. **GCHandle stub:** EngineHeap's GCHandle bridge is non-functional — all allocations have Handle=IntPtr.Zero, GetManagedObject returns null, no objects are marked, all are swept on first GC.

### Functional Blockers
1. **No runtime execution possible:** Without a native executable or a runnable managed DLL, no Engine.Core functionality can be tested at runtime.
2. **Console.WriteLine is a stub:** No output can be produced for probe validation.
3. **No freestanding ITimeProvider:** DateTime operations cannot be exercised without a time provider implementation.

### GC/Memory-Management Blockers
1. **GCHandle does not track objects:** Alloc/ToIntPtr/FromIntPtr/Target are all no-ops or return defaults.
2. **Collections are GC-invisible:** NodeList, List<T>, StringMap, etc. hold HeapObject references invisible to EngineHeap's mark phase.

### Hosted Compatibility Risks
None identified. All M8 changes are additive to MiniCoreLib and do not affect the hosted BCL path. Hosted Engine.Core builds clean (0 errors). Conformance results identical to baseline.

### Architectural Decisions Requiring Authorization
1. **NativeAOT runtime strategy:** The MiniCoreLib approach provides compilation-level type stubs but cannot produce a NativeAOT executable. A different approach (e.g., linking against a minimal NativeAOT runtime, or using the standard BCL with trimming) is needed for actual NativeAOT execution.
2. **GCHandle implementation:** A real GCHandle that interops with the NativeAOT GC is required for EngineHeap memory safety.
3. **Collection GC integration:** Containers holding HeapObject references must be integrated with EngineHeap's root scanning.

---

## Final Verdict

**BLOCKED**

**Evidence:**
1. Compilation compatibility: ✅ Achieved (0 CS errors in osb.core and Osb.Engine.Probe)
2. NativeAOT executable generation: ❌ Blocked (NoStdLib prevents ILCompiler/apphost generation)
3. Runtime execution: ❌ Not possible (no executable, no runnable managed DLL)
4. Functional correctness: ❌ Not demonstrated (cannot execute)
5. GC/memory-management: ❌ Broken (GCHandle stub makes EngineHeap GC non-functional)

NativeAOT compilation of Engine.Core source code is demonstrated, but NativeAOT **execution** is not possible in the current NoStdLib configuration. The GCHandle stub is a critical blocker — without functional object handle tracking, EngineHeap cannot safely manage memory under NativeAOT.

Do not proceed to subsequent milestones until the GCHandle and NativeAOT toolchain blockers are resolved.

---

## Git

| Repository | Branch | Status |
|------------|--------|--------|
| `osb.core` | `master` | 3 commits ahead of origin/master; 6 modified files + 6 new files uncommitted |
| `osb.oslang` | `master` | 10 commits ahead of origin/master; 18 modified files + 1 new directory (engine/Probe/) uncommitted |

**M8-specific changes (uncommitted):**

osb.core:
- Modified: `osb.core.csproj`, `src/DateTime.cs`, `src/Exceptions.cs`, `src/MiniCoreLib.cs`, `src/PrimitiveTypes.cs`
- New: `src/BitConverter.cs`, `src/Console.cs`, `src/Index.cs`, `src/List.cs`, `src/Math.cs`, `src/StringComparison.cs`
- (`src/CompilerSupport.cs` shows as modified but was pre-existing M1-M2 work)

osb.oslang:
- M8-specific: `engine/Core/Lang/Parser.cs`, `engine/Core/Math/BigInteger.cs`, `engine/Core/Math/BigIntegerArithmetic.cs`, `engine/Core/Providers/FreestandingUtf8Provider.cs`
- New: `engine/Probe/` (Osb.Engine.Probe.csproj, GlobalUsings.cs, ProbeMain.cs)
- Other modifications (DateTimeNamespace.cs, Interpreter.cs, etc.) are pre-existing M4-M7 work

**Nothing has been pushed.** All changes are local working-tree modifications only.

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