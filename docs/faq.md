<!-- slug: faq | category: reference -->

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
