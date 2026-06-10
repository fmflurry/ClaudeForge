# Analysis Pipeline Prometheus Metrics

## Metrics Exposed by the API (`/metrics` endpoint)

The ClaudeForge API exposes Prometheus-compatible metrics via the `/metrics` endpoint on port 8080. These metrics are produced by the analysis pipeline, worker service, and reputation modules.

### Queue Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `analysis_queue_size` | Gauge | `priority` (high, normal, low) | Current number of queued analysis jobs |
| `analysis_queue_depth_total` | Counter | `priority` | Cumulative jobs enqueued |
| `analysis_workers_polled_total` | Counter | `worker_id` | Jobs polled by workers (rate → throughput) |

### Analysis Lifecycle Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `analysis_completed_total` | Counter | `status` (passed, failed, review), `tool_version` | Completed analyses by final status |
| `analysis_duration_seconds` | Histogram | `tool` (static, dynamic, total) | Duration buckets: 0.1, 0.5, 1, 2, 5, 10, 30, 60, 120, 300 |
| `analysis_retries_total` | Counter | `reason` | Retried analysis jobs |
| `analysis_timeout_total` | Counter | `phase` (static, dynamic) | Timed-out analysis runs |
| `analysis_static_findings_total` | Counter | `severity` (critical, high, medium, low), `tool` (eslint, semgrep, gitleaks, trivy) | Static analysis findings by severity and tool |
| `analysis_score` | Gauge | `plugin_id` | Final security score (0–100) |
| `sandbox_executions_total` | Counter | `result` (success, error, timeout) | Dynamic analysis sandbox runs |
| `sandbox_duration_seconds` | Histogram | — | Sandbox execution time buckets |

### Worker Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `worker_active_count` | Gauge | — | Currently active (polling) workers |
| `worker_jobs_processed_total` | Counter | `worker_id` | Jobs processed by each worker |
| `worker_errors_total` | Counter | `worker_id`, `error_type` | Worker-level errors |
| `worker_memory_bytes` | Gauge | `worker_id` | Process memory usage per worker |

### Appeals Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `appeals_pending_count` | Gauge | — | Appeals awaiting resolution |
| `appeals_total` | Counter | `status` (approved, rejected, escalated) | Completed appeals |
| `appeals_resolution_seconds` | Histogram | — | Time to resolve an appeal |

### Reputation Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `karma_total` | Counter | `event_type` (submission, analysis_pass, analysis_fail, appeal_approved, badge_awarded) | Cumulative karma awarded |
| `karma_distribution` | Gauge | `bucket` (0-10, 11-50, 51-100, 101-500, 500+) | Author count per karma range |
| `badges_awarded_total` | Counter | `badge_type` | Badges granted |
| `leaderboard_refresh_duration_seconds` | Histogram | — | Leaderboard recalculation time |

### Safe Zone Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `safe_zone_plugins_total` | Gauge | `org_id` | Approved plugins per org |
| `safe_zone_approvals_total` | Counter | `org_id`, `auto` (true, false) | Plugin approvals |
| `safe_zone_blocks_total` | Counter | `org_id` | Plugin blocks |

### System Metrics

| Metric | Type | Labels | Description |
|--------|------|--------|-------------|
| `database_connections_active` | Gauge | — | Active Npgsql connections |
| `queue_poll_duration_seconds` | Histogram | — | PG queue poll cycle duration |

---

## Suggested Grafana Dashboard Layout

### Row 1: Queue & Worker Health

```
┌─────────────────────────────────────────────────────────────────────┐
│ [Queue Depth]          │ [Active Workers]       │ [Poll Rate]       │
│ Stat: analysis_queue   │ Stat: worker_active    │ Stat: workers_    │
│ _size (current)        │ _count (current)      │ polled_total /s   │
│ Threshold line at 50   │ Alert threshold < 1    │ Sparkline last 1h │
├────────────────────────┼────────────────────────┼─────────────────────┤
│ [Queue Depth History]                              Time series 6h    │
│ area chart: analysis_queue_size by priority                          │
└─────────────────────────────────────────────────────────────────────┘
```

### Row 2: Analysis Throughput

```
┌─────────────────────────────────────────────────────────────────────┐
│ [Completed Analyses]      │ [Failure Rate]        │ [Avg Duration]  │
│ Stat: analysis_completed  │ Stat: % failed in     │ Stat: p50/p95   │
│ _total / 15min            │ last 15min            │ analysis_duration│
├───────────────────────────┴───────────────────────┴─────────────────┤
│ [Analysis Duration Distribution]  Histogram over last 24h          │
│ stacked: passed / failed / review                                  │
└─────────────────────────────────────────────────────────────────────┘
```

### Row 3: Findings & Security

```
┌─────────────────────────────────────────────────────────────────────┐
│ [Findings by Severity]        │ [Findings by Tool]                 │
│ Stacked bar: critical/high/   │ Stacked bar: eslint/semgrep/      │
│ medium/low over last 24h      │ gitleaks/trivy                     │
├───────────────────────────────┴────────────────────────────────────┤
│ [Average Security Score]      Time series 7d                       │
│ gauge chart + sparkline       │ Threshold lines at 50, 80          │
└─────────────────────────────────────────────────────────────────────┘
```

### Row 4: Appeals & Reputation

```
┌────────────────────────────┐ ┌────────────────────────────────────┐
│ [Pending Appeals]          │ │ [Karma Awarded]                   │
│ Stat + sparkline           │ │ Stat: karma_total / 24h           │
├────────────────────────────┤ ├────────────────────────────────────┤
│ [Appeal Resolution Time]   │ │ [Karma Distribution]              │
│ Histogram p50/p95/p99      │ │ Bar chart: authors per bucket     │
└────────────────────────────┘ └────────────────────────────────────┘
```

### Row 5: Safe Zone & System

```
┌─────────────────────────────────────┬────────────────────────────────┐
│ [Safe Zone Approvals]              │ [DB Connections]              │
│ Stacked: auto vs manual            │ Stat: database_connections    │
│ Sparkline: total approved over 7d  │ active (current) + limit line │
└─────────────────────────────────────┴────────────────────────────────┘
```

---

## Implementation Notes

- Use `prometheus-net.AspNetCore` NuGet package to expose `/metrics`
- Register metrics via DI as singletons (`Meter` / `Counter` / `Histogram` / `Gauge`)
- Each worker registers itself on startup and deregisters on shutdown
- Metrics survive worker restarts (PG-backed counters rehydrate on boot)
- Prometheus scrape interval: 15s (default)
