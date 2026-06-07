## 1. Backend Setup — Marketplace Stats Endpoint

- [x] 1.1 Create GetMarketplaceStatsUseCase in ClaudeForge.Application/Modules/Telemetry/UseCases/
- [x] 1.2 Create IMarketplaceStatsPort interface in ClaudeForge.Application/Modules/Telemetry/Ports/
- [x] 1.3 Create MarketplaceStatsDto (DTO) in ClaudeForge.Application/Modules/Telemetry/Ports/
- [x] 1.4 Implement EfMarketplaceStatsAdapter in ClaudeForge.Infrastructure/Telemetry/
- [x] 1.5 Add IMemoryCache and caching logic to adapter (5-10 minute TTL)
- [x] 1.6 Create StatsController with GET /api/v1/stats endpoint in ClaudeForge.Api/Modules/Telemetry/
- [x] 1.7 Register use-case and adapter in module extension method (dependency injection)
- [x] 1.8 Update OpenAPI schema to include GET /api/v1/stats endpoint with example response
- [x] 1.9 Write unit tests for GetMarketplaceStatsUseCase (mocked port)
- [x] 1.10 Write integration tests for EfMarketplaceStatsAdapter (test database queries)
- [x] 1.11 Write integration tests for StatsController endpoint

## 2. Backend Verification — Stats Endpoint

- [x] 2.1 Run backend unit and integration tests for stats module
- [x] 2.2 Verify GET /api/v1/stats returns correct JSON with all four fields
- [x] 2.3 Test cache behavior: verify second request is faster (cached)
- [x] 2.4 Test error handling: trigger database error and verify RFC7807 response
- [x] 2.5 Verify OpenAPI schema is correct (dotnet build generates OpenAPI spec)

## 3. Frontend Setup — Home Metrics Domain

- [x] 3.1 Create src/app/features/home/domain/models/marketplace-metrics.model.ts with readonly interface
- [x] 3.2 Create src/app/features/home/domain/ports/marketplace-stats.port.ts with port interface
- [x] 3.3 Create src/app/features/home/infrastructure/adapters/http-marketplace-stats.adapter.ts
- [x] 3.4 Implement HTTP adapter: fetch from GET /api/v1/stats, map to domain model, handle errors
- [x] 3.5 Add Zod schema for runtime validation of API response in HTTP adapter
- [x] 3.6 Create src/app/features/home/application/facades/home-metrics.facade.ts
- [x] 3.7 Implement facade with signal-based store (isLoadingStats, stats, statsError signals)
- [x] 3.8 Implement facade loadStats() method using port injection
- [x] 3.9 Register facade and adapter in home feature providers (dependency injection)
- [x] 3.10 Write unit tests for marketplace-metrics model
- [x] 3.11 Write unit tests for home-metrics facade (mocked port)
- [x] 3.12 Write integration tests for http-marketplace-stats adapter

## 4. Frontend Setup — SEO Metadata Service

- [x] 4.1 Create src/app/shared/services/seo-metadata.service.ts injecting Angular Title and Meta services
- [x] 4.2 Create src/app/shared/models/seo-config.ts with centralized configuration (title, description, keywords, OG tags, etc.)
- [x] 4.3 Implement SEO service method: setSeoMetadata(config: SeoConfig) to update all meta tags
- [x] 4.4 Create src/app/shared/services/structured-data.service.ts for JSON-LD injection
- [x] 4.5 Implement Organization JSON-LD schema generation and injection
- [x] 4.6 Implement WebSite JSON-LD schema generation and injection
- [x] 4.7 Implement ItemList JSON-LD schema generation for featured plugins
- [x] 4.8 Provide SEO service and structured-data service in root providers (app.config.ts)
- [x] 4.9 Write unit tests for seo-metadata service (mock Title and Meta)
- [x] 4.10 Write unit tests for structured-data service (verify JSON-LD structure)

## 5. Frontend UI — Stats Band Component

- [x] 5.1 Create src/app/features/home/presentation/stats-band.component.ts
- [x] 5.2 Implement stats band component: inject home-metrics facade, call loadStats() on init
- [x] 5.3 Implement template with four metric cards (total plugins, total downloads, publisher count, categories)
- [x] 5.4 Implement loading state: show "Loading marketplace stats…" with aria-busy
- [x] 5.5 Implement error state: show error message with retry button
- [x] 5.6 Implement empty state: graceful degradation if no metrics available
- [x] 5.7 Add number formatting (use existing formatDownloads utility or create shared utility)
- [x] 5.8 Add responsive styling: grid layout on desktop, single column on mobile
- [x] 5.9 Add ARIA labels and semantic HTML (article or region role, live regions for loading/error)
- [x] 5.10 Write component tests for stats band (test all loading/error/success states)

## 6. Frontend Updates — Landing Page Integration

- [x] 6.1 Update landing-page.component.ts: import and include stats-band component
- [x] 6.2 Update landing-page.component template: add <cf-stats-band> above "Popular plugins" section
- [x] 6.3 Update landing-page.component.ts: inject SeoMetadataService
- [x] 6.4 Update landing-page.component.ts: call SEO service in ngOnInit to set landing page metadata
- [x] 6.5 Update landing-page.component.ts: call structured-data service to inject landing page JSON-LD
- [x] 6.6 Verify landing page retains all existing functionality (hero CTAs, search, featured plugins, footer)
- [x] 6.7 Update component tests for landing page (test SEO service calls)

## 7. Frontend Verification — Metrics and SEO

- [x] 7.1 Run npm run test for all new components and services
- [x] 7.2 Check test coverage for home/metrics domain (>80%)
- [x] 7.3 Check test coverage for SEO services (>80%)
- [x] 7.4 Run npx tsc --noEmit to verify TypeScript compilation (no errors)
- [x] 7.5 Run npm run lint to verify eslint/prettier compliance
- [x] 7.6 Verify landing page loads without errors in development server
- [x] 7.7 Verify stats band displays loading state while fetching (open DevTools Network tab)
- [x] 7.8 Verify stats band displays real metrics once loaded
- [x] 7.9 Verify stats band error state works (mock API error, inspect error message)
- [x] 7.10 Verify all four metrics are displayed with correct formatting

## 8. SEO Verification — Metadata in HTML

- [x] 8.1 Open landing page in browser; inspect HTML head (F12 DevTools)
- [x] 8.2 Verify <title> tag matches expected landing page title
- [x] 8.3 Verify <meta name="description"> is present and correct
- [x] 8.4 Verify <meta name="canonical"> points to correct absolute URL
- [x] 8.5 Verify Open Graph tags present: og:title, og:description, og:image, og:type
- [x] 8.6 Verify Twitter Card tags present: twitter:card, twitter:title, twitter:description
- [x] 8.7 Verify JSON-LD <script> tags present: Organization, WebSite, ItemList (search DevTools console)
- [ ] 8.8 Test social media unfurls using Facebook Share Debugger (https://developers.facebook.com/tools/debug) — *deferred, og-image.png asset not created yet*
- [ ] 8.9 Test social media unfurls using Twitter Card Validator (https://cards-dev.twitter.com/validator) — *deferred, og-image.png asset not created yet*
- [x] 8.10 Verify ItemList JSON-LD contains all 6 featured plugins with correct fields

## 9. Angular SSR Configuration

- [x] 9.1 Check if @angular/ssr is installed (npm list @angular/ssr)
- [x] 9.2 If not installed, run ng add @angular/ssr
- [x] 9.3 Update angular.json: configure SSR build configuration with server entry point
- [x] 9.4 Create/update server.ts, main.server.ts, app.config.server.ts, app.routes.server.ts for SSR
- [x] 9.5 Update package.json build script to build SSR server bundle
- [x] 9.6 Create robots.txt in frontend/public/robots.txt with allow/disallow rules and sitemap reference
- [x] 9.7 Create sitemap.xml in frontend/public/sitemap.xml at build time
- [x] 9.8 Ensure robots.txt and sitemap.xml are served as static assets by the SSR server
- [x] 9.9 Run ng build locally and inspect dist/frontend/{browser,server} folders
- [x] 9.10 Verify SSR server bundle exists at dist/frontend/server/server.mjs

## 10. SSR Verification — Server-Rendered Output

- [x] 10.1 Inspect curl response for landing page: verify <title>, <meta>, JSON-LD are server-rendered (not empty)
- [x] 10.2 Inspect curl response for catalog route: verify metadata is server-rendered for catalog route
- [x] 10.3 Verify robots.txt is served by SSR server and contains proper allow/disallow rules
- [x] 10.4 Verify sitemap.xml is served by SSR server and is valid XML
- [x] 10.5 Verify robots.txt includes Sitemap: reference to sitemap.xml
- [x] 10.6 Verify sitemap.xml contains entries for /, /catalog, /docs with proper schema
- [x] 10.7 Test SSR server locally with `node dist/frontend/server/server.mjs` and curl requests
- [x] 10.8 Verify SSR server renders pages with full HTML (title, meta, script tags) on each request
- [x] 10.9 Verify authenticated routes like /dashboard are served as SPA shell with guards (not server-rendered with data)
- [ ] 10.10 Run Lighthouse on landing page served via SSR, verify good SEO score — *deferred, coverage gate pending*

## 11. Documentation

- [ ] 11.1 Update project README or docs with SSR build and deployment instructions
- [ ] 11.2 Document deployment requirement: Node SSR server must run (node dist/frontend/server/server.mjs)
- [ ] 11.3 Document SEO metadata configuration location (shared/infrastructure/seo/)
- [ ] 11.4 Document how to add new SSR routes (update app.routes.server.ts, ensure guards are SSR-safe)
- [ ] 11.5 Document stats endpoint contract (URL, response schema, cache behavior)
- [ ] 11.6 Add architecture diagram or code structure documentation for home/metrics domain

## 12. Integration Testing — E2E

- [ ] 12.1 Write Playwright E2E test: landing page loads via SSR, stats band displays metrics
- [ ] 12.2 Write Playwright E2E test: landing page has correct SEO metadata (check HTML head)
- [ ] 12.3 Write Playwright E2E test: stats band error handling (mock API 500, inspect error state)
- [ ] 12.4 Write Playwright E2E test: hero CTAs, search, featured plugins still work
- [ ] 12.5 Write Playwright E2E test: SSR renders pages with server-side data on initial request
- [ ] 12.6 Run all E2E tests locally and verify pass — *deferred, coverage gate pending*

## 13. Final QA and Deployment

- [x] 13.1 Review all code changes against project conventions (facade-only, no any types, immutability, Clean Architecture)
- [ ] 13.2 Code review: get approval from team lead or architecture reviewer — *deferred pending merge decision*
- [x] 13.3 Verify git status is clean (all changes staged/committed)
- [x] 13.4 Build backend (dotnet build) and verify no errors
- [x] 13.5 Build frontend (ng build) and verify SSR server bundle generated
- [ ] 13.6 Run full test suite (backend + frontend + E2E) and verify all pass — *deferred, coverage gate pending*
- [ ] 13.7 Test on staging environment (if available) before production deployment — *deferred*
- [ ] 13.8 Create pull request with comprehensive description referencing this change — *awaiting user decision*
- [ ] 13.9 Deploy backend stats endpoint to production — *deferred*
- [ ] 13.10 Deploy frontend with SSR server to production — *deferred*
- [ ] 13.11 Verify landing page works on production (stats display, metadata present, SSR renders) — *deferred*
- [ ] 13.12 Monitor stats endpoint cache hit rate and response times for 24 hours — *deferred*
- [ ] 13.13 Check Google Search Console for crawl activity increase after deployment — *deferred*
