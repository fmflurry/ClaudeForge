# Rust Security Scanner

Scans Rust codebases for common security vulnerabilities and unsafe patterns.

## Overview

The Rust Security Scanner examines Rust source code for `unsafe` blocks, common
vulnerability patterns, dependency advisories, and memory safety issues.

## Installation

```bash
claude plugin install rust-security-scanner
```

## Configuration

No additional configuration required.

## Usage

Run as a skill against your Rust workspace to get a comprehensive security report.

## API

- `scan(workspacePath: str) -> SecurityReport` — full workspace security scan
- `check_unsafe(filePath: str) -> list[UnsafeUsage]` — enumerate unsafe blocks

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
