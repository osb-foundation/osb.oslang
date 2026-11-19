# engine — OSLANG interpreter core

The new OSLANG engine source tree. **Compiled twice:**

1. **Host** (`host/Host.Test`) — normal .NET, used by the conformance runners
   (`dotnet run`). Fast to build and debug.
2. **OSB** (`host/Host.Osb`) — freestanding NativeAOT, linked against
   `MiniCoreLib` (from `osb.core`), no BCL, no GC allocations from C#,
   ≤ 400 lines per file. Produces `OSLANG.EXE`.

Only the *host layer* (console, files, time, memory, syscall transport)
differs between the two targets. The engine core is shared.

## Layout (populated in O3)

```
engine/
  Lang/        lexer, parser, AST (arena-allocated)
  Values/      tagged value model, strings, numbers
  Heap/        own mark-sweep GC over the engine's managed heap
  Eval/        interpreter, control flow, OOP, exceptions
  Stdlib/      OSL.* libraries (MATH, FILE, DIR, JSON, CSV, XML, CNF, I18N)
  Host/        host-layer interface (IHostApi) implemented by Host.Test / Host.Osb
```

See [`../docs/ENGINE-DESIGN.md`](../docs/ENGINE-DESIGN.md) for the design
(value/heap/GC model, string model, C# subset, exception model, memory budget).

This directory is intentionally empty until O3. Nothing here is built yet.