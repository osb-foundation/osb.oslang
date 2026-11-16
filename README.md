# OSLANG

OSLANG is the scripting language for OSB (Operating System Basic), a DOS-like operating system built from scratch.

## Overview

OSLANG is a dynamically-typed, object-oriented scripting language designed for the OSB platform. It provides a complete language stack including a lexer, parser, compiler, and runtime with support for modern programming features.

## Features

- **Object-Oriented** - Classes, interfaces, inheritance, constructors
- **Functional** - Arrow functions, lambdas, first-class functions
- **Modern Syntax** - String interpolation, multiline strings, escape sequences
- **Structured Data** - Arrays, objects, enums, enum sets
- **Control Flow** - Switch/case, foreach, break, try/catch
- **Built-in APIs** - Console, File, App, JSON, CSV, XML, Config, Network
- **Internationalization** - I18N support for globalized applications

## Language Versions

- **Current**: OSLANG 0.73
- **Previous**: 0.72, 0.7, 0.62, 0.61, 0.6, 0.51, 0.5, 0.4, 0.3, 0.2, 0.1

## Project Structure

- **Lexing/** - Tokenizer and lexer
- **Parsing/** - Parser and AST generation
- **Compilation/** - Semantic analysis and compilation
- **Runtime/** - Interpreter and runtime environment
- **Ast/** - Abstract syntax tree definitions
- **Diagnostics/** - Error reporting and diagnostics
- **Extensibility/** - Extension API for host applications
- **OslangInterpreter.cs** - Main interpreter entry point
- **docs/** - Language documentation and guides
- **samples/** - Example OSLANG programs

## Usage

OSLANG can be used as:
- A standalone interpreter for `.osl` and `.oslang` files
- An embedded scripting language in OSB applications
- A configuration language for OSB tools

## Building

```bash
dotnet build
```

## Part of OSB

This is part of the [OSB](https://github.com/osb-system) - Operating System Basic project, a DOS-like operating system built from scratch.

## License

MIT License - see [LICENSE](LICENSE) for details.