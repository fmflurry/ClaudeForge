# Scaling Guide — Analysis Workers

## Architecture

Analysis workers are **stateless** processes that poll the PostgreSQL-backed job queue using `SELECT ... FOR UPDATE SKIP LOCKED`. This enables safe horizontal scaling without coordination overhead.

```
                    ┌──────────────┐
                    │  PostgreSQL   │
                    │  (job queue)  │
                    └──────┬───────┘
                           │
            ┌──────────────┼──────────────┐
            │              │              │
       ┌────▼────┐   ┌────▼────┐   ┌────▼────┐
       │ Worker  │   │ Worker  │   │ Worker  │
       │   #1    │   │   #2    │   │   #N    │
       └─────────┘   └─────────┘   └─────────┘
```

Each worker:
- Polls the `analysis_jobs` table every 1 second (configurable)
- Acquires one job via `SKIP LOCKED`
- Runs static + dynamic analysis sequentially
- Updates job status on completion
- Moves to next job (no batch processing)

---

## Horizontal Scaling

### Manual Scaling (Docker Compose)

```bash
# Scale to 4 workers
docker compose -f devops/docker-compose.analysis.yml up -d --scale analysis-worker=4

# Scale back to 2
docker compose -f devops/docker-compose.analysis.yml up -d --scale analysis-worker=2
```

### Environment Override

```bash
# Via deploy-analysis.sh (default: 2)
WORKER_COUNT=4 ./deploy-analysis.sh
```

### Docker Compose Default

In `docker-compose.analysis.yml`:

```yaml
services:
  analysis-worker:
    deploy:
      replicas: 2  # Default — override at runtime
```

---

## Capacity Planning

### Single Worker Throughput

| Metric | Value |
|--------|-------|
| Analyses per minute (avg) | ~2 |
| Analysis duration (avg) | ~25 seconds |
| Analysis duration (P95) | ~120 seconds |
| Memory per worker | ~200-500 MB |
| CPU per worker | ~0.5-1.5 cores |
| DB connections per worker | 1 (pooled) |

### Current Limits

| Resource | Limit | Basis |
|----------|-------|-------|
| Max workers | **10** | PG connection pool (default 100 connections, reserve 80 for API) |
| Max queue depth | **1000** | Before backpressure kicks in |
| Max analysis time | **5 minutes** | Hard timeout per job |
| Docker socket contention | **N workers** | Sandbox containers run inside worker containers |

### Scaling Decision Matrix

| Queue Depth | Workers Needed | Recommendation |
|-------------|----------------|---------------|
| 0–20 | 1–2 | Normal operation |
| 20–50 | 2–4 | Moderate load |
| 50–100 | 4–6 | Busy — consider scaling up |
| 100–200 | 6–8 | High load — add workers |
| 200–500 | 8–10 | Heavy load — max scaling |
| 500+ | N/A | Investigate bottleneck (tools, sandbox, DB) |

---

## Auto-Scaling Concept

The PG-based queue design is compatible with auto-scaling. Below is the logical design; implementation depends on the orchestration platform.

### Metrics-Driven Scaling

```
Monitor: analysis_queue_size (Prometheus gauge)
Scale trigger: queue_depth > 100 AND worker_active_count < MAX_WORKERS
Scale-down trigger: queue_depth < 10 AND worker_active_count > MIN_WORKERS
```

### Implementation Approaches

#### Option A: Cron-Based Scaler (simplest)

```bash
# Every 2 minutes via cron
QUEUE_DEPTH=$(curl -s http://metrics:9090/api/v1/query?query=analysis_queue_size | jq '.data.result[0].value[1] | tonumber')
WORKERS=$(curl -s http://metrics:9090/api/v1/query?query=worker_active_count | jq '.data.result[0].value[1] | tonumber')

if [ "$QUEUE_DEPTH" -gt 100 ] && [ "$WORKERS" -lt 10 ]; then
    TARGET=$(( WORKERS + 1 ))
    docker compose -f devops/docker-compose.analysis.yml up -d --scale "analysis-worker=${TARGET}"
elif [ "$QUEUE_DEPTH" -lt 10 ] && [ "$WORKERS" -gt 2 ]; then
    TARGET=$(( WORKERS - 1 ))
    docker compose -f devops/docker-compose.analysis.yml up -d --scale "analysis-worker=${TARGET}"
fi
```

#### Option B: Systemd Timer + Script

- `analysis-scaler.service` — runs scaler logic
- `analysis-scaler.timer` — `OnCalendar=*:0/2` (every 2 minutes)
- Same logic as cron but with proper logging, rate limiting, and error handling

#### Option C: Kubernetes HPA (if migrating to K8s)

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: analysis-worker
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: analysis-worker
  minReplicas: 2
  maxReplicas: 10
  metrics:
    - type: External
      external:
        metric:
          name: analysis_queue_size
        target:
          type: AverageValue
          averageValue: 25  # Target ~25 jobs per worker
```

---

## Safe SKIP LOCKED Queue Access

The PostgreSQL queue uses `SKIP LOCKED` to allow concurrent workers without lock contention:

```sql
-- Each worker runs this in a transaction:
BEGIN;

UPDATE analysis_jobs
SET status = 'processing',
    worker_id = :worker_id,
    started_at = NOW()
WHERE id = (
    SELECT id
    FROM analysis_jobs
    WHERE status = 'queued'
    ORDER BY priority DESC, created_at ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED
);

COMMIT;
```

This guarantees:
- No two workers process the same job
- No table-level locking
- Linear scalability with worker count
- Fair ordering (priority + FIFO)

---

## Resource Limits per Worker

In `docker-compose.analysis.yml`:

```yaml
deploy:
  resources:
    limits:
      cpus: "2.0"
      memory: 1G
    reservations:
      cpus: "1.0"
      memory: 512M
```

These limits prevent a single worker from starving the host or other workers.

---

## Monitoring for Scaling Decisions

| Signal | Source | Use |
|--------|--------|-----|
| `analysis_queue_size` | Prometheus | Primary scale trigger |
| `analysis_duration_seconds` | Prometheus | Detect performance regressions |
| `worker_active_count` | Prometheus | Current scale state |
| `database_connections_active` | Prometheus | PG connection pool pressure |
| `sandbox_duration_seconds` | Prometheus | Sandbox throughput bottleneck |

---

## Best Practices

1. **Always use `SKIP LOCKED`** — never `SELECT ... FOR UPDATE` without `SKIP LOCKED` in concurrent workers
2. **Set timeouts** — each job has a 5-minute hard timeout; workers that exceed it are considered failed
3. **Graceful shutdown** — workers complete current job before SIGTERM takes effect (use `IHostedService.StopAsync`)
4. **Worker ID uniqueness** — each instance sets `WORKER_ID` environment variable for metrics and logging
5. **Connection pool** — each worker maintains its own Npgsql connection pool (MinPoolSize=1, MaxPoolSize=5)
6. **No shared state** — workers never share in-memory state; all coordination is through the database

---

## Scaling Checklist

- [ ] PG `max_connections` set high enough (≥ 100 for 10 workers + API + monitoring)
- [ ] Docker socket accessible from all workers
- [ ] Pull-through cache for analysis Docker images (sandbox base images)
- [ ] Prometheus scraping all workers
- [ ] Alertmanager configured for WorkerDown alert
- [ ] Graceful shutdown handling in worker service
- [ ] Connection string uses PGBouncer or direct connection (direct recommended for SKIP LOCKED)
