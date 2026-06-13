# Backup Strategy — Analysis Pipeline & Gamification Tables

## Scope

The following new tables require backup beyond the existing full-database dump:

| Schema | Table | Size Estimate | Criticality |
|--------|-------|---------------|-------------|
| public | `analysis_results` | Medium | High — irreplaceable analysis findings |
| public | `analysis_jobs` | Medium | High — queued work |
| public | `safe_zone_plugins` | Small | High — org security policy |
| public | `appeals` | Small | High — dispute records |
| public | `author_reputation` | Small | Medium — can rebuild from karma_events |
| public | `karma_events` | Medium | Medium — audit trail |
| public | `badges` | Tiny | Low — static definitions |
| public | `author_badges` | Small | Medium — badge awards |
| public | `leaderboard_cache` | Small | Low — can rebuild on demand |
| public | `notifications` | Medium | Low — transient |
| public | `analysis_config` | Tiny | High — scoring weights, thresholds |
| public | `config_change_log` | Small | Medium — audit trail |

**Total estimated size**: ~2–5 GB (depends on plugin submission volume)

---

## Backup Commands

### Full Daily Backup (all new tables)

```bash
#!/usr/bin/env bash
# Full daily backup of analysis and gamification tables
# Schedule: daily at 02:00 UTC via cron

BACKUP_DIR="/var/backups/claudeforge/analysis"
TIMESTAMP=$(date -u +"%Y%m%dT%H%M%SZ")
DB_NAME="${DB_NAME:-plugin_marketplace}"
DB_HOST="${DB_HOST:-localhost}"
DB_USER="${DB_USER:-postgres}"

mkdir -p "${BACKUP_DIR}/${TIMESTAMP}"

TABLES=(
  analysis_results analysis_jobs safe_zone_plugins appeals
  author_reputation karma_events badges author_badges
  leaderboard_cache notifications analysis_config config_change_log
)

for table in "${TABLES[@]}"; do
  pg_dump \
    -h "${DB_HOST}" \
    -U "${DB_USER}" \
    -d "${DB_NAME}" \
    --table="${table}" \
    --format=custom \
    --compress=9 \
    --file="${BACKUP_DIR}/${TIMESTAMP}/${table}.dump"
done

# Single combined dump (convenience restore)
pg_dump \
  -h "${DB_HOST}" \
  -U "${DB_USER}" \
  -d "${DB_NAME}" \
  -t 'analysis_results|analysis_jobs|safe_zone_plugins|appeals|author_reputation|karma_events|badges|author_badges|leaderboard_cache|notifications|analysis_config|config_change_log' \
  --format=custom \
  --compress=9 \
  --file="${BACKUP_DIR}/${TIMESTAMP}/analysis-full.dump"

# Generate checksum manifest
cd "${BACKUP_DIR}/${TIMESTAMP}"
sha256sum *.dump > checksums.sha256

# Clean up backups older than retention period
find "${BACKUP_DIR}" -maxdepth 1 -type d -mtime +30 -exec rm -rf {} \;
```

### Incremental Hourly Backup (WAL archiving)

The primary backup mechanism is **continuous WAL archiving**. The table-level dumps above provide point-in-time snapshots; WAL archiving enables point-in-time recovery (PITR).

```bash
# postgresql.conf settings for WAL archiving
archive_mode = on
archive_command = 'test ! -f /var/backups/claudeforge/wal/%f && cp %p /var/backups/claudeforge/wal/%f'
archive_timeout = 60  # Force archive every 60s even on idle systems
```

---

## Schedule

| Frequency | Time | Type | Retention |
|-----------|------|------|-----------|
| Hourly | Every hour at :05 | WAL archive | 7 days |
| Daily | 02:00 UTC | Full table dump (custom format) | 30 days |
| Weekly | Sunday 03:00 UTC | Full table dump + vacuum analyze | 90 days |
| Monthly | 1st 04:00 UTC | Archive dump (offsite copy) | 12 months |

---

## Retention Policy

| Backup Type | Local Retention | Offsite (S3) Retention |
|-------------|----------------|------------------------|
| WAL archives | 7 days | 30 days |
| Daily dumps | 30 days | 90 days |
| Weekly dumps | 90 days | 12 months |
| Monthly dumps | 12 months | 36 months |

---

## Test Restore Procedure

### Monthly Restore Drill (automated)

```bash
#!/usr/bin/env bash
# Monthly restore test — validates backup integrity
# Schedule: 1st of month at 05:00 UTC

set -euo pipefail

BACKUP_FILE="${1:-/var/backups/claudeforge/analysis/latest/analysis-full.dump}"
TEST_DB="claudeforge_restore_test_$(date +%Y%m%d)"

echo "=== Restore Drill: $(date) ==="

# Step 1: Verify checksum
echo "[1/5] Verifying checksum..."
cd "$(dirname "${BACKUP_FILE}")"
sha256sum -c checksums.sha256 --ignore-missing

# Step 2: Create test database
echo "[2/5] Creating test database..."
createdb "${TEST_DB}"

# Step 3: Restore from dump
echo "[3/5] Restoring dump..."
pg_restore \
  -d "${TEST_DB}" \
  --format=custom \
  --verbose \
  "${BACKUP_FILE}" 2>&1 | tail -20

# Step 4: Validate row counts
echo "[4/5] Validating row counts..."
TABLES=(
  analysis_results analysis_jobs safe_zone_plugins appeals
  author_reputation karma_events badges author_badges
  leaderboard_cache notifications analysis_config config_change_log
)
for table in "${TABLES[@]}"; do
  COUNT=$(psql -d "${TEST_DB}" -t -c "SELECT COUNT(*) FROM ${table};")
  echo "  ${table}: ${COUNT} rows"
done

# Step 5: Verify constraints
echo "[5/5] Checking foreign keys and constraints..."
psql -d "${TEST_DB}" -c "SELECT 'All tables OK' AS status WHERE (
  SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'
) = (
  SELECT COUNT(DISTINCT table_name) FROM (
    SELECT unnest(ARRAY['analysis_results','analysis_jobs','safe_zone_plugins',
      'appeals','author_reputation','karma_events','badges','author_badges',
      'leaderboard_cache','notifications','analysis_config','config_change_log'])
  ) AS t(table_name)
);"

# Cleanup
echo "[Done] Dropping test database..."
dropdb "${TEST_DB}"
echo "=== Restore Drill Complete: $(date) ==="
```

### Manual Restore (Emergency)

```bash
# 1. Identify the target backup
ls -la /var/backups/claudeforge/analysis/
RESTORE_FILE="/var/backups/claudeforge/analysis/20260101T020000Z/analysis-full.dump"

# 2. Restore to a new database
createdb claudeforge_restore_target
pg_restore -d claudeforge_restore_target --format=custom --verbose "${RESTORE_FILE}"

# 3. Verify data integrity
psql -d claudeforge_restore_target -c "
  SELECT table_name, (xpath('/row/count/text()', query_to_xml(
    'SELECT count(*) FROM ' || table_name, true, false, ''
  )))[1]::text::int AS row_count
  FROM information_schema.tables
  WHERE table_schema = 'public'
  ORDER BY table_name;
"

# 4. Rename to production (short downtime)
psql -c "ALTER DATABASE plugin_marketplace RENAME TO plugin_marketplace_old;"
psql -c "ALTER DATABASE claudeforge_restore_target RENAME TO plugin_marketplace;"
```

---

## Monitoring & Alerting

- **Backup success/failure**: monitored via cron exit code → systemd journal
- **Backup age alert**: Prometheus `time() - backup_timestamp_seconds > 86400` → Alertmanager
- **WAL archive lag**: `pg_stat_archiver` → Grafana dashboard
- **Restore drill failure**: GitHub issue auto-created via webhook

---

## Implementation Checklist

- [ ] Configure WAL archiving in `postgresql.conf`
- [ ] Create backup directory: `sudo mkdir -p /var/backups/claudeforge/{analysis,wal}`
- [ ] Set permissions: `sudo chown -R postgres:postgres /var/backups/claudeforge`
- [ ] Deploy daily backup script to cron
- [ ] Deploy monthly restore drill to cron
- [ ] Configure offsite sync to S3 (or equivalent)
- [ ] Test restore from backup (manual drill, first month)
- [ ] Add Prometheus backup-age metric exporter
