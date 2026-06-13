# Plugin Marketplace Design

## Context

**Current State (Greenfield)**
ClaudeForge is a brand-new, open-source repository designed to build a central plugin marketplace for Claude Code. No existing code or infrastructure exists; the marketplace will be built from scratch using battle-tested architecture patterns and team conventions.

**Constraints**
- **Infrastructure**: Single OVH VM, PostgreSQL primary data store, optional Qdrant for Phase 2+
- **Technology Stack**: Angular 22 (standalone components), .NET 8 Core, Node.js (CLI), PostgreSQL, Docker Compose
- **Authentication**: None for MVP (privacy-first, browser-storage only)
- **Accessibility**: Free, open, no paywall, CLI required, opt-in privacy-respecting telemetry
- **Browser-Only Constraint**: Web dashboard never writes to developer filesystem; real plugin installation happens only via CLI

**Global Engineering Rules** (from CLAUDE.md + project conventions)
- No `any` types in TypeScript
- Angular components interact with use-cases exclusively through facades (never directly)
- Immutability: all data transformations produce new objects, never mutate in-place
- Many small files: high cohesion, low coupling; typical 200–400 LOC per file, max 800
- Clean Architecture everywhere:
  - Frontend: presentation → application (facades/stores) → domain (models/ports) → infrastructure (adapters)
  - Backend: Application (driving/HTTP) → Core (domain hexagon) → Infrastructure (driven/EF, object storage)
- Structured error handling: RFC 7807 ProblemDetails, domain exceptions with user-friendly messages
- Validated at system boundaries: FluentValidation on inputs, server-authoritative

---

## Goals / Non-Goals

### Goals

Ship the 13 MVP capabilities with clean seams for Phase 2 and Phase 3:
1. **plugin-catalog** — Browse, list, and detail plugins with metadata and version history
2. **plugin-upload** — Submit and upload new plugins without authentication
3. **plugin-download** — Download and install plugins via web UI or CLI
4. **plugin-search** — Full-text search plugins by name, description, and metadata (PostgreSQL FTS MVP; Qdrant optional Phase 2)
5. **plugin-versioning** — Track, publish, and manage multiple versions per plugin with release notes
6. **plugin-categorization** — Filter by type (skill, hook, agent, command, plugin), language, and use-case
7. **plugin-telemetry** — Anonymized download/install counters and aggregated metrics (privacy-first, no PII)
8. **cli-sdk** — CLI tool (install, remove, list, update, search, publish, scaffold, validate)
9. **plugin-template** — Scaffolding template and guided creation flow (language templates: TS, Python, Go, Rust)
10. **dashboard** — Polished UI showing installed plugins, versions, update availability, and team context (browser-storage backed)
11. **discovery-service** — Find plugins by name, criteria, or semantic intent
12. **docs** — Searchable documentation for users and plugin authors
13. **team-context** — Persist team membership and preferences in browser storage (no login)

All 13 wrapped in clean module boundaries, outgoing ports (dependency inversion), and test-seams for Phase 2 additions (auth, ratings, recommendations, dependency resolution, security scanning).

### Non-Goals (Explicitly Deferred)

- **Authentication**: No login, JWT, or identity provider for MVP. Deferred to Phase 2 if needed; team context lives in browser storage only.
- **Ratings & Reviews**: Community feedback deferred to Phase 2.
- **Notifications**: Alerts for new versions or recommended plugins deferred to Phase 2.
- **Recommendation Engine**: ML/LLM-powered suggestions deferred to Phase 3.
- **Dependency Resolution & Auto-Install**: Declare in manifest, display/validate in CLI, but do not resolve/auto-install in MVP. Resolution seam added Phase 3.
- **Security Scanning**: Automated vulnerability/malware scanning deferred to Phase 2.
- **Advanced Search**: Multi-criteria query builder deferred to Phase 3.
- **Qdrant Integration**: Optional vector search deferred to Phase 2; PostgreSQL FTS covers MVP.

---

## Decisions

### 1. Backend Layering: .NET Clean Architecture with Hexagonal Ports/Adapters

**Decision**

Organize the .NET backend as three layers with one composition root and reflection-discovered modules:
- **Application** (driving layer): HTTP Minimal API endpoints, FluentValidation request validators, AutoMapper for DTO↔domain
- **Core** (domain hexagon): Zero infrastructure references, pure business logic, domain entities, outgoing ports (interfaces)
- **Infrastructure** (driven layer): EF Core entities/DbContext, object storage adapter, Qdrant adapter, concrete port implementations

Modules (each with its own Application + Core boundary):
1. **PluginCatalog** (plugin-catalog, plugin-categorization) — List, get, filter, and categorize plugins
2. **PluginPublishing** (plugin-upload, plugin-versioning) — Upload, publish, and version management
3. **PluginDistribution** (plugin-download) — Download and package delivery
4. **PluginSearch** (plugin-search, discovery-service) — Full-text and semantic search with ranking
5. **Telemetry** (plugin-telemetry) — Ingest events, aggregation, counters
6. **Docs** (docs) — Plugin documentation indexing and retrieval

Shared infrastructure under `Core/` (shared kernel) and `Infrastructure/`:
- `Core/Data/Entities` — EF entity models used by all modules
- `Infrastructure/Persistence/Context` — DbContext
- `Infrastructure/Adapters` — Port implementations (search, storage, telemetry)

**Why**

- Aligns with `dotnet-clean-architecture` skill and team conventions
- Module isolation via reflection (composition root wires dependencies) + strict compile-time boundaries (NetArchTest enforcer)
- Each module owns its Application layer and exposes outgoing ports; no cross-module type references
- Ports/adapters enable swapping storage (local FS ↔ OVH S3), search (Postgres FTS ↔ Qdrant), with zero change to domain logic

**Alternatives Considered**

- **Classic four-project split** (API.csproj, Core.csproj, Infrastructure.csproj, Data.csproj): Heavier, slower CI, more ceremony — rejected
- **One module per noun** (Plugin, Version, Category modules): Fragments coupled operations; upload touches multiple modules, violating cohesion — rejected
- **Vertical slice / MediatR**: Diverges from skill definition and team patterns — rejected

**Risk → Mitigation**

- **Module isolation is convention-only without enforcement**: Mitigate with NetArchTest as part of CI; enforce Core has no Infra/EF references, no cross-module type references in domain/application
- **Upload spanning concerns**: Keep in PluginPublishing module; expose catalog reads to other modules via outgoing `IPluginRepositoryPort` interface only

---

### 2. Frontend Layering: Angular 22 Standalone, Clean Architecture with Facades & Signal Stores

**Decision**

Organize the Angular frontend as standalone components with four layers, signal-based stores, and domain-driven feature modules:
- **Presentation**: Standalone components, OnPush change detection, inject() for dependency provision
- **Application**: Facades (use-case orchestrators) and signal-based stores (ResourceState<T> pattern)
- **Domain**: Models, business rules (pure functions), outgoing ports (interfaces), mappers
- **Infrastructure**: HTTP adapters (generated from backend OpenAPI), localStorage adapters, external API calls

Feature domains (each with isolated store/facade/domain):
1. **Catalog** — Browse, list, detail, version history, category filters → `CatalogFacade`
2. **Search** — Keyword search + discovery criteria builder, ranking display → `SearchFacade`
3. **Dashboard** — Installed plugin view (browser storage backed), update availability, install/remove intent orchestration → `DashboardFacade`
4. **Team Context** — Team identifier, recommendation scoping (localStorage only, no HTTP) → `TeamContextFacade`
5. **Telemetry** — Client-side anonymous ID generation, opt-out toggle, fire-and-forget event POST → `TelemetryFacade`
6. **Docs** — Documentation tree, full-text doc search, plugin doc tab → `DocsFacade`

Cross-domain communication via Context Registry (published events) only; no direct service injection between domains.

Browser-storage adapters implement domain ports:
- `TeamContextStoragePort` ← localStorage team ID
- `TelemetryPreferencePort` ← localStorage opt-out toggle
- `InstalledPluginsStoragePort` ← localStorage installed plugin registry

This makes localStorage swappable and fully testable without real browser storage in unit tests.

**Why**

- Applies `angular-clean-architecture` skill: DDD domains, ports/adapters, no any type, facade boundary mandatory
- Signal-based stores (`input()` signals, `effect()`) are performant and eliminate change-detection ceremonies
- Browser-storage-as-ports keeps browser-only constraint clean and testable
- Facades enforce that components never touch use-cases directly (team rule)

**Alternatives Considered**

- **NgRx**: Legacy pattern, overly complex for MVP, introduces observable boilerplate — rejected
- **Service-with-BehaviorSubject**: Legacy pattern, no encapsulation, encourages direct mutation — rejected
- **Server-side installed state**: Violates no-auth and browser-only constraint — rejected

**Risk → Mitigation**

- **Dashboard install model (RESOLVED)**: Web dashboard records installed plugins + versions in `InstalledPluginsStoragePort` (localStorage adapter) and offers a download link. The **CLI performs the real on-disk install**. DashboardFacade tracks intent and browser-storage state only; it never writes to the developer filesystem. Mitigate by isolating install-intent from install-execution in facade design.

---

### 3. Plugin Storage Model: Object Storage + Immutable Versioning + PostgreSQL Metadata

**Decision**

Store plugin package artifacts (tar.gz and zip) in object storage via `IPackageStoragePort`:
- **Dev adapter**: Local filesystem volume (Docker bind-mount)
- **Prod adapter**: OVH Object Storage (S3-compatible via AWS SDK)

Packages are **immutable** and keyed as `plugins/{pluginId}/{version}/package.tar.gz` with:
- SHA-256 checksum
- Size in bytes
- Metadata (format, released timestamp) in PostgreSQL

Plugin metadata, versions, counters, and telemetry stored in PostgreSQL (never blobs in the database).

**PostgreSQL Schema Sketch**

```sql
-- Core plugin catalog
CREATE TABLE plugins (
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name TEXT NOT NULL,
  name_normalized TEXT NOT NULL UNIQUE,  -- lowercase, normalized for dedup
  slug TEXT NOT NULL UNIQUE,             -- URL-friendly identifier
  description TEXT NOT NULL,
  author TEXT NOT NULL,
  download_count BIGINT NOT NULL DEFAULT 0,  -- denormalized cache, source of truth is aggregates
  search_vector TSVECTOR,                -- Full-text search vector
  created_at TIMESTAMPTZ DEFAULT NOW(),
  updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Versions and releases
CREATE TABLE plugin_versions (
  id UUID PRIMARY KEY,
  plugin_id UUID NOT NULL REFERENCES plugins ON DELETE CASCADE,
  version TEXT NOT NULL,                 -- Semantic version: 1.0.0
  version_sort BIGINT NOT NULL,          -- Computed sort key for semver DESC queries
  release_notes TEXT DEFAULT '',
  is_latest BOOLEAN DEFAULT FALSE,       -- Denormalized, kept in sync
  package_key TEXT NOT NULL,             -- Object storage path: plugins/{id}/{version}/package.tar.gz
  package_format TEXT NOT NULL,          -- 'tar.gz' | 'zip'
  size_bytes BIGINT NOT NULL,
  sha256 TEXT NOT NULL,
  download_count BIGINT DEFAULT 0,       -- Denormalized counter (aggregates are source of truth)
  readme_text TEXT,                      -- Extracted from package on upload
  released_at TIMESTAMPTZ DEFAULT NOW(),
  UNIQUE(plugin_id, version),
  -- Partial unique index ensures only one is_latest per plugin
  UNIQUE (plugin_id) WHERE is_latest = TRUE
);

-- Categorization (controlled vocab)
CREATE TABLE categories (
  id SMALLSERIAL PRIMARY KEY,
  dimension TEXT NOT NULL,               -- 'type' | 'language' | 'use_case'
  value TEXT NOT NULL,                   -- E.g., 'skill', 'hook', 'agent', 'command', 'plugin'
  display_name TEXT,
  description TEXT,
  UNIQUE(dimension, value)
);

-- Plugin-to-category mapping
CREATE TABLE plugin_categories (
  plugin_id UUID NOT NULL REFERENCES plugins ON DELETE CASCADE,
  category_id SMALLINT NOT NULL REFERENCES categories,
  PRIMARY KEY (plugin_id, category_id)
);

-- Telemetry: raw events (internal only, not exposed via API)
CREATE TABLE telemetry_events (
  id BIGSERIAL PRIMARY KEY,
  event_type TEXT NOT NULL,              -- 'download' | 'install'
  plugin_id UUID REFERENCES plugins ON DELETE SET NULL,
  version TEXT,                          -- Semantic version string
  anon_client_id CHAR(64),               -- SHA-256(UUID v4), never PII
  client_os TEXT,                        -- Coarse: 'darwin' | 'linux' | 'windows'
  client_arch TEXT,                      -- Coarse: 'x64' | 'arm64'
  occurred_at TIMESTAMPTZ DEFAULT NOW()
);

-- Telemetry: aggregated summaries (read-only views serve from these)
CREATE TABLE telemetry_aggregates (
  plugin_id UUID NOT NULL,
  version TEXT NOT NULL,                 -- '' (empty) = rollup across all versions
  event_type TEXT NOT NULL,              -- 'download' | 'install'
  count BIGINT DEFAULT 0,
  window_start DATE NOT NULL,            -- Daily aggregate window
  PRIMARY KEY (plugin_id, version, event_type, window_start),
  FOREIGN KEY (plugin_id) REFERENCES plugins
);

-- Indexes for performance
CREATE INDEX idx_plugins_search_vector ON plugins USING GIN(search_vector);
CREATE INDEX idx_plugin_versions_sort ON plugin_versions(plugin_id, version_sort DESC);
CREATE INDEX idx_telemetry_events_ts ON telemetry_events(occurred_at DESC);
CREATE INDEX idx_telemetry_events_plugin ON telemetry_events(plugin_id);
```

**Counter Integrity**

- Plugin and version download counts are **denormalized caches** for fast reads
- **Source of truth**: `telemetry_aggregates` table
- Counter increment: atomic `UPDATE telemetry_aggregates SET count=count+1` on POST /api/v1/telemetry/events
- Rollup job: nightly batch aggregates raw events into daily windows, then purges raw events >90 days old

**Documentation**

- README extracted from package (tar.gz root or plugin.json `docs_url` reference) on upload
- Stored in `plugin_versions.readme_text` for fast retrieval without unpacking
- Full-text indexed via search_vector update trigger on publish

**Why**

- Object storage decouples binaries from database, enables efficient streaming, small backups, and cache headers
- Immutable versioning (package never overwritten) ensures reproducibility and safe rollback
- Generated `version_sort` column makes semver-descending queries cheap (no in-app sorting)
- Raw + aggregate split: raw events are internal-only (never exposed), aggregates serve read endpoints and protect privacy
- 90-day retention for raw events balances analytics and privacy; aggregates kept indefinitely

**Alternatives Considered**

- **Blobs in PostgreSQL**: Bad for streaming large files, slow backups, couples data lifecycle — rejected
- **Pure filesystem in prod**: Not portable, no audit trail, hard to backup — rejected (dev-only)
- **Counters-only (no raw events)**: Loses Phase 2 analytics capability — rejected

**Risk → Mitigation**

- **Raw telemetry growth**: Mitigate with 90-day retention job; purge raw after aggregating into daily windows
- **Object storage outage**: Download endpoint returns 503; metadata reads stay up (separate systems)
- **Counter atomicity**: Use PostgreSQL transactions; increment in ingestion handler, not on download hot path (separate path prevents double-counting)

---

### 4. Search: PostgreSQL FTS MVP with Optional Qdrant Semantic Phase 2

**Decision**

MVP search uses PostgreSQL full-text search (FTS):
- `plugins.search_vector` = weighted `to_tsvector()` combining name (weight A), description (weight B), and tags (weight C)
- GIN index on `search_vector` for fast queries
- Ranking = `ts_rank()` blended with download count and recency (published date) as tiebreakers
- Filters = SQL `JOIN`/`WHERE` on `plugin_categories` for type, language, use-case
- Single outgoing port: `ISearchIndexPort`

**Adapters**:
- `PostgresSearchAdapter` (default, MVP)
- `QdrantSearchAdapter` (Phase 2, feature-flagged via config)

Qdrant integration is **hybrid**: FTS retrieves candidates, Qdrant re-ranks via semantic relevance if enabled. Degrades gracefully to FTS if Qdrant is unavailable.

Search vector staleness → auto-updated via PostgreSQL **generated column** or trigger on publish; nightly reindex optional if needed.

**REST Endpoint**

```
GET /api/v1/plugins/search?q=<query>&type=<skill|hook|...>&language=<lang>&page=1&limit=20
GET /api/v1/search?q=<query>&...     # Thin alias, delegates to same use-case
GET /api/v1/discovery?keyword=<query>&...  # Distinct contract (different ranking intent)
```

**Why**

- PostgreSQL FTS covers all MVP discovery scenarios (name, description, metadata search) with zero new infrastructure
- Single-port seam (`ISearchIndexPort`) makes Qdrant additive without refactor
- Hybrid approach honors optional Qdrant goal; no coupling to vector DB until measured demand

**Alternatives Considered**

- **Qdrant day-one**: Embedding pipeline, cost, operational overhead before demand signal — rejected
- **ILIKE pattern matching**: No ranking, O(n) scans, poor UX — rejected
- **Elasticsearch / Meilisearch**: Overhead for greenfield; PostgreSQL native covers MVP — rejected

**Risk → Mitigation**

- **FTS relevance tuning**: Tiebreaker weights (download count, recency) handle commodity queries; no-results path suggests browsing categories or refining query
- **search_vector staleness**: Generated column or trigger keeps vector in sync on publish; nightly REINDEX optional if performance drifts

---

### 5. Telemetry & Anonymization: Client-Side ID, Opt-Out, Privacy-First

**Decision**

Anonymized telemetry collection with **zero PII, zero fingerprinting**:

**Client-Side ID Generation**
- On first visit, generate random UUID v4 locally
- Hash UUID v4 with SHA-256 → 64-character hex string
- Store in `localStorage['plugin-marketplace:anon-id']`
- Reuse same ID across sessions until storage cleared
- **Clearing localStorage = fresh ID, no re-linkage** (no persistent fingerprinting)

**Telemetry Ingestion**
- POST `/api/v1/telemetry/events` fires asynchronously (fire-and-forget)
- Server validates required fields (event_type, anon_client_id, plugin_id), returns 400 if malformed (logged, not stored)
- Atomically writes `telemetry_events` row + increments `telemetry_aggregates` counter
- Coarse OS/Arch only (darwin|linux|windows, x64|arm64); IP not logged with events; 404 downloads never increment

**Storage & Exposure**
- Raw events in `telemetry_events` table: **internal-only, never exposed via API**
- Aggregates in `telemetry_aggregates` table: only thing read endpoints touch (GET /api/v1/plugins/{id}/telemetry/summary)
- Summary endpoint returns totals + 7-day activity, cached 5 minutes

**Opt-Out**
- Client-side flag: `localStorage['plugin-marketplace:telemetry-disabled']`
- When set, `TelemetryFacade` emits no events
- Server has **no opt-out registry** (no identity), no per-user tracking
- Re-enable → rotates anon ID (fresh start, no linkage to prior events)

**Privacy Guarantee**
- No PII columns, no emails, no IP addresses tied to events, no user fingerprinting
- Aggregates-only exposure is a hard boundary; raw events never leave backend except for authorized analytics

**Why**

- Client-side ID + client-side opt-out is consistent with no-auth/browser-only architecture
- Aggregate-only exposure is a hard privacy boundary; raw tables are internal analytics only
- Rotation on re-enable breaks temporal linking (cannot reconstruct user journeys)
- Coarse telemetry (not PII) still enables platform health monitoring and phase-2 analytics

**Alternatives Considered**

- **Server-side IP/UA hashing**: Still fingerprinting; rejected
- **Expose raw events**: Breaks privacy guarantee; rejected
- **Sync telemetry on download hot path**: Adds latency, couples semantics; rejected

**Risk → Mitigation**

- **Counter abuse (no authentication)**: Mitigate with per-IP rate limiting on POST /api/v1/telemetry/events + POST /api/v1/plugins/{id}/download endpoints. Phase 2: token bucket or distributed rate limiting.
- **Double-counting**: **Single increment path**: POST /api/v1/plugins/{id}/download is the canonical counter increment; client does NOT separately POST a download event. Prevents duplicate tallies.

---

### 6. CLI/SDK: Node.js, TypeScript, npm Distribution, OpenAPI-Generated Client

**Decision**

Distributed via npm as a Node.js binary wrapped in TypeScript:

**Binary Command**
```
claude plugin <subcommand> [args]
```

Subcommands: `install`, `remove`, `list`, `update`, `search`, `publish`, `scaffold`, `validate`, `config`

**Local State**
- `~/.claude-plugins/config.json` — API URL (env override: `CLAUDE_PLUGINS_API_URL`), opt-in preferences
- `~/.claude-plugins/installed.json` — registry of installed plugins: name, version, install timestamp, on-disk path
- `~/.claude-plugins/backups/` — rollback directory (copy of previous version before extraction)

**API Contract**
- Generate strongly-typed client from backend OpenAPI/Swagger specification (using `@openapi-generator-plus/typescript-client` or similar)
- Same generated client consumed by Angular infra layer
- Single source of truth (backend OpenAPI spec) keeps 3 consumers aligned, eliminates hand-written-client drift

**Scaffolding**
- Language templates bundled in CLI package: TypeScript, Python, Go, Rust
- Interactive guided flow: name, version, description, entrypoints, optional dependencies
- Generates `plugin.json` manifest + boilerplate handler files
- `claude plugin validate` checks manifest presence, required fields, semver format

**Why**

- npm distribution is frictionless for Node.js audience (most Claude Code users)
- Node.js richest CLI/prompt ecosystem (inquirer, ora, chalk) for polished UX
- OpenAPI-generated client enforces API contract consistency and no-any types
- Bundled scaffolding + validation lowers author friction and improves manifest quality

**Alternatives Considered**

- **.NET global tool**: Requires .NET SDK on every developer machine, friction — rejected
- **Rust binary**: Best distribution, but slow compile/test cycle and no team expertise — rejected (reconsider Phase 2+ if distribution issues arise)
- **Hand-written API client**: Diverges from backend spec over time; rejected

**Risk → Mitigation**

- **Dependency management**: MVP CLI parses, displays, and validates declared `dependencies{}` in manifest, but does **not resolve or auto-install**. Phase 3 introduces `IDependencyResolverPort` seam.
- **Update/extraction failures**: Keep previous version in backups dir; rollback on extraction error leaves developer with working plugin.

---

### 7. REST API Surface (MVP, /api/v1)

**Decision**

Full REST API surface for MVP:

```
# Plugin CRUD & Browsing
GET /api/v1/plugins
  Query: ?page=1&limit=20&type=skill&language=typescript&useCase=dev-team&sort=recent
  Response: { data: [{id, name, slug, description, author, download_count, latest_version, ...}], totalCount, page, limit, totalPages }

GET /api/v1/plugins/{pluginId}
  Response: { id, name, slug, description, author, download_count, created_at, latest_version, all_versions: [{version, released_at, download_count}] }

POST /api/v1/plugins/upload
  Body: multipart/form-data { package (file: tar.gz or zip), metadata (JSON: name, description, author, ..., types[], languages[], useCaseTags[]) }
  Response: 201 { id, name, slug, version: 1.0.0, ... }

# Versioning
GET /api/v1/plugins/{pluginId}/versions
  Response: { data: [{version, released_at, download_count, package_format, size_bytes, is_latest}], ... }

POST /api/v1/plugins/{pluginId}/versions
  Body: multipart { package, release_notes }
  Response: 201 { id, version, ... }

GET /api/v1/plugins/{pluginId}/versions/{version}
  Response: { version, released_at, release_notes, download_count, size_bytes, sha256, package_format }

# Immutability enforcement
PATCH /api/v1/plugins/{pluginId}/versions/{version}
  Response: 405 Method Not Allowed (versions are immutable)

# Download & Distribution
GET /api/v1/plugins/{pluginId}/download?version=latest
  Query: ?version=1.0.0 (default: latest)
  Response: 200 stream (Content-Type: application/gzip, Content-Disposition: attachment, Content-Length, ETag)
  Side effect: Atomically increments plugin_versions.download_count + telemetry_aggregates

# Search & Discovery
GET /api/v1/plugins/search?q=async&type=skill&language=typescript&page=1&limit=20
  Response: { data: [{id, name, slug, description, relevance_score, download_count}], ... }

GET /api/v1/search?q=...&type=...&language=...
  Thin alias: delegates to /api/v1/plugins/search

GET /api/v1/discovery?keyword=lambda&criteria=...&sort=relevance
  Response: { data: [{id, name, description, relevance_score}], ... }

# Categorization
GET /api/v1/categories
  Response: { dimensions: { type: [{value, displayName, count}], language: [...], useCase: [...] } }

# Telemetry
POST /api/v1/telemetry/events
  Body: { eventType: 'download'|'install', pluginId, version, anonClientId, clientOs, clientArch }
  Response: 202 Accepted (fire-and-forget, validation logged not stored)

GET /api/v1/plugins/{pluginId}/telemetry/summary
  Response: { download_count, install_count, last_7d_downloads, last_7d_installs, ... } (cached 5min)

# Documentation
GET /api/v1/docs?search=authentication&page=1&limit=10
  Response: { data: [{slug, title, excerpt}], ... }

GET /api/v1/docs/{slug}
  Response: { slug, title, content (markdown), last_updated }
```

**Why**

- Mirrors proposal requirements and architect's specification
- `/plugins/search` and `/search` are distinct endpoints (search is thin alias; discovery has different contract/ranking)
- Immutable versioning enforced at HTTP level (405 on PATCH)
- Atomic counter increment on download endpoint (single path) prevents double-counting
- Paginated collections + single-resource direct return for clarity
- Fire-and-forget telemetry (202 Accepted) decouples from response latency

**Risk → Mitigation**

- **Catalog `/plugins/search` vs `/search` path overlap**: Default adopted: `/api/v1/plugins/search` is a thin alias delegating to the same search use-case backend as `/api/v1/search`. Confirm if a single canonical path is preferred later.

---

### 8. Docker & DevOps: Single Compose for Dev→Prod on OVH

**Decision**

Single `docker-compose.yml` for both development and production, with optional service profiles:

**Dev Environment**
```yaml
version: '3.8'
services:
  api:
    build: ./backend
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__Postgres: ...
      PackageStorage__Type: LocalFileSystem
      PackageStorage__LocalPath: /packages
    ports:
      - 5000:8080
    depends_on:
      - postgres
    volumes:
      - ./packages:/packages

  web:
    build: ./frontend
    environment:
      API_BASE_URL: http://localhost:5000/api/v1
    ports:
      - 3000:80
    depends_on:
      - api

  postgres:
    image: postgres:16
    environment:
      POSTGRES_PASSWORD: devpassword
      POSTGRES_DB: plugin_marketplace
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./schema.sql:/docker-entrypoint-initdb.d/schema.sql
    ports:
      - 5432:5432

  qdrant:
    image: qdrant/qdrant:latest
    profiles:
      - semantic
    volumes:
      - qdrant_data:/qdrant/storage
    ports:
      - 6333:6333

volumes:
  postgres_data:
  qdrant_data:
```

**Prod Environment (OVH)**
- Same images, `docker-compose.prod.yml` overlay or environment-specific:
  - `ASPNETCORE_ENVIRONMENT: Production`
  - `PackageStorage__Type: OVHObjectStorage` (S3-compatible)
  - `PackageStorage__BucketName: claude-plugins`
  - `PackageStorage__S3Endpoint: https://s3.ovh.fr`
  - `ConnectionStrings__Postgres: Server=<ovh-managed-postgres>` (OVH Managed PostgreSQL)
  - Qdrant: OVH-managed or optional (profile controlled)

**Config & Secrets**
- 12-factor: all env vars, no hardcoded values
- `.env` (dev, git-ignored): local API keys, DB passwords, S3 keys
- OVH Prod: secrets injected via OVH secret manager or Kubernetes secrets

**Database Migrations**
- `dotnet ef database update` on startup (via Program.cs or init container)
- Forward-only migrations in `Infrastructure/Migrations/`
- Rollback: restore from backup + previous image tag

**Web Static Build**
- Frontend `npm run build` → dist folder
- Served by nginx sidecar with runtime-injected API base URL (env var → config on container startup)

**Why**

- One compose file satisfies both dev and prod requirements (proposal mandate)
- `--profile semantic` gates Qdrant (optional, default off) for MVP
- Env-only config changes between dev/prod keeps drift near zero
- OVH Managed services (Postgres, Object Storage) handle scaling and backups
- Immutable packages + nightly backup snapshots enable safe rollback

**Alternatives Considered**

- **Kubernetes from MVP**: Over-engineered, complexity tax — rejected; door open for Phase 2
- **Postgres bundled in app container**: Couples data lifecycle, breaks prod — rejected (dev-only acceptable)

**Risk → Mitigation**

- **Single OVH VM SPOP (Single Point of Potential Failure)**: Prefer OVH Managed PostgreSQL (HA) + Object Storage (geo-redundant). Nightly encrypted backups to separate storage. Image tags enable instant rollback.

---

### 9. Cross-Cutting: Error Handling, Validation, Manifest Schema, API Versioning

**Decision**

**Error Handling**
- RFC 7807 `ProblemDetails` envelope for all error responses
- Global `ExceptionHandler` middleware converts domain exceptions → 400 (client error) or 500 (unexpected)
- Domain exceptions extend `ProblemDetailsException` with user-friendly `Detail` messages matching spec error strings
- Exact error strings from spec enable integration test parity

**Success Responses**
- Collections (lists): paginated envelope `{ data: [...], totalCount, page, limit, totalPages }`
- Single resources (GET detail): resource returned directly (no wrapper)
- Downloads: stream with proper `Content-Type`, `Content-Disposition: attachment`, `Content-Length`, `ETag`

**Validation**
- **Server-authoritative**: FluentValidation on Application requests (semver format, category enum, required fields, file presence)
- Server returns 400 with exact messages matching spec
- **Frontend mirrors** in domain rules (pure functions) for UX polish (no server round-trip for "name required")
- Server is source of truth; client validation is UX only

**API Versioning**
- URL-prefix: `/api/v1` from day one
- Longevity: v1 supported minimum 2 years before deprecation; v2 if breaking changes needed
- No version in Accept header; version is in URL path

**Plugin Package Format**
- Accept `.tar.gz` (preferred) and `.zip`
- Manifest file: `plugin.json` at archive root (required)

**Canonical Plugin Manifest Schema (RESOLVED to Multi-Type)**

```json
{
  "name": "string (required, 1-128 chars)",
  "version": "semver (required, must match plugin_versions.version)",
  "description": "string (required, <500 chars)",
  "author": "string (required, contact name or handle)",
  "types": ["skill" | "hook" | "agent" | "command" | "plugin"],
    // Required: >=1 type. Canonical enum: exactly these 5 values.
  "languages": ["typescript" | "python" | "go" | "rust" | "..."],
    // Required: >=1 language the plugin targets or is written in
  "useCaseTags": [
    "dev-team" | "product-owner" | "product-manager" | "devops" | "security" | "data-analyst"
  ],
    // Optional: use-case audience (subset of controlled vocab)
  "entrypoints": [
    {
      "name": "string",
      "description": "string",
      "signature": "string (type signature or handler name)"
    }
  ],
  "dependencies": {
    "plugin-name": ">=1.0.0"  // Declared, displayed, validated; not resolved in MVP
  },
  "license": "string (default: MIT)",
  "docsUrl": "https://example.com/docs (optional)",
  "readme": "string (optional; if not provided, extracted from package README)"
}
```

**Server Validation on Upload**
- Manifest presence (required)
- Required fields: name, version, description, author, types (>=1), languages (>=1)
- Semver format validation on version
- Category enum validation (types, languages, useCaseTags against controlled vocab)
- Unique plugin name + version pair
- Extract README from tar.gz/zip root → `plugin_versions.readme_text`

**Dependency Management Seam**
- `dependencies{}` is stored + displayed in manifest and catalog views
- CLI validates/warns about conflicts but does **not resolve or auto-install** in MVP
- Phase 3 introduces `IDependencyResolverPort` interface; adapter TBD

**Why**

- ProblemDetails + paginated envelope are team conventions (easy integration tests)
- Server-authoritative validation prevents drift; frontend validation is UX only
- Storing-but-not-resolving dependencies creates a clean seam for Phase 3
- Multi-type canonical schema (RESOLVED) consolidates type categorization and manifest types into one enum
- API versioning in URL is REST-idiomatic and easy to route

**Alternatives Considered**

- **Custom error format**: Breaks team convention, no integration-test parity — rejected
- **Expose raw events via API**: Privacy violation, breaks aggregate guarantee — rejected
- **Resolve dependencies MVP**: Scope creep, complex; defer to Phase 3 — rejected

**Risk → Mitigation**

- **Manifest schema divergence**: Canonical schema documented in design + enforced via FluentValidation + Wallaby tests. CLI scaffold generates compliant manifests. Warn on upload if manifest version missing critical fields.

---

## Risks / Trade-offs

[Risk] → [Mitigation]

1. **Module isolation is convention-only** → Enforce with NetArchTest in CI; compile-time checks prevent cross-module type references
2. **Raw telemetry growth (no-auth, no de-duplication)** → 90-day retention job; aggregate daily and purge raw events beyond window
3. **Counter abuse without authentication** → Per-IP rate limiting on download + telemetry POST endpoints; Phase 2 token bucket
4. **Full-text search relevance tuning** → Weighted tsvector + download/recency tiebreakers; no-results path suggests categories
5. **Single OVH VM SPOF (Single Point of Potential Failure)** → Use OVH Managed PostgreSQL (HA) + Object Storage (geo-redundant); nightly encrypted backups; immutable packages enable instant rollback
6. **Manifest schema divergence across plugin versions** → Single canonical schema with server validation + Wallaby tests; CLI scaffold enforces compliance
7. **Dashboard install model: web-side state vs. reality** → Web records intent + browser storage only; CLI is the canonical installer; no file-system writes from web
8. **Search endpoint path overlap** → `/api/v1/plugins/search` is thin alias; confirm single canonical path preference later
9. **Qdrant optional but easy to forget** → Config-flag controls adapter selection; graceful fallback to FTS if unavailable

---

## Migration Plan

### Initial Deploy (Greenfield → OVH Prod)

1. **Bootstrap**
   - Provision OVH Managed PostgreSQL (HA)
   - Provision OVH Object Storage (S3-compatible)
   - Create S3 bucket: `claude-plugins`
   - Create secrets store (OVH secret manager or Kubernetes)

2. **Build & Push**
   - `dotnet publish` backend → Docker image
   - `npm run build` frontend → static assets
   - Push images to OVH registry (or Docker Hub public)

3. **Deploy**
   - `docker-compose -f docker-compose.prod.yml up -d`
   - Or `helm apply` if using OVH Managed Kubernetes (Phase 2)
   - `dotnet ef database update` (runs on container startup or via init job)
   - Verify health: `GET /api/v1/categories` returns 200

4. **Test**
   - Smoke test: Upload test plugin, download, verify counter
   - CLI: `npm install -g @claudeforge/claude-plugin-cli`
   - `claude plugin search` queries backend
   - `claude plugin install <test-plugin>` works end-to-end

### Forward-Only Migrations

- All database schema changes via `dotnet ef migrations add` + `dotnet ef database update`
- Migrations are idempotent; safe to replay
- Never use `dotnet ef database update` with `--target` to rollback (breaks forward-only guarantee)

### Config Gates

- Feature flags in `appsettings.json`:
  - `Features:QdrantEnabled` (default false) gates semantic search adapter
  - `Features:TelemetryRetention` (default 90 days) gates raw event purge schedule
  - `PackageStorage:Type` (default LocalFileSystem → OVHObjectStorage prod) switches storage adapter

### Rollback

1. **Plugin outage**: Revert to previous image tag: `docker image rm <current-tag> && docker pull <previous-tag> && docker-compose up`
2. **Database corruption**: Restore from nightly backup (encrypted, separate storage)
3. **Breaking migration**: `dotnet ef database update --target <previous-migration>` (dev only; prod rollback is image revert + backup restore)

---

## Open Questions

### RESOLVED (Baked into Decisions above)

1. ✅ **Dashboard install model**: Web dashboard records installed plugins + versions in browser storage (`InstalledPluginsStoragePort` adapter) and offers download links. CLI performs the real on-disk install. Decision 2 + Risk mitigation covers this.
2. ✅ **Canonical plugin type = MULTI-TYPE full set**: `types[]` where plugin may have skill|hook|agent|command|plugin (>=1 required). Decision 9 (Cross-Cutting) reconciles manifest schema and categorization to this single canonical enum.

### Remaining (Default Adopted, Confirm Later)

1. **Local plugin directory convention (CLI)**
   - *Default adopted*: CLI installs plugins to `~/.claude-plugins/` directory; confirm integration with Claude Code's real plugin discovery path in Phase 1 implementation.
   - *Rationale*: Isolated from system CLI plugins, user-controllable, easy to backup/migrate.

2. **Telemetry raw-event retention window**
   - *Default adopted*: Retain raw `telemetry_events` rows for 90 days, then aggregate into daily windows in `telemetry_aggregates` and purge raw. Aggregates kept indefinitely.
   - *Action*: Confirm against privacy policy and compliance requirements (GDPR, etc.).

3. **Download counter single increment path**
   - *Default adopted (architect's recommendation)*: GET /api/v1/plugins/{id}/download is the SOLE canonical counter increment endpoint. Client does NOT separately POST a download telemetry event (prevents double-counting).
   - *Rationale*: Single source of truth, atomic increment, simpler client logic.

4. **Catalog `/plugins/search` vs `/search` path overlap**
   - *Default adopted*: Keep `/api/v1/plugins/search` as a thin alias delegating to the same search use-case backend. Distinct `/api/v1/search` and `/api/v1/discovery` endpoints with different ranking contracts.
   - *Confirm*: Is a single canonical search path preferred (combine into `/api/v1/search` only)? Or keep both for backwards-compat/intent clarity?

---

*Design document completed: clean seams for Phase 2/3 extensions, privacy-first telemetry, no-auth MVP, OpenAPI-driven API contracts, and portable dev→prod on OVH.*
