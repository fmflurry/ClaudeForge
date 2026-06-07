## ADDED Requirements

### Requirement: Angular prerender configuration for public routes
The build process SHALL configure Angular's `@angular/ssr` prerendering to generate static HTML files for public-facing routes at build time. The routes to prerender are: `/` (landing), `/catalog` (plugin catalog), and `/docs` (documentation). The prerendered HTML files SHALL be served directly by the web server without client-side routing overhead.

#### Scenario: Public routes are prerendered during build
- **WHEN** the application is built with `ng build --prerender`
- **THEN** static HTML files are generated for `/`, `/catalog`, and `/docs` in the dist output directory

#### Scenario: Prerendered HTML is served for public routes
- **WHEN** a user requests `/` or `/catalog` or `/docs`
- **THEN** the web server returns the pre-generated static HTML file (with full DOM and metadata already rendered)

#### Scenario: Authenticated routes remain client-side only
- **WHEN** a user requests `/dashboard` or `/auth/login` or other authenticated routes
- **THEN** those routes are NOT prerendered and remain client-side rendered only

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

### Requirement: Prerender configuration in angular.json or prerender config file
The application configuration SHALL include prerender settings (routes to prerender, prerender guard conditions) in the `angular.json` build configuration or a dedicated prerender routes file (e.g., `prerender-routes.ts`). The configuration SHALL be maintainable and support easy addition of new routes.

#### Scenario: Prerender routes defined in configuration
- **WHEN** the build tool reads the configuration
- **THEN** it finds the list of routes to prerender: /, /catalog, /docs

#### Scenario: Prerender guards prevent rendering of authenticated routes
- **WHEN** the prerender process evaluates a route like /dashboard
- **THEN** a guard condition returns false and that route is skipped from prerendering

### Requirement: Prerendered output includes SEO metadata
The prerendered HTML files SHALL include all SEO metadata (title, meta description, Open Graph tags, Twitter Card, JSON-LD structured data, canonical URL) already rendered in the static HTML at build time. When a crawler or social media unfurl service fetches the page, the metadata is immediately available without executing JavaScript.

#### Scenario: Landing page prerendered HTML contains SEO tags
- **WHEN** the landing page is prerendered
- **THEN** the generated HTML file includes `<title>`, `<meta name="description">`, `<meta property="og:*">`, `<script type="application/ld+json">`, etc.

#### Scenario: Prerendered HTML is crawlable without JavaScript
- **WHEN** a search engine bot or social media crawler requests the prerendered landing page
- **THEN** it receives complete HTML with all SEO metadata and no JavaScript execution is required for the metadata to be visible

#### Scenario: Prerendered metadata matches frontend service output
- **WHEN** the landing page is both prerendered and client-rendered
- **THEN** the static prerendered metadata matches the metadata set by the frontend SEO service for consistency

### Requirement: Build documentation for prerender setup
The project documentation SHALL include instructions for configuring Angular prerender, building with prerender flag, and verifying prerendered output. The documentation SHALL specify the hosting/deployment requirement (server must serve the static prerendered HTML files for public routes).

#### Scenario: Build instructions are documented
- **WHEN** a developer reads the documentation
- **THEN** it explains how to run `ng build --prerender` and what the output structure looks like

#### Scenario: Deployment guide mentions prerender output
- **WHEN** deployment is configured
- **THEN** the deployment guide specifies that web server should serve the prerendered HTML files from the dist output
