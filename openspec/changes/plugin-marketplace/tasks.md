# Tasks: Plugin Marketplace

## 1. Repository & Tooling Scaffold

- [x] 1.1 Create monorepo root layout (`/backend`, `/frontend`, `/cli`, `/plugin-template`, `/seed-plugins`, `/docs`, `/infra`) with top-level `README.md` and root `.gitignore` covering .NET, Node, Angular, and `/packages` artifacts
- [x] 1.2 Add root `.editorconfig` enforcing team conventions (no-tabs, final newline, max line length) shared across .NET and TypeScript
- [x] 1.3 Scaffold .NET solution `ClaudeForge.sln` with project skeletons: `Application`, `Core`, `Infrastructure`, `Tests` (xUnit), `ArchTests` (NetArchTest); wire project references so Core has zero outward references and Application/Infrastructure depend on Core only
- [x] 1.4 Scaffold Angular 22 standalone workspace in `/frontend` (`ng new`, standalone, OnPush default, strict mode, `"strict": true` + `noImplicitAny`); confirm `any` is lint-banned via ESLint `@typescript-eslint/no-explicit-any: error`
- [x] 1.5 Scaffold Node + TypeScript CLI package in `/cli` (`package.json` with `bin: claude-plugin`, `tsconfig` strict, `noImplicitAny`), build via `tsup`/`tsc`
- [x] 1.6 Add `.env.example` at root documenting all dev env vars (`ConnectionStrings__Postgres`, `PackageStorage__Type`, `PackageStorage__LocalPath`, `API_BASE_URL`, `CLAUDE_PLUGINS_API_URL`) and git-ignore real `.env`
- [x] 1.7 Configure formatters/linters per stack: `dotnet format` config, ESLint + Prettier for Angular, ESLint + Prettier for CLI; add root `npm`/`make` scripts to run all three
- [x] 1.8 Add CI workflow (lint + format check + build for all three packages) running on PR; fail on `any`, format drift, or build error
- [x] 1.9 Create dev `docker-compose.yml` in `/infra` with the full dev stack — `api` (.NET, hot-reload/watch), `web` (Angular dev server), `postgres:16` (named volume + `/packages` bind-mount), and `qdrant` behind a `--profile semantic` flag (off by default); wire service env vars from root `.env`
- [x] 1.10 Add dev `Dockerfile`s for `api` and `web` (dev targets supporting live reload) plus a root `make up` / `npm run dev:up` convenience script; document one-command stack startup in `README.md`
- [x] 1.11 Verify the dev stack boots end-to-end: `docker compose up` brings api+web+postgres healthy, the API reaches Postgres, and the web app reaches the API — establishing the containerized dev environment all subsequent groups build against

## 2. Database & Shared Kernel

- [x] 2.1 Write failing tests for EF Core entity mappings (Plugin, PluginVersion, Category, PluginCategory, TelemetryEvent, TelemetryAggregate) asserting required fields, unique constraints, and cascade rules
- [x] 2.2 Define Core domain entities and value objects (`Plugin`, `PluginVersion`, `Category`, `SemVer` value object, `AnonClientId` value object) as immutable types in `Core/`
- [x] 2.3 Define EF Core entity models and `MarketplaceDbContext` in `Infrastructure/Persistence` matching the design schema (plugins, plugin_versions, categories, plugin_categories, telemetry_events, telemetry_aggregates)
- [x] 2.4 Configure EF mappings: `name_normalized` unique, `slug` unique, `UNIQUE(plugin_id, version)`, partial unique index `is_latest=TRUE`, GIN index on `search_vector`, `version_sort` index, telemetry indexes
- [x] 2.5 Create initial EF Core migration generating the full schema; verify `dotnet ef database update` against a local Postgres produces the design's tables/indexes
- [x] 2.6 Implement `search_vector` maintenance (PostgreSQL generated column or trigger) weighting name=A, description=B, tags=C; cover with a test asserting vector populates on insert/update
- [x] 2.7 Implement controlled-vocabulary seed for `categories` (dimensions: `type` = skill|hook|agent|command|plugin; `language` = typescript|python|go|rust; `use_case` = dev-team|product-owner|product-manager|devops|security|data-analyst) as an idempotent seeder run on startup
- [x] 2.8 Define shared cross-cutting primitives in Application: ProblemDetails exception base (`ProblemDetailsException`), paginated envelope DTO `{ data, totalCount, page, limit, totalPages }`, and pagination request binder with defaults (page=1, limit=20) and validation
- [x] 2.9 Implement global exception-handling middleware mapping domain exceptions → RFC 7807 ProblemDetails (400/404/405/409/500) with spec-exact `detail` strings
- [x] 2.10 Add NetArchTest rules in `ArchTests`: Core references no EF/Infrastructure types; no cross-module domain/application type references; wire into CI

## 3. Object Storage & Package Format

- [x] 3.1 Write failing tests for `IPackageStoragePort` contract (put immutable, get stream, exists, compute SHA-256 + size) using the local FS adapter
- [x] 3.2 Define `IPackageStoragePort` outgoing port in Core with key convention `plugins/{pluginId}/{version}/package.{ext}` and immutability guarantee (reject overwrite)
- [x] 3.3 Implement `LocalFileSystemPackageStorageAdapter` in Infrastructure (Docker bind-mount path from config); wire selection via `PackageStorage:Type`
- [x] 3.4 Write failing tests for package validation: tar.gz and zip extraction, manifest presence (`plugin.json`/`manifest.json` at root), corrupted-archive rejection, unsupported-format rejection
- [x] 3.5 Implement package format validator and archive reader (tar.gz + zip) producing extracted manifest bytes + README text, with spec-exact error strings ("Unsupported package format. Allowed: tar.gz, zip", "Package file is corrupted or not a valid archive", "Package must contain plugin.json or manifest.json at root level")
- [x] 3.6 Define canonical manifest schema model + FluentValidation validator (name 1-128, version semver, description <500 non-empty, author required, `types[]` ≥1 from enum, `languages[]` ≥1 from enum, optional `useCaseTags[]`, `entrypoints[]`, `dependencies{}`, license default MIT, optional docsUrl/readme); cover valid/invalid cases with tests

## 4. Backend — PluginCatalog Module (plugin-catalog, plugin-categorization)

- [x] 4.1 Write failing tests for `IPluginRepositoryPort` (list with pagination/sort/filter, get-by-id with versions, existence/duplicate-name checks)
- [x] 4.2 Define `IPluginRepositoryPort` in PluginCatalog Core and implement EF adapter in Infrastructure (case-insensitive name normalization, semver-descending version ordering via `version_sort`)
- [x] 4.3 Implement `ListPluginsUseCase` with pagination, sort (`downloads|createdAt|name` + `order`), and combined category/tag filters (AND across dimensions, intersection semantics); cover catalog spec scenarios incl. empty state, page-beyond-range, invalid-sort default, invalid-category 400
- [x] 4.4 Implement `GetPluginDetailsUseCase` returning plugin + versions (semver desc, `isLatest`, per-version download counts), 404 "Plugin not found", and empty-versions/`latestVersion=null` case
- [x] 4.5 Implement `ListCategoriesUseCase` returning `{ types, languages, useCases }` with displayName/description and per-value counts; cover empty-system 200 case
- [x] 4.6 Implement Minimal API endpoints `GET /api/v1/plugins`, `GET /api/v1/plugins/{pluginId}`, `GET /api/v1/categories` with FluentValidation request validators
- [x] 4.7 Write integration tests (WebApplicationFactory + test Postgres) asserting paginated envelope shape, filter intersection, sort order, and ProblemDetails error bodies for all catalog + categorization scenarios

## 5. Backend — PluginPublishing Module (plugin-upload, plugin-versioning)

- [x] 5.1 Write failing tests for upload use-case: valid multipart submission, missing package (400 "Package file is required"), missing required field (400 "Required field missing: name"), invalid semver, duplicate name (409, case-insensitive), README extraction
- [x] 5.2 Implement `UploadPluginUseCase` orchestrating manifest validation, package storage (SHA-256 + size), plugin + initial version persistence (`isLatest=true`), `name_normalized`/`slug` generation, README extraction to `readme_text`
- [x] 5.3 Implement `POST /api/v1/plugins/upload` (multipart/form-data) returning 201 with `pluginId` + version reference; map duplicate to 409, validation to 400 with spec-exact strings
- [x] 5.4 Write failing tests for versioning use-case: publish new version (flips prior `isLatest`), duplicate version (409 "Version 1.5.0 already exists"), invalid format (400 with format message), publish-to-nonexistent-plugin (404), immutable PATCH (405)
- [x] 5.5 Implement `PublishVersionUseCase` enforcing semver, single-`isLatest` invariant (atomic flip), immutability, and release-notes storage (default empty string)
- [x] 5.6 Implement endpoints `POST /api/v1/plugins/{pluginId}/versions`, `GET /api/v1/plugins/{pluginId}/versions` (paginated, semver desc), `GET /api/v1/plugins/{pluginId}/versions/{version}`, and `PATCH .../versions/{version}` → 405 Method Not Allowed
- [x] 5.7 Write integration tests covering full publish lifecycle, version-history pagination defaults/out-of-range, per-version download counts, and immutability enforcement
- [x] 5.8 Add per-IP rate limiting middleware applied to upload + version-publish endpoints

## 6. Backend — PluginDistribution Module (plugin-download)

- [x] 6.1 Write failing tests for download use-case: latest resolution (no version param), explicit version, 404 plugin, 404 specific version ("Plugin version 9.9.9 not found"), invalid version format (400)
- [x] 6.2 Implement `DownloadPluginUseCase` resolving version (default latest), streaming from `IPackageStoragePort`, and producing headers (Content-Type gzip/zip, Content-Disposition attachment `{name}-{version}.tar.gz`, Content-Length, ETag, optional Cache-Control)
- [x] 6.3 Implement atomic single-path counter increment on successful download only: increment `telemetry_aggregates` (source of truth) + denormalized `plugin_versions.download_count`/`plugins.download_count` inside one transaction; never increment on 404
- [x] 6.4 Implement `GET /api/v1/plugins/{pluginId}/download?version=latest` streaming endpoint with attachment disposition
- [x] 6.5 Write integration + concurrency tests asserting exactly-N increments under concurrent downloads (no race), failed-download no-increment, and correct headers
- [x] 6.6 Apply per-IP rate limiting to the download endpoint

## 7. Backend — PluginSearch Module (plugin-search, discovery-service)

- [x] 7.1 Write failing tests for `ISearchIndexPort` against Postgres FTS: name/description/keyword match, case-insensitivity, ranking (exact > prefix > partial), download-count and recency tiebreakers
- [x] 7.2 Define `ISearchIndexPort` in PluginSearch Core and implement `PostgresSearchAdapter` (ts_rank blended with download_count + recency, category filters via joins)
- [x] 7.3 Implement `SearchPluginsUseCase` (query + type/language filters, pagination, relevance_score, empty-result message with category suggestions); cover search spec scenarios incl. invalid pagination 400 and OR-within-type / AND-across-dimension filter logic
- [x] 7.4 Implement `DiscoverPluginsUseCase` (keyword required → 400 "Keyword cannot be empty" when blank, language/useCase/type criteria, relevance score 0-1, contextual metadata incl. all languages + maturity indicator, criteria echo on empty results)
- [x] 7.5 Implement endpoints `GET /api/v1/plugins/search`, `GET /api/v1/search` (thin alias delegating to same use-case), `GET /api/v1/discovery`
- [x] 7.6 Add `QdrantSearchAdapter` seam behind `Features:QdrantEnabled` flag with graceful FTS fallback (interface + no-op/disabled stub only; full impl deferred) and a test asserting fallback path logs and returns FTS results
- [x] 7.7 Write integration tests for search + discovery covering ranking order, combined criteria accuracy, and empty/no-results paths

## 8. Backend — Telemetry Module (plugin-telemetry)

- [x] 8.1 Write failing tests for ingest use-case: valid event persists raw row + increments aggregate, malformed event (missing eventType/anonClientId/pluginId) → 400 logged-not-stored, coarse OS/arch only, no PII columns
- [x] 8.2 Implement `IngestTelemetryEventUseCase` (fire-and-forget semantics, atomic raw insert + aggregate increment, 404-download events never reach this path)
- [x] 8.3 Implement `GetTelemetrySummaryUseCase` reading aggregates only (total downloads/installs, last-7d activity) with 5-minute cache
- [x] 8.4 Implement endpoints `POST /api/v1/telemetry/events` (202 Accepted) and `GET /api/v1/plugins/{pluginId}/telemetry/summary`; enforce that raw events are never exposed via API
- [x] 8.5 Implement nightly rollup + 90-day raw-event purge job (aggregate into daily windows, then delete raw >90d) gated by `Features:TelemetryRetention`; cover with a test
- [x] 8.6 Apply per-IP rate limiting to telemetry POST; write integration tests for ingest, malformed rejection, aggregate-only summary, and cache behavior

## 9. Backend — Docs Module (docs)

- [x] 9.1 Write failing tests for docs search + retrieval (full-text over doc pages, relevance ranking title>content, pagination, slug retrieval, 404)
- [x] 9.2 Implement docs storage model + `IDocsRepositoryPort` sourcing static marketplace doc pages (markdown) and plugin README content extracted on upload
- [x] 9.3 Implement `SearchDocsUseCase` (full-text, ranked, paginated up to 20) and `GetDocPageUseCase` (by slug, returns markdown + last_updated)
- [x] 9.4 Implement endpoints `GET /api/v1/docs?search=...` and `GET /api/v1/docs/{slug}`; cover plugin-doc-by-version surfacing and missing-doc placeholder behavior in tests
- [x] 9.5 Write integration tests for docs search ranking, pagination, and graceful missing-README handling

## 10. OpenAPI Publication

- [x] 10.1 Configure OpenAPI/Swagger generation for all `/api/v1` endpoints with accurate schemas (paginated envelope, ProblemDetails, multipart upload, streaming download) and publish a static `openapi.json`
- [x] 10.2 Add a build/CI step that emits `openapi.json` as an artifact consumed by both the Angular infra layer and the CLI client generator; add a test asserting spec generation succeeds and contains all documented operations

## 11. Frontend — Foundation

- [x] 11.1 Set up app shell, routing, and layout (header, nav incl. Catalog/Search/Dashboard/Docs, OnPush components) with runtime-injected `API_BASE_URL` config
- [x] 11.2 Implement signal-based `BaseStore<T>` with `ResourceState<T>` pattern (idle/loading/success/error) and immutable update helpers; unit-test state transitions
- [x] 11.3 Generate typed HTTP client from `openapi.json` into the infrastructure layer; add shared HTTP adapter with ProblemDetails error parsing (no `any`)
- [x] 11.4 Implement localStorage adapter base + three domain ports as adapters: `TeamContextStoragePort`, `TelemetryPreferencePort`, `InstalledPluginsStoragePort` (each swappable, unit-tested with in-memory fake)
- [x] 11.5 Implement Context Registry for cross-domain event publication (no direct service injection between domains); unit-test publish/subscribe
- [x] 11.6 Establish design-system shell (shared UI primitives: table, badge, modal, empty-state, pagination, toast) used by feature domains

## 12. Frontend — Catalog Domain (catalog)

- [x] 12.1 Write failing tests for catalog domain models, mappers, and pure filter/sort rules
- [x] 12.2 Implement catalog domain (models, ports, mappers) and HTTP adapter consuming `/api/v1/plugins`, `/api/v1/plugins/{id}`, `/api/v1/categories`
- [x] 12.3 Implement `CatalogStore` (signal store) + `CatalogFacade` (list, paginate, filter by category/tags, sort, get detail) — components consume facade only
- [x] 12.4 Build catalog components: plugin list (paginated, sortable, filterable), plugin detail with version history, empty-state ("No plugins found. Try adjusting your filters."); unit + component tests covering empty/loaded/error states

## 13. Frontend — Search & Discovery Domain (search)

- [x] 13.1 Write failing tests for search domain rules (filter combination, relevance display, empty-result messaging)
- [x] 13.2 Implement search domain + HTTP adapter for `/api/v1/plugins/search` and `/api/v1/discovery`
- [x] 13.3 Implement `SearchStore` + `SearchFacade` (keyword search, type/language filters, discovery criteria builder, ranked results)
- [x] 13.4 Build search/discovery components (search bar, filter chips, ranked result list with relevance + match-field indicators, no-results-with-suggestions); component tests

## 14. Frontend — Team Context Domain (team-context)

- [x] 14.1 Write failing tests for team-context domain (validation rules: special chars/length limits, browser-scoped persistence via fake storage port)
- [x] 14.2 Implement team-context domain + `TeamContextStoragePort` adapter (key `plugin-marketplace:team`, no HTTP)
- [x] 14.3 Implement `TeamContextStore` + `TeamContextFacade` (set/change/clear team, preset list, init prompt, optional team query-param hint for analytics only)
- [x] 14.4 Build team-context UI (first-visit welcome overlay with presets + custom + skip, switch/clear controls, header display, validation errors); component tests covering all team-context scenarios

## 15. Frontend — Dashboard Domain (dashboard)

- [x] 15.1 Write failing tests for dashboard domain (install-intent vs install-execution separation, update-availability computation from marketplace latest, team-scoped grouping)
- [x] 15.2 Implement dashboard domain + `InstalledPluginsStoragePort` adapter (records name/version/installedAt/path intent in browser storage only — never writes developer filesystem)
- [x] 15.3 Implement `DashboardStore` + `DashboardFacade` orchestrating install/remove intent, update checks against catalog API, team-scoped grouping and "Recommended for [Team]" badges
- [x] 15.4 Build dashboard components: installed-plugins table (status, actions), update-available indicators with confirm + release-notes, details modal (deps, docs link/placeholder), remove flow, search/install-from-marketplace section, periodic 5-min background update check with graceful failure; component tests for empty/loaded/update-available/error states

## 16. Frontend — Telemetry Client Domain (telemetry)

- [ ] 16.1 Write failing tests for anon-ID generation (UUID v4 → SHA-256 → 64-hex, persisted under `plugin-marketplace:anon-id`, rotates on re-enable), opt-out gate, and fire-and-forget no-op when disabled
- [ ] 16.2 Implement telemetry domain + `TelemetryPreferencePort` adapter (opt-out key `plugin-marketplace:telemetry-disabled`)
- [ ] 16.3 Implement `TelemetryStore` + `TelemetryFacade` (generate/reuse anon ID, opt-out toggle, fire-and-forget POST to `/api/v1/telemetry/events`, emit nothing when disabled, rotate ID on re-enable) — confirm client does NOT separately POST download events (single increment path)
- [ ] 16.4 Build telemetry settings UI (enable/disable toggle, privacy explanation); component tests for opt-out persistence and ID rotation

## 17. Frontend — Docs Domain (docs)

- [ ] 17.1 Write failing tests for docs domain (search ranking display, category tree, per-plugin doc tab)
- [ ] 17.2 Implement docs domain + HTTP adapter for `/api/v1/docs` and `/api/v1/docs/{slug}`
- [ ] 17.3 Implement `DocsStore` + `DocsFacade` (full-text search, doc tree navigation, plugin doc tab content)
- [ ] 17.4 Build docs components (sidebar category tree, searchable doc viewer with highlight/snippet, plugin "Docs" tab, missing-doc placeholder); component tests

## 18. CLI/SDK (cli-sdk)

- [ ] 18.1 Write failing tests for local registry/config layer: `~/.claude-plugins/config.json` (api-url, env override `CLAUDE_PLUGINS_API_URL`, default URL), `~/.claude-plugins/installed.json`, `~/.claude-plugins/backups/`
- [ ] 18.2 Implement config + local-registry modules (read/write installed records immutably, resolve API URL precedence, validate URL format)
- [ ] 18.3 Generate typed API client from `openapi.json` into CLI (shared contract with frontend, no `any`)
- [ ] 18.4 Implement `config` command (`set api-url`, `show`) with connectivity test and spec-exact messages
- [ ] 18.5 Implement `search` command (keyword query, table output Name/Version/Description/Downloads, default limit 10, `--limit`, no-results exit 0)
- [ ] 18.6 Implement `install` command (resolve name/version, download package to plugins dir, record in registry, halt-before-write on network error with non-zero exit, fire install telemetry event)
- [ ] 18.7 Implement `list` command (table Name/Version/InstalledDate/Status, `--check-updates` queries marketplace, "No plugins installed" empty case)
- [ ] 18.8 Implement `update` command (fetch latest, already-up-to-date message, dependency-conflict halt, backup-then-extract with rollback on failure)
- [ ] 18.9 Implement `remove` command (delete from disk + registry, confirmation message, non-existent → message + non-zero exit)
- [ ] 18.10 Implement `validate` command (manifest presence, required fields incl. type/entrypoints, semver check, dependency conflict warnings, exit codes) reusing canonical manifest schema
- [ ] 18.11 Implement `publish` command (validate manifest, compress directory, check version not existing, upload multipart, duplicate-version handling, success URL output)
- [ ] 18.12 Implement `scaffold` command (`--name`/`--language`, infer-from-dir, `--interactive` guided flow with prompts/defaults/resume, language templates) delegating to template package
- [ ] 18.13 Wire `claude plugin <subcommand>` dispatcher with `--help`; write CLI integration tests against a mocked API for each command's happy + error paths

## 19. Plugin Template Package (plugin-template)

- [ ] 19.1 Write failing tests asserting scaffolded output for each language (TS, Python, Go, Rust): required files, subdirs (`src/`, `docs/`, `tests/`, `assets/`), populated `plugin.json`, sectioned `README.md`
- [ ] 19.2 Build TypeScript template (plugin.json, package.json, src/index.ts with JSDoc entrypoints, tests, .gitignore, README sections Overview/Installation/Configuration/Usage/API/Contributing/License)
- [ ] 19.3 Build Python, Go, and Rust templates with language-appropriate manifest/build files (pyproject.toml, go.mod, Cargo.toml) and example entrypoints
- [ ] 19.4 Implement manifest generation producing canonical-schema-compliant `plugin.json` (types[], languages[], useCaseTags, entrypoints, dependencies, license default MIT); test generated manifests pass `validate`

## 20. Seed Test Plugins

- [ ] 20.1 Author 10 real seed plugins across varied types/languages/use-cases using the template + canonical manifest (valid packages with READMEs)
- [ ] 20.2 Implement a seeding task/script that uploads the 10 plugins via the upload pipeline (or direct repository seeding) into a fresh dev database, including ≥1 multi-version plugin and ≥1 multi-type plugin; verify catalog/search/categories return them

## 21. DevOps Hardening

- [ ] 21.1 Harden the dev compose from Group 1 for parity (healthchecks, resource limits, restart policies, pinned image digests) and document the qdrant `--profile semantic` opt-in path
- [ ] 21.2 Add production multi-stage `Dockerfile` targets (backend: dotnet publish runtime image; frontend: npm build → nginx with runtime-injected API base URL) extending the Group 1 dev Dockerfiles
- [ ] 21.3 Add `docker-compose.prod.yml` overlay for OVH (Production env, `PackageStorage__Type=OVHObjectStorage`, S3 endpoint/bucket, managed Postgres connection string)
- [ ] 21.4 Implement `OVHObjectStorageAdapter` (S3-compatible via AWS SDK) implementing `IPackageStoragePort`; integration-test against a local S3-compatible mock
- [ ] 21.5 Implement startup secret/config validation (fail fast if required env vars missing) and run EF migrations on startup/init
- [ ] 21.6 Verify dev→prod parity: bring up dev compose, run smoke flow (upload → download → counter), confirm only env vars differ between dev and prod adapters

## 22. Documentation

- [ ] 22.1 Author Getting Started / installation guide (web UI + CLI, prerequisites, verification) as seedable doc pages surfaced by the Docs module
- [ ] 22.2 Author Contributor/Author guide (structure, manifest schema, scaffolding, versioning + release notes, publishing) as doc pages
- [ ] 22.3 Author General/FAQ + Privacy & Telemetry docs (data collected, opt-out, retention, no PII) and API reference page
- [ ] 22.4 Verify per-plugin docs surfacing end-to-end (README extracted on upload appears in plugin Docs tab; missing-README placeholder shown)

## 23. End-to-End Verification

- [ ] 23.1 Run backend verification gate: `dotnet build` clean + xUnit suite green + NetArchTest isolation rules passing
- [ ] 23.2 Run frontend verification gate: `npx tsc --noEmit`, ESLint (no `any`), Angular unit/component tests green
- [ ] 23.3 Run CLI verification gate: typecheck + lint + CLI test suite green
- [ ] 23.4 Run full integration smoke against docker-compose: upload → catalog list → search/discovery → download (counter increments once) → telemetry summary → CLI install/list/update/remove
- [ ] 23.5 Confirm ≥80% coverage across backend, frontend, and CLI; record coverage report and close any gaps

## Notes & Reconciled Conflicts

**Phase 2/3 seams — deferred (not actionable here):** Qdrant semantic adapter implementation, auth/identity, ratings & reviews, notifications, recommendation engine, `IDependencyResolverPort` auto-install, security scanning, advanced multi-criteria query builder, and usage/efficiency telemetry metrics. Seams (ports, feature flags, schema columns) are created in the tasks above but their full implementations are out of scope for MVP.

Two spec/design conflicts were reconciled to design.md (source of truth):

- **Type vocabulary conflict:** `plugin-upload/spec.md` and `plugin-template/spec.md` reference older single-`category`/`type` enums (`skill, hook, agent, other` and `skill/hook/integration/utility`). The design RESOLVED this to the canonical multi-type `types[]` enum `skill|hook|agent|command|plugin` plus `languages[]` and `useCaseTags[]`. Tasks anchor to the canonical schema (tasks 3.6, 2.7, 19.4).

- **Dependencies auto-install conflict:** `plugin-template/spec.md` scenario "Dependencies auto-installed on plugin install" contradicts the design's explicit Non-Goal (declare/display/validate only, no resolution in MVP). Tasks implement declare/validate/display only (18.10, 19.4); auto-install is deferred to the Phase 3 `IDependencyResolverPort` seam.
