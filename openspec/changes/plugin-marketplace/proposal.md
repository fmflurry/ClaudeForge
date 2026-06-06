# Plugin Marketplace Proposal

## Why

Claude Code's ecosystem lacks a central, open marketplace for sharing plugins, skills, and hooks. Teams and developers reinvent the wheel, duplicating functionality and slowing adoption. ClaudeForge addresses this gap with a free, no-paywall hub that lowers barriers to sharing and discovery: guided plugin creation captures metadata the AI ecosystem needs, a CLI/SDK makes installation frictionless, and semantic search helps users find exactly what they need. With growing Claude Code adoption and no existing open marketplace, now is the time to build.

## What Changes

**New Frontend Application**
- Angular 22 single-page app for browsing, uploading, and managing plugins
- Polished dashboard showing installed plugins and versions
- Team context persisted in browser storage (no login required)

**New Backend REST API**
- .NET Core (latest) with Clean Architecture (application/domain/infrastructure layers)
- Endpoints for plugin CRUD, search, versioning, categorization
- Anonymized telemetry collection endpoints for plugin downloads, installs, and usage aggregation (privacy-first, no PII)
- Optional Qdrant integration for semantic plugin discovery
- PostgreSQL for plugin metadata, versions, user submissions, and aggregated telemetry

**New CLI/SDK Package**
- Command-line tool to add, remove, list, and update plugins
- Integrated with the backend API
- Enables one-command plugin installation on developer machines

**New Plugin Template & Scaffolding**
- Starter template so developers easily create and submit plugins
- Guided, structured creation capturing all metadata the ecosystem needs
- Bootstrap script to get contributors up and running

**Infrastructure**
- Docker containerization bundling frontend, backend, PostgreSQL, and optional Qdrant
- Designed for portable dev→prod deployment on OVH
- Single compose file for local and production environments

## Capabilities

### New Capabilities

- `plugin-catalog` — Browse and list plugins with metadata, descriptions, and version history (MVP)
- `plugin-upload` — Submit and upload new plugins without authentication (MVP)
- `plugin-download` — Download and install plugins via web UI or CLI (MVP)
- `plugin-search` — Full-text search plugins by name, description, and metadata; optional semantic search via Qdrant (MVP)
- `plugin-versioning` — Track, publish, and manage multiple versions of each plugin; auto-increment and release notes (MVP)
- `plugin-categorization` — Filter and browse plugins by type, language, and use-case (dev team, PO, PM, DevOps) (MVP)
- `plugin-telemetry` — Collect anonymized telemetry on plugin downloads, installs, usage frequency, and efficiency metrics; privacy-first, no PII (download/install counters are MVP; aggregated usage & efficiency analytics are Phase 2)
- `cli-sdk` — CLI tool to add, remove, list, and update plugins from the command line (MVP)
- `plugin-template` — Scaffolding template and guided creation flow for plugin authors (MVP)
- `dashboard` — Polished UI showing installed plugins, versions, update status, and team context (MVP)
- `discovery-service` — Find suitable plugins by name, criteria, or semantic intent (MVP)
- `docs` — Clear, searchable documentation for users installing, configuring, and publishing plugins (MVP)
- `team-context` — Persist team membership and plugin preferences in browser storage without authentication (MVP)
- `plugin-validation` — Quality, security, and format validation of submitted plugins before publishing (Phase 2)
- `plugin-security-scan` — Automated security scanning to ensure plugins pose no risk to users or data (Phase 2)
- `ratings-reviews` — Community ratings, comments, and recommendations on plugins (Phase 2)
- `notifications` — Alert users to new versions of installed plugins or newly published relevant plugins (Phase 2)
- `recommendation-engine` — Suggest plugins based on user code context, language, and usage patterns (Phase 3)
- `dependency-management` — Declare, resolve, and manage plugin dependencies (Phase 3)
- `advanced-search` — Multi-criteria filtering and query builder for specialized plugin discovery (Phase 3)

### Modified Capabilities

None — greenfield repository, no existing specs to modify.

## Impact

**New Codebases**
- **Frontend**: Angular 22 SPA, standalone components, reactive stores, Clean Architecture (presentation/application/domain layers)
- **Backend**: .NET Core REST API with Clean Architecture, minimal endpoints for MVP
- **CLI/SDK**: Node.js or Rust package distributed via npm/cargo; wraps backend API
- **Database**: PostgreSQL schema for plugins, versions, metadata, user submissions, and team context
- **Vector Store**: Optional Qdrant deployment for semantic search (deferred to Phase 2 if MVP shows demand)
- **DevOps**: Docker Compose (dev) and single-container production image (OVH-deployable)
- **Documentation**: Contributor guide, user guide, API reference

**No Breaking Changes**
- Greenfield repository; no existing code impacted

**Telemetry & Analytics**
- Aggregate tables in PostgreSQL for download/install counters and usage metrics
- Privacy-first: anonymized/hashed client IDs, no PII, opt-out respected
- Time-series data store optional in Phase 2 for deeper efficiency analytics

**Future Authentication**
- Authentication layer deferred; all features accessible without login for MVP
- Can be layered in (identity provider, JWT) in Phase 2 if needed

**Team & Skills**
- Reuses existing user Clean Architecture patterns (Angular + .NET)
- Leverages PostgreSQL expertise
- Introduces Docker, Qdrant, and semantic search as optional Phase 2 additions
