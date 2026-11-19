# OSLANG Engine Design

**Repo:** `osb.oslang` · **Phase:** O0 (design, no engine code yet) · **Spec target:** OSLANG 0.73 (`OSLANG-SPEC.md` + per-version specs) · **Status:** DECIDED unless marked [OPEN].

This document is the design record for the new engine (`engine/`, `host/`, `os/`). It is written once, before O3 code, and updated in the same commit as any design change (OSB-ARCHITECTURE §7 docs-are-code).

---

## 1. Build targets (D1: one engine, two targets)

| Target | Build | Platform code | Purpose |
|---|---|---|---|
| `Host.Test` | normal `dotnet build`/`dotnet run` | `System.Console`, `System.IO`, `System.DateTime`, `Environment.TickCount` | conformance runner, oracle comparison, GC stress tests; allocates freely |
| `Host.Osb` | freestanding NativeAOT (ilc) against `osb.core/src/MiniCoreLib.cs` | osbkt `INT 21h` syscalls only | ships as `A:\OSB\BIN\OSLANG.EXE` (staged by `osb.kernel/build.sh` in O6) |

The engine core (`Lexing`, `Parsing`, `Ast`, `Runtime` incl. GC/values/heap, `Eval`) is **one source tree compiled twice**. Only the host layer differs: `IHostApi` (console, files, time, process args, memory failure) implemented by `Host.Test` (BCL) and by `Host.Osb` (osbkt). No BCL surface (`System.IO/Text/Xml/Linq/Collections.Generic/Math/DateTime...`) may appear in engine core or `Host.Osb`.

---

## 2. Allowed C# subset (freestanding target, MiniCoreLib)

Verified with a freestanding probe compiled to a native object with ilc and **booted in QEMU** (`/tmp/kilo/probe`; serial sign-on printed, ACPI poweroff exited QEMU rc=0):

- ✅ interfaces + virtual calls through interface and base-class references
- ✅ generics (generic static class + method)
- ✅ structs with methods
- ✅ `stackalloc`, `fixed`, `unsafe`
- ✅ string literals (printed via `fixed char*` over the literal)
- ✅ `switch` on int with `default`
- ✅ `[StructLayout(LayoutKind.Explicit)]`/`FieldOffset` tagged-value-style unions
- ✅ DllImport("osbnative") direct pinvoke to boot.S helpers (outb/inb/osb_poweroff)
- ❌ **C# delegates are NOT freestanding**: `new Work(method)` needs `Delegate.InitializeOpenStaticThunk`, which MiniCoreLib does not provide. The engine implements closures/named-function values as its own heap objects with an explicit invoke slot — never via `System.Delegate` or `MulticastDelegate`.

Hard rules for engine core + Host.Osb:
- No `new` except engine heap objects allocated through the engine allocator; no BCL allocation paths.
- No C# exceptions (`throw`/`try`/`catch`/`finally`) for ordinary control flow anywhere in the engine; errors travel as return codes + an unwind slot in the interpreter (see §6).
- `fixed`/`stackalloc`/pointers allowed; raw heap pointers must never be held in C# locals across an allocation (D2 roots).
- **Max 400 lines per source file** (engine code; docs kept short as well).

---

## 3. Value / heap / GC model (D2)

**Tagged values.** A `Value` is a fixed-size struct: kind tag + payload union (explicit layout, per §2): `NULL`, `BOOLEAN`, `NUMBER` (IEEE-754 double, own formatting/parsing — no `System.Math`/`Double.ToString`), `STRING`, `ARRAY`, `OBJECT` (class instance), `ENUM` (enum type + member), `CLOSURE` (function value + captured frame), `KEY-EVENT` (`KEYCODE.KEY`/`CHAR`/`CTRL`/`ALT`/`SHIFT`), `NATIVE-FN` (OSL.* host callable).

**Heap.** The engine owns its heap inside the process's memory block: a bump arena first (O3b), precise non-moving mark-sweep GC (O3c). Heap objects carry a header (mark bit, kind, size). Strings, arrays, objects, closures live there; numbers/booleans/null are immediate.

**Roots.** An explicit root/frame stack owned by the interpreter; every heap reference reachable from executing code is either on that stack or reachable from it. No raw heap pointer is stored in a C# local across any allocation that could trigger a collection. GC stress tests in O3c (bounded memory over long loops; poison-fill freed memory in test builds).

**Memory budget.** Ring-3 process memory today is one fixed 2 MiB block (OSB-ARCHITECTURE §8); K1 adds a grow-in-2-MiB-steps syscall up to a per-process limit, released at reap. The engine carves its arena from the process block and reports heap peak per O3 sub-phase. Startup must fit in the base block; growth covers large scripts.

---

## 4. String model (D3)

- Immutable, **length-prefixed UTF-8** in the engine heap. Length stored in bytes; no null termination relied upon.
- **[OPEN] SQ-001:** indexing unit for `COUNT()`/`LENGTH`, `SUBSTR(start, length)`, `INDEXOF(v)` — UTF-8 bytes, Unicode code points, or UTF-16 code units? Spec says "Unicode text" (§3) with no indexing rule (OSLANG-SPEC §13, 0.61). Proposal: code points; decision needed before O3b.
- **[OPEN] SQ-002:** case-folding for the case-insensitive language (§2): ASCII-only invariant culture or full Unicode? Proposal: ASCII-only (identifiers, keywords, enum members, property names); owner to confirm.
- Encodings: script source is read as bytes and decoded as UTF-8 (spec: source files are text; 0.61 escapes `\n \t \\`, triple quotes, `${}` interpolation per OSLANG-SPEC §13).
- Number/date formatting and parsing are hand-written (no `ToString`/`Parse` from BCL).

---

## 5. Module layout

```
engine/
  Lexing/     lexer, tokens
  Parsing/    parser, AST (arena-allocated nodes)
  Ast/        AST node definitions
  Values/     tagged Value, heap object layouts (strings, arrays, objects, closures)
  Heap/       bump arena + mark-sweep GC, root stack
  Eval/       interpreter: control flow (return/break/continue/exception unwind), OOP, functions
  Host/       IHostApi interface
host/
  Host.Test/  normal .NET IHostApi (BCL) + conformance runner main
  Host.Osb/   freestanding IHostApi over osbkt INT 21h
os/
  OSLANG.EXE entry: arg parsing, script load via osbkt file primitives,
  error formatting with source locations, exit codes, Ctrl+C (EINTR)
lib/          OSL.* standard library over IHostApi (O4)
tests/conformance/   .osl + .out + .in + tags (O2)
samples/             including samples/seed/ (kernel seed scripts moved in O0)
archive/kernel-port/ old in-kernel port (read-only, not built)
reference/           Osb.Lang 0.72 reference/oracle (untouched, keeps building)
```

`Osb.Lang.csproj` and docs must not reference the pre-split `src/` layout (see §7 housekeeping).

---

## 6. Exception model (no C# exceptions)

The interpreter evaluates expressions/statements into a control result: `Normal | Return | Break | Continue | Exception`, plus a current-error slot (numeric OSLANG error code + message + source location `file:line`).

- `TRY … CATCH ERR … END`: the interpreter unwinds the active frames to the nearest `TRY` frame (explicit stack unwind over the interpreter's own frames — not the C# stack) and binds `ERR` to the error message.
- Runtime errors carry source locations where the lexer/parser tracked them; IO/JSON/date errors all surface through `CATCH` (OSLANG-SPEC §14).
- `EXIT(code)` ends the process with the given code (0.7 / OSL.APP.EXIT).
- Network (`OSB.NET`/HTTP) exists on the language surface and raises a defined, catchable OSLANG runtime error "not supported on this host" (plan item 4, SQ-017 assigns its code/message).
- `ERR` holds the message string; error categories: lexical, syntax, semantic, runtime (0.1).

---

## 7. Recursion / stack limits

User recursion consumes interpreter frames, not the C# call stack. An explicit frame stack with a hard depth cap (measured like the kernel port's MaxCallDepth=12, re-measured for the OSB stack size) raises a catchable "recursion too deep" runtime error on overflow. The C# stack is used only by the engine's own bounded-depth operations.

---

## 8. Host-layer API (`IHostApi`)

Small, transport-agnostic surface (kernel syscalls are the transport; the engine is not a trust boundary — the kernel validates everything):

- console: `Write(text)`, `Clear()`, input line `ReadLine()`, raw key events `GetKey()` / `KeyAvailable()`, size/cursor/color (K2), `Beep()`
- file: `OpenRead|Write|Append|Create`, `Read`, `Write`, `Seek`, `Close`, `Stat`, `List`, `MakeDir`, `Delete`, `Rename`, `GetCwd` — real error codes (K3)
- time: `Now` (date/time components), `Ticks` (0.5 DATETIME/TICKS syscalls)
- process: `Exit(code)`, `Args`
- memory: allocation failure surfaces as an OSLANG out-of-memory error

`Host.Test` implements with BCL; `Host.Osb` with osbkt. Scripts are opened by the interpreter with the caller's uid — read permission suffices (D4); the kernel enforces.

---

## 9. Binary size (probe measurement)

Freestanding probe (`/tmp/kilo/probe`, ~150 lines exercising interfaces/virtual/generics/structs/stackalloc/literals/switch, serial I/O, QEMU-booted, poweroff rc=0):

- `probe.o`: text 2,660 B + data 1,848 B ≈ **4.5 KB** native code for the whole program
- Booted ELF (`probe.elf`) runs and power-offs cleanly under QEMU (GRUB multiboot2, same pipeline as `osb.kernel/build.sh`)

Extrapolation: a full engine (lexer, parser, arena AST, interpreter, GC, value model) will be tens of KB of native code — comfortably inside one 2 MiB process block, with K1 growth available for large heaps. No BCL is linked, so there is no runtime overhead to measure.

---

## 10. Numbers, dates, calendar

- NUMBER = double; formatting/parsing hand-written (fixed-precision fractional output, exponent for large magnitudes — the kernel port's 12-fraction-digit truncation is a known defect, OSLANG-GAP §5.10, not a spec mandate).
- DATE/TIME (0.5): own Gregorian calendar over the `DATETIME`/`TICKS` syscalls; `DATETIME = [DATE, TIME]` structural array (OSLANG-SPEC §3); no timezone objects, no arithmetic (0.5 §17/§18).
- `MATH` (0.4): hand-written `SQRT/ABS/POW/FLOOR/CEIL/ROUND/TRUNC/SIGN/MIN/MAX/CLAMP/RANDOM/LOG/LOG10/EXP/SIN/COS/TAN/ASIN/ACOS/ATAN/ATAN2`, constants `PI`/`E` (no `System.Math`).

---

## 11. Open design questions (delegated to the owner)

See `docs/SPEC-QUESTIONS.md`. The engine does **not** guess at spec ambiguities; each blocks the corresponding O3/O4 sub-task until answered.
