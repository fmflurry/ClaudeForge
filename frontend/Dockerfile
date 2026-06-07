# ─── Production multi-stage Dockerfile for Angular SSR frontend ───────────────
# Stage 1 (build): Node.js + Angular CLI — produces SSR bundle
# Stage 2 (runtime): Node.js serving the SSR bundle via Express
#
# Build context: repo root (required — frontend is an npm workspace and its
# dependencies are installed at root level, not in frontend/node_modules).
#
# Runtime environment variables:
#   PORT             — HTTP port the Node server listens on (default: 4000)
#   SSR_API_BASE_URL — Internal API URL for server-side HTTP calls
#   NG_ALLOWED_HOSTS — Comma-separated allowed host header values
#
# Build: docker build -f frontend/Dockerfile -t claudeforge-web:latest .
#        (run from repo root)
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM node:22-alpine AS build

WORKDIR /repo

# Copy workspace manifests (root + frontend workspace) for dependency install.
# Root package.json declares workspaces; root package-lock.json is the complete
# lockfile that includes all workspace packages.
COPY package.json package-lock.json* ./
COPY frontend/package.json ./frontend/

# Install all workspace dependencies from the authoritative root lockfile.
# --legacy-peer-deps: angular-eslint@21 has a peer constraint on @angular/cli <22,
# while this project uses @angular/cli@22 — the conflict is benign and pre-existing.
RUN npm ci --workspace=frontend --legacy-peer-deps

# Copy frontend source
COPY frontend/ ./frontend/

# Build SSR bundle (produces dist/frontend/browser + dist/frontend/server)
WORKDIR /repo/frontend
RUN npm run build

# ── Stage 2: Node SSR runtime ─────────────────────────────────────────────────
FROM node:22-alpine AS runtime

WORKDIR /app

# Install only production dependencies in an isolated environment.
# We copy the frontend package.json and root lockfile so npm can resolve
# just the production deps declared in frontend/package.json.
COPY package.json package-lock.json* ./
COPY frontend/package.json ./frontend/
RUN npm ci --workspace=frontend --omit=dev --legacy-peer-deps

# Copy the compiled SSR + browser bundles from the build stage.
# Layout expected by dist/frontend/server/server.mjs:
#   dist/frontend/server/server.mjs  ← SSR entry point
#   dist/frontend/browser/           ← static assets served by Express
COPY --from=build /repo/frontend/dist/frontend ./dist/frontend

# The SSR server resolves its express/angular imports from node_modules at the
# workspace root. Point NODE_PATH so Node finds them at runtime.
ENV NODE_PATH=/app/node_modules

EXPOSE 4000

ENV PORT=4000

CMD ["node", "dist/frontend/server/server.mjs"]
