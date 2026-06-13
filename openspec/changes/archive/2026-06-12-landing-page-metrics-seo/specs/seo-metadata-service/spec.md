## ADDED Requirements

### Requirement: SEO metadata service sets Title and Meta tags per route
The frontend SHALL provide an SEO metadata service that wraps Angular's `Title` and `Meta` services. The service SHALL expose a method to update the page title, meta description, Open Graph tags (og:title, og:description, og:image), Twitter Card tags (twitter:card, twitter:title, twitter:description), canonical URL, and keywords. The service SHALL be injectable and used by routing guards or route resolvers to set metadata on each route.

#### Scenario: Metadata set on landing route
- **WHEN** the landing page route is activated
- **THEN** the SEO service sets title (e.g., "ClaudeForge - Plugin Marketplace for Claude Code"), description, og:title, og:description, og:image, twitter:card, canonical URL, and keywords

#### Scenario: Metadata set per route
- **WHEN** the user navigates to a different route (e.g., /catalog)
- **THEN** the SEO service updates all meta tags to match that route's SEO requirements

#### Scenario: Canonical URL prevents duplicate content
- **WHEN** a route is visited
- **THEN** a canonical link tag is set to the absolute URL of that route (e.g., https://example.com/catalog)

### Requirement: JSON-LD structured data for Organization and WebSite
The SEO service SHALL generate and inject JSON-LD structured data for `Organization` and `WebSite` schema.org types at the application root. The Organization schema SHALL include name, description, logo URL, and links to social profiles. The WebSite schema SHALL include name, URL, and searchAction (enabling sitelink search box in search results).

#### Scenario: Organization JSON-LD rendered
- **WHEN** the page loads
- **THEN** a `<script type="application/ld+json">` tag containing Organization schema is injected into the document head

#### Scenario: WebSite JSON-LD rendered
- **WHEN** the page loads
- **THEN** a second JSON-LD script contains WebSite schema with SearchAction

### Requirement: JSON-LD ItemList for featured plugins on landing page
The landing page component SHALL dynamically generate and inject JSON-LD structured data for an `ItemList` of the top 6 featured plugins. Each plugin in the list SHALL have name, description, author, and URL. This metadata enables search engines to understand the marketplace catalog structure.

#### Scenario: ItemList JSON-LD generated for featured plugins
- **WHEN** the featured plugins are loaded on the landing page
- **THEN** the SEO service generates a JSON-LD ItemList schema containing all 6 featured plugins with name, description, author, and @id fields

#### Scenario: ItemList updated when featured plugins change
- **WHEN** the catalog facade updates the featured plugins list
- **THEN** the JSON-LD ItemList is regenerated and re-injected into the document

### Requirement: Social media unfurls
The service SHALL set Open Graph and Twitter Card meta tags such that when the landing page URL is shared on Facebook, Twitter, LinkedIn, or other social platforms, the unfurl displays the marketplace name, description, and a preview image.

#### Scenario: Facebook unfurl shows metadata
- **WHEN** landing page URL is shared on Facebook
- **THEN** the unfurl displays og:title, og:description, and og:image

#### Scenario: Twitter unfurl shows metadata
- **WHEN** landing page URL is shared on Twitter
- **THEN** the unfurl displays twitter:card, twitter:title, twitter:description, and twitter:image

### Requirement: SEO configuration is injectable and centralized
The landing page and any other public-facing components SHALL inject the SEO service and call it in ngOnInit or via a route resolver. All SEO configuration (titles, descriptions, keywords, image URLs) SHALL be centralized in a configuration object or constants file to enable easy maintenance and updates.

#### Scenario: SEO service is injectable
- **WHEN** a component constructor includes SeoService as a dependency
- **THEN** Angular provides the singleton service instance

#### Scenario: Landing page sets SEO metadata on init
- **WHEN** the landing page ngOnInit runs
- **THEN** it calls the SEO service to set all metadata for the landing route

#### Scenario: SEO config is centralized
- **WHEN** the SEO service is used
- **THEN** it reads configuration from a single source (e.g., a constant or service class) for consistency
