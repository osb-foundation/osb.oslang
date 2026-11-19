# O3.1 Freestanding Dependency Analysis

**Engine:** `osb.oslang/engine/` (Osb.Engine.dll, net10.0)
**Target:** OSB Ring 3 (freestanding, no .NET BCL, NativeAOT/MiniCoreLib)

---

## 1. Dependency Classification

| Category | Dependencies | Freestanding Viability |
|----------|--------------|------------------------|
| **Language Core** | AST, Lexer, Parser, Value types | ✅ Pure C#, no BCL |
| **Managed Runtime** | `System.Collections.Generic` (List, Dictionary, IReadOnlyList) | ⚠️ MiniCoreLib provides subset |
| **BCL - Collections** | `List<T>`, `Dictionary<K,V>`, `HashSet<T>` | ⚠️ MiniCoreLib has basic collections |
| **BCL - Strings** | `StringBuilder`, `System.Text.Encoding.UTF8`, `CultureInfo` | ❌ No direct equivalent |
| **BCL - Math** | `System.Math` (Sin, Cos, Tan, Sqrt, Pow, PI, E, etc.) | ❌ Must implement or host-call |
| **BCL - Arrays** | `System.Array.Copy` | ⚠️ MiniCoreLib has `Buffer.BlockCopy` |
| **BCL - Random** | `System.Random` | ❌ Must implement or host-call |
| **BCL - DateTime** | `System.DateTime`, `DateTimeOffset` | ❌ Must implement or host-call |
| **BCL - Exceptions** | `InvalidOperationException`, `ArgumentOutOfRangeException` | ⚠️ MiniCoreLib has basic exceptions |
| **BCL - Runtime** | `System.Runtime.CompilerServices.Unsafe`, `MethodImpl` | ❌ Not in MiniCoreLib |
| **GC/Alloc** | `GCHandle`, `GC.Collect` (not used directly) | ❌ Manual memory management needed |
| **Host Services** | `IHostApi` (console, files, time, RNG) | ✅ Abstracted via interface |

---

## 2. Code Paths Blocking Freestanding Compilation

### Critical (must replace/remove)

| File | BCL Dependency | Replacement Strategy |
|------|----------------|---------------------|
| `Values/Value.cs` | `System.Text.Encoding.UTF8`, `CultureInfo`, `Math.Floor/Abs` | Custom UTF-8 codec; custom math; custom number formatting |
| `Eval/Interpreter.cs` | `System.Math.*`, `System.Random`, `System.DateTime`, `System.Array.Copy`, `Encoding.UTF8` | Host-call math/RNG/time; custom array copy; custom UTF-8 |
| `Lang/Lexer.cs` | `double.Parse(CultureInfo)`, `Encoding.UTF8` | Custom number parser; custom UTF-8 |
| `Heap/EngineHeap.cs` | `System.Runtime.CompilerServices.Unsafe`, `GCHandle` | Raw pointer arithmetic; manual alloc/free |

### Moderate (can wrap via Host API)

| File | BCL Dependency | Replacement Strategy |
|------|----------------|---------------------|
| `Eval/Interpreter.cs` | `System.Text.StringBuilder` | Host `StringBuilder` or custom |
| `Host/HostTest.cs` | `System.IO`, `Console` | Remove; use `IHostApi` only |

### Non-blocking (test infrastructure only)

| File | Status |
|------|--------|
| `ConformanceRunner.cs` | Test runner only; exclude from freestanding build |
| `Host/HostTest.cs` | BCL host implementation; exclude from freestanding build |

---

## 3. Minimum Interface Boundary

```csharp
// 1. LANGUAGE ENGINE (pure logic, no BCL)
public interface ILanguageEngine
{
    ParseResult Parse(string source);
    ExecutionResult Execute(Program program, IHostServices host);
}

// 2. MEMORY MANAGEMENT (pluggable)
public interface IMemoryManager
{
    HeapString AllocString(ReadOnlySpan<byte> utf8);
    HeapArray AllocArray(int length);
    HeapObjectInstance AllocObject(ClassDefinition cls);
    HeapClosure AllocClosure(FunctionDecl fn, Value[] captured);
    void Collect(); // GC trigger
}

// 3. HOST API (platform services)
public interface IHostApi
{
    void Write(string text);
    string ReadLine();
    long NowTicks();           // UTC ticks for time
    double Random();           // [0,1) for RNG
    void Exit(int code);
}

// 4. PLATFORM IMPLEMENTATION (freestanding)
public interface IFreestandingPlatform : IHostApi, IMemoryManager
{
    // Implemented in OSB Ring 3 via syscalls
    // e.g., sys_alloc, sys_write, sys_time, sys_random
}
```

---

## 4. Incremental Port Path

### Phase 1: Core Language (no stdlib)
- [ ] Extract `Lang/`, `Values/Value.cs` (core only), `Heap/EngineHeap.cs` (interface + stub)
- [ ] Replace `List<T>`/`Dictionary<K,V>` with `MiniCoreLib` or custom `ArrayList`/`HashMap`
- [ ] Implement custom UTF-8 codec (`Utf8Encoder/Decoder`)
- [ ] Implement custom number parser/formatter

### Phase 2: Math & Time via Host
- [ ] Move all `System.Math.*` calls to `IHostApi.Math` (sin, cos, sqrt, pow, PI, E, etc.)
- [ ] Move `System.Random` to `IHostApi.Random()`
- [ ] Move `System.DateTime` to `IHostApi.NowTicks()` + custom calendar math

### Phase 3: Memory Manager Implementation
- [ ] Implement `IFreestandingPlatform.MemoryManager` using `sys_alloc`/`sys_free`
- [ ] Implement bump allocator + mark-sweep GC without `GCHandle`/`Unsafe`
- [ ] Replace `GCHandle`-based Value storage with raw pointer offsets

### Phase 4: String Operations
- [ ] Implement `HeapString` methods (SubstringByCodePoints, IndexOf, GetCodePoint) without `Encoding.UTF8`
- [ ] Implement UTF-8 validation on alloc

### Phase 5: Build Integration
- [ ] Create `Osb.Engine.Freestanding.csproj` targeting `net10.0` + `<RuntimeIdentifier>native</RI>`
- [ ] Link with MiniCoreLib or custom `corelib`
- [ ] Test in QEMU with OSB kernel

---

## 5. Key Architectural Decisions

1. **No duplicate interpreter** - The same `Interpreter` class works for both hosted and freestanding; only `IHostApi`/`IMemoryManager` implementations differ.

2. **BCL-free `Value` struct** - Current `Value` uses `object` for heap refs. For freestanding, replace with `nint` (raw pointer) + manual type tag, or keep `object` if MiniCoreLib supports it.

3. **UTF-8 at boundary only** - Engine operates on `HeapString` (validated UTF-8 + code-point offsets). Host/Platform handles encoding conversion if needed.

4. **Math as host service** - `System.Math` is large. Delegate to host (which can use hardware FPU or soft-float libm).

5. **GC is platform-specific** - Hosted uses .NET GC; Freestanding uses custom mark-sweep. Interface: `IMemoryManager.Collect()`.

---

## 6. Estimated Effort

| Phase | Files to Modify | New Files | Risk |
|-------|-----------------|-----------|------|
| 1 | 5 | 3 | Medium |
| 2 | 2 | 1 | Low |
| 3 | 2 | 2 | High |
| 4 | 1 | 2 | Medium |
| 5 | 1 | 1 | Low |

**Total:** ~11 files modified, ~9 new files, ~3-4 weeks for minimal freestanding MVP.

---

## 7. Recommendation for O3.2

**O3.2 Milestone:** "Freestanding Core + Hosted Math/Time Delegation"

1. Create `Osb.Engine.Core` library (no BCL math/time/UTF-8)
2. Extract `IMathProvider`, `ITimeProvider`, `IRandomProvider` from `IHostApi`
3. Implement hosted providers using BCL (for regression testing)
4. Implement freestanding stub providers (panic/unimplemented)
5. Run conformance suite with hosted providers → all green
6. Document freestanding TODOs per provider

This keeps the interpreter single-source while making the freestanding boundary explicit and testable.