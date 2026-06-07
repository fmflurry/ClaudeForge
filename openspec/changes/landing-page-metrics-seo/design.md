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

### Decision 5: SEO Metadata Approach — Static Prerender vs. Meta Tags Only
**Choice**: **Static prerender with Angular `@angular/ssr`** for public routes (/, /catalog, /docs). Meta tags applied at runtime for fallback and dynamic content.

**Rationale for Prerender (RECOMMENDED)**:
- **Crawlability**: Search engines (Google, Bing) and social media crawlers get full HTML with metadata already rendered. No JavaScript execution needed.
- **Performance**: Prerendered pages serve static HTML instantly; no server-side rendering overhead.
- **Reliability**: Metadata is "baked in" at build time; no runtime dependencies on the SEO service during crawl.
- **Social unfurls**: Facebook, Twitter, LinkedIn crawlers can parse metadata immediately without waiting for JavaScript.

**Prerender Trade-off & Mitigation**:
- **Build time increases**: Prerendering adds 30-60 seconds to the build process (acceptable for CI/CD).
  - *Mitigation*: Parallelize other build steps; document in CI/CD pipeline.
- **Hosting requirement**: Web server must serve prerendered HTML files from dist; some hosting platforms (Vercel, Netlify) handle this automatically.
  - *Mitigation*: Document hosting/deployment requirements. Fallback meta tags provide basic unfurl support if prerender is not deployed correctly.
- **Metrics are stale**: Prerendered stats are as fresh as the last build. If stats change between builds, prerendered HTML shows old numbers until next deploy.
  - *Mitigation*: Acceptable for landing page (metrics don't change rapidly). Publish a timestamp in JSON-LD indicating when data was last updated. Consider daily scheduled builds or on-demand builds triggered by metric changes.

**Alternative: Meta Tags Only (NOT RECOMMENDED)**:
- Simpler: no prerender configuration; only client-side meta tag updates.
- **Major limitation**: Social media crawlers execute JavaScript inconsistently (some do, some don't). Meta tags set by JavaScript may not be visible to crawlers.
- **Risk**: Facebook unfurls may show generic title/image; Twitter cards may not render rich preview.
- **SEO impact**: Crawlers that don't execute JavaScript won't see metadata; search engine visibility reduced for some crawlers.

**Recommendation**: Implement prerender for maximum reach. Use client-side meta tags as fallback for content that changes frequently (e.g., plugin list updates).

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
| **Prerendered metrics are stale between builds** | Acceptable for landing page (metrics don't change frequently). Include `datePublished` in JSON-LD to indicate freshness. Consider daily scheduled builds or on-demand builds on metrics changes. |
| **Stats endpoint becomes a bottleneck if cache misses** | Monitor cache hit rate; adjust TTL if needed. Database indexes on plugin/telemetry tables must exist. Alerts on cache miss spikes. |
| **Social media crawlers evolve; some may bypass prerendered HTML** | Prerender is resilient: even if crawler does execute JS, client-side meta tags apply as fallback. Best-effort; cannot guarantee all crawlers work. |
| **Prerender adds 30-60s to build time** | Acceptable for CI/CD pipeline. Document in build docs. Can parallelize with other build steps. |
| **Hosting platform must serve prerendered HTML files correctly** | Document hosting requirements (e.g., static file serving from dist, Angular routing fallback to index.html for SPA routes). Test deployment on actual hosting platform. |
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

5. **Phase 5: Prerender & Static Assets**
   - Install/upgrade `@angular/ssr` if not present.
   - Configure `angular.json` prerender routes: /, /catalog, /docs.
   - Create `robots.txt` with allow/disallow rules.
   - Create `sitemap.xml` generation script (or use build-time generation).
   - Update `ng build` command to include `--prerender` flag.
   - Document build + deployment steps.

6. **Phase 6: Testing & QA**
   - Run `ng build --prerender` locally; inspect dist output for prerendered HTML.
   - Verify `<title>`, `<meta>` tags, JSON-LD in prerendered HTML.
   - Test social media unfurls (use Facebook/Twitter Share Debugger, LinkedIn Post Inspector).
   - Verify robots.txt/sitemap.xml are served correctly.
   - E2E test landing page loads correctly; stats display; error handling works.
   - Performance profiling (Lighthouse) on prerendered vs. non-prerendered.

7. **Phase 7: Deployment**
   - Deploy backend stats endpoint first (blue-green if applicable).
   - Deploy frontend with prerender flag.
   - Verify hosting platform serves prerendered HTML files.
   - Monitor stats endpoint cache hit rate and response times.
   - Monitor SEO metrics (Google Search Console) for improved crawlability.

## Open Questions

1. **Cache invalidation event**: Should stats cache be invalidated on plugin publish/delete events? Requires event bus integration. Deferred for now (TTL-only invalidation acceptable).

2. **Multilingual SEO**: How should SEO metadata be handled if i18n is added in the future? Prerender per language? Canonical links with hreflang? Deferred; assumes English-only for now.

3. **Dynamic metrics in prerendered HTML**: If metrics are prerendered (stale), should we show a "last updated" timestamp in the stats band to be transparent? Consider adding a `dataPublished` ISO timestamp to the stats response and displaying it.

4. **Staging vs. Production SEO**: Should staging deployment have different robots.txt (disallow all) to prevent staging indexing? Yes; environment-specific robots.txt generation recommended. Add to deployment docs.

5. **Stats accuracy**: If a plugin is deleted, should its downloads be removed from totalDownloads aggregate? Check PluginCatalog domain logic. Define "soft delete" vs. "hard delete" behavior. Likely out of scope for this change; document assumption.
