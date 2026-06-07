## Context

The landing page (`/`) is the primary entry point for the ClaudeForge marketplace. Currently, it displays static placeholder metrics and has minimal SEO optimization, limiting organic discoverability and social media presence. The marketplace has real data (plugin counts, download aggregates, publishers) stored in the database via the Telemetry and PluginCatalog modules. This design brings that data to the landing page while ensuring crawlability for search engines and social platforms.

## Goals / Non-Goals

**Goals:**
- Display real-time marketplace metrics (plugin count, total downloads, publisher count, category count) on the landing page hero.
- Enable search engine crawlability with proper SEO metadata (title, description, canonical, Open Graph, Twitter Card, JSON-LD structured data).
- Support social media unfurls (Facebook, Twitter, LinkedIn) with rich preview cards.
- Ensure landing page remains fast and accessible (loading state, error handling, no layout shift).
- Use Clean Architecture and existing project patterns (facade-only in components, signal-based stores, port/adapter boundaries).

**Non-Goals:**
- Dynamic pre-rendering of every plugin detail page (out of scope; focus on public routes only: /, /catalog, /docs).
- Real-time metrics updates (5-10 minute cache is acceptable).
- Personalized SEO metadata based on user login state (landing is public; personalization left to future).
- Multi-language SEO metadata (assumes English-only for now; i18n integration left to future).

## Decisions

### Decision 1: Backend Stats Endpoint vs. Inline Aggregation
**Choice**: Dedicated `GET /api/v1/stats` endpoint with cache.

**Rationale**:
- Separates concerns: telemetry and catalog modules expose their aggregates via a single clean interface.
- Cacheable: 5-10 minute TTL prevents database thrashing on high traffic.
- Reusable: other clients (mobile app, analytics dashboard) can call the same endpoint.

**Alternatives Considered**:
- Inline aggregation in landing page load: Violates backend Clean Architecture (application layer should not directly query database). Couples frontend to database schema.
- Embedded in catalog endpoint: Conflates catalog (list plugins) with stats (aggregate metrics). Harder to cache independently.

### Decision 2: Stats Module Location (Telemetry vs. New Stats Module)
**Choice**: Extend existing Telemetry module with stats use-case and adapter.

**Rationale**:
- Telemetry module already aggregates and caches download data.
- Minimal coupling: stats use-case depends on Telemetry port for downloads, queries PluginCatalog for plugin count.
- Reduces new module boilerplate.

**Alternatives Considered**:
- New dedicated "Stats" module: Cleaner separation but adds module scaffolding overhead. Deferred as premature.

### Decision 3: Frontend Metrics Domain Architecture
**Choice**: Separate home/metrics domain with port + HTTP adapter + signal-based facade.

**Rationale**:
- Follows project convention: ports abstract data source, adapters are concrete implementations.
- No direct HttpClient in components: components inject facade, never HTTP.
- Immutable signal updates: facade updates stats signal without mutation; supports signals-based reactivity.
- Reusable: metrics facade could be injected into dashboard or admin panels later.
- Testable: port can be mocked; adapter can be tested independently.

**Alternatives Considered**:
- Direct HttpClient in landing page component: Violates "facade-only" rule. Harder to test.
- RxJS observables instead of signals: Project uses signals for new code. Mixing would be inconsistent.

### Decision 4: Stats Band Component Integration
**Choice**: New stats band component injecting metrics facade, integrated into landing page template.

**Rationale**:
- Separation of concerns: stats band is its own component with loading/error/empty states.
- Non-blocking: async fetch does not delay page render. Other sections (search, featured) load in parallel.
- Accessible: ARIA labels and live regions for screen readers.

**Alternatives Considered**:
- Inline stats in hero title: Couples stats to hero styling; harder to test independently.
- Separate stats page/route: Increases cognitive load; metrics are more visible in hero.

### Decision 5: SEO Metadata Approach — Angular SSR vs. Static Prerender vs. Meta Tags Only
**Choice**: **Angular SSR (server-side rendering) with Node @angular/ssr server** for public routes (/, /catalog, /docs).

**Rationale for SSR (CHOSEN)**:
- **Crawlability**: Search engines (Google, Bing) and social media crawlers get full HTML with metadata rendered server-side on each request. No JavaScript execution needed.
- **Fresh metrics**: Unlike static prerender, SSR renders fresh per request. Marketplace stats are bounded by the 5-minute backend API cache, but always current within that window.
- **Hydration + TransferState**: Server renders initial state, transfers data to client via `TransferState` to prevent double-fetch (browser does not re-fetch from `/api/v1/stats` after hydration).
- **Reliability**: Node SSR server handles rendering; no build-time dependencies on static files.
- **Social unfurls**: Facebook, Twitter, LinkedIn crawlers receive fully-rendered HTML with metadata; unfurls work without waiting for JavaScript.

**SSR Trade-off & Mitigation**:
- **Hosting requirement**: Web service must run a Node SSR server process (`node dist/frontend/server/server.mjs`), not just serve static files.
  - *Mitigation*: Document deployment (Docker, Node version, memory requirements). CI/CD pipeline builds and packages the server bundle.
- **Metrics freshness bounded by cache**: Stats are bounded by the 5-minute API cache, not freshly fetched per request (same as static prerender, but expected).
  - *Mitigation*: Acceptable for landing page. Metadata sets `datePublished` to indicate when stats were last refreshed.
- **Server resource overhead vs. static**: SSR requires memory/CPU per request vs. static file serving.
  - *Mitigation*: Vertical scale the Node container; typical landing page SSR is lightweight. Monitor response times.

**Alternatives Considered**:
- **Static Prerender**: Build-time prerendering, metrics stale between deploys. Simpler infrastructure but metrics always lag behind until next build.
- **Meta Tags Only (NOT RECOMMENDED)**: No server-side rendering. Social media crawlers execute JavaScript inconsistently; unfurls may fail or show generic content.

**Recommendation**: Implement SSR for maximum reach + fresh metrics within cache window. Use browser-global guards to prevent TransferState hydration issues in dev/test.

### Decision 6: Prerender Routes Selection
**Choice**: Prerender only public routes: `/`, `/catalog`, `/docs`. Do NOT prerender authenticated routes (`/dashboard`, `/auth/*`, `/org/*`).

**Rationale**:
- Public routes benefit most from SEO and social unfurls.
- Authenticated routes should not be cached in static HTML (privacy risk; user-specific data could be baked in).
- Reduces prerender output size and build time.

**Alternatives Considered**:
- Prerender all routes: Security risk; authenticated content could leak into static files.
- Dynamic prerender based on route: Over-engineered; static prerender for public, client-render for auth is simpler.

### Decision 7: robots.txt and sitemap.xml Generation
**Choice**: Auto-generate both files at build time; include in dist output.

**Rationale**:
- robots.txt guides crawlers; allows public routes, disallows authenticated areas.
- sitemap.xml helps crawlers discover all public routes with metadata (priority, last-modified).
- Automated generation prevents stale/manual files; stays in sync with actual routes.

**Alternatives Considered**:
- Manual robots.txt/sitemap.xml: Error-prone; easy to forget updating when routes change.

### Decision 8: JSON-LD Structured Data
**Choice**: Inject three JSON-LD blocks:
1. **Organization** schema (global, at app root): name, logo, description, social links.
2. **WebSite** schema (global): name, URL, searchAction (enables Google sitelink search box).
3. **ItemList** schema (landing page only): featured plugins with name, description, author, @id.

**Rationale**:
- Organization establishes domain credibility; helps Google Knowledge Panel if applicable.
- WebSite schema with SearchAction improves search engine UI (sitelink search box).
- ItemList helps crawlers understand plugin catalog structure; may enable rich search results if Google indexes plugin lists.

**Alternatives Considered**:
- Product schema for each plugin: Deferred; requires per-plugin prerender or dynamic JSON-LD injection.
- AggregateOffer for marketplace: Not applicable; plugins are not for sale.

### Decision 9: Cache Strategy for Stats Endpoint
**Choice**: 5-10 minute cache in the backend stats use-case (via IMemoryCache or similar).

**Rationale**:
- Balances freshness and performance: stats don't change rapidly, but cache is short-lived enough for most use cases.
- Reduces database queries on high-traffic landing page.
- Implementation: EF adapter caches aggregate result; invalidate cache on plugin publish/update events (if event sourcing is available) or let it auto-expire.

**Alternatives Considered**:
- No cache: Database churn on every landing page load. Unacceptable at scale.
- Long cache (1 hour+): Stats could be noticeably stale. Users see "2,000 plugins" when really 2,050. Less trustworthy.
- Invalidate on every publish: Complex event-driven cache invalidation; requires publish event wired to stats cache. Deferred for future optimization.

### Decision 10: Type Safety and No `any` Types
**Choice**: All frontend domain models, adapters, and facades use strict TypeScript with no `any` or `$any()`.

**Rationale**:
- Follows project rule: "Never use 'any' type."
- Ensures type safety for metrics model, stats response validation, and signal mutations.
- Zod or similar schema validation at HTTP adapter boundary enforces runtime type safety.

**Alternatives Considered**:
- Loose typing: Violates project convention; risks runtime type errors.

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| **Metrics freshness bounded by 5-min API cache** | Acceptable for landing page (metrics don't change rapidly). Include `datePublished` in JSON-LD to indicate last refresh. Same freshness window as static prerender. |
| **Stats endpoint becomes a bottleneck if cache misses** | Monitor cache hit rate; adjust TTL if needed. Database indexes on plugin/telemetry tables must exist. Alerts on cache miss spikes. |
| **Social media crawlers evolve; some may bypass server-rendered HTML** | SSR is resilient: even if crawler executes JS post-render, server-side content is already complete. Client-side meta tags apply as fallback. Best-effort; cannot guarantee all crawlers work. |
| **Hosting must run Node SSR server, not just serve static files** | Document deployment (Docker, Node.js version, memory/CPU sizing). CI/CD pipeline builds server bundle. Monitor Node process health and response times. |
| **Node SSR server uses more resources than static file serving** | Acceptable overhead for typical landing page SSR (lightweight). Vertical scale container as needed. Monitor server metrics; auto-scale if traffic spikes. |
| **TransferState hydration bugs on browser-global objects** | Guards check `isPlatformBrowser()` before accessing window/document; prevents server-side crashes. Comprehensive E2E test coverage for SSR scenarios. |
| **JSON-LD structured data may not be indexed by search engines** | JSON-LD is best-effort for search engine optimization. Rich snippets are not guaranteed. Implementation is still valuable; risk is low (no negative impact if not indexed). |
| **Stats domain adds complexity to frontend folder structure** | Justified by reusability (metrics facade can be injected elsewhere) and testability. Small price for Clean Architecture compliance. |

## Migration Plan

1. **Phase 1: Backend**
   - Add `GetMarketplaceStatsUseCase` to Telemetry module (application layer).
   - Add `IMarketplaceStatsPort` port interface.
   - Implement `EfMarketplaceStatsAdapter` (infrastructure layer) with EF queries and caching.
   - Add `StatsController` endpoint `GET /api/v1/stats` routed through the use-case.
   - Update OpenAPI schema.
   - Write integration tests for use-case + adapter.

2. **Phase 2: Frontend — Metrics Domain**
   - Create `home/domain/models/marketplace-metrics.model.ts`.
   - Create `home/domain/ports/marketplace-stats.port.ts`.
   - Create `home/infrastructure/adapters/http-marketplace-stats.adapter.ts`.
   - Create `home/application/facades/home-metrics.facade.ts` (signal-based).
   - Write unit tests for domain, adapter, facade.

3. **Phase 3: Frontend — SEO Service**
   - Create `shared/services/seo-metadata.service.ts` injecting Angular Title/Meta services.
   - Create `shared/models/seo-config.ts` with centralized title/description/OG config.
   - Wire SEO service into landing page component (ngOnInit).
   - Create `shared/services/structured-data.service.ts` for JSON-LD generation.

4. **Phase 4: Frontend — Stats Band UI & Landing Page Updates**
   - Create `home/presentation/stats-band.component.ts` injecting metrics facade.
   - Update `home/presentation/landing-page.component.ts` to inject stats band and SEO service.
   - Style stats band with responsive grid, loading/error states.
   - Write component tests for stats band and landing page.

5. **Phase 5: Angular SSR & Static Assets**
   - Install/upgrade `@angular/ssr` if not present.
   - Configure `angular.json` SSR build configuration with server entry point.
   - Create `robots.txt` with allow/disallow rules.
   - Create `sitemap.xml` generation script (or use build-time generation).
   - Update `ng build` command to build SSR server bundle (`dist/frontend/server/server.mjs`).
   - Update Docker and deployment configuration to run Node SSR server (`node dist/frontend/server/server.mjs` on port 4200 or configured port).
   - Document build, deployment, and Node server resource requirements.

6. **Phase 6: Testing & QA**
   - Build SSR bundle locally: `ng build --configuration=ssr` or equivalent.
   - Run SSR server locally: `node dist/frontend/server/server.mjs`.
   - Verify `<title>`, `<meta>` tags, JSON-LD are rendered server-side in HTML response.
   - Test social media unfurls (use Facebook/Twitter Share Debugger, LinkedIn Post Inspector).
   - Verify robots.txt/sitemap.xml are served correctly by the SSR server.
   - E2E test landing page loads correctly via SSR; stats display; error handling works.
   - Verify TransferState transfers server-rendered data to client (browser DevTools check for transfer in HTML).
   - Performance profiling (Lighthouse) on SSR-rendered pages.

7. **Phase 7: Deployment**
   - Deploy backend stats endpoint first (blue-green if applicable).
   - Deploy frontend SSR bundle: ensure Node server process is running (`node dist/frontend/server/server.mjs`).
   - Verify Docker/hosting infrastructure starts the Node SSR server correctly.
   - Monitor stats endpoint cache hit rate and response times.
   - Monitor Node SSR server health: response times, memory usage, error rates.
   - Monitor SEO metrics (Google Search Console) for improved crawlability.

## Open Questions

1. **Cache invalidation event**: Should stats cache be invalidated on plugin publish/delete events? Requires event bus integration. Deferred for now (TTL-only invalidation acceptable).

2. **Multilingual SEO**: How should SEO metadata be handled if i18n is added in the future? Prerender per language? Canonical links with hreflang? Deferred; assumes English-only for now.

3. **Dynamic metrics in prerendered HTML**: If metrics are prerendered (stale), should we show a "last updated" timestamp in the stats band to be transparent? Consider adding a `dataPublished` ISO timestamp to the stats response and displaying it.

4. **Staging vs. Production SEO**: Should staging deployment have different robots.txt (disallow all) to prevent staging indexing? Yes; environment-specific robots.txt generation recommended. Add to deployment docs.

5. **Stats accuracy**: If a plugin is deleted, should its downloads be removed from totalDownloads aggregate? Check PluginCatalog domain logic. Define "soft delete" vs. "hard delete" behavior. Likely out of scope for this change; document assumption.
