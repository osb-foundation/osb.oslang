# OSB / OSLANG — Runtime Reliability Audit Backlog

**Repo/branch:** `osb.oslang` / `master`  
**Commit:** `7d620a5` (HEAD; Batches 1-3 applied)  
**Date:** 2026-11-25  
**Scope:** OSLANG freestanding interpreter runtime (`osb.oslang/engine/Core/...`),  
          allocation/TLS helpers (`OsbRuntimeExports.cs`, `OsbMemoryProvider.cs`, `EngineHeap.cs`).

This backlog is the output of Phase 1 (state/evidence collection) and Phase 2
(audit) of the OSB/OSLANG reliability initiative. It is NOT a license to
rewrite the runtime; each entry is a small, independently verifiable fix.

---

## How to read

Severity: CRITICAL > HIGH > MEDIUM > LOW.
Confidence: CONFIRMED (code trace / test), LIKELY (strong code evidence),
LATENT (defect present but code path not exercised).

"Fixes implemented" below reflect Batches 1-3. Remaining items are the
recommended backlog for subsequent sessions.

---

## Batch 3 — implemented

### R3 — StringMap.Remove breaks open-addressing probe chains — HIGH — CONFIRMED — FIXED
### R6 — FrameStack.Current returns null! when empty — MEDIUM — CONFIRMED — FIXED

See the detailed entries in the prioritized backlog below.

---

## Batch 1 — implemented (commit a774f87)

### R1 — Soft-throw exceptions silently swallowed at function boundaries — CRITICAL — CONFIRMED — FIXED

- **Area:** Error propagation / interpreter control flow.
- **Evidence:** `engine/Core/Eval/Interpreter.cs`. `ExecutionContext.Throw()` sets
  `CurrentFlow = ControlFlow.Exception` and populates `CurrentException` /
  `CurrentExceptionMessage`, but `CallFunction` (lines ~122, 134) and `CallMethod`
  (lines ~156, 168) unconditionally executed `_ctx.CurrentFlow = ControlFlow.Normal`
  before returning. Any runtime error raised inside a called function body
  (division by zero, undefined variable, arg-count mismatch, unknown function,
  index out of bounds) was destroyed; execution continued with `NullValue` and
  `TRY/CATCH` could not catch errors thrown inside functions. Top-level
  `Interpreter.Execute` also called `Throw("No MAIN function found")` then
  returned `NullValue`; `OsbMain.cs` never inspected `CurrentFlow`, so the
  process exited 0 with no diagnostic.
- **Root cause:** `Throw()` is a "soft throw" (a flag, not an unwind), and the
  function call/return path unconditionally cleared the flag. Error state was
  stored but never surfaced to the user.
- **Impact:** Invalid runtime states silently accepted; callers continue after
  failure; diagnostics insufficient to identify failure origin; try/catch
  ineffective across function boundaries.
- **Fix:** `CallFunction`/`CallMethod` now preserve a pending `Exception` across
  the function boundary and only normalize `Return`/`Break`/`Continue`/`Normal`.
  Added public `Interpreter.CurrentFlow`, `CurrentException`,
  `CurrentExceptionMessage`, `ExceptionLocation` getters so the host can observe
  and surface errors.
- **Dependencies:** None.
- **Regression test:** `host/CollectionTests/InterpreterTests.cs` — 4 tests:
  exception propagates across a function call, TRY/CATCH still consumes an
  exception, no-MAIN surfaces an exception, normal RETURN still normalizes flow.
- **Result:** 261/261 CollectionTests pass (257 pre-existing + 4 new).

---

## Prioritized backlog (not yet implemented)

### R2 — Allocation failure silently ignored / null-deref risk — CRITICAL — CONFIRMED — FIXED (commit e80d676)

- **Area:** Memory and allocation reliability.
- **Evidence:** `engine/Core/Heap/EngineHeap.cs` `Allocate`:
  `if (obj == null) return null!;` — allocation failure returned a null heap
  object wrapped in a `Value`; callers did not check. `OsbMemoryProvider.Alloc`
  (`OsbMemoryProvider.cs:77`) returns `IntPtr.Zero` on OOM; callers unchecked.
  `Value` accessors cast a possibly-null `_heapRef` with `null!`, so OOM could
  yield `NullReferenceException` or silent wrong results.
- **Root cause:** Allocation-failure semantics were not explicit or consistent;
  `null!` suppressed the nullable warning and disguised failure.
- **Impact:** Out-of-memory produced a crash (NRE) or silent incorrect behavior;
  no clean error propagation; violated "do not return default values that disguise
  failure."
- **Fix:** `EngineHeap.Allocate<T>` now throws `OutOfMemoryException` when the
  factory returns null instead of returning `null!`. No invalid heap reference
  can enter a `Value`; failure information (type + message) is preserved and
  surfaces at the host boundary through the existing `catch (Exception)`. OOM is
  a hard, non-recoverable condition — it is NOT recoverable through language
  TRY/CATCH (the architecture does not define recoverable OOM).
- **Allocation failure contract:**
  - Success: allocator returns a non-null heap object; `_allocCount` incremented.
  - Failure (allocator returns null): throws `OutOfMemoryException`; `_allocCount`
    NOT incremented (no partial state).
  - Invalid arguments: `OsbMemoryProvider.Alloc` returns `IntPtr.Zero` for
    `sizeBytes <= 0`; throws `ArgumentException` for non-power-of-two alignment.
  - Internal invariant violations: surface as exceptions, never silent nulls.
- **Dependencies:** R1 (error propagation in place; host boundary surfaces errors).
- **Regression test:** `host/CollectionTests/AllocationTests.cs` — 7 tests:
  successful allocation, null factory throws OOM, null factory does not increment
  alloc count, provider oversized alloc returns zero, provider invalid alignment
  throws, zero-size alloc returns zero, no invalid heap reference enters Value.
- **Limitation:** `EngineHeap` does not wire `OsbMemoryProvider.Alloc` into its
  allocation path (pre-existing dead `_bumpPtr` tracking); wiring them is a
  broader change out of R2 scope. Allocation failure is therefore exercised via
  fault-injected (null-returning) factory rather than real memory exhaustion.
- **Confidence:** CONFIRMED.

### R3 — StringMap.Remove breaks open-addressing probe chains — HIGH — CONFIRMED — FIXED

- **Area:** Object/handle integrity & collections.
- **Evidence:** `engine/Core/Collections/StringMap.cs` `Remove` set
  `_occupied[index] = false`; `FindIndex` returned -1 at the first unoccupied
  slot, so any key inserted by probing through a deleted slot became unfindable.
  `_deletedCount`/`"\0"` handling was inconsistent. `Remove` is unused in the engine.
- **Root cause:** Classic open-addressing deletion bug; tombstone handling
  incomplete/inconsistent.
- **Impact:** If `Remove` is enabled, lookups silently fail (keys lost) — silent
  acceptance of invalid state, data corruption risk for locals/globals/function/
  class maps.
- **Implemented fix:** Correct tombstone semantics. A separate `_deleted[]` array
  tracks tombstones; `_occupied` stays true for tombstones so probe chains
  continue past them and stop only at truly empty slots. `FindIndex` skips
  tombstones and matches only live entries. `Insert` reuses the earliest tombstone
  or an empty slot, updates live entries in place, and decrements `_deletedCount`
  when reclaiming a tombstone. `Resize` rehashes only live entries and clears
  tombstone state. `Clear` resets `_deleted`. The public `Remove` API is preserved
  and now correct.
- **Dependencies:** None.
- **Regression test:** `host/CollectionTests/StringMapTests.cs` — insert 200 keys,
  delete every 3rd, verify all remaining keys findable with correct values after
  resize; remove-absent returns false; repeated remove idempotent; reinsert removed
  key; indexer throws KeyNotFoundException on removed key; enumeration skips
  tombstones (count matches Count).
- **Limitation:** With .NET string hash randomization, probe-chain collisions are
  probabilistic per process; the 200-key test makes chains near-certain but not
  guaranteed in every process. Tombstone semantics are verified by invariant
  (all live keys remain findable) regardless.
- **Confidence:** CONFIRMED.

### R4 — Ambiguous value accessors disguise failures — HIGH — CONFIRMED

- **Area:** Error propagation / value system.
- **Evidence:** `engine/Core/Values/Value.cs`: `AsNumber` returns `double.NaN`
  for non-numbers (line 74, NaN indistinguishable from valid NaN); `AsString`/
  `AsArray`/`AsObject`/… return `null!` for wrong kind (lines 75-82).
  `StandardLibrary.cs`: `NUMBER` returns `Value.Number(0)` for non-number args
  (line 29, silent default); `StringOp` returns `Value.NullValue` silently on
  wrong arg type (lines 107, 113).
- **Root cause:** Accessors return default/ambiguous values instead of routing
  through `ctx.Throw()`; callers cannot distinguish failure from a valid value.
- **Impact:** Invalid values silently propagate through expressions; the original
  error can be overwritten by a subsequent generic throw (e.g. `EvaluateBinary`
  throws "operator not supported" after a sub-expression already failed, losing
  the real location/message).
- **Recommended fix:** Make accessor failure explicit. Either add `TryAs*`
  bool-returning accessors or have kind-mismatch route through `ctx.Throw()` at
  the call site. Avoid returning `double.NaN`/0/`null` as failure indicators.
- **Dependencies:** R1.
- **Regression test:** Calling a string/array/number accessor on a mismatched kind
  raises a catchable runtime error (not NaN/0/null).
- **Confidence:** CONFIRMED.

### R5 — Test gap: interpreter/runtime not covered — HIGH — CONFIRMED

- **Area:** Test quality and coverage.
- **Evidence:** `host/CollectionTests` (257 tests) covers lexer/parser/collections
  only. `host/Host.Test/Oslang.ConformanceRunner` targets the REFERENCE
  implementation (`Osb.Lang`), not `Osb.Engine.Core`. No tests exist for the new
  engine's error propagation, OOM, invalid handles, try/catch across function
  boundaries, or allocation failure. Tests that exist validate happy paths; none
  verify state integrity after failures.
- **Impact:** Defects R1-R4 escape detection; "code executes without crashing" is
  mistaken for correct behavior.
- **Recommended fix:** Expand `host/CollectionTests` with interpreter-level tests
  (as started in Batch 1): valid/invalid input, boundary values, empty values,
  repeated ops, simulated allocation failure, invalid/stale handles, cleanup after
  failure, error propagation across layers. Do not weaken assertions.
- **Dependencies:** R1 provides observable error state needed for assertions.
- **Regression test:** The Batch 1 `InterpreterTests.cs` is the seed; extend per
  fix.
- **Confidence:** CONFIRMED.

### R6 — FrameStack.Current returns null! when empty — MEDIUM — CONFIRMED — FIXED

- **Area:** Runtime state / stack integrity.
- **Evidence:** `engine/Core/Collections/FrameStack.cs` `Current` returned
  `null!` when `_count == 0`. Callers may NRE with no context.
- **Root cause:** Non-nullable return without empty-guard contract.
- **Impact:** NullReferenceException on stack underflow; not a clean runtime error.
- **Implemented fix:** `Current` now throws `InvalidOperationException("Empty frame stack")`
  when empty, matching the existing `Pop` contract. Non-nullable return type and
  all existing callers are unchanged; callers only access `Current` when a frame is
  present during normal execution, so behavior is unchanged on valid paths and
  invalid access now fails fast with a clear diagnostic.
- **Dependencies:** None.
- **Regression test:** `host/CollectionTests/FrameStackTests.cs` — Current on empty
  throws; Current after Push returns top; Current after multiple Push; Current after
  Pop returns previous; repeated Push/Pop maintains Count; Pop on empty throws;
  Current after Pop-to-empty throws; Count tracks Push/Pop.
- **Confidence:** CONFIRMED (latent).

### R7 — ValuesEqual uses reference equality for Array/Object; Closure unhandled — MEDIUM — CONFIRMED

- **Area:** Object/handle integrity / equality semantics.
- **Evidence:** `engine/Core/Eval/ValueUtilities.cs:32-36`: `ValuesEqual` compares
  arrays/objects by reference (`a.AsArray == b.AsArray`, `a.AsObject ==
  b.AsObject`); `Closure` falls to the `_ => false` arm. Identity-based equality
  is intentional per architecture, but Closure is silently never-equal and there
  is no documentation pinning identity-vs-value semantics.
- **Impact:** Two semantically-identical arrays/objects compare false; Closure
  equality unspecified; risk of confusing identity with value equality.
- **Recommended fix:** Document the established identity-based equality model.
  Add explicit Closure handling (identity) so it is not silently the catch-all.
  Do not change to value equality without architectural decision.
- **Dependencies:** None.
- **Regression test:** Two distinct arrays with identical content compare false
  (identity); same reference compares true; Closure identity-based.
- **Confidence:** CONFIRMED.

### R8 — Inconsistent error model: C# exceptions bypass soft-throw — MEDIUM — CONFIRMED

- **Area:** Error propagation consistency.
- **Evidence:** Some paths throw real C# exceptions (`InvalidOperationException`
  in `StandardLibrary.Count` line 101, `StringMap.Add`/`this[]`, `FrameStack.Pop`)
  that bypass the `ControlFlow.Exception` soft-throw system entirely, while
  others use `ctx.Throw()`. Inconsistent propagation across execution boundaries.
- **Root cause:** Two error models coexist without a single convention.
- **Impact:** Callers cannot rely on one error-handling path; diagnostics
  inconsistent; some errors escape as unhandled C# exceptions.
- **Recommended fix:** Adopt one explicit model (the `ControlFlow.Exception`
  soft-throw, now propagated correctly via R1). Convert ad-hoc `throw` sites to
  `ctx.Throw()` where they represent runtime errors, or document that C# exceptions
  are programmer bugs (not runtime errors) and let them crash.
- **Dependencies:** R1.
- **Regression test:** `COUNT` on unsupported type raises catchable runtime error.
- **Confidence:** CONFIRMED.

### R9 — Top-level error not surfaced to user — MEDIUM — LIKELY

- **Area:** Error propagation / host boundary.
- **Evidence:** `OsbMain.cs` calls `interpreter.Execute(program)` but never checks
  `interpreter.CurrentFlow`; `Execute` returns `NullValue` on "No MAIN" and on
  swallowed exceptions; process exits 0 with no diagnostic. (Note: OsbMain.cs also
  carries the pre-existing O8.15 probe block which must be preserved untouched.)
- **Root cause:** Host entry point does not observe interpreter error state.
- **Impact:** Runtime failures exit silently with code 0; no diagnostic.
- **Recommended fix:** After `interpreter.Execute(program)`, if
  `CurrentFlow == Exception`, print `CurrentExceptionMessage` and `Exit(1)`.
- **Dependencies:** R1 (public getters now exist).
- **Regression test:** Host-level: run a program with no MAIN / division by zero,
  verify non-zero exit and error message printed.
- **Confidence:** LIKELY (code inspection; not yet tested at host level).
- **Note:** This is Batch 2 candidate; do not touch OsbMain.cs until the pre-existing
  probe changes can be cleanly separated.

---

## Batch 1 verification summary

- `osb.Engine.Core`: builds, 0 errors (4 pre-existing nullable warnings).
- `osb.Engine.Osb` (Host.Osb): builds, 0 errors.
- `host/CollectionTests`: 261 passed, 0 failed (257 pre-existing + 4 new R1 tests).
- Commit `a774f87` on `osb.oslang/master` contains only Batch 1 files.
- Pre-existing uncommitted changes `OsbMain.cs` + `OSLANG.EXE` (O8.15 probe)
  are preserved and untouched.

---

## Recommended next milestone

Batch 2: R2 (allocation-failure semantics) + R9 (host-level error surfacing).
Both depend on R1 (now complete) and are independently testable. Do not begin
Batch 2 until R2's allocation-failure semantics are explicitly decided (document
the gap if recovery is undefined per the architecture).
