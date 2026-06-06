# Deployment Commander

Command-line plugin for orchestrating multi-environment deployments.

## Overview

The Deployment Commander provides a unified command interface for deploying applications
across development, staging, and production environments using Go orchestration and
TypeScript configuration management.

## Installation

```bash
claude plugin install deployment-commander
```

## Configuration

No additional configuration required.

## Usage

```
claude plugin run deployment-commander deploy --env staging --version 1.2.3
```

## API

- `deploy(env: string, version: string): DeployResult` — deploy to an environment
- `rollback(env: string): RollbackResult` — roll back the last deployment

## Contributing

See the ClaudeForge contributor guide for contribution guidelines.

## License

MIT
