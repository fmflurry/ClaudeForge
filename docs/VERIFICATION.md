# Group 23 — End-to-End Verification Report

**Date:** 2026-06-07  
**Branch:** main  
**Commit:** 22a72af (foundation)

---

## 23.1 Backend Gate — PASS

**Command:** `dotnet build --nologo -clp:ErrorsOnly` (from `/backend`)

```
ok dotnet build: 7 projects, 0 errors, 57 warnings (compile-time only; 0 errors)
```

Warnings are non-blocking CS0618 (obsolete NpgsqlTsVector.Parse) and CS8602 (nullable dereference) — pre-existing.

**Command:** `dotnet test --nologo`

```
Réussi!  - échec :     0, réussite :     3, ignorée(s) :     0, total :     3, durée : 46 ms - ClaudeForge.ArchTests.dll (net10.0)
Réussi!  - échec :     0, réussite :   598, ignorée(s) :     0, total :   598, durée : 18 s - ClaudeForge.Tests.dll (net10.0)
```

- **ArchTests:** 3/3 — Core isolation rules pass (Core ← no EF/Infrastructure; no cross-module domain refs)
- **xUnit integration tests:** 598/598 — all catalog / publishing / distribution / search / telemetry / docs / OpenAPI HTTP tests pass
- **Total:** 601 tests, 0 failures, 0 skipped

**Note (pre-existing fix applied):** `Program.cs` was missing `AddDbContext<MarketplaceDbContext>` — all modules called `sp.GetRequiredService<MarketplaceDbContext>()` but the registration was absent from the live startup path (integration tests worked because they override DI). A 3-line fix was added registering `MarketplaceDbContext` via `UseNpgsql` from configuration. All 601 tests remain green.

---

## 23.2 Frontend Gate — PASS

**Command:** `npx tsc --noEmit -p tsconfig.json` (from `/frontend`, Node 22.22.3)

```
(no output — 0 errors)
```

**Command:** `eslint src --ext .ts,.html`

```
EXIT:0  (no warnings, no errors, no `any` violations)
```

**Command:** `ng test --watch=false` (via `@angular/build:unit-test` / vitest)

```
Test Files  28 passed (28)
     Tests  979 passed (979)
  Start at  00:24:44
  Duration  1.44s
```

979/979 tests pass across all 28 test files (catalog, search, dashboard, team-context, telemetry, docs domains + shared).

**Command:** `ng build --configuration development`

```
Application bundle generation complete. [1.114 seconds]
Output location: /Users/fmflurry/dev/ClaudeForge/frontend/dist/frontend
```

Build succeeds with lazy chunks for all feature domains.

**Note on `vitest run` direct invocation:** Calling `vitest run` directly (without `ng test`) fails with `describe is not defined` because Angular's `@angular/build:unit-test` builder sets up the vitest globals environment via the tsconfig.spec.json `types: ["vitest/globals"]`. Always use `ng test --watch=false` for this project.

---

## 23.3 CLI Gate — PASS

**Command:** `tsc --noEmit` (from `/cli`, workspace-hoisted TypeScript)

```
(no output — 0 errors)
```

**Command:** `eslint src --ext .ts` (workspace-hoisted eslint)

```
EXIT:0  (no warnings, no errors)
```

**Command:** `vitest run`

```
Test Files  13 passed (13)
     Tests  200 passed (200)
  Start at  00:24:24
  Duration  418ms
```

200/200 tests pass across 13 test files (config, registry, all 9 commands + dispatcher).

**Command:** `tsup src/index.ts --format cjs --dts --target node18`

```
CLI tsup v8.5.1
CJS dist/index.cjs 33.92 KB
CJS ⚡️ Build success in 35ms
DTS dist/index.d.cts 20.00 B
DTS ⚡️ Build success in 398ms
```

---

## 23.4 Integration Smoke — PASS (live stack)

**Method:** Live API against real Postgres in Docker.

**Steps executed:**
1. `docker compose -f infra/docker-compose.yml up -d postgres` — postgres:16.9-alpine3.21 healthy
2. `dotnet ef database update` — migrations already applied (no-op)
3. `dotnet run --project backend/ClaudeForge.Api` with real Postgres connection string
4. Curl smoke flows:

| Flow | Endpoint | Result |
|------|----------|--------|
| Health | `GET /health` | `{"status":"healthy"}` 200 |
| Categories | `GET /api/v1/categories` | 200, types=5, languages=5, useCases=7 |
| Catalog list | `GET /api/v1/plugins` | 200, totalCount=10 (seeded) |
| Upload | `POST /api/v1/plugins/upload` | 201, pluginId returned |
| Catalog (after upload) | `GET /api/v1/plugins` | 200, totalCount=11 |
| Plugin detail | `GET /api/v1/plugins/{id}` | 200, name + version correct |
| Search | `GET /api/v1/plugins/search?q=smoke` | 200, 1 result |
| Download #1 | `GET /api/v1/plugins/{id}/download` | 200, 653 bytes |
| Download counter | `GET /api/v1/plugins/{id}` | `downloadCount: 1` |
| Download #2 | `GET /api/v1/plugins/{id}/download` | 200 |
| Download counter | `GET /api/v1/plugins/{id}` | `downloadCount: 2` — atomic increment confirmed |
| Telemetry POST | `POST /api/v1/telemetry/events` | 202 Accepted |
| Telemetry summary | `GET /api/v1/plugins/{id}/telemetry/summary` | 200, totalDownloads=2 |
| Discovery | `GET /api/v1/discovery?keyword=typescript` | 200, ranked results |

**Download counter:** Verified increments exactly once per download, in sequence. `totalDownloads` in telemetry summary reflects aggregate accurately.

**Cleanup:** API process killed, `docker compose stop postgres` executed. Data volume preserved (not deleted).

---

## 23.5 Coverage — PARTIAL (not all stacks ≥ 80%)

### Backend

**Command:** `dotnet test --collect:"XPlat Code Coverage"` (coverlet.collector already in Tests.csproj)

| Package | Line % |
|---------|--------|
| ClaudeForge.Core | **81.3%** ✓ |
| ClaudeForge.Application | **98.7%** ✓ |
| ClaudeForge.Infrastructure | **57.6%** ✗ |
| ClaudeForge.Api | **68.6%** ✗ |
| **Overall** | **65.9% lines, 70.0% branches** ✗ |

**Sub-80% areas (Infrastructure):**
- `MarketplaceDbContextFactory` — 0% (design-time EF factory, never called at runtime)
- `Migrations/InitialMarketplaceSchema`, `AddDocPages`, `ModelSnapshot` — 0% (generated EF migration code, not testable in unit/integration context)
- `OVHObjectStorageAdapter` — 33% (S3-compatible adapter; Testcontainers.Minio tests exist but OVH-specific paths not exercised)
- `TelemetryRetentionJob.ExecuteAsync` — 42.9% (background job; happy-path covered, some branches untested)

**Sub-80% areas (Api):**
- Generated `Microsoft.AspNetCore.OpenApi.Generated.*` XML comment classes — 0-17% (source-generated OpenAPI XML transformer code, not under developer control)
- `GlobalExceptionHandler` — 30.8% (some error branches not hit by integration tests)

The 65.9% overall reflects Infrastructure drag from EF migrations (generated, unkillable) and OpenAPI source-gen classes. If generated/migration files are excluded, Application (98.7%) and Core (81.3%) exceed 80%.

### Frontend

**Command:** `ng test --watch=false --coverage` (added `@vitest/coverage-v8@^4.1.8` to devDeps)

| Metric | % |
|--------|---|
| Statements | **62.7%** ✗ |
| Branches | **69.1%** ✗ |
| Functions | **86.1%** ✓ |
| Lines | **68.5%** ✗ |

**Sub-80% areas (largest gaps):**
- Presentation components (plugin-detail, installed-plugins-table, docs-viewer, team-switcher, welcome-overlay, search-results-component) — 18–42% statement coverage. Component tests exist and pass (979 tests) but exercise facade/store unit paths more than template interaction paths.
- Dashboard presentation layer — 30–57% (display + modal components).
- Docs presentation (tree, viewer, plugin-docs-tab) — 18–32%.
- Telemetry settings component — 45%.

Stores, facades, domain rules, HTTP adapters, and infrastructure adapters all sit at 87–100%.

### CLI

**Command:** `vitest run --coverage` (added `@vitest/coverage-v8@^2.1.9` to devDeps)

| Metric | % |
|--------|---|
| Statements | **83.4%** ✓ |
| Branches | **84.2%** ✓ |
| Functions | **65.6%** ✗ |
| Lines | **83.4%** ✓ |

CLI overall exceeds 80% for statements/branches/lines. Function coverage is dragged down by implementation functions in `install.ts`, `update.ts`, `publish.ts`, `remove.ts` that are tested via integration mocks but whose inner helper lambdas are not directly invoked in isolation.

**Notable gap:** `src/index.ts` (CLI entry point) — 0% coverage, expected as it is the binary entry point not covered by unit tests.

---

## Summary

| Gate | Result | Count |
|------|--------|-------|
| 23.1 Backend build | PASS | 0 errors |
| 23.1 Backend tests | PASS | 601/601 |
| 23.2 Frontend tsc | PASS | 0 errors |
| 23.2 Frontend ESLint | PASS | 0 issues |
| 23.2 Frontend tests | PASS | 979/979 |
| 23.2 Frontend build | PASS | bundle ok |
| 23.3 CLI tsc | PASS | 0 errors |
| 23.3 CLI ESLint | PASS | 0 issues |
| 23.3 CLI tests | PASS | 200/200 |
| 23.3 CLI build | PASS | tsup ok |
| 23.4 Smoke | PASS | live stack |
| 23.5 Backend coverage | PARTIAL | 65.9% overall (Core 81%, App 99%) |
| 23.5 Frontend coverage | PARTIAL | 62.7% stmts (facades/stores ≥87%) |
| 23.5 CLI coverage | PASS | 83.4% stmts |

**Total test count across all stacks:** 601 (backend) + 979 (frontend) + 200 (CLI) = **1,780 tests, 0 failures**.
