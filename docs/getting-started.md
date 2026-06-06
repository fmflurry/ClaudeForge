<!-- slug: getting-started | category: guide -->

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
