# OSLANG Owner Decisions — Technical Review

**Review Date:** 2026-11-19  
**Sources Reviewed:** `OWNER-DECISIONS.md`, `SPEC-QUESTIONS.md`, `ENGINE-DESIGN.md`, `O1a-GAP-ANALYSIS.md`, `OSLANG-PLAN.md`, `OSLANG-SPEC.md` (unified), `OSLANG-0.73-SPEC.md`, `OSB-ARCHITECTURE.md`  
**Status:** All items remain **PENDING OWNER APPROVAL** — no decisions implemented.

---

## Executive Summary

| Category | Count | Items |
|---|---|---|
| **READY FOR OWNER APPROVAL** | 5 | SQ-005, SQ-011, SQ-012, SQ-014 |
| **OWNER CLARIFICATION REQUIRED** | 10 | SQ-001, SQ-002, SQ-003, SQ-004, SQ-007, SQ-008, SQ-009, SQ-013, SQ-015, SQ-020 |
| **ARCHITECTURAL VERIFICATION REQUIRED** | 3 | SQ-006, SQ-016, SQ-019 |
| **DEFERRED** | 2 | SQ-010, SQ-017, SQ-018 |

---

## Detailed Review by SQ

---

### SQ-001: String indexing unit (UTF-8 bytes / code points / UTF-16 code units)

**Spec text (OSLANG-SPEC §3, §13):** "Unicode text" — no indexing rule specified. `COUNT()`/`LENGTH`, `SUBSTR(start, length)`, `INDEXOF(v)`, `LEFT`/`RIGHT`, `PADSTART`/`PADEND` exist.

**ENGINE-DESIGN §4:** "Proposal: code points; decision needed before O3b." Strings are "immutable, length-prefixed UTF-8 in the engine heap. Length stored in bytes."

**OWNER-DECISIONS recommendation:** "Unicode code points" — rationale: "code points align with `SUBSTR`/`INDEXOF` semantics"

**⚠️ Technical Issues:**
1. **Code points ≠ grapheme clusters** — The original recommendation conflated Unicode code points (scalar values) with grapheme clusters (user-perceived characters). `SUBSTR`/`INDEXOF` on code points will split combining sequences (e.g., "e\u0301" = 2 code points, 1 grapheme). The spec says "Unicode text" but does not specify grapheme awareness.
2. **ENGINE-DESIGN stores strings as UTF-8 bytes** — Length is stored in bytes. If indexing unit = code points, every `SUBSTR`/`INDEXOF`/`COUNT` requires UTF-8 decoding to count code points. This is a significant runtime cost not acknowledged.
3. **Host.Test (BCL) uses UTF-16** — `Host.Osb` uses UTF-8. If engine core uses code-point indexing, both host layers must transcode consistently. This is feasible but must be designed.

**What spec explicitly establishes:** Nothing — "Unicode text" with no indexing rule.

**What remains unspecified:** Whether indexing is by bytes, code points, or grapheme clusters.

**Architectural dependency:** String storage format (ENGINE-DESIGN: UTF-8 bytes) vs. indexing unit. If code points, every string operation pays decode cost.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner must choose:
- **Bytes** (simplest, matches UTF-8 storage, `COUNT` = byte length)
- **Code points** (specifies `COUNT` = code point count, requires decode on every operation)
- **Grapheme clusters** (most user-friendly, requires grapheme segmentation algorithm)

**Correction applied:** Removed "grapheme-aware operations" from rationale — code points are NOT grapheme-aware.

---

### SQ-002: Case-folding rule (ASCII-only / full Unicode)

**Spec text (OSLANG-SPEC §2):** "Case-insensitive everywhere: keywords, identifiers, enum members, property names" — no fold table specified.

**ENGINE-DESIGN §4:** "Proposal: ASCII-only (identifiers, keywords, enum members, property names); owner to confirm."

**OWNER-DECISIONS recommendation:** "ASCII-only (A-Z → a-z via `c + 0x20` for `'A'`–`'Z'` only)" — rationale: "OS is DOS-like; identifiers/keywords/enums are ASCII in practice. Full Unicode (Turkish İ, ß) adds ICU-scale tables and locale dependency. Spec says 'case-insensitive everywhere' but no algorithm. Algorithm: for each character `c`, if `'A'` ≤ `c` ≤ `'Z'` then `c + 0x20` else `c` unchanged. Non-ASCII preserved unchanged."

**⚠️ Technical Issues:**
1. **Original `c | 0x20` was imprecise** — Bitwise OR with 0x20 affects non-letter ASCII characters (e.g., `[` → `{`, `@` → `` ` ``). Correct algorithm applies only to ASCII letters A-Z.
2. **SQ-018 cross-reference was wrong** — SQ-018 is about FOR loop variable visibility and negative STEP, not locale. The locale question for date formatting is a separate unspecified issue.
3. **"Invariant culture" is a .NET concept** — In freestanding MiniCoreLib, there is no `CultureInfo`. The recommendation now specifies the exact folding algorithm without .NET terminology.

**What spec explicitly establishes:** Case-insensitivity required; no algorithm specified.

**What remains unspecified:** Exact folding algorithm; whether non-ASCII letters participate.

**Architectural dependency:** Lexer + identifier resolution (ENGINE-DESIGN: O3a). No kernel dependency.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner must confirm exact algorithm: ASCII A-Z → a-z via `c + 0x20` for `'A'`–`'Z'` only; all other code points unchanged.

---

### SQ-003: EVENT/ON/RAISE required for 0.73?

**Spec text (OSLANG-SPEC §6, §16):** Specified once in 0.3, never revisited. §16 item 8: "Events — specified once in 0.3 and never revisited; no version states whether they are required for 0.73."

**OSLANG-0.73-SPEC:** Does not mention events at all.

**ENGINE-DESIGN:** No mention of events in design.

**OSLANG-GAP.md:** "Events, generics and VIRTUAL/OVERRIDE are missing... Events, generics and VIRTUAL/OVERRIDE are missing: Runtime/Events/ and Runtime/Generics/ are unreferenced directories... kernel: EVENT at top level explicitly fails with 'Events are not supported in this OSLANG subset'."

**OWNER-DECISIONS recommendation:** "Defer to post-0.73 (owner scope decision)" — rationale: "Spec 0.3 introduced them; 0.73 §16 says 'never revisited; no version states whether they are required for 0.73'. Both reference and kernel port treat as dead code. Implementing adds complexity without conformance value. **Spec does not explicitly establish exclusion from 0.73.**"

**✅ Analysis:** Recommendation is fully aligned with spec (§16 item 8) and both implementations. No contradiction. The deferral is an owner scope decision, not a spec mandate.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner decides whether to include as optional extension or defer entirely.

---

### SQ-004: SWITCH expression syntax/semantics

**Spec text (OSLANG-SPEC §6, OSLANG-0.3-SPEC):** "`SWITCH` is also available as an expression: `CASE1 => "Monday"` (0.3)." No grammar, typing rule, exhaustiveness, or no-match value specified.

**OSLANG-SPEC §12 (primitive methods):** No mention of switch expressions.

**OWNER-DECISIONS recommendation:** "Adopt `CASE val => expr` arms; no-match → runtime error" — rationale: "No `DEFAULT` required; expression form must yield a value. Treat missing match as runtime error catchable by `CATCH`."

**⚠️ Technical Issues:**
1. **Syntax** — Spec example shows `CASE1 => "Monday"` (no colon, `=>` directly after case value). This differs from statement form `CASE 1 ...`. Need to confirm if `CASE 1 => expr` or `CASE 1: => expr`.
2. **Typing** — Spec doesn't specify if all arms must have same type. Recommendation doesn't address this — this is a separate owner decision.
3. **Exhaustiveness** — Spec doesn't require `DEFAULT`. Recommendation says "no `DEFAULT` required" but then "no-match → runtime error". This is consistent.
4. **Runtime error category** — Which of the four categories (lexical, syntax, semantic, runtime)? Should be runtime.

**What spec explicitly establishes:** Switch expressions exist; syntax uses `=>`; no `DEFAULT` shown in example.

**What remains unspecified:** Exact grammar, typing rules, exhaustiveness, no-match behavior.

**Architectural dependency:** Parser + evaluator (O3e). No kernel dependency.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner must confirm:
- Exact syntax: `CASE val => expr` vs `CASE val: => expr`
- Typing rule: common type required across all arms? any?

---

### SQ-005: FOR IN / FOR EACH collection domain

**Spec text (OSLANG-SPEC §6):** "`FOR Item IN Collection … END`, over any collection."

**OSLANG-SPEC §3 (Types):** Only `ARRAY` is a collection type. `STRING`, `OBJECT`, `ENUM`, `DATE`, `TIME`, key event, `SIZE`, `CURSOR` are not collections.

**OSLANG-GAP.md:** "FOR IN / FOR EACH statements | 0.62 | ❌ | ❌ | neither has it"

**OWNER-DECISIONS recommendation:** "Arrays only for 0.73; strings as `ARRAY<STRING>` (chars) later" — rationale: "Only arrays exist as collections in 0.73. Strings are not collections yet. `IEnumerable`-shaped types don't exist in OSLANG."

**✅ Analysis:** Correct. Spec says "any collection" but only `ARRAY` is a collection type in the type system. Strings are primitive `STRING` type, not `ARRAY<STRING>`. The recommendation to support arrays only for 0.73 is spec-compliant.

**Recommendation status:** **READY FOR OWNER APPROVAL**

---

### SQ-006: Error message text and codes

**Spec text (OSLANG-SPEC §14):** "Four categories are distinguished from 0.1: lexical, syntax, semantic and runtime. `TRY … CATCH ERR … END` where `ERR` carries 'the message'. No message texts, no numeric error codes, no source-location format are pinned."

**ENGINE-DESIGN §6:** "Runtime errors carry source locations where the lexer/parser tracked them; IO/JSON/date errors all surface through `CATCH`... `ERR` holds the message string; error categories: lexical, syntax, semantic, runtime (0.1)." Also: "numeric OSLANG error code + message + source location".

**OWNER-DECISIONS recommendation:** "Engine chooses; conformance tests assert only category + source location" — rationale: "Spec pins four categories but no message texts or numeric codes. Conformance `.out` files must not depend on exact wording."

**⚠️ Technical Issues:**
1. **Source location format** — ENGINE-DESIGN says "source locations where the lexer/parser tracked them" but format unspecified. Conformance tests need a stable format (e.g., `file:line:col` or `file:line`).
2. **Error codes vs categories** — Spec distinguishes categories but doesn't require numeric codes. ENGINE-DESIGN mentions "numeric OSLANG error code + message + source location". The recommendation says "engine chooses" but ENGINE-DESIGN already proposes numeric codes. This is a contradiction.
3. **ERR binding** — Spec says `ERR` carries "the message" (string). ENGINE-DESIGN says error slot has "numeric OSLANG error code + message + source location". What does `ERR` bind to — the full object or just the message string?

**What spec explicitly establishes:** Four categories; `ERR` is a string message; no pinned texts/codes/locations.

**What remains unspecified:** Exact message texts, numeric codes, source location format, what `ERR` binds to (string vs object).

**Architectural dependency:** Exception model (ENGINE-DESIGN §6), conformance corpus (O2).

**Recommendation status:** **ARCHITECTURAL VERIFICATION REQUIRED** — Owner must decide after architectural alignment:
- Does `ERR` bind to a string (spec) or an error object with code+message+location (ENGINE-DESIGN)?
- If object, what is the source location format for conformance tests?
- Are numeric error codes part of the language surface (exposed to scripts) or internal only?

---

### SQ-007: DATE.FORMAT/TIME.FORMAT extra tokens

**Spec text (OSLANG-SPEC §11.5):** Requires `YYYY/MM/DD` and `HH/mm/ss` tokens; examples use those only. `MONTHNAME()`/`DAYNAME()` say "the runtime locale determines the result" but OSB has no locale concept.

**SPEC-QUESTIONS.md:** "SQ-018 covers the locale question" — **This is incorrect.** SQ-018 is about FOR loop variable visibility, not locale.

**OWNER-DECISIONS recommendation:** "Reject unknown tokens with defined error" — rationale: "Spec requires `YYYY/MM/DD` and `HH/mm/ss` only. `MONTHNAME`/`DAYNAME` mention 'runtime locale' but OSB has no locale. **SPEC-QUESTIONS SQ-018 cross-ref is wrong** — SQ-018 is FOR loop, not locale."

**⚠️ Technical Issues:**
1. **Wrong SQ cross-reference** — SQ-018 is not about locale. The locale question for date formatting is a separate unspecified issue.
2. **Spec doesn't say "reject unknown tokens"** — It only specifies required tokens. Three interpretations exist: reject, ignore/pass-through, or implement common extensions. Recommendation picks "reject" but this is not spec-mandated.
3. **MONTHNAME/DAYNAME locale dependency** — These functions explicitly mention "runtime locale determines the result" but no locale system exists. This is a separate spec gap.

**What spec explicitly establishes:** Required tokens (`YYYY/MM/DD`, `HH/mm/ss`). `MONTHNAME`/`DAYNAME` mention locale.

**What remains unspecified:** Handling of unknown tokens; locale system for `MONTHNAME`/`DAYNAME`; whether other tokens (YY, M, D, H, AM/PM) are supported.

**Architectural dependency:** O4 (DATE/TIME over DATETIME/TICKS syscalls). No kernel dependency beyond syscalls.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner must decide:
- Unknown tokens: reject / ignore / implement common subset?
- Locale for `MONTHNAME`/`DAYNAME`: fixed English / configurable / stub returning key?
- Fix cross-reference: locale question is NOT SQ-018.

---

### SQ-008: TYPEOF exact strings

**Spec text (OSLANG-SPEC §3):** "`TYPEOF(value)` returns the runtime type name; for a class instance it returns the class name, for an enum the enum name (0.51, 0.61)."

**OSLANG-SPEC §12 (primitive methods):** `TOSTRING()` exists on all types.

**OWNER-DECISIONS recommendation:** "Uppercase for primitives (`NUMBER`, `STRING`, `BOOLEAN`, `ARRAY`, `NULL`), class name as declared, enum name as declared, `KEYEVENT` for key events, `SIZE`/`CURSOR` for those structs, `ENUMSET` for enum sets"

**⚠️ Technical Issues:**
1. **Key event type** — Spec §3 lists "Key event" as a type (0.7). OSLANG-0.73-SPEC §4 says keyboard event has `KEY` (enum `KEYCODE`), `CHAR`, `CTRL`, `ALT`, `SHIFT`. What does `TYPEOF(keyEvent)` return? Spec doesn't say. Recommendation proposes `KEYEVENT` — **this is a proposed name, not a normative requirement.**
2. **`SIZE`/`CURSOR`** — Spec §3 lists these as types returned by `OSL.CONSOLE.SIZE()`/`GETCURSOR()`. Are they distinct types or `ARRAY`/`OBJECT`? Spec doesn't clarify. Recommendation proposes distinct names — **proposed, not normative.**
3. **Enum sets** — Spec §9: "Enum sets are immutable and offer `CONTAINS()`, `COUNT()`, `FOREACH()`." `TYPEOF(enumSet)` not specified. Recommendation proposes `ENUMSET` — **proposed, not normative.**
4. **Closures/modules** — Spec doesn't specify `TYPEOF` for arrow functions (closures) or module values.

**What spec explicitly establishes:** Primitives: type name; class instance: class name; enum: enum name.

**What remains unspecified:** Key event, `SIZE`, `CURSOR`, enum sets, closures, module values, case sensitivity (uppercase vs as-declared).

**Architectural dependency:** O3c/O3e. No kernel dependency.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner must confirm:
- Key event type name: proposed `KEYEVENT` (not `KEYCODE`)
- `SIZE`/`CURSOR`: distinct type names or `ARRAY`/`OBJECT`?
- Case: uppercase for primitives, as-declared for user types
- Closures/module values

---

### SQ-009: Array mutation: SORT/REVERSE/PUSH/POP

**Spec text (OSLANG-SPEC §12):** "`SORT()` does not mutate and returns a new array." Mutation paragraph: "The mutating methods — `ADD`, `PUSH`, `POP`, `REMOVE`, `CLEAR`, `REVERSE`, `SORT` — change the array they are called on... The host additionally leaves `SORT()` non-mutating and returns a new array; the kernel port mutates in place. One of the two should change."

**OSLANG-SPEC §12 (ARRAY methods):** Lists `REVERSE()` and `SORT()` without mutation annotation. Lists `PUSH`, `POP`, `ADD`, `REMOVE`, `CLEAR` as mutating methods.

**OSLANG-GAP.md:** "ARRAY.SORT() throws a raw InvalidOperationException for non-number/string elements, which TRY/CATCH cannot catch." "SORT() does not mutate and returns a new array; the 'Mutation' paragraph says mutating methods change in place... In-place for everything is the more useful reading of 'ADD/PUSH/POP exist at all', so that is what the kernel does."

**OWNER-DECISIONS recommendation:** "`SORT` non-mutating (returns new array); `REVERSE` — owner decision required; `PUSH`/`POP`/`ADD`/`REMOVE`/`CLEAR` mutate in place" — rationale: "Spec §12 says `SORT` does not mutate; 'Mutation' paragraph lists `REVERSE` as mutating ('mutating methods... change the array they are called on'). `PUSH` as statement must mutate. Host returns new array from `SORT`; kernel mutates. **SORT contradiction documented; REVERSE not explicitly specified as non-mutating.**"

**⚠️ Technical Issues:**
1. **`SORT` contradiction** — Spec §12 says `SORT` non-mutating; mutation paragraph lists `SORT` as mutating. This is the acknowledged drift.
2. **`REVERSE` is explicitly listed as mutating** — Spec's Mutation paragraph lists `REVERSE` among "mutating methods" that "change the array they are called on". The original recommendation made `REVERSE` non-mutating, but spec doesn't explicitly say that. This is an inference.
3. **`REVERSE` as statement** — If `REVERSE` is non-mutating and returns new array, `arr.REVERSE()` as a statement discards the result (useless). The mutation paragraph says "`arr.PUSH(x)` works as a statement" implying mutating methods are useful as statements. If `REVERSE` is non-mutating, it's useless as a statement.

**What spec explicitly establishes:** `SORT()` non-mutating (returns new array) per first paragraph; `PUSH`/`POP`/`ADD`/`REMOVE`/`CLEAR` are mutating (implied by "works as a statement"). Mutation paragraph contradicts by listing `SORT` and `REVERSE` as mutating.

**What remains unspecified:** `REVERSE` mutation behavior; what `arr.PUSH(x)` as statement returns.

**Architectural dependency:** O3c (arrays). No kernel dependency.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner must decide on `REVERSE`:
- Option A: `REVERSE` mutating (consistent with mutation paragraph listing it as mutating)
- Option B: `REVERSE` non-mutating (consistent with functional style, but useless as statement)
- Option C: `REVERSE` mutating, `SORT` non-mutating (split, as original recommendation proposed but spec doesn't explicitly support)

---

### SQ-010: EOF behavior of file streams

**Spec text (OSLANG-SPEC §11.2):** Streams expose `READLINE()`, `READ()`, `EOF()`. No statement of what `READLINE()` returns at EOF, whether `READ()` blocks or errors at EOF, or whether `EOF()` becomes true after a failed read.

**O1a-GAP-ANALYSIS.md K3 design:** Proposes `READLINE` syscall returning bytes read (0=EOF), `EOF` syscall returning 1/0.

**OWNER-DECISIONS recommendation:** "`READLINE()` at EOF → `NULL`; `READ()` at EOF → 0 bytes; `EOF()` true after failed read" — rationale: "`NULL` is distinct from empty string; `READ` returning 0 matches POSIX-like semantics without being Unix. `EOF()` becomes true after a read that returns 0/`NULL`."

**⚠️ Technical Issues:**
1. **Spec is completely silent** — This is a pure implementation decision. The recommendation is reasonable but not spec-mandated.
2. **`READ()` blocking vs error** — Spec doesn't say. Recommendation says "0 bytes" (non-blocking, EOF indication). But kernel read syscall could block. Host.Test (BCL) `Stream.Read` blocks until data or EOF. Host.Osb kernel read may behave differently.
3. **`EOF()` timing** — "True after failed read" means `EOF()` is sticky after a read returns EOF. This is reasonable but not specified.

**What spec explicitly establishes:** Nothing — completely silent.

**What remains unspecified:** All three behaviors.

**Architectural dependency:** O4 (`OSL.FILE` over K3); K3 syscall design (O1a-GAP-ANALYSIS).

**Recommendation status:** **DEFERRED** — Spec is silent; any reasonable behavior works. Can be decided during O4 implementation based on kernel syscall behavior. Not a blocker for 0.73 conformance.

---

### SQ-011: Path case-sensitivity

**Spec text:** OSLANG-SPEC silent. OSB-ARCHITECTURE §9: "Case-insensitive path resolution in the DOS layer while preserving on-disk names."

**OSLANG-PLAN D4:** "The script needs read permission only (the interpreter opens it as the caller; the kernel enforces)."

**O1a-GAP-ANALYSIS.md:** "Kernel normalizes to case-insensitive DOS resolution; engine passes paths through unmodified."

**OWNER-DECISIONS recommendation:** "Kernel normalizes to case-insensitive DOS resolution; engine passes paths through unmodified" — rationale: "OSB-ARCHITECTURE §9: 'case-insensitive path resolution in the DOS layer while preserving on-disk names'. Engine must not normalize; kernel resolves `A:\OSB\BIN\X.OSL` → `x.osl`."

**✅ Analysis:** Fully consistent across OSB-ARCHITECTURE, OSLANG-PLAN, O1a-GAP-ANALYSIS. Engine passes raw paths; kernel (ext4 driver) does case-insensitive resolution.

**Recommendation status:** **READY FOR OWNER APPROVAL**

---

### SQ-012: EXIT(code) range and meaning

**Spec text (OSLANG-SPEC §11.8):** `OSL.APP.EXIT(Code)` "ends the program". No valid range, no mapping to process exit code.

**OSLANG-PLAN D4:** "Script exit code = process exit code."

**OSB-ARCHITECTURE §5.1 / ABI.md:** Kernel `EXIT` (4Ch) takes `AL` (byte, 0-255).

**OWNER-DECISIONS recommendation:** "0–255 (byte), truncated modulo 256" — rationale: "Kernel `EXIT` (4Ch) takes `AL` (byte). Negative values wrap. Document truncation."

**✅ Analysis:** Correct. Kernel syscall takes byte in AL. Truncation modulo 256 is the only feasible mapping. Negative values wrap (two's complement).

**Recommendation status:** **READY FOR OWNER APPROVAL**

---

### SQ-013: Number precision and formatting

**Spec text (OSLANG-SPEC §3):** "One numeric type (floating point where the host needs it; integer syntax allowed)."

**OSLANG-SPEC §11.4 (MATH):** `ROUND` `ROUND(V, Digits)` `TRUNC` `TRUNC(V, Digits)` — no rounding mode specified.

**ENGINE-DESIGN §10:** "NUMBER = double; formatting/parsing hand-written (fixed-precision fractional output, exponent for large magnitudes — the kernel port's 12-fraction-digit truncation is a known defect, OSLANG-GAP §5.10, not a spec mandate)."

**OSLANG-GAP §5.10:** "Number formatting truncates to 12 fractional digits with trailing zeros stripped — so `0.1 + 0.2` prints `0.3`, but `1 / 3` prints `0.333333333333` and large magnitudes lose precision."

**OWNER-DECISIONS recommendation:** "IEEE-754 double; fixed-precision fractional output (e.g. 12 digits), exponent for large magnitudes; `ROUND` = banker's rounding (ties to even)" — rationale: "IEEE-754 double is the only portable choice. Banker's rounding is standard for financial."

**⚠️ Technical Issues:**
1. **Three separate concerns conflated:**
   - **Representation:** IEEE-754 double (spec says "floating point" — only portable choice ✓)
   - **Output formatting:** "fixed-precision fractional output (e.g. 12 digits)" — spec doesn't specify digit count. Kernel port uses 12 (defect per GAP). Recommendation says "e.g. 12 digits" but doesn't mandate.
   - **`ROUND` semantics:** Banker's rounding (ties to even) — spec doesn't specify. Common in financial, but not universal. `ROUND` in many BASICs uses round-half-up.
2. **`ROUND(V, Digits)`** — Spec allows optional digits parameter. Rounding mode applies to both forms.
3. **Formatting of `TOSTRING()` / `PRINT`** — Not specified. Recommendation says "fixed-precision fractional output" but doesn't define the algorithm (e.g., `0.1 + 0.2` → `"0.3"` or `"0.300000000000"`?).

**What spec explicitly establishes:** Single numeric type = floating point; `ROUND` exists with optional digits.

**What remains unspecified:** Exact representation (but IEEE-754 double is only practical choice), output formatting algorithm, `ROUND` rounding mode.

**Architectural dependency:** O3b (own double math/formatting). No kernel dependency.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner must decide:
- Output formatting: exact algorithm (e.g., "shortest round-trip decimal representation" like Java/JS, or fixed N digits?)
- `ROUND` mode: banker's (ties to even) or round-half-up (traditional BASIC)?
- `ROUND(V, Digits)` with negative digits? (spec doesn't say)

---

### SQ-014: KEYCODE numeric values and serial mapping

**Spec text (OSLANG-0.73-SPEC §3):** "The exact numeric values are implementation-defined unless explicitly specified by the runtime. Programs must use the enum members rather than relying on numeric values."

**OSB-ARCHITECTURE §12.4 / K2:** "Terminal API for `OSL.CONSOLE`/`OSL.APP`... raw key events... from PS/2 **and** serial (decode `ESC[`/`ESC O` sequences)."

**O1a-GAP-ANALYSIS.md K2:** "KEYCODE numeric values must be fixed by the runtime for deterministic serial mapping. PS/2 set-1 scancodes + serial `ESC[`/`ESC O` sequences → `KEYCODE` table required in kernel."

**OWNER-DECISIONS recommendation:** "Runtime fixes numeric values; deterministic PS/2 + serial ESC[/ESC O → KEYCODE table" — rationale: "Spec: 'exact numeric values are implementation-defined unless explicitly specified by the runtime'. Kernel K2 must decode scancodes/serial sequences; engine must not depend on numeric values (use enum members). Fixing values enables deterministic serial mapping."

**✅ Analysis:** Correct interpretation. Spec says values are implementation-defined "unless explicitly specified by the runtime" — meaning the runtime CAN fix them. K2 requires a deterministic mapping table. Fixing values in the runtime satisfies both.

**Recommendation status:** **READY FOR OWNER APPROVAL**

---

### SQ-015: USING module resolution

**Spec text (OSLANG-SPEC §10):** "`USING ModuleName` imports a library module or another source file; must appear before any declaration, circular imports are rejected, unknown modules are an error; modules cached by canonical path. No file extension, no path-relative-to-what rule, no `.OSL` vs library-module distinction."

**OSLANG-SPEC §10 (layering):** "Library modules live under the reserved `OSL` namespace (0.6); host infrastructure under `OSB`. Users cannot define names inside those namespaces."

**OWNER-DECISIONS recommendation:** "`USING OSL.*` = builtin namespace; `USING foo` = `foo.osl` relative to importing file's directory" — rationale: "Spec: 'modules cached by canonical path'. No extension rule given. Relative to importer is standard (like `#include`). `OSL` namespace reserved (§10)."

**⚠️ Technical Issues:**
1. **Canonical path** — "Relative to importing file's directory" for `foo.osl` is standard. But what about `USING OSL.CONSOLE`? Is that a built-in namespace that doesn't correspond to a file? Spec says "library modules live under reserved `OSL` namespace" — these are built-in, not file-based.
2. **File extension** — Spec doesn't specify. Recommendation assumes `.osl`. Could be `.osl` or no extension.
3. **Process base directory vs importer directory** — For `USING foo` in a multi-file program, relative to importer is correct. For `USING foo` in MAIN, relative to process base directory (script directory). **This distinction is unresolved.**

**What spec explicitly establishes:** `USING` imports modules; `OSL.*` reserved for built-ins; cached by canonical path; no extension/relative rules.

**What remains unspecified:** File extension; relative-to-importer vs relative-to-base; how built-in `OSL.*` modules are resolved.

**Architectural dependency:** O3e (multi-file/modules); O4 host libraries.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner must confirm:
- File extension: `.osl` mandatory? optional?
- `USING OSL.CONSOLE` → built-in (no file)
- `USING foo` → `foo.osl` relative to importing file's directory (or base directory for MAIN)

---

### SQ-016: OSB.NET runtime error identity

**Spec text (OSLANG-SPEC §11.9):** Lists `OSB.NET.PING`/`DOWN` as host extension.

**OSLANG-PLAN item 4:** "Network features are out of scope (`OSB.NET`, HTTP). They must exist in the language surface and fail with a defined, catchable OSLANG runtime error ('not supported on this host')."

**ENGINE-DESIGN §6:** "Network (`OSB.NET`/HTTP) exists on the language surface and raises a defined, catchable OSLANG runtime error 'not supported on this host' (plan item 4, SQ-017 assigns its code/message)."

**OWNER-DECISIONS recommendation:** "Single error code/message: 'Network not supported on this host' at call time, catchable by `CATCH`" — rationale: "Spec lists `OSB.NET.PING`/`DOWN` as host extension; plan requires surface exists and fails with defined error. Same error for both; raised at call time (not only in `TRY`)."

**⚠️ Technical Issues:**
1. **SQ-017 cross-reference wrong** — ENGINE-DESIGN says "SQ-017 assigns its code/message" but SQ-017 is about `INPUT` EOF, not OSB.NET.
2. **Error code vs message** — Spec/plan says "catchable OSLANG runtime error" — does this mean a numeric error code + message, or just a message string? ENGINE-DESIGN §6 proposes numeric error codes. If numeric codes, what code?
3. **Call time vs inside TRY** — Recommendation says "raised at call time (not only in `TRY`)" — meaning the error is raised immediately when `OSB.NET.PING` is called, and can be caught by a surrounding `TRY`. This is correct.

**What spec explicitly establishes:** `OSB.NET.PING`/`DOWN` exist as host extensions.

**What remains unspecified:** Exact error identity (code/message), whether same for both, raised at call time.

**Architectural dependency:** O4 (`OSB.NET` stub); error taxonomy (SQ-006).

**Recommendation status:** **ARCHITECTURAL VERIFICATION REQUIRED** — Depends on SQ-006 resolution (error code system). Owner should decide after SQ-006.

---

### SQ-017: INPUT with no data available

**Spec text (OSLANG-SPEC §6):** "`INPUT` reads a line, always returns STRING. No rule for EOF / closed input / non-interactive input."

**ENGINE-DESIGN §8 (IHostApi):** "input line `ReadLine()`" — no EOF behavior specified.

**OWNER-DECISIONS recommendation:** "`INPUT` at EOF → runtime error catchable by `CATCH`" — rationale: "Host may block on `Console.ReadLine`; serial may not. Error is safest; empty string is ambiguous; blocking forever breaks scripts."

**⚠️ Technical Issues:**
1. **Spec is silent** — Any behavior (error, empty string, block) is an implementation choice.
2. **Host.Test vs Host.Osb divergence** — Host.Test (BCL `Console.ReadLine`) blocks until newline or EOF (Ctrl+Z/Ctrl+D). Host.Osb (serial/PS/2) may not block the same way. The recommendation picks error, but this means Host.Test must simulate EOF error instead of returning empty string or blocking.
3. **`READLINE()` on streams (SQ-010)** — Returns `NULL` at EOF per SQ-010 recommendation. `INPUT` is a language built-in, not a stream method. Should they be consistent?

**What spec explicitly establishes:** `INPUT` reads a line, returns STRING. No EOF rule.

**What remains unspecified:** EOF/closed input/non-interactive behavior.

**Architectural dependency:** O3b (`INPUT` built-in); Host.Test vs Host.Osb implementation.

**Recommendation status:** **DEFERRED** — Spec silent; can be decided during O3b implementation. Not a blocker for 0.73 conformance (no conformance test can assert exact EOF behavior without spec).

---

### SQ-018: FOR loop variable visibility / negative STEP

**Spec text (OSLANG-SPEC §6):** "`FOR i = a TO b [STEP s]`, bounds inclusive. No rule for whether `i` survives after `END`, or for negative `STEP` direction (does `FOR i = 5 TO 1 STEP -1` run?)."

**OWNER-DECISIONS recommendation:** "Loop variable survives after `END` with last value; `STEP` negative allowed (5 TO 1 STEP -1 runs)" — rationale: "Spec: bounds inclusive, no rule for survival or negative step. Surviving matches BASIC tradition. Negative step is useful and unambiguous with inclusive bounds."

**⚠️ Technical Issues:**
1. **Variable survival** — In BASIC, FOR loop variable typically retains last value after loop. But in structured languages, loop variable is often scoped to the loop. Spec doesn't say.
2. **Negative STEP** — With inclusive bounds, `FOR i = 5 TO 1 STEP -1` runs 5,4,3,2,1. `FOR i = 1 TO 5 STEP -1` would run zero iterations (start > end with negative step). This is unambiguous.
3. **Parameter/local shadowing** — If `i` is a parameter, does the FOR loop create a new local or use the parameter? Spec doesn't say. 0.73 assignment rule says assignment follows resolution order, but FOR loop variable declaration is not an assignment.

**What spec explicitly establishes:** `FOR i = a TO b [STEP s]`, bounds inclusive.

**What remains unspecified:** Variable survival after `END`; negative STEP behavior; interaction with parameters/locals of same name.

**Architectural dependency:** O3b (control flow).

**Recommendation status:** **DEFERRED** — Spec silent; reasonable defaults can be chosen during O3b. Not a blocker for 0.73 conformance.

---

### SQ-019: ARGS vs 128-byte GETARGS cap

**Spec text (OSLANG-SPEC §4):** "`ARGS` is the read-only command-line argument array (0.41/0.7)."

**OSB-ARCHITECTURE §12.3 / ABI.md:** `GETARGS` (23h) cap 128 bytes (129 with NUL). `SpawnReq.argsLen` max 128 bytes.

**OSLANG-PLAN D4:** "Script arguments share the 128-byte `GETARGS` limit. [OPEN] raise it?"

**O1a-GAP-ANALYSIS.md:** No specific mention of GETARGS truncation detection.

**OWNER-DECISIONS recommendation:** "Truncate; document cap; raise cap to 1 KiB in ABI v8; warning behavior undecided" — rationale: "Spec: `ARGS` is read-only argument array. Kernel `GETARGS` (23h) cap 128 bytes. **Kernel already returns full length enabling truncation detection** (ABI.md: 'returns the FULL value length (a short buffer truncates the copy, not the reported length)'). Engine can detect truncation."

**⚠️ Technical Issues:**
1. **Can kernel detect truncation?** Current `GETARGS` (23h) takes `buf` + `cap`. If `cap` ≤ 129, kernel copies up to `cap-1` bytes + NUL, returns full length. If actual args > 128, kernel returns truncated data but **reports the full length** (per ABI.md: "returns the FULL value length (a short buffer truncates the copy, not the reported length)"). So the caller CAN detect truncation by comparing returned length vs buffer capacity.
2. **OSLANG engine gets args via `OSL.APP.ARGS` / global `ARGS`** — These are populated from `GETARGS` at startup. If engine detects truncation (reported length > received buffer), it can warn.
3. **ABI v8 cap raise** — Raising `GETARGS` cap requires kernel ABI change (new version). `SpawnReq.argsLen` also capped at 128. Both must be raised together.
4. **Warning mechanism** — Where does warning go? `PRINT` to stderr? `OSL.CONSOLE`? Not designed yet. **Do not introduce stderr or OSL.CONSOLE dependencies without architectural evidence.**

**What spec explicitly establishes:** `ARGS` is read-only argument array.

**What remains unspecified:** Behavior when arguments exceed kernel cap (128 bytes); warning mechanism.

**Architectural dependency:** O6 (argument passing); kernel ABI (GETARGS, SpawnReq); OSLANG engine startup.

**Recommendation status:** **ARCHITECTURAL VERIFICATION REQUIRED** — Kernel already returns full length allowing truncation detection. Owner should confirm warning mechanism (pending diagnostic channel design) and ABI v8 timeline.

---

### SQ-020: CLEAR semantics outside OSL.CONSOLE

**Spec text (OSLANG-SPEC §6):** "`CLEAR` clears the screen."

**OSLANG-SPEC §11.1 (OSL.CONSOLE):** `CLEAR()` exists in OSL.CONSOLE.

**OSB-ARCHITECTURE / K2:** Terminal API required for `OSL.CONSOLE`.

**ENGINE-DESIGN §8 (IHostApi):** "console: `Write(text)`, `Clear()`, input line `ReadLine()`, raw key events... size/cursor/color (K2)"

**O1a-GAP-ANALYSIS.md K2:** Proposes `CONS_CLEAR` (7Dh) syscall with AL=0 full, AL=1 line, AL=2 area.

**OWNER-DECISIONS recommendation:** "Bare `CLEAR` = `OSL.CONSOLE.CLEAR()`; both require terminal API (K2)" — rationale: "Spec §6: `CLEAR` 'clears the screen'. 0.7 adds `OSL.CONSOLE.CLEAR()`. They should be identical. Kernel VGA-only clear is insufficient for terminal apps; K2 provides the abstraction."

**⚠️ Technical Issues:**
1. **Kernel VGA clear (6Fh) exists** — Current kernel has `CLEAR` (6Fh) that clears VGA + serial ESC[2J H. This works for basic VGA console.
2. **OSL.CONSOLE.CLEAR()** — Part of terminal API (K2) which supports alternate screen buffer, cursor positioning, etc.
3. **Are they identical?** For basic VGA console, yes. For alternate screen buffer (OSL.CONSOLE.ENTER()/ALTERNATE()), `CLEAR` should clear the alternate buffer. Bare `CLEAR` in a non-terminal-app script might not have entered alternate mode.
4. **Engine IHostApi.Clear()** — ENGINE-DESIGN has `Clear()` in IHostApi. This is the abstraction point. Host.Test uses `Console.Clear()`. Host.Osb uses kernel clear syscall (6Fh or future K2).
5. **K2 not yet implemented** — If K2 not yet implemented (O1b), what does bare `CLEAR` do in O3b?

**What spec explicitly establishes:** `CLEAR` clears screen; `OSL.CONSOLE.CLEAR()` exists.

**What remains unspecified:** Whether bare `CLEAR` and `OSL.CONSOLE.CLEAR()` are identical in all contexts (alternate buffer, frame buffering); behavior before K2 implementation.

**Architectural dependency:** O3b (`CLEAR` built-in); O5 (`OSL.CONSOLE`); K2 terminal API.

**Recommendation status:** **OWNER CLARIFICATION REQUIRED** — Owner must decide:
- Bare `CLEAR` always maps to `OSL.CONSOLE.CLEAR()` (requires K2 even for simple scripts)?
- Or bare `CLEAR` uses basic VGA clear (6Fh), `OSL.CONSOLE.CLEAR()` uses K2?
- If K2 not yet implemented (O1b), what does bare `CLEAR` do in O3b?

---

## Contradictions Between Documents

| Document A | Document B | Contradiction |
|---|---|---|
| ENGINE-DESIGN §6: "numeric OSLANG error code + message + source location" | OWNER-DECISIONS SQ-006: "Engine chooses; conformance tests assert only category + source location" | ENGINE-DESIGN proposes numeric error codes; OWNER-DECISIONS says engine chooses (implies codes optional) |
| ENGINE-DESIGN §6: "SQ-017 assigns its code/message" (for OSB.NET) | SQ-017 is about INPUT EOF, not OSB.NET | Cross-reference error |
| SPEC-QUESTIONS SQ-007: "SQ-018 covers the locale question" | SQ-018 is FOR loop variable/STEP, not locale | Cross-reference error |
| O1a-GAP-ANALYSIS SQ-011: "Kernel normalizes paths" | OSLANG-PLAN D4: "interpreter opens it as the caller; kernel enforces" | Consistent — kernel does resolution |
| OSLANG-SPEC §12: "SORT() does not mutate" | OSLANG-SPEC §12 Mutation para: "SORT... mutating methods change in place" | Spec self-contradiction (acknowledged drift) |
| OWNER-DECISIONS (original) SQ-009: "REVERSE non-mutating" | Spec Mutation para lists REVERSE as mutating | Original recommendation contradicted spec's mutation paragraph |

---

## Summary

### Items by Final Status

| Status | Items |
|---|---|
| **READY FOR OWNER APPROVAL** | SQ-005, SQ-011, SQ-012, SQ-014 |
| **OWNER CLARIFICATION REQUIRED** | SQ-001, SQ-002, SQ-003, SQ-004, SQ-007, SQ-008, SQ-009, SQ-013, SQ-015, SQ-020 |
| **ARCHITECTURAL VERIFICATION REQUIRED** | SQ-006, SQ-016, SQ-019 |
| **DEFERRED** | SQ-010, SQ-017, SQ-018 |

---

## Files Updated

1. **`OWNER-DECISIONS.md`** — Corrected decision table with:
   - Fixed rationales (SQ-001: removed grapheme claim; SQ-002: exact ASCII algorithm; SQ-003: owner scope decision; SQ-004: syntax vs typing; SQ-008: proposed names flagged; SQ-009: SORT/REVERSE separated; SQ-015: extension/MAIN resolution; SQ-019: warning undecided; SQ-020: K2 dependency)
   - Status column updated to four normalized categories
   - All items remain PENDING

2. **`OWNER-DECISIONS-REVIEW.md`** — This document with corrected review date and consistent classifications

---

*End of review. No implementation changes made. All decisions remain PENDING OWNER APPROVAL.*