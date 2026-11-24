# OSLANG

OSLANG is the scripting language for OSB (Operating System Basic), a DOS-like operating system built from scratch.

## What this repo holds

| Path | Purpose |
|---|---|
| `OSLANG-*-SPEC.md` | The canonical language specification (0.1 … 0.73). **Spec is the truth.** |
| `OSLANG-SPEC.md` | Unified, deduplicated feature reference (folds the per-version specs). |
| `OSLANG-GAP.md` | Gap analysis: spec vs the reference implementation and the (now archived) kernel port. |
| `reference/` | **Reference implementation** (`Osb.Lang`, currently 0.72). Normal .NET, keeps building. Oracle until the new engine passes the conformance corpus. |
| `archive/kernel-port/` | The old in-kernel OSLANG port (0.72 subset), copied out of the kernel in O0. Read-only, not built. |
| `engine/` | **New engine** (host + OSB). Empty until O3. |
| `host/` | Host layers (`Host.Test` normal .NET, `Host.Osb` freestanding). Empty until O2/O6. |
| `lib/` | OSL.* standard-library implementations. Empty until O4. |
| `os/` | `OSLANG.EXE` entry point. Empty until O6. |
| `tests/conformance/` | Conformance corpus (`.osl` + `.out` + `.in` + `tags`). Empty until O2. |
| `samples/` | Example programs, including `samples/seed/` (the old kernel seed scripts). |
| `docs/ENGINE-DESIGN.md` | Engine design: value/heap/GC, string model, C# subset, exception model. |
| `docs/SPEC-QUESTIONS.md` | Ambiguities found in the spec set — answered by the owner, never guessed. |

## Language versions

- **Current:** OSLANG 0.8
- **Previous:** 0.72, 0.7, 0.62, 0.61, 0.6, 0.51, 0.5, 0.4, 0.3, 0.2, 0.1

## Status (2026-11-19, phase O0)

- Specs, gap analysis, reference implementation, and the archived kernel port are in place.
- The **new engine does not exist yet** — phases O1–O7 are the delivery.
- `OSLANG.EXE` does not exist yet; `.OSL` dispatch is not implemented yet (O6).
- The kernel has **zero** OSLANG code (removed in O0).

## Building

```bash
# Reference implementation (normal .NET):
cd reference && dotnet build
```

The new engine tree has no build entry until O2/O3.

## Part of OSB

This is part of the [OSB](https://github.com/osb-foundation) - Operating System Basic project, a DOS-like operating system built from scratch.

## License

MIT License - see [LICENSE](LICENSE) for details.