#!/usr/bin/env bash
# ─── Analysis Pipeline Deployment Script ────────────────────────────────────
# Deploys the analysis worker service: pulls image, runs migrations, scales up.
#
# Usage:
#   ./deploy-analysis.sh               # uses env vars from current session
#   CONNECTION_STRING="..." ./deploy-analysis.sh  # override connection string
#
# Required environment variables:
#   CONNECTION_STRING   PostgreSQL connection string for the target database
#
# Optional environment variables:
#   DOCKER_HOST         Docker socket path (default: unix:///var/run/docker.sock)
#   API_KEY             Service auth key for health-check endpoint
#   IMAGE_TAG           Docker image tag (default: latest)
#   COMPOSE_FILE        Path to docker-compose file (default: see below)
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

# ── Color output helpers ─────────────────────────────────────────────────────
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

info()  { echo -e "${GREEN}[INFO]${NC}  $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error() { echo -e "${RED}[ERROR]${NC} $*" >&2; }

# ── Validate environment ──────────────────────────────────────────────────────
REQUIRED_VARS=("CONNECTION_STRING")
for var in "${REQUIRED_VARS[@]}"; do
    if [ -z "${!var:-}" ]; then
        error "Missing required environment variable: ${var}"
        exit 1
    fi
done

# Defaults
DOCKER_HOST="${DOCKER_HOST:-unix:///var/run/docker.sock}"
API_KEY="${API_KEY:-}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-${SCRIPT_DIR}/docker-compose.analysis.yml}"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../../.." && pwd)"

# ── Step 1: Pull latest image ──────────────────────────────────────────────────
info "Step 1/5: Pulling latest analysis-worker image..."
docker pull "claudeforge/analysis-worker:${IMAGE_TAG}"

# ── Step 2: Run EF Core migrations ─────────────────────────────────────────────
info "Step 2/5: Running database migrations..."
# Run migrations from the API project (migrations are in Infrastructure)
# Requires dotnet-ef tool or the published migration bundle.
if command -v dotnet &>/dev/null; then
    pushd "${PROJECT_ROOT}/backend" >/dev/null

    # Ensure EF Core tool is available
    if ! dotnet tool list --global | grep -q dotnet-ef; then
        info "Installing dotnet-ef tool..."
        dotnet tool install --global dotnet-ef
    fi

    dotnet ef database update \
        --project ClaudeForge.Infrastructure/ClaudeForge.Infrastructure.csproj \
        --startup-project ClaudeForge.Api/ClaudeForge.Api.csproj \
        --connection "${CONNECTION_STRING}"

    popd >/dev/null
else
    # Fallback: run migration bundle if pre-built
    MIGRATION_BUNDLE="${PROJECT_ROOT}/backend/ClaudeForge.Infrastructure/MigrationBundle"
    if [ -f "${MIGRATION_BUNDLE}" ]; then
        "${MIGRATION_BUNDLE}" --connection "${CONNECTION_STRING}"
    else
        warn "dotnet CLI not available and no migration bundle found. Skipping migrations."
        warn "Run migrations manually or pre-build a migration bundle."
    fi
fi

# ── Step 3: Deploy analysis workers ────────────────────────────────────────────
info "Step 3/5: Deploying analysis workers..."
export DOCKER_HOST
export CONNECTION_STRING

docker compose \
    -f "${COMPOSE_FILE}" \
    up -d \
    --scale analysis-worker="${WORKER_COUNT:-2}"

# ── Step 4: Health check ───────────────────────────────────────────────────────
info "Step 4/5: Running health check..."
HEALTHY=false
for i in $(seq 1 12); do
    sleep 5
    HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
        "${API_KEY:+-H X-API-Key: ${API_KEY}}" \
        "http://localhost:8081/health" 2>/dev/null || echo "000")

    if [ "${HTTP_CODE}" = "200" ]; then
        HEALTHY=true
        break
    fi
    info "  Waiting for worker to become healthy... (attempt ${i}/12, HTTP ${HTTP_CODE})"
done

if [ "${HEALTHY}" = false ]; then
    error "Health check failed: worker did not return 200 within 60 seconds"
    error "Check worker logs: docker compose -f ${COMPOSE_FILE} logs analysis-worker"
    exit 1
fi
info "  Worker health check: OK"

# ── Step 5: Verify metrics endpoint ────────────────────────────────────────────
info "Step 5/5: Verifying metrics endpoint..."
METRICS=$(curl -s "${API_KEY:+-H X-API-Key: ${API_KEY}}" \
    "http://localhost:8081/metrics" 2>/dev/null || echo "")

if echo "${METRICS}" | grep -q "analysis_queue_size"; then
    info "  Metrics endpoint: OK (analysis_queue_size found)"
else
    warn "  Metrics endpoint may not be exposing analysis metrics"
    warn "  Response excerpt: $(echo "${METRICS}" | head -5)"
fi

info "=== Deployment complete ==="
echo ""
echo "  Workers:         http://localhost:8081 (scaled: ${WORKER_COUNT:-2})"
echo "  Metrics:         http://localhost:8081/metrics"
echo "  Health check:    http://localhost:8081/health"
echo ""
