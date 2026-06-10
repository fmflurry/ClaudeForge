# Troubleshooting Guide

## Common Issues and Solutions

### "Plugin not found in safe zone"

**Error message**: `Plugin not approved for your organization. Contact your admin to request approval.`

**Causes**:
1. Plugin is not approved for the active org's safe zone
2. Wrong org context is active
3. Plugin does not exist in the catalog

**Solutions**:

```bash
# Check active org
claudeforge org show

# Switch to correct org
claudeforge org use <correct-org-id>

# Request approval (member)
claudeforge org request-approval <plugin-id> --reason "..."

# Approve (admin)
claudeforge org approve <plugin-id>
```

**Admin**: Check pending requests to see if approval was already requested:
```bash
claudeforge org list --pending
```

---

### "Rate limited"

**Error message**: `Rate limit exceeded. Maximum X submissions per hour per IP.`

**Causes**:
- Too many submissions from your IP address (limit: 10/hour)
- Too many submissions as an author (limit: 50/day)
- Too many appeals (limit: 5/day)

**Solutions**:

1. **Wait**: Check `Retry-After` header in the response for seconds until reset
2. **Check your karma**: Higher karma authors may get higher limits in future (planned)
3. **Reduce submission rate**: Bundle changes into fewer submissions
4. **For appeals**: Review before filing — frivolous appeals consume rate and incur karma penalties

```bash
# Check rate limit status
curl -I https://<marketplace-domain>/api/v1/plugins/submit
# Look for: X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset
```

---

### "Analysis failed"

**Error message**: `Analysis failed: [error details]`

**Causes and Fixes**:

| Cause | Symptom | Fix |
|-------|---------|-----|
| Docker not available | Worker cannot start sandbox | Ensure Docker daemon is running on analysis worker host |
| Tool not installed | ESLint/Semgrep/Gitleaks/Trivy not found | Run `npm install -g eslint semgrep gitleaks trivy` or use Docker-based tools |
| Plugin package corrupt | Extraction fails | Re-package plugin as valid tar.gz or zip |
| Analysis timeout | Plugin exceeds 5 min total | Optimize plugin init; reduce file count |
| Out of memory | Worker OOM | Reduce plugin size or increase memory limit in config |
| Docker pull failure | Sandbox image not found | Run `docker pull alpine:latest` on worker host |

**Check analysis queue status**:
```bash
# View queue and worker status (admin)
GET /api/v1/control-center/metrics
# Look for: queueLength, failedToday, failureRate
```

**View worker logs**:
```bash
# SSH into analysis worker host
journalctl -u analysis-worker -n 50 --no-pager

# Or if using Docker Compose
docker-compose -f analysis-docker-compose.yml logs --tail=50 worker
```

---

### "Authentication failed"

**Error message**: `Authentication required` or `Invalid token`

**Causes**:
- Token expired
- Not logged in
- Invalid or malformed token

**Solutions**:

```bash
# Re-authenticate
claudeforge login

# Or set token explicitly
export CLAUDEFORGE_API_TOKEN=<your-token>

# Verify authentication
claudeforge whoami
```

If running in CI/CD, ensure the API token is set in environment variables (never hardcoded).

---

### "Config validation failed"

**Error message**: `Configuration validation failed: [details]`

**Common validation errors**:

| Error | Cause | Fix |
|-------|-------|-----|
| `static_weight + dynamic_weight must equal 1.0` | Sum is not 1.0 | Adjust so static + dynamic = 1.0 |
| `pass_threshold must be greater than fail_threshold` | Pass <= fail | Set pass > fail |
| `Values must be in range [0, 100]` | Out of bounds | Ensure all thresholds 0–100 |
| `max_workers must be >= 1` | Invalid count | Set to at least 1 |

**Example**: Correcting invalid configuration:
```json
// INVALID: weights sum to 0.9
{ "static_weight": 0.6, "dynamic_weight": 0.3 }

// VALID: weights sum to 1.0
{ "static_weight": 0.6, "dynamic_weight": 0.4 }

// INVALID: pass <= fail
{ "pass_threshold": 50, "fail_threshold": 60 }

// VALID: pass > fail
{ "pass_threshold": 60, "fail_threshold": 50 }
```

---

### "Migration errors"

**Error message**: `Relation "analysis_results" does not exist` or similar DB error

**Causes**:
- Database migrations not yet applied
- Wrong database connection string
- Schema version mismatch

**Solutions**:

```bash
# Run EF Core migrations
dotnet ef database update --context MarketplaceDbContext

# Verify migration status
dotnet ef migrations list --context MarketplaceDbContext

# If migration fails, check:
# 1. Database connection string in appsettings.json
# 2. Database server is running
# 3. User has permissions to create tables
```

**Verify schema**:
```sql
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public';
-- Expected: analysis_results, appeals, organizations, 
--           org_members, safe_zone_plugins, author_reputation,
--           reputation_events, analysis_jobs
```

---

## Checking Analysis Queue Status

### Via CLI (Admin)

```bash
curl -H "Authorization: Bearer <token>" \
  https://<marketplace-domain>/api/v1/control-center/metrics | jq '.analysis'
```

Expected fields:
```json
{
  "queueLength": 5,
  "inProgress": 2,
  "completedToday": 45,
  "failedToday": 2,
  "avgProcessingTimeMs": 120000,
  "failureRate": 0.04
}
```

### Via PostgreSQL

```sql
-- Queue length
SELECT status, COUNT(*) FROM analysis_jobs GROUP BY status;

-- Stuck jobs (queued for > 10 minutes)
SELECT * FROM analysis_jobs 
WHERE status = 'queued' 
AND created_at < NOW() - INTERVAL '10 minutes';

-- Failed jobs with errors
SELECT * FROM analysis_jobs 
WHERE status = 'failed' 
ORDER BY updated_at DESC 
LIMIT 10;
```

---

## Viewing Logs

### Analysis Worker Logs

```bash
# Worker logs
journalctl -u analysis-worker -f

# Or with Docker Compose
docker-compose -f analysis-docker-compose.yml logs -f worker

# If using systemd
sudo journalctl -u claudeforge-analysis-worker -n 200 --no-pager
```

### API Gateway Logs

```bash
# Nginx/Apache access logs
tail -f /var/log/nginx/access.log | grep /api/v1/

# Application logs
journalctl -u claudeforge-api -f
```

### Control Center Logs

```bash
docker-compose logs -f control-center
```

### Audit Log

Via API:
```http
GET /api/v1/control-center/audit-log
Authorization: Bearer <admin_token>
```

---

## Known Issues

| Issue | Status | Workaround |
|-------|--------|------------|
| Analysis queue backlog > 100 jobs | Auto-alert triggers | Scale workers or increase `max_workers` |
| Appeal notification delays | Under investigation | Manually check appeal status via API |
| Docker image pull failures during peak | Known | Pre-pull images on worker hosts |
| Leaderboard cache stale up to 5 min | Acceptable | Force refresh via API (planned) |

---

## Contact / Support

| Channel | Details |
|---------|---------|
| GitHub Issues | https://github.com/your-org/claudeforge/issues |
| Security | security@your-org.com |
| Slack | `#claudeforge-support` (internal) |
| Email | support@your-org.com |

For urgent issues (queue down, security incidents), use Slack or email. For feature requests and non-urgent bugs, use GitHub Issues.
