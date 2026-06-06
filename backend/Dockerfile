# ─── Production multi-stage Dockerfile for .NET API ─────────────────────────
# Stage 1 (build): restore + publish to a self-contained release layout
# Stage 2 (runtime): minimal aspnet runtime image — no SDK, no source
#
# Build: docker build -t claudeforge-api:latest ./backend
# ─────────────────────────────────────────────────────────────────────────────

# ── Stage 1: build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy project manifests first for layer-cached restore
COPY ClaudeForge.Core/*.csproj ClaudeForge.Core/
COPY ClaudeForge.Application/*.csproj ClaudeForge.Application/
COPY ClaudeForge.Infrastructure/*.csproj ClaudeForge.Infrastructure/
COPY ClaudeForge.Api/*.csproj ClaudeForge.Api/

RUN dotnet restore ClaudeForge.Api/ClaudeForge.Api.csproj

# Copy source
COPY . .

# Publish: Release configuration, no debug symbols, self-contained=false (aspnet runtime image handles runtime)
RUN dotnet publish ClaudeForge.Api/ClaudeForge.Api.csproj \
    -c Release \
    -o /publish \
    --no-restore

# ── Stage 2: runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Non-root user for security hardening
RUN adduser --disabled-password --gecos "" appuser
WORKDIR /app

# Copy published output
COPY --from=build /publish ./

# /packages will be mounted as a volume (LocalFileSystem mode) or unused (OVH mode)
RUN mkdir /packages && chown appuser:appuser /packages

USER appuser

EXPOSE 8080

ENTRYPOINT ["dotnet", "ClaudeForge.Api.dll"]
