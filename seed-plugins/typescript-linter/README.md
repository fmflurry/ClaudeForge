# TypeScript Linter

A linter plugin for TypeScript projects that enforces code quality rules.

## Overview

The TypeScript Linter plugin integrates with ClaudeForge to provide real-time linting
feedback on TypeScript codebases, enforcing team-agreed code quality standards.

## Installation

```bash
claude plugin install typescript-linter
```

## Configuration

No additional configuration required. The plugin uses sensible defaults aligned with
TypeScript strict mode.

## Usage

The plugin activates automatically on TypeScript files and surfaces lint violations
as Claude Code suggestions.

## API

- `lint(filePath: string): LintResult[]` — lint a single file
- `lintProject(rootDir: string): LintResult[]` — lint an entire project

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
