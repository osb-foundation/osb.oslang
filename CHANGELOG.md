# Changelog

## [0.8.0] - Olorun (Generation 0)

### Added
- Initial release of osb.oslang under OSB generation 0 (Olorun).
- OSLANG language implementation: lexer, parser, compiler, runtime and specs (OSLANG 0.1–0.8 tracked separately in OSLANG-*-SPEC.md).
- Language spec versions tracked per-generation; OSLANG 0.8.0 codename.

### Stable
- Build succeeds (`dotnet build`); publishes OsbEngineNativeAot executable (linux-x64, ELF magic 7f454c46, x86-64); verifies ELF, executes, checks out native ELF.

### Experimental / Incomplete
- Ports O8.* (R4, R7, R8, R9, R10...) archived read-only; Ring-3 interpreter integration pending.
