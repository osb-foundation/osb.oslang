# OSB / OSLANG — Runtime Reliability Audit Backlog

**Repo/branch:** `osb.oslang` / `master`  
**Commit:** `f21419a` (HEAD; Batches 1-7 applied)  
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

"Fixes implemented" below reflect Batches 1-6. Remaining items are the
recommended backlog for subsequent sessions.

---

## Batch 3 — implemented

### R3 — StringMap.Remove breaks open-addressing probe chains — HIGH — CONFIRMED — FIXED
### R6 — FrameStack.Current returns null! when empty — MEDIUM — CONFIRMED — FIXED

See the detailed entries in the prioritized backlog below.

---

## Batch 4 — implemented (commit b792aa1)

### R4 — Ambiguous value accessors disguise failures — HIGH — CONFIRMED — FIXED

See the detailed entry in the prioritized backlog below.

## Batch 5 — implemented (commit e7f3d73)

### R7 — Closure identity-based equality — MEDIUM — CONFIRMED — FIXED

- **Area:** Object/handle integrity / equality semantics.
- **Evidence:** `engine/Core/Eval/ValueUtilities.cs`: `ValuesEqual` compared
  arrays/objects by reference (`a.AsArray == b.AsArray`,
  `a.AsObject == b.AsObject`); `Closure` fell to the `_ => false` catch-all,
  leaving closure equality unspecified.
- **Fix:** Added an explicit `ValueKind.Closure =>
  a.AsClosure == b.AsClosure` arm to `ValuesEqual`. `HeapClosure` is a
  `sealed class : HeapObject` with no `==`/`Equals` overload, so `==` is
  reference identity — matching the established Array/Object semantics.
- **Regression test:** `host/CollectionTests/ClosureEqualityTests.cs` — 18 tests:
  closure compared with itself true; distinct closures with the same body /
  equivalent captures / different captures false; closure vs non-kind false;
  array/object/primitive identity unchanged; `==` does not execute the closure
  body (end-to-end through the interpreter).
- **Result:** 772/772 CollectionTests pass (754 pre-existing + 18 new R7 tests).
- **Dependencies:** None.
- **Confidence:** CONFIRMED.

---

## Batch 6 — implemented

### R5 — Test gap: interpreter/runtime not covered — HIGH — CONFIRMED — FIXED

- **Area:** Test quality and coverage of `osb.Engine.Core` runtime behavior.
- **Evidence:** `host/CollectionTests` covered lexer/parser/collections only. No tests existed for the engine's function/method invocation, expression evaluation, flow control, object/array mutation, closure reference creation, or exception propagation across execution boundaries.
- **Fix:** Added `host/CollectionTests/RuntimeTests.cs` — 46 focused test methods (96 assertions) covering: function invocation with args/return values, nested function calls, expressions (binary/unary/string concat), if/elif/else, for/while/do-while loops, break/continue/return, object field round-trip, constructors, method invocation, member/method-not-found errors, array mutation methods (push/pop/first/last/count/contains/removeat/insertat), empty collections, boundary conditions, undefined variable, unknown function, arg-count mismatch, division by zero, try/catch consumption and propagation, catch-body throw, frame cleanup after exception, and closure reference creation.
- **Defect found and fixed:** `ExpressionEvaluator.EvaluateNew` did not check `_ctx.CurrentFlow` after evaluating constructor arguments, and `Interpreter.CallMethod` unconditionally reset flow to `Normal` at entry. `NEW Foo(1 / 0)` therefore swallowed the division-by-zero exception and returned an object with `Normal` flow. Fixed by checking flow after each argument evaluation in `EvaluateNew` (engine/Core/Eval/ExpressionEvaluator.cs), matching the existing `EvaluateCall`/`EvaluateMethodCall` pattern. No architecture, value, closure, or allocation changes.
- **Regression test:** `Runtime.NewWithArgThatThrowsPreservesException`.
- **Result:** 868/868 CollectionTests pass (772 pre-existing + 96 new R5 tests).
- **Dependencies:** R1 (soft-throw exception flow preserved across calls).
- **Confidence:** CONFIRMED.

---

## Batch 7 — implemented

### R8 — Inconsistent error model: C# exceptions bypass soft-throw — MEDIUM — CONFIRMED — FIXED

- **Area:** Error propagation consistency.
- **Evidence:** Two paths threw real C# exceptions that bypassed the `ControlFlow.Exception` soft-throw system:
  1. `StandardLibrary.Count` (line 107) threw `InvalidOperationException` for unsupported value kinds (Number, Boolean, Object, Enum, EnumSet, Closure, KeyEvent, NativeFunction).
  2. `ValueUtilities.ToDisplayString` (line 47) threw `InvalidOperationException` when printing arrays via PRINT/SHOW.
  Other cited sites (`StringMap.Add`/`this[]`, `FrameStack.Pop`, `ExpressionEvaluator.Evaluate` unknown type) are internal invariants / programmer bugs and correctly remain as C# exceptions.
- **Root cause:** Two error models coexisted without a single convention for user-facing runtime errors.
- **Impact:** User code errors (invalid `COUNT` argument, printing arrays) escaped as unhandled C# exceptions instead of catchable runtime errors; `TRY/CATCH` could not consume them.
- **Fix:**
  - `StandardLibrary.Count`: Added `ExecutionContext` and `SourceLocation` parameters; replaced `throw` with `ctx.Throw($"COUNT not supported for {value.Kind}", location)`. Call site in `TryCall` updated to pass context.
  - `ValueUtilities.ToDisplayString`: Changed Array case from throwing to returning `"ARRAY[{length}]"`, consistent with `Value.ToString()` representation. No C# exception; printing arrays now works.
- **Dependencies:** R1 (soft-throw propagation).
- **Regression tests:** `host/CollectionTests/RuntimeTests.cs`:
  - `Runtime.CountOnUnsupportedTypeThrowsCatchableError` — verifies `COUNT(5)` produces `ControlFlow.Exception` with message "COUNT not supported for Number".
  - `Runtime.PrintArrayDoesNotThrowCSharpException` — verifies `PRINT [1,2]` does not crash with `InvalidOperationException`; flow remains `Normal`.
- **Result:** 871/871 CollectionTests pass (868 pre-existing + 3 new R8 assertions).
- **Confidence:** CONFIRMED.

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

### R4 — Ambiguous value accessors disguise failures — HIGH — CONFIRMED — FIXED (commit b792aa1)

- **Area:** Error propagation / value system.
- **Evidence:** `engine/Core/Values/Value.cs`: `AsNumber` returned `double.NaN`
  for non-numbers; `AsString`/`AsArray`/`AsObject`/… returned `null!` for wrong
  kind. `StandardLibrary.cs`: `NUMBER` returned `Value.Number(0)` for non-number
  args; `StringOp` returned `Value.NullValue` silently on wrong arg type.
  `ExpressionEvaluator.EvaluateBinary` and statement executors (Return/Break/
  Continue) overwrote a pending `Exception` flow with a subsequent result or a
  generic error, destroying the original error message.
- **Root cause:** Accessors returned default/ambiguous values instead of making
  failure explicit; statement executors clobbered the soft-throw `Exception`
  flow established by R1.
- **Impact:** Invalid values silently propagated through expressions; the
  original error could be overwritten by a subsequent generic throw, losing the
  real location/message; `NUMBER`/`StringOp` silently substituted defaults.
- **Implemented fix:**
  - `Value`: added `TryAsX` accessors (bool + out) for every typed accessor
    (`TryAsNumber`, `TryAsBoolean`, `TryAsString`, `TryAsArray`, `TryAsObject`,
    `TryAsEnum`, `TryAsEnumSet`, `TryAsClosure`, `TryAsKeyEvent`,
    `TryAsNativeFunction`). No `ExecutionContext` dependency; failure is
    explicit via the return flag, never disguised as NaN/0/null!/NullValue.
    `AsX` properties remain as unchecked convenience getters used only after
    kind validation in engine call sites (all such call sites guard kind first,
    so the NaN/null! paths are dead in practice).
  - `StandardLibrary.NUMBER`: ctx.Throw "NUMBER expects a single numeric
    argument" on wrong arg count or non-numeric arg, instead of returning 0.
  - `StandardLibrary.StringOp`: ctx.Throw "Expected a string argument" /
    "Expected a string and a number argument" on wrong arg type, instead of
    returning NullValue; `location` plumbed through for context.
  - `ExpressionEvaluator.EvaluateBinary`/`EvaluateCall`/`EvaluateMethodCall`:
    preserve a pending `Exception` flow after sub-expression evaluation
    (return NullValue immediately), so the original error is not overwritten by
    a generic "operator not supported" error.
  - `StatementExecutor` Return/Break/Continue: only set flow when currently
    Normal, so a pending Exception from an evaluated expression is not
    clobbered by a subsequent Return/Break/Continue.
- **Dependencies:** R1 (soft-throw exception flow preserved across calls).
- **Regression test:** `host/CollectionTests/ValueAccessorTests.cs` — 29 tests:
  TryAsX correct and mismatched access (number, string, array, object, boolean);
  NUMBER valid / invalid / no-args via interpreter (flow + message preserved);
  LEFT valid / invalid arg types via interpreter; `(1/0)+"abc"` preserves
  "Division by zero." instead of a generic operator error; function-call error
  ("Division by zero.") remains observable across the call boundary (R1);
  valid coercions (`1+2`, `"a"+"b"`) continue to work.
- **Limitation:** `AsX` properties still return NaN/null! on mismatch but are
  only reachable via unguarded calls; no engine call site does this. Wiring
  every existing guarded call site to `TryAsX` is a broader cleanup out of R4
  scope.
- **Confidence:** CONFIRMED.

### R5 — Test gap: interpreter/runtime not covered — HIGH — CONFIRMED — FIXED (Batch 6)

- **Area:** Test quality and coverage of `osb.Engine.Core` runtime behavior.
- **Evidence:** `host/CollectionTests` covered lexer/parser/collections only. No tests existed for the engine's function/method invocation, expression evaluation, flow control, object/array mutation, closure reference creation, or exception propagation across execution boundaries.
- **Impact:** Defects R1-R4 escape detection; "code executes without crashing" is mistaken for correct behavior.
- **Fix:** Added `host/CollectionTests/RuntimeTests.cs` (46 test methods, 96 assertions) covering function invocation, nested calls, expressions, if/elif/else, loops, break/continue/return, object fields, constructors, method invocation, array mutation methods, empty collections, boundary conditions, undefined variable, unknown function, arg-count mismatch, division by zero, try/catch consumption and propagation, catch-body throw, frame cleanup after exception, and closure reference creation.
- **Defect found and fixed:** `ExpressionEvaluator.EvaluateNew` did not check `_ctx.CurrentFlow` after evaluating constructor arguments, and `Interpreter.CallMethod` unconditionally reset flow to `Normal` at entry. `NEW Foo(1 / 0)` swallowed the division-by-zero exception and returned an object with `Normal` flow. Fixed by checking flow after each argument evaluation in `EvaluateNew` (engine/Core/Eval/ExpressionEvaluator.cs), matching the existing `EvaluateCall`/`EvaluateMethodCall` pattern. No architecture, value, closure, or allocation changes.
- **Regression test:** `Runtime.NewWithArgThatThrowsPreservesException`.
- **Result:** 868/868 CollectionTests pass (772 pre-existing + 96 new R5 tests).
- **Dependencies:** R1 provides observable error state needed for assertions.
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

### R7 — ValuesEqual uses reference equality for Array/Object; Closure unhandled — MEDIUM — CONFIRMED — FIXED (commit e7f3d73)

- **Area:** Object/handle integrity / equality semantics.
- **Evidence:** `engine/Core/Eval/ValueUtilities.cs`: `ValuesEqual` compares
  arrays/objects by reference (`a.AsArray == b.AsArray`, `a.AsObject ==
  b.AsObject`); `Closure` previously fell to the `_ => false` catch-all, leaving
  closure equality unspecified. Now an explicit `ValueKind.Closure =>
  a.AsClosure == b.AsClosure` arm compares closure references by identity
  (`HeapClosure` is a `sealed class : HeapObject` with no `==`/`Equals`
  overload).
- **Impact (resolved):** Closure equality is no longer unspecified; distinct
  closure instances compare false even with identical bodies or equivalent
  captures, and a closure compares equal to itself.
- **Fix:** `engine/Core/Eval/ValueUtilities.cs` — added the explicit Closure
  arm to `ValuesEqual`. Array, object, primitive, null, and invalid-value
  behavior are unchanged.
- **Regression test:** `host/CollectionTests/ClosureEqualityTests.cs` — 18 tests:
  closure vs itself true; distinct closures same body / equivalent captures /
  different captures false; closure vs non-kind false; array/object/primitive
  identity unchanged; `==` does not execute the closure body (end-to-end via
  interpreter).
- **Result:** 772/772 CollectionTests pass (754 pre-existing + 18 new).
- **Dependencies:** None.
- **Confidence:** CONFIRMED.

### R8 — Inconsistent error model: C# exceptions bypass soft-throw — MEDIUM — CONFIRMED — FIXED (Batch 7)

- **Area:** Error propagation consistency.
- **Evidence:** Two paths threw real C# exceptions that bypassed the `ControlFlow.Exception` soft-throw system:
  1. `StandardLibrary.Count` (line 107) threw `InvalidOperationException` for unsupported value kinds (Number, Boolean, Object, Enum, EnumSet, Closure, KeyEvent, NativeFunction).
  2. `ValueUtilities.ToDisplayString` (line 47) threw `InvalidOperationException` when printing arrays via PRINT/SHOW.
  Other cited sites (`StringMap.Add`/`this[]`, `FrameStack.Pop`, `ExpressionEvaluator.Evaluate` unknown type) are internal invariants / programmer bugs and correctly remain as C# exceptions.
- **Root cause:** Two error models coexisted without a single convention for user-facing runtime errors.
- **Impact:** User code errors (invalid `COUNT` argument, printing arrays) escaped as unhandled C# exceptions instead of catchable runtime errors; `TRY/CATCH` could not consume them.
- **Fix:**
  - `StandardLibrary.Count`: Added `ExecutionContext` and `SourceLocation` parameters; replaced `throw` with `ctx.Throw($"COUNT not supported for {value.Kind}", location)`. Call site in `TryCall` updated to pass context.
  - `ValueUtilities.ToDisplayString`: Changed Array case from throwing to returning `"ARRAY[{length}]"`, consistent with `Value.ToString()` representation. No C# exception; printing arrays now works.
- **Dependencies:** R1 (soft-throw propagation).
- **Regression tests:** `host/CollectionTests/RuntimeTests.cs`:
  - `Runtime.CountOnUnsupportedTypeThrowsCatchableError` — verifies `COUNT(5)` produces `ControlFlow.Exception` with message "COUNT not supported for Number".
  - `Runtime.PrintArrayDoesNotThrowCSharpException` — verifies `PRINT [1,2]` does not crash with `InvalidOperationException`; flow remains `Normal`.
- **Result:** 871/871 CollectionTests pass (868 pre-existing + 3 new R8 assertions).
- **Confidence:** CONFIRMED.

### R9 — Top-level error not surfaced to user — MEDIUM — LIKELY — FIXED

- **Area:** Error propagation / host boundary.
- **Evidence:** `OsbMain.cs` calls `interpreter.Execute(program)` but never checks
  `interpreter.CurrentFlow`; `Execute` returns `NullValue` on "No MAIN" and on
  swallowed exceptions; process exits 0 with no diagnostic. (Note: OsbMain.cs also
  carries the pre-existing O8.15 probe block which must be preserved untouched.)
- **Root cause:** Host entry point does not observe interpreter error state.
- **Impact:** Runtime failures exit silently with code 0; no diagnostic.
- **Fix:** After `interpreter.Execute(program)` in `OsbMain.cs`, added a check for
  `interpreter.CurrentFlow == ControlFlow.Exception`. If true, prints
  `CurrentExceptionMessage` and exits with code 1. This mirrors the existing
  C# exception catch block but handles the soft-throw `ControlFlow.Exception` path.
- **Dependencies:** R1 (public getters `CurrentFlow`, `CurrentExceptionMessage` exist).
- **Regression test:** `host/CollectionTests/HostErrorSurfacingTests.cs` — 5 tests:
  - `HostSurfacesRuntimeErrorAfterExecute` — verifies division by zero produces `ControlFlow.Exception` with message after `Execute()`.
  - `HostSurfacesNoMainError` — verifies missing MAIN produces `ControlFlow.Exception` with "No MAIN function found".
  - `HostSurfacesDivisionByZeroError` — verifies division by zero error message.
  - `HostSurfacesUnknownFunctionError` — verifies unknown function error.
  - `HostNormalExecutionFlowIsNormal` — verifies successful execution leaves flow as `Normal`.
- **Result:** 881/881 CollectionTests pass (871 pre-existing + 10 new R9 tests).
- **Confidence:** CONFIRMED.
- **Note:** The pre-existing O8.15 probe typo (`L6666666666666ength` → `Length`) was corrected to allow compilation; all O8.15 probe markers and logic are preserved.

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

Batch 8 (R9) complete. R1–R9 all FIXED. The O8.15 probe changes in OsbMain.cs are preserved (with a typo fix to allow compilation). No further reliability audit items remain in this backlog.
