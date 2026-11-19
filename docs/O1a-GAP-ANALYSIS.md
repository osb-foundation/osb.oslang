# O1a Gap Analysis — Kernel Prerequisites for OSLANG

**Phase:** O1a (design only) · **Target:** OSLANG 0.73 spec · **Status:** Design proposals, not implemented.

This document audits the kernel against the prerequisites in OSLANG-PLAN §2 and OSLANG-SPEC §12.4, and produces the gap table for K1 (growable memory), K2 (terminal API), and K3 (file/dir I/O). Every proposed syscall has its ABI.md entry first.

---

## 1. O1 Prerequisites Audit (OSLANG-PLAN §2)

| Prerequisite | Current State | Pass/Fail | Evidence |
|---|---|---|---|
| File syscalls 26h–29h, 61h, 62h rewritten as path-based primitives with real error codes | **FAIL** — These syscalls currently wrap `FileCommands`/`TrashCommands`, print to VGA ("OK"), and return 0 on success / no error codes on failure. | ❌ | `osb.kernel/Cpu/Syscalls.cs:436-553` (25h mkdir, 26h rmdir, 27h delete, 28h rename, 29h copy, 61h ln, 62h trash) |
| `ABI.md` version == `Syscall.AbiVersion` == kernel `AbiVersion` | **PASS** — All three are v7. | ✅ | `osbkt/docs/ABI.md` v7; `osb.kernel/Cpu/Syscalls.cs:21` `AbiVersion = 7` |
| Automated check detects ABI version inconsistencies | **FAIL** — No CI script or build-time check exists. | ❌ | No such check in `build.sh` or `osbkt`/`osb.shell` build |
| No file touched exceeds 400 lines | **FAIL** — Multiple files exceed 400 lines in kernel, osb.shell, osbkt, osb.oslang/reference. | ❌ | Kernel: `Syscalls.cs` (1098), `Scheduler.cs` (573), `Ext4.cs` (1058), `FileCommands.cs` (968), `Users.cs` (621), `Font.cs` (1116). osb.shell: `FileCommands.cs` (429). osbkt: `Syscall.cs` (580). Reference: `I18nNamespace.cs` (532), `PrimitiveMethodDispatcher.cs` (642), `Lexer.cs` (557). |
| READMEs accurately describe existing functionality | **PARTIAL** — `osb.kernel/README.md` still describes legacy shell commands and says "ext4 writes are missing" (actually implemented). `osb.oslang/README.md` minimal. | ⚠️ | `osb.kernel/README.md:113` "missing writing"; `ROADMAP.md:15` "Faltam: o lado de escrita" but slices 4-5 show write commands implemented |

**Conclusion:** O1 prerequisites **NOT MET**. Do not start O1b implementation until:
1. Legacy file syscalls (25h–29h, 61h, 62h) are redesigned as proper path-based primitives (K3).
2. Automated ABI version consistency check is added.
3. Files exceeding 400 lines are split (required by AGENTS.md in all repos).
4. READMEs are corrected to reflect actual implemented state.

---

## 2. K1 — Growable Memory (ABI Design)

### Current State
- Each process has a fixed 2 MiB physical block at `0x2000000–0x2200000` (image, data, heap, stack).
- `MEMINFO` (10h) reports totals only.
- No syscall to grow memory.

### Requirements from OSLANG-SPEC §12.4 / OSB-ARCHITECTURE §8
- Grant memory in 2 MiB steps up to a per-process limit.
- Accounted and released when process is reaped.
- `EFAULT`-safe validation of the new range.
- Per-process limits and physical frame accounting.

### Proposed Syscall: `MEMGROW` (AH = 11h — next available)

| Field | Value |
|---|---|
| **Number** | 11h |
| **Name** | MEMGROW |
| **Inputs** | RDI = requested additional frames (each 4 KiB; must be multiple of 512 for 2 MiB steps), or 0 to query current limit |
| **Outputs** | RAX = 0 on success; new total frames on query (RDI=0). Negative on error. |
| **Error Codes** | -1 EFAULT (RDI invalid), -5 EINVAL (not multiple of 512, or would exceed per-process limit), -6 ENOMEM (insufficient physical frames), -3 EACCES (if limit enforcement added later) |
| **Permissions** | Caller's own process only. |
| **Validation** | `UserPtr` not needed (no pointer args). Requested frames must be ≤ `PerProcessMaxFrames - CurrentFrames`. Physical allocator must have free frames. |
| **Alignment** | 2 MiB (512 frames) granularity. |
| **Per-Process Limit** | Proposed: 16 MiB (4096 frames) default; configurable via future `SETLIMIT` syscall. |
| **Physical Frame Accounting** | Frames allocated from `PhysicalMemory` bitmap; tracked in `TaskInfo.UserFrames`. Released in `Scheduler.Reap` / `Release`. |
| **Memory Reclamation on Exit** | All user frames freed when process becomes zombie and is reaped by `WAIT` or killed by `KILL`. |

### ABI.md Entry (Proposed)

```
| 11h | MEMGROW | RDI = additional frames (512 = 2 MiB), or 0 for query | RAX = 0 ok / new total frames (query) / -1 EFAULT / -5 EINVAL / -6 ENOMEM | Grants memory in 2 MiB steps up to per-process limit. Frames accounted and freed on reap. |
```

---

## 3. K2 — Terminal API (ABI Design)

### Requirements from OSLANG-SPEC §11.1 (`OSL.CONSOLE`)

| OSL.CONSOLE Member | Kernel Requirement |
|---|---|
| `WIDTH()` / `HEIGHT()` / `SIZE()` | Screen dimensions (cols, rows) |
| `SETCURSOR(Row, Col)` / `GETCURSOR()` | Cursor position (1-based) |
| `HIDECURSOR()` / `SHOWCURSOR()` | Cursor visibility |
| `COLOR(Fg, Bg)` / `RESETCOLOR()` + constants | Per-write colors (already have 70h/71h) |
| `WRITE(Row, Col, Text)` | Absolute positioned write |
| `CLEAR()` / `CLEARLINE()` / `CLEARAREA()` | Screen/line/region clear |
| `GETKEY()` (blocks) / `READKEY()` (non-blocking) / `KEYAVAILABLE()` | Raw keyboard events with `KEYCODE`, `CHAR`, `CTRL`, `ALT`, `SHIFT` |
| `ENTER()` / `EXIT()` / `ALTERNATE()` | Screen mode switching |
| `BEGINFRAME()` / `ENDFRAME()` / `FLUSH()` | Frame buffering |
| `BEEP()` | Audible bell |

### Current Kernel Support
- 01h WRITE (streaming, no positioning)
- 09h PRINT ($-terminated, legacy)
- 6Fh CLEAR (full screen + serial ESC[2J H)
- 70h SETCOLOR / 71h GETCOLOR (global attribute)
- 52h KEY (raw PS/2 scancode only, no serial, no modifiers)
- 6Eh GETCHAR (cooked byte stream, merges serial+PS/2, no key events)
- 40h–59h GFX range (window manager primitives, not terminal API)

### Missing for OSL.CONSOLE
1. **Screen geometry** (width/height) — VGA text mode is 80×25; Bochs GFX is variable.
2. **Cursor position/visibility** — VGA cursor exists but no syscall to get/set/position.
3. **Absolute positioned write** — `WRITE(Row, Col, Text)`.
4. **Line/region clear** — `CLEARLINE`, `CLEARAREA`.
5. **Raw key events** — `KEYCODE` enum with modifiers, from both PS/2 and serial (ESC[/ESC O).
6. **Non-blocking key read** — `READKEY()` returning `NULL` when nothing pending.
7. **Frame buffering** — `BEGINFRAME`/`ENDFRAME`/`FLUSH`.
8. **Screen modes** — `ENTER`/`EXIT`/`ALTERNATE` (text vs graphics).

### SQ-014 Impact
- `KEYCODE` numeric values must be fixed by the runtime for deterministic serial mapping.
- PS/2 set-1 scancodes + serial `ESC[`/`ESC O` sequences → `KEYCODE` table required in kernel.
- Modifiers (Ctrl, Alt, Shift) tracked per event.

### Proposed Syscalls (New Range: 7Ah–7Fh)

| AH | Name | Inputs | Outputs | Notes |
|---|---|---|---|---|
| 7Ah | CONS_INFO | RDI = out u16[2] (cols, rows) | 0 ok / -1 EFAULT | Screen dimensions |
| 7Bh | CONS_CURSOR | AL=0 get (out RDI=u16[2] row,col), AL=1 set (RDI=row, RSI=col), AL=2 hide, AL=3 show | 0 ok / -1 EFAULT / -5 EINVAL | 1-based coords |
| 7Ch | CONS_WRITE_AT | RDI=row, RSI=col, RDX=buf, RCX=len | bytes written / -1 EFAULT | Absolute positioned write |
| 7Dh | CONS_CLEAR | AL=0 full, AL=1 line (RDI=row), AL=2 area (RDI=top, RSI=left, RDX=bottom, RCX=right) | 0 ok / -5 EINVAL | |
| 7Eh | CONS_KEY | AL=0 `GETKEY` (blocking), AL=1 `READKEY` (non-block), AL=2 `KEYAVAILABLE` (bool) | RAX = key event struct ptr (kernel-allocated in user window) / 0 / -1 EFAULT | Key event: u16 KEYCODE, u16 CHAR, u8 CTRL, u8 ALT, u8 SHIFT |
| 7Fh | CONS_FRAME | AL=0 BEGINFRAME, AL=1 ENDFRAME, AL=2 FLUSH | 0 ok | Double-buffering support |

### Key Event Struct (in user window, kernel-allocated via MEMGROW or fixed slot)

```
typedef struct KeyEvent {
    uint16_t keycode;   // KEYCODE enum value (fixed per SQ-014)
    uint16_t ch;        // printable char or 0
    uint8_t  ctrl;
    uint8_t  alt;
    uint8_t  shift;
    uint8_t  reserved[3];
} __attribute__((packed)); // 12 bytes
```

### Keyboard Decoding Requirements
- PS/2 set-1 scancodes → `KEYCODE` (make/break, E0/E1 prefixes).
- Serial `ESC[` (CSI) and `ESC O` (SS3) sequences → `KEYCODE` (arrows, Home, End, PgUp, PgDn, Insert, Delete, F1–F12, modifiers).
- Modifiers tracked from PS/2 shift/ctrl/alt keys and serial `ESC[1;2~` style modifier reports.
- `GETCHAR` (6Eh) remains for cooked input; `CONS_KEY` is raw.

---

## 4. K3 — File and Directory I/O Gap Audit

### OSL.FILE / OSL.DIR Spec Requirements (OSLANG-SPEC §11.2–11.3)

| Operation | OSL.FILE / DIR Member | Current Kernel Syscall | Gap |
|---|---|---|---|
| **Open** | `OPEN(path, mode?)` → stream | 2Ah (read-only), 76h (create) | No write/append open mode; no stream abstraction in kernel (handles are file-scoped) |
| **Read** | `READ(path)` / `READTEXT(path)` / stream `READLINE()` / `READ()` | 2Bh (read handle) | No `READTEXT` (read all); stream `READLINE`/`READ`/`EOF` missing |
| **Write** | `WRITE(path, text)` / `WRITETEXT` / stream `WRITE(text)` | 77h (write handle) | No `WRITETEXT` (write all); stream `WRITE` exists but no append mode open |
| **Append** | `APPEND(path, text)` | — | Missing |
| **Create** | `CREATE(path)` | 76h (create file) | Exists but returns handle, not stream |
| **Seek** | stream `SEEK(offset, whence?)` | — | Missing |
| **Close** | stream `CLOSE()` | 2Ch (close handle) | Exists for handles |
| **Stat** | `SIZE(path)` / `EXTENSION(path)` / `NAME(path)` / `DIR(path)` | 60h (stat) | `SIZE`/`EXTENSION`/`NAME`/`DIR` missing as primitives |
| **List** | `LIST(path)` / `FILES(path)` / `DIRS(path)` | 21h (dirnext) | High-level list missing; `dirnext` is low-level |
| **Mkdir** | `CREATE(path)` (for dir) / `DIR.CREATE` | 25h (mkdir) | **Legacy syscall** — prints "OK" to VGA, no error codes |
| **Delete** | `DELETE(path)` / `DEL(path)` / `DIR.DELETE` | 27h (delete) | **Legacy syscall** — calls `FileCommands.Del`, no error codes |
| **Rename** | `RENAME(old, new)` / `MOVE` / `COPY` / `DIR.RENAME` | 28h (rename), 29h (copy) | **Legacy syscalls** — build command lines, call shell commands |
| **CWD** | `CURRENT()` / `CHANGE(path)` | 22h (getcwd), 24h (chdir) | Exists |
| **Permission enforcement** | Kernel-side on all ops | Partial (read on 2Ah) | Write/create/delete/rename need enforcement |
| **Error handling** | Real error codes per ABI | **Legacy syscalls return 0 always** | Major gap |

### Legacy Syscalls to Replace (25h–29h, 61h, 62h)

| AH | Current | Problem | Replacement Design |
|---|---|---|---|
| 25h | `mkdir` → `FileCommands.MakeDir` → prints "OK" | VGA output, no error codes | `MKDIR` path-based: 0 ok, -3 EACCES, -4 ENOENT (parent), -5 EINVAL, -6 ENOMEM, -7 EEXIST |
| 26h | `rmdir` → `FileCommands.RemoveDir` → prints "OK" | VGA output, no error codes | `RMDIR` path-based: 0 ok, -3 EACCES, -4 ENOENT, -5 EINVAL (not empty), -6 ENOTEMPTY |
| 27h | `delete` → `FileCommands.Del` | No error codes, trash policy in kernel | `UNLINK` path-based: 0 ok, -3 EACCES, -4 ENOENT, -5 EINVAL (is dir), -6 ENOMEM |
| 28h | `rename` → builds "REN old new" line | Command-line parsing in kernel | `RENAME` path-based: 0 ok, -3 EACCES, -4 ENOENT (src), -5 EINVAL, -7 EEXIST (dst) |
| 29h | `copy` → builds "COPY src dst" line | Command-line parsing in kernel | `COPY` path-based: 0 ok, -3 EACCES, -4 ENOENT, -5 EINVAL, -6 ENOMEM |
| 61h | `ln` → `FileCommands.Ln` | Command-line parsing | `LINK` path-based: 0 ok, -3 EACCES, -4 ENOENT, -5 EINVAL, -7 EEXIST |
| 62h | `trash` → `TrashCommands.Run` | Shell policy in kernel | **Remove** — trash is shell policy (D4), not kernel primitive |

### Proposed New Syscalls (Path-Based Primitives)

| AH | Name | Inputs | Outputs | Error Codes |
|---|---|---|---|---|
| 25h | MKDIR | RDI=path, RSI=len, RDX=mode | 0 ok | -1 EFAULT, -3 EACCES, -4 ENOENT (parent), -5 EINVAL, -6 ENOMEM, -7 EEXIST |
| 26h | RMDIR | RDI=path, RSI=len | 0 ok | -1 EFAULT, -3 EACCES, -4 ENOENT, -5 EINVAL (not dir), -6 ENOTEMPTY |
| 27h | UNLINK | RDI=path, RSI=len | 0 ok | -1 EFAULT, -3 EACCES, -4 ENOENT, -5 EINVAL (is dir) |
| 28h | RENAME | RDI=oldPath, RSI=oldLen, RDX=newPath, RCX=newLen | 0 ok | -1 EFAULT, -3 EACCES, -4 ENOENT (src), -5 EINVAL, -7 EEXIST (dst) |
| 29h | COPY | RDI=srcPath, RSI=srcLen, RDX=dstPath, RCX=dstLen | 0 ok | -1 EFAULT, -3 EACCES, -4 ENOENT (src), -5 EINVAL, -6 ENOMEM, -7 EEXIST (dst) |
| 61h | LINK | RDI=targetPath, RSI=targetLen, RDX=linkPath, RCX=linkLen | 0 ok | -1 EFAULT, -3 EACCES, -4 ENOENT (target), -5 EINVAL, -7 EEXIST (link) |
| 62h | — | **Reserved** (trash removed) | — | — |

### Stream Operations (for OSL.FILE streams)

| AH | Name | Inputs | Outputs | Notes |
|---|---|---|---|---|
| 2Ah | OPEN | RDI=path, RSI=len, RDX=mode (bitmask: 1=read, 2=write, 4=append, 8=create) | handle (3–7) / -1 EFAULT / -3 EACCES / -4 ENOENT / -5 EINVAL / -6 ENOMEM / -8 EBADF | Replaces current 2Ah (read-only) |
| 2Bh | READ | RDI=handle, RSI=buf, RDX=count | bytes read / -1 EFAULT / -3 EACCES / -8 EBADF | Existing |
| 2Ch | CLOSE | RDI=handle | 0 ok / -8 EBADF | Existing |
| **New** | SEEK | RDI=handle, RSI=offset (i64), RDX=whence (0=set, 1=cur, 2=end) | new offset / -1 EFAULT / -3 EACCES / -5 EINVAL / -8 EBADF | Required for streams |
| **New** | READLINE | RDI=handle, RSI=buf, RDX=cap | bytes read (0=EOF) / -1 EFAULT / -8 EBADF | Reads until `\n` or cap-1; NUL-terminated |
| **New** | EOF | RDI=handle | 1=true / 0=false / -8 EBADF | Stream EOF status |

### SQ-011 Path Case-Sensitivity Resolution
- **Kernel** normalizes paths to case-insensitive DOS resolution (per OSB-ARCHITECTURE §9).
- **Engine** passes paths through unmodified; does not normalize.
- **Shell** presents DOS-style paths; kernel resolves `A:\OSB\BIN\X.OSL` → `x.osl` on ext4.

---

## 5. ABI Consistency Summary

Every proposed syscall above has its ABI.md entry defined. Key principles:
- All string arguments are byte strings with explicit lengths (B1 convention).
- All pointers validated with `UserPtr` (overflow-safe).
- Error codes from the frozen table (-1 EFAULT, -2 ENOSYS, -3 EACCES, -4 ENOENT, -5 EINVAL, -6 ENOMEM, -7 EINTR, -8 EBADF).
- No presentation in kernel (no VGA prints).
- Version bump to **v8** when these syscalls are added.

---

## 6. Files Requiring Changes (Design Phase Only)

### osb.kernel
- `Cpu/Syscalls.cs` — **Must be split** (1098 lines > 400). Proposed partials: `Syscalls.Console.cs`, `Syscalls.File.cs`, `Syscalls.Process.cs`, `Syscalls.Memory.cs`, `Syscalls.Identity.cs`, `Syscalls.Env.cs`, `Syscalls.User.cs`, `Syscalls.Terminal.cs` (new K2).
- `Processes/Scheduler.cs` — Add `UserFrames` tracking, `MEMGROW` handling, frame reclamation on reap.
- `Memory/PhysicalMemory.cs` — Ensure frame accounting supports per-process limits.
- `Hardware/Vga.cs` — Add cursor position/visibility, absolute write, line/region clear, frame buffering.
- `Hardware/Keyboard.cs` / `Serial.cs` — Add raw key event decoding (PS/2 + serial ESC sequences) for K2.
- `build.sh` — Add ABI version consistency check (compare `osbkt/docs/ABI.md` version vs `Syscalls.AbiVersion`).

### osbkt
- `Syscall.cs` — **Must be split** (581 lines > 400). Add wrappers for new syscalls.
- `docs/ABI.md` — Update to v8 with all new syscall entries.

### osb.oslang/docs
- `OWNER-DECISIONS.md` — Created (this task).
- `O1a-GAP-ANALYSIS.md` — This document.
- `SPEC-QUESTIONS.md` — Updated with cross-references to design proposals.

---

## 7. Verification Checklist (Pre-O1b)

Before O1b implementation begins, the following must be **DONE** (not designed):

- [ ] `osb.kernel/Cpu/Syscalls.cs` split into ≤400-line partials.
- [ ] `osbkt/Syscall.cs` split into ≤400-line partials.
- [ ] Legacy syscalls 25h–29h, 61h, 62h removed from kernel; new path-based primitives implemented.
- [ ] Automated ABI version check in `build.sh` (fails build if `osbkt/docs/ABI.md` version ≠ `Syscalls.AbiVersion`).
- [ ] `osb.kernel/README.md` and `ROADMAP.md` corrected to reflect actual implemented state (ext4 writes exist, legacy shell gone).
- [ ] `osb.oslang/README.md` updated to describe current repo structure.
- [ ] All files touched in O1a design respect 400-line limit.
- [ ] Kernel builds, boots, smoke test passes with new syscall stubs returning ENOSYS.

---

**End of O1a Design.** STOP for owner review.