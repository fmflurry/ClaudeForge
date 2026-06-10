#!/usr/bin/env bash
# ─── Full ClaudeForge Deployment Script ─────────────────────────────────────
# Orchestrates multi-phase deployment: API, frontend, CLI, and analysis workers.
#
# Usage:
#   ./deploy.sh                         # full deployment
#   DEPLOY_PHASE=api ./deploy.sh        # deploy only API
#   DEPLOY_PHASE=analysis ./deploy.sh   # deploy only analysis workers
#
# Required environment variables:
#   CONNECTION_STRING   PostgreSQL connection string
#
# Optional environment variables:
#   DEPLOY_PHASE        Comma-separated phases: api,frontend,cli,analysis (default: all)
#   DOCKER_HOST         Docker socket path
#   API_KEY             Service auth key for health checks
#   IMAGE_TAG           Docker image tag (default: latest)
# ─────────────────────────────────────────────────────────────────────────────

set -euo pipefail

# ── Color helpers ──────────────────────────────────────────────────────────────
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m'

info()    { echo -e "${GREEN}[INFO]${NC}   $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC}   $*"; }
error()   { echo -e "${RED}[ERROR]${NC}  $*" >&2; }
section() { echo ""; echo -e "${CYAN}═══════════════════════════════════════════════${NC}"; echo -e "${CYAN}  $*${NC}"; echo -e "${CYAN}═══════════════════════════════════════════════${NC}"; }

# ── Config ────────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../../.." && pwd)"
DEPLOY_PHASE="${DEPLOY_PHASE:-all}"
IMAGE_TAG="${IMAGE_TAG:-latest}"

# ── Validate ──────────────────────────────────────────────────────────────────
if [ -z "${CONNECTION_STRING:-}" ]; then
    error "CONNECTION_STRING is required"
    exit 1
fi
export CONNECTION_STRING

# ── Helper: run phase if selected ──────────────────────────────────────────────
should_run() {
    local phase="$1"
    if [ "${DEPLOY_PHASE}" = "all" ]; then
        return 0
    fi
    [[ ",${DEPLOY_PHASE}," == *",${phase},"* ]]
}

# ── Phase: API ─────────────────────────────────────────────────────────────────
deploy_api() {
    section "Phase: API Deployment"

    info "Building API image..."
    docker build \
        -t "claudeforge/api:${IMAGE_TAG}" \
        -f "${PROJECT_ROOT}/backend/Dockerfile" \
        "${PROJECT_ROOT}/backend"

    info "Running API database migrations..."
    pushd "${PROJECT_ROOT}/backend" >/dev/null
    dotnet ef database update \
        --project ClaudeForge.Infrastructure/ClaudeForge.Infrastructure.csproj \
        --startup-project ClaudeForge.Api/ClaudeForge.Api.csproj \
        --connection "${CONNECTION_STRING}"
    popd >/dev/null

    info "Deploying API service..."
    docker compose -f "${PROJECT_ROOT}/infra/docker-compose.yml" up -d api

    info "Checking API health..."
    for i in $(seq 1 12); do
        sleep 5
        HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:5010/health" 2>/dev/null || echo "000")
        if [ "${HTTP_CODE}" = "200" ]; then
            info "  API health check: OK"
            break
        fi
        if [ "${i}" = "12" ]; then
            error "API health check failed after 60s"
            return 1
        fi
        info "  Waiting... (attempt ${i}/12, HTTP ${HTTP_CODE})"
    done
}

# ── Phase: Frontend ────────────────────────────────────────────────────────────
deploy_frontend() {
    section "Phase: Frontend Deployment"

    info "Building frontend image..."
    docker build \
        -t "claudeforge/frontend:${IMAGE_TAG}" \
        -f "${PROJECT_ROOT}/frontend/Dockerfile" \
        "${PROJECT_ROOT}"

    info "Deploying frontend service..."
    docker compose -f "${PROJECT_ROOT}/infra/docker-compose.yml" up -d web

    info "Checking frontend health..."
    for i in $(seq 1 12); do
        sleep 5
        HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://localhost:4000" 2>/dev/null || echo "000")
        if [ "${HTTP_CODE}" = "200" ]; then
            info "  Frontend health check: OK"
            break
        fi
        if [ "${i}" = "12" ]; then
            error "Frontend health check failed after 60s"
            return 1
        fi
        info "  Waiting... (attempt ${i}/12, HTTP ${HTTP_CODE})"
    done
}

# ── Phase: CLI ─────────────────────────────────────────────────────────────────
deploy_cli() {
    section "Phase: CLI Deployment"
    info "CLI is distributed via npm registry. Run:"
    info "  cd cli && npm publish"
    info "Skipping container deployment (npm package)."
}

# ── Phase: Analysis Workers ────────────────────────────────────────────────────
deploy_analysis() {
    section "Phase: Analysis Worker Deployment"

    if [ ! -f "${SCRIPT_DIR}/deploy-analysis.sh" ]; then
        error "deploy-analysis.sh not found in ${SCRIPT_DIR}"
        return 1
    fi

    # Delegate to the specialized analysis deployment script
    # Pass through all relevant environment variables
    DOCKER_HOST="${DOCKER_HOST:-unix:///var/run/docker.sock}" \
    API_KEY="${API_KEY:-}" \
    IMAGE_TAG="${IMAGE_TAG}" \
    CONNECTION_STRING="${CONNECTION_STRING}" \
    "${SCRIPT_DIR}/deploy-analysis.sh"
}

# ── Main ───────────────────────────────────────────────────────────────────────
main() {
    section "ClaudeForge Full Deployment (tag: ${IMAGE_TAG})"
    info "Phases: ${DEPLOY_PHASE}"

    # Run phases in dependency order
    if should_run "api";      then deploy_api; fi
    if should_run "frontend"; then deploy_frontend; fi
    if should_run "cli";      then deploy_cli; fi
    if should_run "analysis"; then deploy_analysis; fi

    section "Deployment Complete"
    info "All requested phases deployed successfully."
    echo ""
    info "Services:"
    echo "  API:       http://localhost:5010/health"
    echo "  Frontend:  http://localhost:4000"
    echo "  Workers:   http://localhost:8081/health"
    echo "  Metrics:   http://localhost:8081/metrics"
    echo ""
}

main "$@"
