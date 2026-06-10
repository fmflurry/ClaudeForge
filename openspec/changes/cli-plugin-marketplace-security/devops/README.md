# DevOps Configuration — Analysis Pipeline & Safe Zone

## Files

| File | Task | Purpose |
|------|------|---------|
| `Dockerfile.analysis-worker` | O.1 | Multi-stage Docker image for analysis worker service |
| `docker-compose.analysis.yml` | O.1 | Docker Compose config for deploying analysis workers |
| `prometheus-metrics.md` | O.2 | Prometheus metrics reference and Grafana dashboard layout |
| `alerts.yml` | O.3 | Prometheus alerting rules for pipeline health |
| `logging-config.json` | O.4 | JSON structured logging configuration (Serilog + Microsoft.Extensions) |
| `backup-strategy.md` | O.5 | Backup/restore strategy for new database tables |
| `deploy-analysis.sh` | O.6 | Deployment script for analysis workers only |
| `deploy.sh` | O.6 | Full multi-phase deployment orchestrator |
| `github-actions.yml` | O.7 | CI/CD pipeline for analysis service (build, test, scan, deploy) |
| `scaling-guide.md` | O.8 | Horizontal scaling documentation for analysis workers |

## Usage

### Prerequisites

- Docker & Docker Compose v2
- .NET 10 SDK (for local migrations)
- PostgreSQL 16+ with analysis tables migrated

### Quick Start (Analysis Workers Only)

```bash
export CONNECTION_STRING="Host=db;Port=5432;Database=plugin_marketplace;Username=postgres;Password=..."
./deploy-analysis.sh
```

### Full Deployment

```bash
export CONNECTION_STRING="..."
./deploy.sh
```

### CI/CD Activation

Copy `github-actions.yml` to `.github/workflows/analysis-ci.yml`:
```bash
cp devops/github-actions.yml ../../.github/workflows/analysis-ci.yml
```

### Monitoring

1. Add `prometheus-metrics.md` metric definitions to the API metrics endpoint
2. Deploy `alerts.yml` to Prometheus rules directory
3. Configure your Prometheus to scrape workers on port 8081
