# host — host layers for the OSLANG engine

Two host layers, one engine core. Only this layer differs between targets.

## Host.Test

Normal .NET build (`dotnet run`). Used by:
- the conformance runners (`tests/conformance/`),
- the oracle report (`docs/ORACLE-REPORT.md`),
- stress/GC tests.

Implements `IHostApi` with `System.Console`, `System.IO.File`, `System.DateTime`,
`System.Environment.TickCount`. Allocates freely — it is not bound by the
freestanding rules.

## Host.Osb

Freestanding NativeAOT build. Implements `IHostApi` over `osbkt` syscalls
(`INT 21h`). No BCL, no GC allocations from C#, fixed/stack buffers,
≤ 400 lines per file. Produces `A:\OSB\BIN\OSLANG.EXE` (staged by
`osb.kernel/build.sh` once O6 lands).

## Layout (populated in O6)

```
host/
  Host.Test/   dotnet test runner + console/file/time host
  Host.Osb/    OSLANG.EXE entry, osbkt syscall wrappers, freestanding host
```

These directories are intentionally empty until O2/O6.