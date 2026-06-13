# Test Plan: Performance, Security & Load Tests

> Part of the CLI Plugin Marketplace Security change.
> Covers tasks T.6, T.7, and T.8 — infrastructure-heavy tests that require
> real database seeding, Docker sandbox configuration, and concurrent job execution.
> These are documented but not run in CI; run them manually against a staging environment.

---

## T.6: Performance Test — Analysis Pipeline at Scale

### Goal

Measure p50 / p95 / p99 latency for the full analysis pipeline (static + dynamic scoring,
decision engine, persistence) when the DB is seeded with 1000+ plugins.

### Prerequisites

- Staging PostgreSQL instance with ~10ms network latency (simulates production)
- Docker daemon available for dynamic analysis sandbox
- `dotnet run` or published binary for the API worker
- `psql` client with write access to seed data

### Seed Script

```sql
-- seed_1000_plugins.sql
-- Generates 1000 plugins with random security scores and statuses.

INSERT INTO plugins (id, name, name_normalized, slug, description, author, download_count, visibility, security_score, security_status, created_at)
SELECT
    gen_random_uuid() AS id,
    'PerfPlugin-' || LPAD(gs::text, 5, '0') AS name,
    'perfplugin-' || LPAD(gs::text, 5, '0') AS name_normalized,
    'perf-plugin-' || LPAD(gs::text, 5, '0') AS slug,
    'Performance test plugin #' || gs AS description,
    'perf-author-' || (gs % 100)::text AS author,
    floor(random() * 10000)::bigint AS download_count,
    'public' AS visibility,
    round((random() * 100)::numeric, 2) AS security_score,
    CASE floor(random() * 4)::int
        WHEN 0 THEN 'pending'
        WHEN 1 THEN 'passed'
        WHEN 2 THEN 'failed'
        WHEN 3 THEN 'in_review'
    END AS security_status,
    now() - (random() * interval '90 days') AS created_at
FROM generate_series(1, 1000) AS gs;

-- Seed 1 analysis_result per plugin
INSERT INTO analysis_results (id, plugin_id, plugin_version, static_eslint_score, static_semgrep_score, static_gitleaks_score, static_trivy_score, dynamic_behavior_score, total_score, status, analysis_completed_at, static_weight, dynamic_weight, pass_threshold, fail_threshold, created_at)
SELECT
    gen_random_uuid() AS id,
    p.id AS plugin_id,
    '1.0.0' AS plugin_version,
    round((random() * 100)::numeric, 2) AS static_eslint_score,
    round((random() * 100)::numeric, 2) AS static_semgrep_score,
    round((random() * 100)::numeric, 2) AS static_gitleaks_score,
    round((random() * 100)::numeric, 2) AS static_trivy_score,
    round((random() * 100)::numeric, 2) AS dynamic_behavior_score,
    round((random() * 100)::numeric, 2) AS total_score,
    'passed' AS status,
    now() - (random() * interval '7 days') AS analysis_completed_at,
    0.6, 0.4, 80, 50,
    now() - (random() * interval '7 days') AS created_at
FROM plugins p;

-- Seed safe zone entries for 200 plugins across 10 orgs
INSERT INTO safe_zone_plugins (id, org_id, plugin_id, plugin_version, approved_by, approved_at, is_active)
SELECT
    gen_random_uuid() AS id,
    o.id AS org_id,
    p.id AS plugin_id,
    '1.0.0' AS plugin_version,
    (SELECT id FROM users LIMIT 1) AS approved_by,
    now() AS approved_at,
    true AS is_active
FROM (SELECT id FROM plugins ORDER BY random() LIMIT 200) p
CROSS JOIN (SELECT id FROM organizations LIMIT 10) o;
```

### Measurement Procedure

```bash
# 1. Seed the database
psql "$STAGING_DATABASE_URL" -f seed_1000_plugins.sql

# 2. Run analysis pipeline 100 times, measure
# Use a stopwatch script or the built-in BenchmarkDotNet:
#   dotnet run -c Release --project backend/ClaudeForge.Benchmarks -- --job short --filter *AnalysisPipeline*
```

### Instrumentation Points

| Operation | Metric | Target |
|-----------|--------|--------|
| Score calculation (ScoringEngine.CalculateScore) | p50 < 5ms, p95 < 10ms | Pure in-memory math |
| Decision engine (DecisionEngine.Decide) | p50 < 1ms | Pure in-memory logic |
| Save analysis result (EF Core save) | p50 < 50ms | Single row insert + update |
| Safe zone query (ListSafeZonePlugins) | p50 < 100ms | Indexed query on org_id |
| Leaderboard recalculation | p50 < 500ms | Multi-table aggregation |
| Appeal submission + resolution | p50 < 200ms | 2-3 row writes |

### Reporting

Output a table:

```
Metric        p50      p95      p99
ScoreCalc     0.3ms    0.8ms    1.2ms
Decision      0.05ms   0.1ms    0.2ms
DbSave        12ms     45ms     120ms
SafeZoneQuery 8ms      22ms      55ms
```

If p99 of any operation exceeds 2× the target, flag for optimization.

---

## T.7: Security Test — Sandbox Isolation Verification

### Goal

Verify that the Docker sandbox container used for dynamic analysis enforces
strict isolation: no network access, no filesystem access outside the sandbox,
bounded CPU/memory, and hard timeout enforcement.

### Checklist

| # | Check | Method | Pass Criteria |
|---|-------|--------|---------------|
| 1 | **Network isolation** | Run plugin with `curl 169.254.169.254` (metadata IP) and external HTTP call | Connection timeout / blocked; sandbox reports network_attempt finding |
| 2 | **Filesystem isolation** | Run plugin that reads `/etc/passwd`, `~/.ssh/id_rsa`, `/var/run/docker.sock` | File not found or permission denied; sandbox reports file_access finding |
| 3 | **Process isolation** | Run plugin that calls `ps aux`, `cat /proc/1/cmdline` | Cannot see host processes; only sandbox init process visible |
| 4 | **Memory limit** | Run plugin that allocates 1GB memory | OOM-killed before exceeding container limit (512MB default) |
| 5 | **CPU limit** | Run plugin with infinite loop + 4 threads | CPU throttled to 1 core; sandbox detects and terminates |
| 6 | **Disk limit** | Run plugin that writes 10GB to /tmp | Disk quota enforced (1GB default); write fails with ENOSPC |
| 7 | **Timeout enforcement** | Run plugin that sleeps 120s | Sandbox hard-kills after 60s (configurable) |
| 8 | **Hostname isolation** | `hostname` inside container | Returns container ID, not host hostname |
| 9 | **No privileged mode** | Inspect container capabilities | `docker inspect` shows no privileged, no SYS_ADMIN |
| 10 | **Read-only rootfs** | Attempt to write to `/bin/` | Read-only filesystem error |

### Verification Script (pseudo-code)

```bash
#!/usr/bin/env bash
# verify-sandbox-isolation.sh
# Run each isolation check and report pass/fail.

DB_URL="${1:?Usage: $0 <database_url>}"
FAIL=0

for TEST in network-isolation filesystem-isolation process-isolation memory-limit cpu-limit disk-limit timeout; do
    echo "=== Running $TEST ==="
    
    # Build a test plugin package that exercises this isolation boundary
    # (see test-plugins/ directory for source)
    
    RESP=$(curl -s -X POST "$API_URL/api/v1/plugins/submit" \
        -F "package=@test-plugins/$TEST.tar.gz" \
        -H "Authorization: Bearer $TOKEN")
    
    PLUGIN_ID=$(echo "$RESP" | jq -r '.pluginId')
    
    # Wait for analysis to complete
    sleep 10
    
    RESULT=$(curl -s "$API_URL/api/v1/plugins/$PLUGIN_ID/analysis")
    
    # Check findings for expected detection
    if echo "$RESULT" | jq -e '.findings.dynamic[] | select(.type == "network_attempt" or .type == "file_access" or .type == "process_spawn")' > /dev/null; then
        echo "  PASS: $TEST was detected"
    else
        echo "  FAIL: $TEST was not detected"
        FAIL=1
    fi
done

exit $FAIL
```

### Docker Configuration to Validate

```dockerfile
# Confirm docker-compose or Dockerfile contains:
# --network none                    # No network access
# --read-only                       # Read-only root filesystem
# --memory 512m                     # Memory limit
# --cpus 1                          # CPU limit
# --pids-limit 50                   # Process limit
# --security-opt no-new-privileges  # No privilege escalation
# --cap-drop ALL                    # Drop all capabilities
# --tmpfs /tmp:size=1G,noexec,nosuid # Temporary write space
```

---

## T.8: Load Test — Analysis Queue with 1000 Concurrent Jobs

### Goal

Simulate 1000 concurrent analysis job submissions and measure:
- Queue throughput (jobs/minute)
- Worker backpressure behavior
- Graceful degradation under sustained load
- No job loss (all submitted jobs complete)
- Recovery after load spike

### Setup

- Staging environment with PostgreSQL, Redis (if used for queue), and Docker
- `k6` installed (or `artillery` as alternative)
- Analysis worker scaled to N replicas (start with 2 workers)

### k6 Script

Save as `load-test-analysis.js`:

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';

// Load test plugin packages from disk
const plugins = new SharedArray('plugins', function () {
    return [
        open('./test-plugins/plugin-a.tar.gz', 'b'),
        open('./test-plugins/plugin-b.tar.gz', 'b'),
        open('./test-plugins/plugin-c.tar.gz', 'b'),
    ];
});

export const options = {
    stages: [
        { duration: '1m', target: 100 },   // Ramp up to 100 VUs
        { duration: '2m', target: 500 },   // Ramp up to 500 VUs
        { duration: '3m', target: 1000 },  // Ramp up to 1000 VUs
        { duration: '5m', target: 1000 },  // Sustain 1000 VUs
        { duration: '1m', target: 0 },     // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<30000'], // 95% of requests complete in <30s
        http_req_failed: ['rate<0.01'],     // <1% failure rate
    },
};

export default function () {
    const payload = plugins[Math.floor(Math.random() * plugins.length)];
    
    const res = http.post(
        'https://staging-api.example.com/api/v1/plugins/submit',
        {
            file: http.file(payload, 'test-plugin.tar.gz'),
        },
        {
            headers: {
                Authorization: `Bearer ${__ENV.TEST_TOKEN}`,
            },
            timeout: '120s', // Analysis can take up to 60s
        }
    );
    
    check(res, {
        'status is 202': (r) => r.status === 202,
        'response has pluginId': (r) => r.json('pluginId') !== undefined,
    });
    
    sleep(1);
}
```

### Backpressure Test

```javascript
// backpressure-test.js — submit jobs faster than workers can process
export const options = {
    scenarios: {
        backpressure: {
            executor: 'constant-arrival-rate',
            rate: 100,              // 100 new submissions per second
            timeUnit: '1s',
            duration: '60s',
            preAllocatedVUs: 50,
            maxVUs: 200,
        },
    },
};
```

### Worker Scaling Test

Test with varying worker counts and measure throughput:

| Workers | Jobs/min | p95 latency | Success rate | CPU (avg) |
|---------|----------|-------------|--------------|-----------|
| 1       | 30       | 45000ms     | 99.5%        | 90%       |
| 2       | 55       | 22000ms     | 99.8%        | 75%       |
| 4       | 100      | 12000ms     | 99.9%        | 60%       |
| 8       | 180      | 8000ms      | 99.9%        | 45%       |

### Acceptance Criteria

| Criterion | Threshold |
|-----------|-----------|
| Job loss rate | < 0.1% (all submitted jobs eventually processed) |
| p95 latency at 1000 concurrent | < 30s |
| Error rate | < 1% |
| Queue drain time after load spike | < 5 min (from last submission to last completion) |
| No OOM or crash | Workers stay running for the duration |
| DB connection pool | No pool exhaustion errors |

### Manual Verification Steps

```bash
# 1. Check queue depth during load
curl -s $API_URL/api/v1/control-center/analysis/queue-status | jq '.queueDepth'

# 2. Monitor worker logs for backpressure warnings
docker logs -f analysis-worker-1 --tail 50

# 3. Verify no duplicate analysis results
psql $DATABASE_URL -c "
    SELECT plugin_id, COUNT(*) as results
    FROM analysis_results
    GROUP BY plugin_id
    HAVING COUNT(*) > 1;
"

# 4. Verify all plugins have a result after drain
psql $DATABASE_URL -c "
    SELECT COUNT(*) as total_plugins,
           COUNT(*) FILTER (WHERE security_status != 'pending') as analyzed
    FROM plugins;
"
```

### Queue Backpressure Behavior

Expected flow under overload:

```
Job submitted → Queue (Redis/InMemory) ← Worker pulls at max rate
                    │
                    ├── Queue depth > 1000 → Return 429 Too Many Requests
                    ├── Queue depth > 5000 → Drop oldest job, log warning
                    └── Queue depth > 10000 → Circuit breaker opens, reject all
```

Verify that:
1. HTTP 429 responses are returned under backpressure (not 500)
2. Retry-After header is set
3. Worker auto-recovers when queue drains
4. No jobs silently dropped before the circuit breaker

---

## Appendix: Running the Tests

```bash
# Performance test
psql "$STAGING_DATABASE_URL" -f seed_1000_plugins.sql
dotnet run -c Release --project backend/ClaudeForge.Benchmarks

# Security isolation
bash verify-sandbox-isolation.sh "$STAGING_DATABASE_URL"

# Load test
k6 run load-test-analysis.js -e TEST_TOKEN="$TEST_TOKEN"
```

### Environment Requirements

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| CPU | 4 cores | 8 cores |
| RAM | 8 GB | 16 GB |
| Disk (SSD) | 20 GB | 50 GB |
| Docker | 20.10+ | 24.0+ |
| PostgreSQL | 15+ | 16+ |
| k6 | 0.45+ | 0.50+ |
