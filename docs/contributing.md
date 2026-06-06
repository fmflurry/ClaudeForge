<!-- slug: contributing | category: guide -->

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

Example:

```
## v2.0.0

### Breaking Changes
- Removed deprecated `analyze()` function; use `analyzeCode()` instead

### Features
- Add support for JSX/TSX parsing
- Add multi-file analysis

### Fixes
- Fix incorrect line numbers in error output
```

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
