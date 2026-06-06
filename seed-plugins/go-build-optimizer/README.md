# Go Build Optimizer

Optimizes Go build configurations for faster compilation and smaller binaries.

## Overview

The Go Build Optimizer hook intercepts Go build commands and applies profile-guided
optimizations, cache tuning, and dead-code elimination to reduce build times and
binary sizes in DevOps pipelines.

## Installation

```bash
claude plugin install go-build-optimizer
```

## Configuration

No additional configuration required.

## Usage

The hook activates automatically when `go build` commands are detected in the workspace.

## API

- `optimize(buildConfig: BuildConfig) -> OptimizedConfig` — optimize a build configuration

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
