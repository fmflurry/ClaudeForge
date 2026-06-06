# Data Pipeline Orchestrator

Orchestrates complex data pipelines across Python and Rust processing stages.

## Overview

The Data Pipeline Orchestrator agent manages multi-stage data processing workflows
that span Python ingestion/transformation code and Rust high-performance compute
stages, providing observability, error recovery, and scheduling.

## Installation

```bash
claude plugin install data-pipeline-orchestrator
```

## Configuration

No additional configuration required.

## Usage

Deploy as an agent that continuously monitors and orchestrates your data pipelines,
automatically retrying failed stages and alerting on anomalies.

## API

- `orchestrate(pipeline: PipelineConfig) -> OrchestrationHandle` — start a pipeline
- `status(handle: OrchestrationHandle) -> PipelineStatus` — check pipeline status

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
