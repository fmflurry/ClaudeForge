## ADDED Requirements

### Requirement: Angular SSR configuration for public routes
The build process SHALL configure Angular's `@angular/ssr` server-side rendering to render public-facing routes server-side on each request. The routes rendered server-side are: `/` (landing), `/catalog` (plugin catalog), and `/docs` (documentation). The SSR Node server SHALL render these routes fresh per request, returning complete HTML with SEO metadata already included. The server bundle SHALL be deployed and executed as `node dist/frontend/server/server.mjs`.

#### Scenario: Public routes are rendered server-side per request
- **WHEN** a user or crawler requests `/`, `/catalog`, or `/docs`
- **THEN** the Node SSR server renders the route server-side and returns complete HTML (with full DOM and metadata already rendered)

#### Scenario: SSR-rendered HTML is served for public routes
- **WHEN** a user requests `/` or `/catalog` or `/docs`
- **THEN** the SSR server renders and returns the HTML with all SEO metadata, data transfer state, and client-side bundle references

#### Scenario: Authenticated routes remain client-side only
- **WHEN** a user requests `/dashboard` or `/auth/login` or other authenticated routes
- **THEN** those routes are served as client-side rendered (SPA shell) and authentication guards prevent access

### Requirement: robots.txt generated and served
The project SHALL generate a `robots.txt` file that allows all bots to crawl public routes (`/`, `/catalog`, `/docs`, `/search`) and disallows crawling of authenticated areas (`/dashboard`, `/auth/*`). The `robots.txt` file SHALL be placed in the dist root and served as a static asset.

#### Scenario: robots.txt allows public routes
- **WHEN** a search engine bot crawls the site
- **THEN** it reads robots.txt and sees that `/`, `/catalog`, `/docs` are allowed

#### Scenario: robots.txt disallows authenticated routes
- **WHEN** a search engine bot crawls the site
- **THEN** it reads robots.txt and sees that `/dashboard` and `/auth/` are disallowed

#### Scenario: Sitemap URL is referenced in robots.txt
- **WHEN** robots.txt is served
- **THEN** it includes `Sitemap: https://example.com/sitemap.xml` pointing to the generated sitemap

### Requirement: Sitemap XML generated from public routes
The build process SHALL generate a `sitemap.xml` file containing all public routes (landing, catalog, docs, and key plugin catalog pages if practical) with `<loc>` (URL), `<lastmod>` (last modified date), and `<priority>` (0.8 for landing, 0.7 for catalog/docs). The sitemap SHALL be placed in dist and served at `/sitemap.xml`.

#### Scenario: Sitemap.xml generated with public routes
- **WHEN** the application is built
- **THEN** a sitemap.xml file is generated containing entries for /, /catalog, /docs with proper XML structure

#### Scenario: Sitemap references canonical URLs
- **WHEN** the sitemap is generated
- **THEN** each `<loc>` entry uses the absolute URL (e.g., https://example.com/catalog)

#### Scenario: Sitemap is served at /sitemap.xml
- **WHEN** a user or bot requests `/sitemap.xml`
- **THEN** the web server returns the generated sitemap file with proper Content-Type: application/xml

### Requirement: SSR configuration in angular.json
The application configuration SHALL include SSR build settings in the `angular.json` build configuration, specifying the server entry point and output locations. The configuration SHALL support server-side rendering of public routes and client-side rendering of authenticated routes.

#### Scenario: SSR server bundle is configured
- **WHEN** the build process runs `ng build`
- **THEN** it generates the server bundle at `dist/frontend/server/server.mjs` with all necessary dependencies

#### Scenario: SSR guards prevent rendering of authenticated routes
- **WHEN** the SSR server processes a request to /dashboard
- **THEN** the route guard returns false and the route is served as client-side rendered (SPA shell)

### Requirement: SSR-rendered output includes SEO metadata
The SSR Node server SHALL render all SEO metadata (title, meta description, Open Graph tags, Twitter Card, JSON-LD structured data, canonical URL) server-side in the HTML response. When a crawler or social media unfurl service fetches the page, the metadata is immediately available without executing JavaScript.

#### Scenario: Landing page SSR-rendered HTML contains SEO tags
- **WHEN** the SSR server renders the landing page
- **THEN** the response HTML includes `<title>`, `<meta name="description">`, `<meta property="og:*">`, `<script type="application/ld+json">`, etc., all already rendered server-side

#### Scenario: SSR-rendered HTML is crawlable without JavaScript
- **WHEN** a search engine bot or social media crawler requests the landing page
- **THEN** it receives complete HTML with all SEO metadata rendered server-side; no JavaScript execution is required for the metadata to be visible

#### Scenario: SSR metadata is fresh within cache window
- **WHEN** the SSR server renders the landing page
- **THEN** the metadata includes up-to-date stats bounded by the 5-minute backend cache TTL, and client-side components hydrate with TransferState to prevent double-fetch

### Requirement: Build documentation for SSR setup
The project documentation SHALL include instructions for configuring Angular SSR, building the SSR server bundle, and running the Node server. The documentation SHALL specify the deployment requirement (the Node SSR server process must be running to serve SSR-rendered HTML for public routes).

#### Scenario: Build instructions are documented
- **WHEN** a developer reads the documentation
- **THEN** it explains how to run `ng build` to generate the server bundle and how to start the server with `node dist/frontend/server/server.mjs`

#### Scenario: Deployment guide mentions SSR server setup
- **WHEN** deployment is configured
- **THEN** the deployment guide specifies that the Node SSR server process must be running (e.g., Docker entrypoint or systemd service), memory/CPU requirements, and health check configuration
