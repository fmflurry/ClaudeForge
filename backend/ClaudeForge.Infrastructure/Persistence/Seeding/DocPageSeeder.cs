using ClaudeForge.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaudeForge.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent seeder for the 5 canonical ClaudeForge documentation pages.
///
/// Content is embedded as compile-time constants (no runtime file I/O).
/// Idempotency key: <c>slug</c> (unique DB constraint on <c>doc_pages.slug</c>).
/// <c>search_vector</c> is a GENERATED ALWAYS AS STORED column — the DB populates it
/// automatically; EF never writes it.
/// </summary>
public sealed class DocPageSeeder : IDocPageSeeder
{
    private readonly MarketplaceDbContext _context;

    // -------------------------------------------------------------------------
    // Embedded markdown content for each canonical documentation page.
    // Source: /docs/*.md (authored content, stripped of the metadata comment).
    // -------------------------------------------------------------------------

    private const string GettingStartedContent = """
        # Getting Started

        Welcome to ClaudeForge, the open plugin marketplace for Claude Code. This guide walks you through browsing plugins via the web UI or installing them via the CLI.

        ## Prerequisites

        - **Node.js** ≥ 22.22.3 (required for CLI tools)
        - A modern web browser (for the web UI)

        ## Using the Web UI

        The ClaudeForge web interface offers a friendly way to browse, search, and manage plugins directly in your browser.

        ### Browsing the Catalog

        1. Open the ClaudeForge marketplace at the web UI
        2. The **Catalog** tab shows all available plugins, sorted by creation date (newest first)
        3. Use the filter sidebar to narrow results by:
           - **Plugin Type**: skill, hook, agent, command, plugin
           - **Language**: TypeScript, Python, Go, Rust
           - **Use Case**: dev-team, product-owner, product-manager, devops, security, data-analyst

        ### Searching for Plugins

        Use the search bar at the top to find plugins by name, description, or keywords:
        - Example: searching "testing" returns all plugins tagged with test-related use cases
        - Results appear instantly with filtering options to refine further

        ### Viewing Plugin Details

        Click any plugin card to see:
        - Full description and author information
        - Plugin type and supported languages
        - Version history with release notes
        - Installation instructions
        - Download count and telemetry summary (anonymized)

        ### Dashboard & Team Context

        Once logged in (auth coming soon), you can:
        - Track installed plugins across your team
        - Set team-wide telemetry preferences
        - Manage plugin updates

        ## Using the CLI

        The `claude-plugin` CLI provides a command-line interface for installing, managing, and publishing plugins.

        ### Installation

        Install the CLI globally via npm:

        ```bash
        npm install -g claude-plugin
        ```

        ### Verify Installation

        Check that the CLI is installed correctly:

        ```bash
        claude-plugin --version
        ```

        You should see the version number printed.

        ### Configuration

        Set your API endpoint and authentication (if required):

        ```bash
        claude-plugin config set api-url https://marketplace.claudeforge.com/api/v1
        ```

        To view your current configuration:

        ```bash
        claude-plugin config get
        ```

        ### Searching for Plugins

        Search the marketplace from the command line:

        ```bash
        claude-plugin search "my keyword"
        ```

        Filter by type, language, or use case:

        ```bash
        claude-plugin search "testing" --type skill --language typescript
        ```

        ### Installing Plugins

        Install a plugin by name or ID:

        ```bash
        claude-plugin install plugin-name
        ```

        Install a specific version:

        ```bash
        claude-plugin install plugin-name@1.2.3
        ```

        ### Listing Installed Plugins

        View all installed plugins:

        ```bash
        claude-plugin list
        ```

        Shows name, version, type, and installation path for each plugin.

        ### Updating Plugins

        Update a single plugin to the latest version:

        ```bash
        claude-plugin update plugin-name
        ```

        Update all plugins:

        ```bash
        claude-plugin update --all
        ```

        ### Removing Plugins

        Uninstall a plugin:

        ```bash
        claude-plugin remove plugin-name
        ```

        ## Next Steps

        - **Publishing a plugin?** See [Contributing & Publishing Plugins](./contributing.md)
        - **Questions?** Check the [FAQ](./faq.md)
        - **Privacy concerns?** Read our [Privacy & Telemetry](./privacy-and-telemetry.md) policy
        - **API integration?** Explore the [API Reference](./api-reference.md)
        """;

    private const string ContributingContent = """
        # Contributing & Publishing Plugins

        Learn how to create, validate, version, and publish plugins to the ClaudeForge marketplace.

        ## Plugin Structure

        A ClaudeForge plugin is a self-contained package that extends Claude Code's capabilities. Each plugin must include:

        - **plugin.json** — The canonical manifest (see schema below)
        - **Implementation files** — Code in TypeScript, Python, Go, or Rust
        - **README.md** — User-facing documentation
        - **LICENSE** — Licensing terms (default: MIT)

        ```
        my-plugin/
        ├── plugin.json
        ├── README.md
        ├── LICENSE
        ├── src/
        │   └── index.ts (or index.py, main.go, lib.rs)
        └── tests/
            └── plugin.test.ts
        ```

        ## Plugin Manifest (plugin.json)

        The `plugin.json` file is the source of truth for plugin metadata. All fields are required unless marked optional.

        ### Full Schema

        ```json
        {
          "name": "my-awesome-plugin",
          "version": "1.0.0",
          "description": "A brief, one-line description of what your plugin does.",
          "author": "Your Name <your.email@example.com>",
          "types": ["skill", "hook"],
          "languages": ["typescript", "python"],
          "useCaseTags": ["dev-team", "security"],
          "entrypoints": [
            {
              "name": "mySkill",
              "type": "skill",
              "description": "An example skill",
              "language": "typescript"
            }
          ],
          "dependencies": {
            "axios": "^1.0.0",
            "lodash": "^4.17.0"
          },
          "license": "MIT",
          "docsUrl": "https://github.com/yourname/my-plugin#readme"
        }
        ```

        ### Field Reference

        | Field | Type | Required | Description |
        |-------|------|----------|-------------|
        | `name` | string | Yes | Unique plugin identifier (lowercase, kebab-case). Must be 3-50 characters. |
        | `version` | string | Yes | Semantic version (e.g., "1.0.0", "2.3.4-beta.1"). Must match `^(\d+\.){2}\d+(-[a-z0-9]+)?$` |
        | `description` | string | Yes | Concise one-liner explaining what the plugin does (max 200 chars). |
        | `author` | string | Yes | Author name and email: "John Doe <john@example.com>" |
        | `types` | string[] | Yes | One or more: `skill`, `hook`, `agent`, `command`, `plugin` |
        | `languages` | string[] | Yes | One or more: `typescript`, `python`, `go`, `rust` |
        | `useCaseTags` | string[] | Yes | One or more of: `dev-team`, `product-owner`, `product-manager`, `devops`, `security`, `data-analyst` |
        | `entrypoints` | object[] | Yes | Array of exported functions/classes with `name`, `type`, `description`, `language` |
        | `dependencies` | object | No | npm/pip/go/cargo dependencies with version constraints |
        | `license` | string | No | License type (default: "MIT"). Use SPDX identifiers. |
        | `docsUrl` | string | No | External documentation URL (GitHub, docs site, etc.) |

        ### Example Entrypoint

        ```json
        {
          "name": "analyzeCode",
          "type": "skill",
          "description": "Analyzes TypeScript code and returns AST insights",
          "language": "typescript"
        }
        ```

        ## Scaffolding a New Plugin

        Generate a plugin template with the CLI:

        ```bash
        claude-plugin scaffold --name my-plugin --language typescript
        ```

        Supported languages: `typescript`, `python`, `go`, `rust`

        This creates a ready-to-develop project with:
        - Pre-configured `plugin.json`
        - Source file template
        - Test file template
        - `.gitignore` and LICENSE

        ## Validating Your Plugin

        Before publishing, validate your plugin structure and manifest:

        ```bash
        claude-plugin validate
        ```

        Checks include:
        - `plugin.json` syntax and required fields
        - Version format (semver)
        - Entrypoint existence and correctness
        - Language and type compatibility
        - Dependency declarations
        - File size limits (max 50 MB)

        ## Versioning

        ClaudeForge uses **Semantic Versioning** (semver):

        - **MAJOR** version for incompatible API changes
        - **MINOR** version for backward-compatible features
        - **PATCH** version for bug fixes

        Examples: `1.0.0`, `1.2.3`, `2.0.0-rc.1`

        ### Release Notes

        When publishing a new version, include release notes describing:
        - New features
        - Bug fixes and improvements
        - Breaking changes
        - Deprecations

        ## Publishing Your Plugin

        ### Step 1: Prepare Your Plugin

        1. Ensure `plugin.json` is complete and valid
        2. Update version number in `plugin.json`
        3. Add release notes
        4. Run `claude-plugin validate`

        ### Step 2: Create a Plugin Archive

        The CLI will package your plugin automatically during publish. Include:
        - All source files
        - `plugin.json`
        - `README.md`
        - `LICENSE`
        - Compiled assets (if applicable)

        Do NOT include:
        - `node_modules/`, `.venv/`, `target/`, etc.
        - `.git/` directory
        - IDE settings (`.vscode/`, `.idea/`)

        ### Step 3: Publish to Marketplace

        ```bash
        claude-plugin publish
        ```

        The CLI will:
        1. Validate your plugin structure
        2. Create an archive
        3. Upload to the marketplace
        4. Register your plugin and version

        ### Step 4: Verify Publication

        Check the marketplace:

        ```bash
        claude-plugin search my-plugin
        ```

        Your plugin should appear within seconds.

        ## Multi-Type Plugins

        A plugin can export multiple types (skill, hook, agent, command):

        ```json
        {
          "name": "all-in-one",
          "types": ["skill", "hook", "agent"],
          "entrypoints": [
            {
              "name": "mySkill",
              "type": "skill",
              "language": "typescript"
            },
            {
              "name": "preCommitHook",
              "type": "hook",
              "language": "typescript"
            },
            {
              "name": "researchAgent",
              "type": "agent",
              "language": "python"
            }
          ]
        }
        ```

        ## Multi-Language Plugins

        Support multiple languages in a single plugin:

        ```json
        {
          "name": "polyglot-plugin",
          "languages": ["typescript", "python"],
          "entrypoints": [
            {
              "name": "nodeSkill",
              "type": "skill",
              "language": "typescript"
            },
            {
              "name": "pythonSkill",
              "type": "skill",
              "language": "python"
            }
          ]
        }
        ```

        Each entrypoint must have a corresponding implementation file.

        ## Best Practices

        1. **Clear Naming** — Use descriptive names for plugins and entrypoints
        2. **Documentation** — Write a comprehensive README with examples
        3. **Testing** — Include unit and integration tests; aim for 80%+ coverage
        4. **Error Handling** — Provide clear, actionable error messages
        5. **Dependencies** — Keep dependencies minimal; pin versions for stability
        6. **Compatibility** — Test your plugin on supported Node/Python/Go versions
        7. **Changelog** — Maintain a CHANGELOG.md with version history

        ## Troubleshooting

        ### "Plugin validation failed"

        Check the error message from `claude-plugin validate`:
        - Ensure all required fields are in `plugin.json`
        - Verify entrypoints match actual files
        - Check version format (must be semantic)

        ### "Plugin upload failed"

        - Confirm you have network access to the marketplace
        - Ensure plugin.json name is unique (not already published)
        - Check file size is under 50 MB
        - Verify all entrypoint files are included

        ### "Version already exists"

        Each version must be unique. Increment the version in `plugin.json` and try again.

        ## Next Steps

        - Ready to install plugins? See [Getting Started](./getting-started.md)
        - API for programmatic access? Check [API Reference](./api-reference.md)
        - Privacy questions? Read [Privacy & Telemetry](./privacy-and-telemetry.md)
        """;

    private const string FaqContent = """
        # FAQ

        Frequently asked questions about ClaudeForge, the plugin marketplace.

        ## General

        ### Is authentication required?

        Not yet. ClaudeForge currently supports public, unauthenticated plugin access. Authentication and user accounts are planned for a future release.

        ### Are all plugins free?

        Yes. All plugins on ClaudeForge are free and open to the community. There are no paid tiers or premium plugins at this time.

        ### Can I publish private or paid plugins?

        Currently, all plugins are public. Private plugin hosting and paid plugin models are on the roadmap but not yet available.

        ## Browsing & Installation

        ### How do I search for plugins?

        Use the search bar in the web UI or the CLI:

        ```bash
        claude-plugin search "keyword"
        ```

        You can also filter by type, language, and use case in both interfaces.

        ### What languages are supported?

        ClaudeForge supports plugins written in:
        - TypeScript
        - Python
        - Go
        - Rust

        ### What plugin types are available?

        - **Skill** — Standalone functions that extend Claude Code's capabilities
        - **Hook** — Code that runs at specific lifecycle points (pre/post operations)
        - **Agent** — Long-running or multi-step automation processes
        - **Command** — CLI commands or slash commands
        - **Plugin** — General-purpose extensibility (bundles of the above)

        ### How do I install a plugin?

        Via CLI:

        ```bash
        claude-plugin install plugin-name
        ```

        Plugins are automatically integrated into Claude Code when installed.

        ### Can I install plugins in different versions?

        Yes. Install a specific version:

        ```bash
        claude-plugin install plugin-name@1.2.3
        ```

        List available versions on the plugin's details page or via the API.

        ## Plugin Telemetry

        ### How are downloads counted?

        Downloads are counted when:
        - A user runs `claude-plugin install`
        - A user downloads a plugin via the web UI
        - A programmatic client hits the `/api/v1/plugins/{id}/download` endpoint

        Each event is one count.

        ### What telemetry data is collected?

        ClaudeForge collects **anonymized data only**:
        - Event type (install, download)
        - Plugin ID and version
        - Hashed anonymous client ID (not tied to identity)
        - Coarse OS and architecture info (e.g., "darwin", "linux", "win32", "x86_64", "arm64")

        **We do NOT collect:**
        - IP addresses
        - Personal information
        - Usernames or email addresses
        - File paths or system details beyond OS/arch
        - Plugin configuration or usage patterns

        See [Privacy & Telemetry](./privacy-and-telemetry.md) for complete details.

        ### Can I opt out of telemetry?

        Yes. Toggle telemetry off in the CLI or web UI settings:

        ```bash
        claude-plugin config set telemetry enabled=false
        ```

        Or in the web dashboard, navigate to Settings > Telemetry and toggle off.

        Telemetry is opt-out (enabled by default), but you can disable it at any time.

        ## Team Context

        ### What is team context?

        Team context (coming soon) allows you to:
        - Share plugin installations across a team
        - Set team-wide telemetry preferences
        - Track adoption and usage metrics
        - Coordinate plugin updates

        Team context is currently available **only in the web UI dashboard**. CLI team support is planned.

        ### How do I set up my team?

        This feature is under development. In the meantime, you can:
        - Share plugin names via chat or email
        - Install plugins manually on each machine
        - Follow the [Getting Started](./getting-started.md) guide for standard installation

        ## Publishing

        ### How do I publish a plugin?

        See the full guide in [Contributing & Publishing Plugins](./contributing.md). Quick steps:

        1. Create your plugin (or use `claude-plugin scaffold`)
        2. Fill out `plugin.json` with metadata
        3. Run `claude-plugin validate` to check for errors
        4. Run `claude-plugin publish`

        ### What happens after I publish?

        Your plugin:
        - Appears in the catalog within seconds
        - Is searchable and discoverable
        - Can be installed by anyone
        - Receives a unique ID in the marketplace
        - Version history is tracked automatically

        ### Can I update a plugin after publishing?

        Yes. Update your code, bump the version in `plugin.json`, and run `claude-plugin publish` again. Each version is immutable but you can release new versions indefinitely.

        ### Can I remove or deprecate a plugin?

        Not yet. Once published, plugins cannot be deleted from the marketplace. Deprecation tooling is planned — in the meantime, you can mark your plugin as deprecated in the README.

        ### What are the size limits for plugins?

        Maximum plugin size: **50 MB** (compressed archive)

        If your plugin exceeds this, reduce dependencies or split into separate plugins.

        ## Troubleshooting

        ### My plugin disappeared from search results

        Possible causes:
        - Plugin was published with an error; check the publish status
        - Marketplace indexing takes a few seconds; wait and refresh
        - Your search query doesn't match the plugin name or description

        Try searching by exact name or checking the marketplace homepage directly.

        ### I can't install a plugin

        Ensure:
        - The plugin name is correct (case-insensitive but kebab-case)
        - You have internet access to the marketplace
        - Node.js ≥ 22.22.3 is installed
        - The plugin is compatible with your OS/architecture

        Try:

        ```bash
        claude-plugin search plugin-name
        ```

        If it doesn't appear, the plugin may not exist or may have been removed.

        ### How do I report a security issue?

        ClaudeForge does not yet have a formal security report process. For now:
        - Reach out to the plugin author directly (email in author field)
        - File an issue on the author's repository if public
        - Security features (auth, code signing) are on the roadmap

        ### I found a bug in a plugin

        Contact the plugin author:
        1. Find their email in the plugin's details page
        2. Report the issue with reproduction steps
        3. If the plugin is open-source, file an issue on the repository

        ## API & Integration

        ### Can I use the API programmatically?

        Yes. See [API Reference](./api-reference.md) for complete endpoint documentation.

        Example: Search for plugins via HTTP:

        ```bash
        curl "https://api.claudeforge.com/api/v1/plugins/search?q=testing&type=skill"
        ```

        ### What's the rate limit?

        Rate limits are not yet enforced. Future releases will include fair-use policies.

        ### Can I self-host ClaudeForge?

        ClaudeForge is open source. Details on self-hosting will be published when the project reaches stability.

        ## Feedback & Support

        ### How do I report a bug?

        Open an issue on the [ClaudeForge GitHub repository](https://github.com/anthropics/ClaudeForge/issues).

        ### How do I request a feature?

        Share your idea on the GitHub discussions board or open a feature request issue.

        ### Who maintains ClaudeForge?

        ClaudeForge is maintained by Anthropic and the open-source community.

        ## Still have questions?

        Check the [API Reference](./api-reference.md) for technical details or [Getting Started](./getting-started.md) for setup help.
        """;

    private const string PrivacyAndTelemetryContent = """
        # Privacy & Telemetry

        ClaudeForge collects anonymized telemetry to understand plugin adoption and improve the marketplace. This page explains exactly what data we collect, how we use it, and how to opt out.

        ## What We Collect

        ClaudeForge collects **anonymized events only**. We do not collect personally identifiable information (PII).

        ### Event Data

        Each telemetry event includes:

        | Field | Example | Notes |
        |-------|---------|-------|
        | `eventType` | "install", "download" | Type of action taken |
        | `pluginId` | "550e8400-e29b-41d4-a716-446655440000" | UUID of the plugin |
        | `version` | "1.2.3" | Plugin version (if applicable) |
        | `anonClientId` | "sha256:a1b2c3..." | Hashed anonymous identifier (not linked to identity) |
        | `clientOs` | "darwin", "linux", "win32" | Operating system (coarse-grained) |
        | `clientArch` | "x86_64", "arm64" | CPU architecture (coarse-grained) |

        ### What We DON'T Collect

        - IP addresses
        - Usernames or email addresses
        - System hostnames or domain names
        - File paths or project names
        - Plugin configuration or settings
        - Specific usage patterns or workflows
        - Personal device details beyond OS/arch
        - Timestamps (events are aggregated without time)

        ## How We Use This Data

        ### Analytics & Insights

        We use anonymized telemetry to:
        - Track plugin popularity and adoption trends
        - Identify which plugin types and languages are most used
        - Understand platform growth metrics
        - Detect issues with plugin distribution
        - Plan roadmap priorities based on usage

        ### Aggregate Reports

        Data is aggregated and presented as:
        - Total downloads/installs by plugin
        - Top plugins by category
        - Platform usage distribution (OS, architecture)
        - Monthly growth metrics

        Individual events are **never** tied to specific users or systems.

        ### No Third-Party Sharing

        We do not:
        - Share raw telemetry with third parties
        - Sell or license your event data
        - Provide data to advertisers
        - Pass data to analytics platforms without aggregation

        ## Data Retention

        ### Raw Events

        Raw telemetry events are retained for **90 days** from collection:
        - Allows us to detect and fix real-time issues
        - Enables quick trend analysis
        - Supports privacy by auto-deleting after the window

        ### Aggregated Data

        After 90 days, raw events are deleted and replaced with aggregate statistics:
        - Total download/install counts
        - Platform distribution percentages
        - Trend summaries

        Aggregate data is retained indefinitely (it contains no identifying information).

        ## Opting Out

        Telemetry is **enabled by default** but can be disabled at any time.

        ### Via CLI

        Disable telemetry globally:

        ```bash
        claude-plugin config set telemetry enabled=false
        ```

        Re-enable it later:

        ```bash
        claude-plugin config set telemetry enabled=true
        ```

        Check current status:

        ```bash
        claude-plugin config get telemetry
        ```

        ### Via Web UI

        In the ClaudeForge web dashboard:
        1. Navigate to **Settings**
        2. Find **Telemetry & Privacy**
        3. Toggle **Send usage data** off/on

        Changes take effect immediately.

        ### What Happens When You Opt Out

        When telemetry is disabled:
        - No events are sent to the marketplace
        - Your plugin downloads/installs are still counted (by the marketplace, not by your client)
        - You receive no tracking cookies or identifiers
        - Your privacy is fully respected

        **Note:** Opting out does not hide your plugin from search or prevent others from discovering it.

        ## How We Protect Your Data

        ### Anonymization

        All telemetry is anonymized before storage:
        - `anonClientId` is a **salted hash**, not a username or email
        - OS and architecture are **coarse-grained** (not device-specific)
        - No timestamps are stored with individual events
        - No geographic data is collected

        ### Storage & Access

        - Telemetry data is stored in a PostgreSQL database
        - Access is restricted to ClaudeForge engineering team
        - Data is encrypted in transit (HTTPS)
        - No external analytics platforms (Google Analytics, Mixpanel, etc.) have access

        ### Future Encryption

        We are planning:
        - End-to-end encryption for telemetry (opt-in beta)
        - Differential privacy techniques for additional anonymization
        - Zero-knowledge telemetry aggregation

        ## Compliance

        ClaudeForge aims to respect privacy regulations:
        - **GDPR**: No personal data is collected, so GDPR's definition of personal data does not apply
        - **CCPA**: Users have the right to opt out (via telemetry disable)
        - **PIPEDA**: No PII is stored or shared

        ## Questions?

        If you have concerns about privacy or telemetry:
        - Review this page and the [FAQ](./faq.md)
        - Check your config: `claude-plugin config get`
        - Open an issue on the [GitHub repository](https://github.com/anthropics/ClaudeForge)

        ## Changes to This Policy

        We may update this policy as ClaudeForge evolves. Changes will be:
        - Posted on this page with a version date
        - Announced in release notes
        - Effective 30 days after posting (unless legally required sooner)

        Last updated: June 2026
        """;

    private const string ApiReferenceContent = """
        # API Reference

        ClaudeForge exposes a REST API at `/api/v1` for programmatic access to the plugin marketplace. All endpoints return JSON responses.

        ## Base URL

        ```
        https://api.claudeforge.com/api/v1
        ```

        ## Response Format

        All successful responses follow this envelope:

        ```json
        {
          "data": { /* ... */ },
          "totalCount": 42,
          "page": 1,
          "limit": 20,
          "totalPages": 3
        }
        ```

        ### Fields

        | Field | Type | Description |
        |-------|------|-------------|
        | `data` | array or object | Paginated results or single resource |
        | `totalCount` | integer | Total number of items matching the query |
        | `page` | integer | Current page (1-indexed) |
        | `limit` | integer | Items per page |
        | `totalPages` | integer | Total number of pages |

        ## Error Responses

        Errors return RFC 7807 ProblemDetails format:

        ```json
        {
          "type": "https://api.claudeforge.com/errors/plugin-not-found",
          "title": "Plugin Not Found",
          "status": 404,
          "detail": "Plugin with ID '550e8400-e29b-41d4-a716-446655440000' does not exist.",
          "instance": "/api/v1/plugins/550e8400-e29b-41d4-a716-446655440000"
        }
        ```

        ### Common HTTP Status Codes

        | Status | Meaning |
        |--------|---------|
        | 200 | OK — Request succeeded |
        | 400 | Bad Request — Invalid parameters |
        | 404 | Not Found — Resource does not exist |
        | 409 | Conflict — Duplicate or versioning error |
        | 500 | Internal Server Error — Server-side problem |

        ---

        ## Catalog Endpoints

        ### List All Plugins

        ```
        GET /api/v1/plugins
        ```

        Retrieve paginated list of all plugins.

        #### Query Parameters

        | Parameter | Type | Default | Description |
        |-----------|------|---------|-------------|
        | `page` | integer | 1 | Page number (1-indexed) |
        | `limit` | integer | 20 | Items per page (1-100) |
        | `sort` | string | "createdAt" | Sort field: `createdAt`, `downloadCount`, `name` |
        | `order` | string | "desc" | Sort order: `asc` or `desc` |
        | `type` | string[] | — | Filter by type: skill, hook, agent, command, plugin |
        | `language` | string[] | — | Filter by language: typescript, python, go, rust |
        | `useCase` | string[] | — | Filter by use case: dev-team, product-owner, product-manager, devops, security, data-analyst |

        #### Example

        ```bash
        curl "https://api.claudeforge.com/api/v1/plugins?limit=10&type=skill&language=typescript"
        ```

        ---

        ## Search & Discovery

        ### Search Plugins

        ```
        GET /api/v1/plugins/search
        GET /api/v1/search
        ```

        Search the plugin catalog (both endpoints are equivalent).

        #### Query Parameters

        | Parameter | Type | Default | Description |
        |-----------|------|---------|-------------|
        | `q` | string | — | Search query (name, description, keywords) |
        | `type` | string[] | — | Filter by type |
        | `language` | string[] | — | Filter by language |
        | `useCase` | string[] | — | Filter by use case |
        | `page` | integer | 1 | Page number |
        | `limit` | integer | 20 | Items per page |

        ---

        ## Distribution

        ### Download Plugin

        ```
        GET /api/v1/plugins/{pluginId}/download
        ```

        Download a plugin archive (.tar.gz or .zip).

        ---

        ## Publishing & Versioning

        ### Upload Plugin

        ```
        POST /api/v1/plugins/upload
        ```

        Upload a new plugin to the marketplace (requires multipart form data).

        ### Publish Plugin Version

        ```
        POST /api/v1/plugins/{pluginId}/versions
        ```

        Publish a new version of an existing plugin.

        ### Get Version History

        ```
        GET /api/v1/plugins/{pluginId}/versions
        ```

        Retrieve paginated version history for a plugin.

        ---

        ## Telemetry

        ### Post Telemetry Event

        ```
        POST /api/v1/telemetry/events
        ```

        Send an anonymized telemetry event (download, install, etc.).

        ### Get Telemetry Summary

        ```
        GET /api/v1/plugins/{pluginId}/telemetry/summary
        ```

        Retrieve aggregated telemetry for a plugin.

        ---

        ## Documentation

        ### Search Documentation

        ```
        GET /api/v1/docs
        ```

        Search ClaudeForge documentation.

        ### Get Documentation by Slug

        ```
        GET /api/v1/docs/{slug}
        ```

        Retrieve full documentation page by slug.

        ---

        ## Rate Limits

        Rate limits are not currently enforced. Fair-use policies will be implemented in future releases.

        ## Authentication

        Authentication is not yet implemented. All endpoints are publicly accessible.

        ## Versioning

        The current API version is `v1`. Breaking changes will be communicated in advance, and new versions will be available at `/api/v2`, etc.
        """;

    // -------------------------------------------------------------------------
    // The 5 canonical seed documentation page definitions.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Static list of all seed definitions.
    /// Accessible without instantiation so tests can assert expected slugs/titles/categories.
    /// </summary>
    public static IReadOnlyList<DocPageSeedDefinition> SeedDefinitions { get; } =
    [
        new DocPageSeedDefinition(
            Slug: "getting-started",
            Title: "Getting Started",
            Category: "guide",
            ContentMarkdown: GettingStartedContent),

        new DocPageSeedDefinition(
            Slug: "contributing",
            Title: "Contributing & Publishing Plugins",
            Category: "guide",
            ContentMarkdown: ContributingContent),

        new DocPageSeedDefinition(
            Slug: "faq",
            Title: "FAQ",
            Category: "reference",
            ContentMarkdown: FaqContent),

        new DocPageSeedDefinition(
            Slug: "privacy-and-telemetry",
            Title: "Privacy & Telemetry",
            Category: "reference",
            ContentMarkdown: PrivacyAndTelemetryContent),

        new DocPageSeedDefinition(
            Slug: "api-reference",
            Title: "API Reference",
            Category: "reference",
            ContentMarkdown: ApiReferenceContent),
    ];

    public DocPageSeeder(MarketplaceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken ct = default)
    {
        DateTimeOffset lastUpdated = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        foreach (DocPageSeedDefinition def in SeedDefinitions)
        {
            bool exists = await _context.DocPages
                .AnyAsync(d => d.Slug == def.Slug, ct);

            if (exists)
                continue;

            _context.DocPages.Add(new DocPageEntity
            {
                Id = Guid.NewGuid(),
                Slug = def.Slug,
                Title = def.Title,
                Category = def.Category,
                ContentMarkdown = def.ContentMarkdown,
                LastUpdated = lastUpdated,
            });
        }

        await _context.SaveChangesAsync(ct);
    }
}
