# Code Quality Plugin

Enforces code quality standards across TypeScript and Rust codebases.

## Overview

The Code Quality Plugin combines TypeScript ESLint rules with Rust Clippy integration
to provide a unified code quality gate for polyglot development teams.

## Installation

```bash
claude plugin install code-quality-plugin
```

## Configuration

No additional configuration required.

## Usage

The plugin registers itself as a quality gate that runs automatically on file saves
and pre-commit hooks.

## API

- `checkQuality(filePath: string): QualityReport` — check a single file
- `checkProject(rootDir: string): QualityReport` — check an entire project

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
