## MODIFIED Requirements

### Requirement: Landing page hero displays live marketplace metrics
The landing page hero section SHALL display live marketplace metrics (total plugins, total downloads, number of publishers, categories) instead of placeholder numbers. The stats are fetched via the home metrics facade and displayed in a stats band component. If stats are still loading, a loading message is shown. If an error occurs, a graceful error state is displayed without blocking the rest of the page.

#### Scenario: Hero displays live metrics on page load
- **WHEN** the landing page loads and metrics are fetched successfully
- **THEN** the hero section displays real numbers for all four metrics (e.g., "2,547 Plugins Published", "18,392 Total Downloads")

#### Scenario: Hero shows loading state while fetching
- **WHEN** the landing page loads but metrics are still being fetched
- **THEN** a loading message is displayed in the stats band area (e.g., "Loading marketplace stats…")

#### Scenario: Hero shows error state on fetch failure
- **WHEN** the metrics fetch fails (network error or server error)
- **THEN** the stats band displays an error message and a retry button; other hero sections (CTA buttons) are still visible

#### Scenario: Stats band does not block page rendering
- **WHEN** the landing page renders
- **THEN** the stats band loads asynchronously and does not delay rendering of search form, featured plugins, or footer sections

### Requirement: Landing page sets SEO metadata on route activation
The landing page component SHALL call the SEO metadata service on component init to set the page title, description, Open Graph tags, Twitter Card tags, canonical URL, and keywords. The landing page title SHALL be "ClaudeForge - Plugin Marketplace for Claude Code" or similar, with a description explaining the marketplace purpose.

#### Scenario: Landing page sets title and description
- **WHEN** the landing page component initializes
- **THEN** the SEO service sets title and meta description in the document head

#### Scenario: Landing page sets social media metadata
- **WHEN** the landing page is activated
- **THEN** Open Graph and Twitter Card tags are set for social media unfurls

#### Scenario: Landing page includes JSON-LD structured data
- **WHEN** the landing page loads
- **THEN** JSON-LD schemas for Organization, WebSite, and ItemList (featured plugins) are injected into the head

### Requirement: Landing page retains existing functionality
The landing page component SHALL retain all existing functionality: hero CTA buttons (Browse plugins, Publish plugin, Sign in), search form, featured plugins section, "How it works" section, and footer. The addition of live metrics and SEO metadata SHALL not change the layout or remove existing sections.

#### Scenario: Existing hero CTAs remain functional
- **WHEN** a user clicks "Browse plugins" or "Publish plugin"
- **THEN** navigation still works to /catalog or /docs respectively

#### Scenario: Featured plugins section unchanged
- **WHEN** the featured plugins section renders
- **THEN** it still displays the top 6 plugins sorted by download count, with the same card design

#### Scenario: Search form still navigates on submit
- **WHEN** a user enters a search query and submits
- **THEN** the page navigates to /search with the query parameter unchanged

#### Scenario: Footer links unchanged
- **WHEN** the footer is rendered
- **THEN** all footer navigation links (Plugin Catalog, Documentation, Search, My Plugins) still work
