# OSLANG spec questions

**Rule:** the spec is the truth (OSLANG-PLAN D5). Items below are genuine ambiguities or gaps found while reading the full spec set (`OSLANG-SPEC.md` and `OSLANG-0.1` … `0.73`). They are **answered by the owner, never guessed**. An item blocks the engine/O4 sub-task it touches until answered. Format: `SQ-<nn>` — one-line summary, spec location, the question, and why an implementation choice would be a guess.

---

### SQ-001 String indexing unit (0.51/0.61, OSLANG-SPEC §12–13)

Spec: strings are "Unicode text"; `COUNT()`/`LENGTH`, `SUBSTR(start, length)`, `INDEXOF(v)` exist with no indexing rule. Triple-quoted strings strip common indentation (0.61).

Question: is the unit UTF-8 bytes, Unicode code points, or UTF-16 code units? It affects `COUNT`, `SUBSTR`, `INDEXOF`, `LEFT`/`RIGHT`, `PADSTART`/`PADEND` and array/string interop.

Design impact: O3b (Values/Strings). ENGINE-DESIGN §4 proposes code points.

---

### SQ-002 Case-folding rule for case-insensitivity (0.1, OSLANG-SPEC §2)

Spec: "Case-insensitive everywhere: keywords, identifiers, enum members, property names"; no fold table.

Question: ASCII-only invariant-culture folding, or full Unicode (e.g. Turkish İ, ß, accented letters)? It affects identifier resolution (§4/§11 0.73), keyword recognition, enum member names, and property lookup.

Design impact: lexer + identifier resolution. ENGINE-DESIGN §4 proposes ASCII-only.

---

### SQ-003 EVENT / ON / RAISE semantics (0.3, OSLANG-SPEC §6)

Spec: `EVENT Name`, `ON Name … END ON`, `RAISE Name`; one table row in §6, one paragraph in 0.3, never revisited. 0.73 §16 lists events as "never revisited; no version states whether they are required for 0.73".

Question: are events required in the 0.73 engine? If yes: what is the exact semantics (handler list per event, `RAISE` synchronous, handler reentrancy, `END ON` scope)?

Design impact: O3d (OOP/events). Both reference and kernel port treat events as dead code.

---

### SQ-004 SWITCH expression form (0.3/0.61, OSLANG-SPEC §6)

Spec: "`SWITCH` is also available as an expression: `CASE1 => "Monday"` (0.3)". No grammar, no typing rule, no exhaustiveness rule, no value-when-no-match rule.

Question: exact syntax and semantics of switch-as-expression: are `=>` arms expressions? What does a switch expression evaluate to when no case matches and there is no `DEFAULT`?

Design impact: O3e (switch). Engine currently targets statement form only.

---

### SQ-005 FOR IN / FOR EACH collection domain (0.62, OSLANG-SPEC §6)

Spec: "`FOR Item IN Collection … END`, over any collection".

Question: which runtime types count as "collections"? Arrays at least; strings? Objects? Does `IN` require `IEnumerable`-shaped OSLANG types that do not exist in the language?

Design impact: O3e (FOR IN/EACH).

---

### SQ-006 Error message text and codes (0.1, OSLANG-SPEC §14)

Spec: four categories (lexical, syntax, semantic, runtime); `TRY … CATCH ERR … END` where `ERR` carries "the message". No message texts, no numeric error codes, no source-location format are pinned.

Question: may the engine choose its own message texts and codes? Conformance tests must then avoid asserting exact text unless the spec pins it somewhere it does not.

Design impact: O3e (exceptions); test corpus `.out` files must not depend on message text.

---

### SQ-007 DATE.FORMAT / TIME.FORMAT pattern tokens beyond the required set (0.5, OSLANG-SPEC §11.5)

Spec requires `YYYY/MM/DD` and `HH/mm/ss` tokens; examples use those only.

Question: are other tokens (`YY`, `M`, `D`, `H`, `AM/PM`, week/day names) supported, rejected, or passed through? `MONTHNAME()`/`DAYNAME()` say "the runtime locale determines the result" (0.5 §8.6/§8.7) but OSB has no locale concept yet.

Design impact: O4 (DATE/TIME over `DATETIME`/`TICKS` syscalls). SQ-018 covers the locale question.

---

### SQ-008 TYPEOF exact strings (0.5/0.51/0.61, OSLANG-SPEC §3)

Spec: `TYPEOF` returns "the runtime type name"; for a class instance the class name, for an enum the enum name. Exact strings for closures, key events, `SIZE`/`CURSOR` (0.7), enum sets, and module values not pinned.

Question: exact `TYPEOF` output per value kind, including case ("DATE" vs "date") and enum-set result.

Design impact: O3c/O3e.

---

### SQ-009 Array mutation drift: SORT/REVERSE/PUSH/POP (OSLANG-SPEC §12, "Mutation" note)

Spec §12 says `SORT()` does not mutate and returns a new array; the "Mutation" paragraph says mutating methods change the array in place and notes host/kernel disagreement.

Question: which mutation behavior is normative for 0.73 — `SORT()`/`REVERSE()` non-mutating (return new array), everything else in place? What does `arr.PUSH(x)` as a statement return?

Design impact: O3c (arrays) and O4 `.out` expectations.

---

### SQ-010 EOF behavior of file streams (0.5 §24, 0.62)

Spec: streams expose `READLINE()`, `READ()`, `EOF()`. No statement of what `READLINE()` returns at EOF, whether `READ()` blocks or errors at EOF, or whether `EOF()` becomes true after a failed read.

Question: `READLINE()` at EOF → `NULL`? empty string? runtime error catchable by `CATCH`?

Design impact: O4 (`OSL.FILE` over K3); K3 itself must return real codes for read past end.

---

### SQ-011 Path case-sensitivity (OSB-ARCHITECTURE §9; OSLANG-SPEC silent)

Spec: file operations take paths; OSB mounts ext4 with case-insensitive DOS-style resolution while preserving on-disk names (§9).

Question: does `FILE.READ("A:\OSB\BIN\X.OSL")` match `x.osl` on disk? Does the engine normalize paths before handing them to the host, or does the host/kernel do it?

Design impact: O4 (`OSL.FILE`/`DIR`); shell `.OSL` dispatch (D4).

---

### SQ-012 EXIT(code) range and meaning (0.7, OSLANG-SPEC §11.8)

Spec: `OSL.APP.EXIT(Code)` "ends the program". No valid range, no mapping to process exit code (D4 says script exit code = process exit code).

Question: any restrictions on `Code` (0–255? any 32-bit)? Negative?

Design impact: O6 (`OSLANG.EXE` exit-code propagation).

---

### SQ-013 Number precision and formatting (0.1/0.4, OSLANG-SPEC §3/§12)

Spec: "One numeric type (floating point where the host needs it; integer syntax allowed)".

Question: IEEE-754 double? Formatting digits after the decimal point (kernel port truncates at 12 fractional digits — a defect, not a spec)? Rounding mode for `ROUND`?

Design impact: O3b (own double math/formatting).

---

### SQ-014 KEYCODE numeric values and the serial mapping (0.73 §3, OSB-ARCHITECTURE §12.4/K2)

Spec: "The exact numeric values are implementation-defined unless explicitly specified by the runtime. Programs must use the enum members rather than relying on numeric values." K2 requires decoding PS/2 scancodes and serial `ESC[`/`ESC O` sequences into KEYCODE values.

Question: does the runtime fix numeric values for KEYCODE members (needed for a deterministic serial↔KEYCODE table), or is the mapping entirely internal to the terminal implementation?

Design impact: K2 terminal API + O5 (`OSL.CONSOLE` key events); the engine must not depend on numeric values.

---

### SQ-015 USING module resolution (0.3/0.6, OSLANG-SPEC §10)

Spec: `USING ModuleName` imports a module or source file; "must appear before any declaration, circular imports are rejected, unknown modules are an error"; modules cached by canonical path. No file extension, no path-relative-to-what rule, no `.OSL` vs library-module distinction.

Question: is `USING OSL.CONSOLE` a builtin namespace while `USING foo` loads `foo.osl`? Relative to the importing file's directory or the process base directory? File extension?

Design impact: O3e (multi-file/modules); O4 host libraries live under reserved `OSL` namespace.

---

### SQ-016 OSB.NET runtime error identity (0.62, OSLANG-SPEC §11.9; plan item 4)

Spec lists `OSB.NET.PING`/`DOWN` as a host extension; the plan requires the surface to exist and fail with a defined, catchable OSLANG runtime error "not supported on this host".

Question: which error code/message? Does `PING` fail the same way as `DOWN`? Is it raised at call time or only inside `TRY`?

Design impact: O4 (`OSB.NET` stub); error taxonomy (§6).

---

### SQ-017 INPUT with no data available (0.1)

Spec: `INPUT` reads a line, always returns STRING. No rule for EOF / closed input / non-interactive input.

Question: `INPUT` at end of input → error catchable by `CATCH`, empty string, or block forever?

Design impact: O3b (`INPUT`) and Host.Test vs Host.Osb divergence (host may block, serial may not).

---

### SQ-018 `FOR` loop variable visibility after the loop and `STEP` semantics (0.1)

Spec: `FOR i = a TO b [STEP s]`, bounds inclusive. No rule for whether `i` survives after `END`, or for negative `STEP` direction (does `FOR i = 5 TO 1 STEP -1` run?).

Design impact: O3b (control flow).

---

### SQ-019 `ARGS` vs the 128-byte GETARGS cap (D4 open item; OSB-ARCHITECTURE §12.3)

Spec: `ARGS` is the read-only command-line argument array (0.41/0.7). OSB caps `GETARGS` at 128 bytes (§10).

Question: if arguments exceed 128 bytes total, does `OSLANG` truncate, error, or split? The plan marks "[OPEN] raise it?" — owner decision needed before O6.

Design impact: O6 (argument passing).

---

### SQ-020 `CLEAR` semantics outside OSL.CONSOLE (0.1)

Spec §6: `CLEAR` "clears the screen". `OSL.CONSOLE.CLEAR()` also exists (0.7). Are they identical? Does bare `CLEAR` require the terminal API (K2) or is it VGA-only?

Design impact: O3b (`CLEAR`) vs O5 (`OSL.CONSOLE`).

---

### How answers are recorded

The owner answers in this file (or a reply that I copy here). Until answered, the corresponding code is left unimplemented and marked `SQ-<nn>`; no implementation choice is shipped.
