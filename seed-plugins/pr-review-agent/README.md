# PR Review Agent

Automated pull request review agent that provides actionable feedback.

## Overview

The PR Review Agent autonomously reviews pull requests, surfacing correctness bugs,
style issues, security concerns, and improvement opportunities for both development
teams and product owners.

## Installation

```bash
claude plugin install pr-review-agent
```

## Configuration

No additional configuration required.

## Usage

Attach the agent to your repository. It will automatically review new pull requests
and post structured feedback comments.

## Changelog

- **2.0.0** — Major rewrite with improved reasoning and product-owner summary view
- **1.1.0** — Added product-owner summary mode
- **1.0.0** — Initial release

## API

- `reviewPr(prUrl: string): ReviewResult` — review a pull request by URL
- `summarizeForProductOwner(review: ReviewResult): string` — generate a non-technical summary

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
