# Python Data Analyzer

Analyzes Python data pipelines and provides statistical insights.

## Overview

The Python Data Analyzer plugin combines skill-based analysis with agentic exploration
to help data analysts understand, optimize, and document their Python data pipelines.

## Installation

```bash
claude plugin install python-data-analyzer
```

## Configuration

No additional configuration required.

## Usage

Invoke as a skill for on-demand analysis, or enable agent mode for continuous monitoring
of your data pipeline health.

## API

- `analyze(pipelinePath: str) -> AnalysisReport` — analyze a pipeline module
- `suggest_optimizations(report: AnalysisReport) -> list[Suggestion]` — generate suggestions

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
