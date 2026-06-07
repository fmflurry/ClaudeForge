## Why

The marketplace landing page (`/`) is the first impression for potential plugin users and publishers. Currently, it shows static placeholder content for marketplace metrics and lacks SEO optimization, limiting discoverability via search engines and social media. By adding real marketplace metrics (live download counts, publisher count, catalog size) and implementing proper SEO metadata with structured data, we improve both user confidence and organic reach.

## What Changes

- **Backend**: New aggregate stats endpoint (`GET /api/v1/stats` or extend catalog module) returning marketplace totals (total plugins, total downloads, publisher count, categories count) with short-lived cache.
- **Frontend home domain**: New `metrics` capability with HTTP adapter, facade, and signal-based store for stats fetching.
- **Landing page UI**: Replace static numbers in hero section with a live "stats band" showing real metrics (gracefully handles loading/error/empty states).
- **SEO metadata service**: Angular `Title` and `Meta` services to set per-route title, description, Open Graph tags, Twitter Card, canonical URL, and JSON-LD structured data (Organization + ItemList of featured plugins).
- **Angular SSR**: Configure Angular Server-Side Rendering for public routes (`/`, `/catalog`, `/docs`) using `@angular/ssr` Node server, with `robots.txt` and generated `sitemap.xml`.

## Capabilities

### New Capabilities

- `marketplace-stats-endpoint`: Backend REST endpoint returning marketplace aggregates (plugin count, downloads total, publisher count, category count) with caching.
- `home-metrics-domain`: Frontend domain port, HTTP adapter, and signal-based facade for fetching and displaying marketplace metrics.
- `landing-stats-band-ui`: UI component rendering live marketplace statistics in the hero section with loading/error/empty handling.
- `seo-metadata-service`: Angular service managing Title, Meta tags, Open Graph, Twitter Card, canonical, and JSON-LD structured data per route.
- `static-prerender-public-routes`: Angular prerender configuration for public-facing routes with `robots.txt` and `sitemap.xml` generation.

### Modified Capabilities

- `home-landing-page`: Hero section now displays live metrics instead of static placeholders; SEO metadata applied to landing route.

## Impact

- **Backend**: New use-case + port + EF adapter in Telemetry or new Stats module (Clean/Hexagonal conventions).
- **Frontend**: New `home/domain`, `home/application`, `home/infrastructure` layers following existing catalog feature structure; landing page component refactored to integrate metrics facade.
- **Infrastructure**: Angular SSR (server-side rendering via `@angular/ssr` Node server) renders public routes on each request; hosting must run the Node SSR server process (`node dist/frontend/server/server.mjs`).
- **Dependencies**: `@angular/ssr` for SSR; OpenAPI schema updated with new stats endpoint.
- **Testing**: ≥80% coverage expected for new domain logic, facades, and UI components (signal-based stores, HTTP adapters).
