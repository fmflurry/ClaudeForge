# ─── Production multi-stage Dockerfile for Angular frontend ──────────────────
# Stage 1 (build): Node.js + Angular CLI — produces optimised static bundle
# Stage 2 (runtime): nginx serving the bundle with runtime-injected API base URL
#
# Runtime environment variable:
#   API_BASE_URL  — injected via entrypoint.sh into assets/env-config.js
#                   Default: http://localhost:5010/api/v1
#
# Build: docker build -t claudeforge-web:latest ./frontend
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM node:22-alpine AS build

WORKDIR /app

# Install dependencies with layer caching
COPY package.json package-lock.json* ./
RUN npm ci --prefer-offline

# Copy source and build for production
COPY . .
RUN npx ng build --configuration production

# ── Stage 2: nginx runtime ────────────────────────────────────────────────────
FROM nginx:1.27-alpine AS runtime

# Install envsubst (provided by gettext) for runtime env injection
RUN apk add --no-cache gettext

# Copy compiled Angular app from build stage.
# Angular 17+ outputs to dist/<project-name>/browser
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html

# nginx configuration: handle SPA routing (404 → index.html)
COPY <<'EOF' /etc/nginx/conf.d/default.conf
server {
    listen       80;
    server_name  _;
    root         /usr/share/nginx/html;
    index        index.html;

    # SPA fallback — all non-file requests serve index.html
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets aggressively; Angular appends content hash to filenames
    location ~* \.(js|css|woff2?|ttf|eot|svg|ico|png|jpg|jpeg|gif|webp)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }

    # Health probe for Docker healthcheck + load balancer
    location /nginx-health {
        access_log off;
        return 200 "healthy\n";
        add_header Content-Type text/plain;
    }
}
EOF

# Runtime env injection entrypoint:
#   Generates /usr/share/nginx/html/assets/env-config.js from env vars before nginx starts.
#   Angular app bootstraps with window['env'] from this file.
COPY <<'ENTRYPOINT' /docker-entrypoint.d/40-env-config.sh
#!/bin/sh
set -e
mkdir -p /usr/share/nginx/html/assets
cat > /usr/share/nginx/html/assets/env-config.js <<JS
(function(window) {
  window["env"] = window["env"] || {};
  window["env"]["API_BASE_URL"] = "${API_BASE_URL:-http://localhost:5010/api/v1}";
}(window));
JS
envsubst < /usr/share/nginx/html/assets/env-config.js > /tmp/env-config.js
mv /tmp/env-config.js /usr/share/nginx/html/assets/env-config.js
ENTRYPOINT

RUN chmod +x /docker-entrypoint.d/40-env-config.sh

EXPOSE 80

# nginx base image handles the CMD/entrypoint
