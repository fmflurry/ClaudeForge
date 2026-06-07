## 1. Backend Setup — Marketplace Stats Endpoint

- [ ] 1.1 Create GetMarketplaceStatsUseCase in ClaudeForge.Application/Modules/Telemetry/UseCases/
- [ ] 1.2 Create IMarketplaceStatsPort interface in ClaudeForge.Application/Modules/Telemetry/Ports/
- [ ] 1.3 Create MarketplaceStatsDto (DTO) in ClaudeForge.Application/Modules/Telemetry/Ports/
- [ ] 1.4 Implement EfMarketplaceStatsAdapter in ClaudeForge.Infrastructure/Telemetry/
- [ ] 1.5 Add IMemoryCache and caching logic to adapter (5-10 minute TTL)
- [ ] 1.6 Create StatsController with GET /api/v1/stats endpoint in ClaudeForge.Api/Modules/Telemetry/
- [ ] 1.7 Register use-case and adapter in module extension method (dependency injection)
- [ ] 1.8 Update OpenAPI schema to include GET /api/v1/stats endpoint with example response
- [ ] 1.9 Write unit tests for GetMarketplaceStatsUseCase (mocked port)
- [ ] 1.10 Write integration tests for EfMarketplaceStatsAdapter (test database queries)
- [ ] 1.11 Write integration tests for StatsController endpoint

## 2. Backend Verification — Stats Endpoint

- [ ] 2.1 Run backend unit and integration tests for stats module
- [ ] 2.2 Verify GET /api/v1/stats returns correct JSON with all four fields
- [ ] 2.3 Test cache behavior: verify second request is faster (cached)
- [ ] 2.4 Test error handling: trigger database error and verify RFC7807 response
- [ ] 2.5 Verify OpenAPI schema is correct (dotnet build generates OpenAPI spec)

## 3. Frontend Setup — Home Metrics Domain

- [ ] 3.1 Create src/app/features/home/domain/models/marketplace-metrics.model.ts with readonly interface
- [ ] 3.2 Create src/app/features/home/domain/ports/marketplace-stats.port.ts with port interface
- [ ] 3.3 Create src/app/features/home/infrastructure/adapters/http-marketplace-stats.adapter.ts
- [ ] 3.4 Implement HTTP adapter: fetch from GET /api/v1/stats, map to domain model, handle errors
- [ ] 3.5 Add Zod schema for runtime validation of API response in HTTP adapter
- [ ] 3.6 Create src/app/features/home/application/facades/home-metrics.facade.ts
- [ ] 3.7 Implement facade with signal-based store (isLoadingStats, stats, statsError signals)
- [ ] 3.8 Implement facade loadStats() method using port injection
- [ ] 3.9 Register facade and adapter in home feature providers (dependency injection)
- [ ] 3.10 Write unit tests for marketplace-metrics model
- [ ] 3.11 Write unit tests for home-metrics facade (mocked port)
- [ ] 3.12 Write integration tests for http-marketplace-stats adapter

## 4. Frontend Setup — SEO Metadata Service

- [ ] 4.1 Create src/app/shared/services/seo-metadata.service.ts injecting Angular Title and Meta services
- [ ] 4.2 Create src/app/shared/models/seo-config.ts with centralized configuration (title, description, keywords, OG tags, etc.)
- [ ] 4.3 Implement SEO service method: setSeoMetadata(config: SeoConfig) to update all meta tags
- [ ] 4.4 Create src/app/shared/services/structured-data.service.ts for JSON-LD injection
- [ ] 4.5 Implement Organization JSON-LD schema generation and injection
- [ ] 4.6 Implement WebSite JSON-LD schema generation and injection
- [ ] 4.7 Implement ItemList JSON-LD schema generation for featured plugins
- [ ] 4.8 Provide SEO service and structured-data service in root providers (app.config.ts)
- [ ] 4.9 Write unit tests for seo-metadata service (mock Title and Meta)
- [ ] 4.10 Write unit tests for structured-data service (verify JSON-LD structure)

## 5. Frontend UI — Stats Band Component

- [ ] 5.1 Create src/app/features/home/presentation/stats-band.component.ts
- [ ] 5.2 Implement stats band component: inject home-metrics facade, call loadStats() on init
- [ ] 5.3 Implement template with four metric cards (total plugins, total downloads, publisher count, categories)
- [ ] 5.4 Implement loading state: show "Loading marketplace stats…" with aria-busy
- [ ] 5.5 Implement error state: show error message with retry button
- [ ] 5.6 Implement empty state: graceful degradation if no metrics available
- [ ] 5.7 Add number formatting (use existing formatDownloads utility or create shared utility)
- [ ] 5.8 Add responsive styling: grid layout on desktop, single column on mobile
- [ ] 5.9 Add ARIA labels and semantic HTML (article or region role, live regions for loading/error)
- [ ] 5.10 Write component tests for stats band (test all loading/error/success states)

## 6. Frontend Updates — Landing Page Integration

- [ ] 6.1 Update landing-page.component.ts: import and include stats-band component
- [ ] 6.2 Update landing-page.component template: add <cf-stats-band> above "Popular plugins" section
- [ ] 6.3 Update landing-page.component.ts: inject SeoMetadataService
- [ ] 6.4 Update landing-page.component.ts: call SEO service in ngOnInit to set landing page metadata
- [ ] 6.5 Update landing-page.component.ts: call structured-data service to inject landing page JSON-LD
- [ ] 6.6 Verify landing page retains all existing functionality (hero CTAs, search, featured plugins, footer)
- [ ] 6.7 Update component tests for landing page (test SEO service calls)

## 7. Frontend Verification — Metrics and SEO

- [ ] 7.1 Run npm run test for all new components and services
- [ ] 7.2 Check test coverage for home/metrics domain (>80%)
- [ ] 7.3 Check test coverage for SEO services (>80%)
- [ ] 7.4 Run npx tsc --noEmit to verify TypeScript compilation (no errors)
- [ ] 7.5 Run npm run lint to verify eslint/prettier compliance
- [ ] 7.6 Verify landing page loads without errors in development server
- [ ] 7.7 Verify stats band displays loading state while fetching (open DevTools Network tab)
- [ ] 7.8 Verify stats band displays real metrics once loaded
- [ ] 7.9 Verify stats band error state works (mock API error, inspect error message)
- [ ] 7.10 Verify all four metrics are displayed with correct formatting

## 8. SEO Verification — Metadata in HTML

- [ ] 8.1 Open landing page in browser; inspect HTML head (F12 DevTools)
- [ ] 8.2 Verify <title> tag matches expected landing page title
- [ ] 8.3 Verify <meta name="description"> is present and correct
- [ ] 8.4 Verify <meta name="canonical"> points to correct absolute URL
- [ ] 8.5 Verify Open Graph tags present: og:title, og:description, og:image, og:type
- [ ] 8.6 Verify Twitter Card tags present: twitter:card, twitter:title, twitter:description
- [ ] 8.7 Verify JSON-LD <script> tags present: Organization, WebSite, ItemList (search DevTools console)
- [ ] 8.8 Test social media unfurls using Facebook Share Debugger (https://developers.facebook.com/tools/debug)
- [ ] 8.9 Test social media unfurls using Twitter Card Validator (https://cards-dev.twitter.com/validator)
- [ ] 8.10 Verify ItemList JSON-LD contains all 6 featured plugins with correct fields

## 9. Static Prerender Configuration

- [ ] 9.1 Check if @angular/ssr is installed (npm list @angular/ssr)
- [ ] 9.2 If not installed, run ng add @angular/ssr
- [ ] 9.3 Update angular.json: configure prerender routes (/, /catalog, /docs)
- [ ] 9.4 Create prerender-routes.ts file to define routes and guard conditions (if needed)
- [ ] 9.5 Update package.json build script to include --prerender flag
- [ ] 9.6 Create robots.txt in src/robots.txt with allow/disallow rules and sitemap reference
- [ ] 9.7 Create script or webpack plugin to generate sitemap.xml at build time (or manually create)
- [ ] 9.8 Update src/index.html or build process to ensure robots.txt and sitemap.xml are copied to dist
- [ ] 9.9 Run ng build --prerender locally and inspect dist folder
- [ ] 9.10 Verify prerendered HTML files exist for /, /catalog, /docs

## 10. Prerender Verification — Static Files

- [ ] 10.1 Inspect dist/index.html: verify <title>, <meta>, JSON-LD are pre-rendered (not empty)
- [ ] 10.2 Inspect dist/catalog/index.html: verify metadata is pre-rendered for catalog route
- [ ] 10.3 Verify robots.txt exists in dist root and is readable
- [ ] 10.4 Verify sitemap.xml exists in dist root and is valid XML
- [ ] 10.5 Verify robots.txt includes Sitemap: reference to sitemap.xml
- [ ] 10.6 Verify sitemap.xml contains entries for /, /catalog, /docs with proper schema
- [ ] 10.7 Test prerendered static files locally with `http-server dist` or similar
- [ ] 10.8 Verify prerendered pages are served (not Angular SPA shell) when requesting static files
- [ ] 10.9 Verify authenticated routes like /dashboard are NOT prerendered (should be client-rendered)
- [ ] 10.10 Run Lighthouse on prerendered landing page, verify good SEO score

## 11. Documentation

- [ ] 11.1 Update project README or docs with prerender build instructions
- [ ] 11.2 Document deployment requirement: web server must serve prerendered HTML from dist
- [ ] 11.3 Document SEO metadata configuration location (seo-config.ts)
- [ ] 11.4 Document how to add new routes to prerender (update angular.json or prerender-routes.ts)
- [ ] 11.5 Document stats endpoint contract (URL, response schema, cache behavior)
- [ ] 11.6 Add architecture diagram or code structure documentation for home/metrics domain

## 12. Integration Testing — E2E

- [ ] 12.1 Write Playwright E2E test: landing page loads, stats band displays metrics
- [ ] 12.2 Write Playwright E2E test: landing page has correct SEO metadata (check HTML head)
- [ ] 12.3 Write Playwright E2E test: stats band error handling (mock API 500, inspect error state)
- [ ] 12.4 Write Playwright E2E test: hero CTAs, search, featured plugins still work
- [ ] 12.5 Write Playwright E2E test: prerendered pages are served as static HTML (check response headers)
- [ ] 12.6 Run all E2E tests locally and verify pass

## 13. Final QA and Deployment

- [ ] 13.1 Review all code changes against project conventions (facade-only, no any types, immutability, Clean Architecture)
- [ ] 13.2 Code review: get approval from team lead or architecture reviewer
- [ ] 13.3 Verify git status is clean (all changes staged/committed)
- [ ] 13.4 Build backend (dotnet build) and verify no errors
- [ ] 13.5 Build frontend (ng build --prerender) and verify no errors
- [ ] 13.6 Run full test suite (backend + frontend + E2E) and verify all pass
- [ ] 13.7 Test on staging environment (if available) before production deployment
- [ ] 13.8 Create pull request with comprehensive description referencing this change
- [ ] 13.9 Deploy backend stats endpoint to production
- [ ] 13.10 Deploy frontend with prerender to production
- [ ] 13.11 Verify landing page works on production (stats display, metadata present)
- [ ] 13.12 Monitor stats endpoint cache hit rate and performance for 24 hours
- [ ] 13.13 Check Google Search Console for crawl activity increase after deployment
