# AGENTS.md — OSLANG

This file guides coding agents working in the `osb.oslang` repository.

## Project Identity

**OSLANG** is the scripting language for OSB (Operating System Basic), a DOS-like operating system built from scratch. It provides a complete language stack including a lexer, parser, compiler, and runtime with support for modern programming features.

## Code Policy

1. **Implementation Language**
   - Use **C#** for the language implementation (lexer, parser, compiler, runtime).

2. **Design Principles**
   - Keep the language implementation modular and extensible.
   - Maintain backward compatibility with previous OSLANG versions.
   - Follow established language design patterns.

3. **Dependencies**
   - Standalone library - no dependencies on other OSB projects.
   - Can be embedded in OSB applications via the Extension API.

## Architecture

The language is organized into focused modules:

- **Lexing/** - Tokenizer and lexer
- **Parsing/** - Parser and AST generation
- **Compilation/** - Semantic analysis and compilation
- **Runtime/** - Interpreter and runtime environment
- **Ast/** - Abstract syntax tree definitions
- **Diagnostics/** - Error reporting and diagnostics
- **Extensibility/** - Extension API for host applications

## Build and Validation

```bash
dotnet build
```

## Change Workflow for Agents

1. Inspect relevant files first.
2. Keep edits targeted.
3. Do not break backward compatibility with previous OSLANG versions.
4. Update specification docs when language behavior changes.
5. Check file lengths before finishing (no file may exceed 400 lines).

## Non-Goals and Cautions

- Do not break compatibility with existing OSLANG programs.
- Do not remove language features without deprecation.
- Do not introduce host-OS runtime dependencies for the core language.

## Documentation Anchors

- `README.md`: High-level project/user documentation.
- `OSLANG-*-SPEC.md`: Language specifications for various versions.
- `docs/`: Additional documentation and guides.