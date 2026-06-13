# Claude Plugin CLI

TypeScript/Node.js CLI for managing Claude Code add-ons locally and globally. Provides lifecycle management (`add`, `list`, `update`, `remove`, `rollback`) for four add-on types: **hooks**, **skills**, **agents**, and **plugins**.

## Overview

The `claude plugin` CLI connects to the ClaudeForge marketplace for plugin discovery and publication. The new `claude plugin addon` sub-command group provides local/global add-on management — NPM-like workflows for Claude Code's own configuration directories.

## Installation & Setup

```bash
cd cli && npm install
npm run build
npm run test

# Link for local development (or use via workspace)
npm link
```

## Commands

### `addon add` — Install or scaffold an add-on

**Install from a source directory (with `addon.json`):**

```bash
claude plugin addon add <source-dir> --scope <local|global> [--force]
```

- `<source-dir>`: Directory containing `addon.json` and the add-on files.
- `--scope local`: Install to `./.claude/` (project-local).
- `--scope global`: Install to `~/.claude/` (user-global).
- `--force`: Overwrite an existing add-on with the same name; the prior version is snapshotted.

**Example:** Install a skill from a local directory.

```bash
claude plugin addon add ./my-skill --scope local
```

**Scaffold a new add-on (create path):**

```bash
claude plugin addon add <name> --type <hook|skill|agent|plugin> --scope <local|global> [--lang <ts|py|go|rust>]
```

- `<name>`: Name of the new add-on (used as the identifier; no `--type` or `--lang` → install mode).
- `--type`: Required for scaffold mode. One of: `agent`, `skill`, `hook`, `plugin`.
- `--lang`: Template language. Defaults to `typescript`. Options: `typescript`, `python`, `go`, `rust`.
- `--scope local|global`: Required; see install mode above.

**Constraint:** `--scope local --type plugin` is rejected — plugins are global-only.

**Example:** Create a new TypeScript skill locally.

```bash
claude plugin addon add my-analyzer --type skill --scope local --lang typescript
```

---

### `addon list` — List installed add-ons

```bash
claude plugin addon list [--scope <local|global>]
```

- `--scope local|global`: Optional. If omitted, lists both local and global scopes with headers.

Output format per add-on: `type  name  version  [stored: v1, v2, ...]`

**Example:** List all installed add-ons.

```bash
claude plugin addon list
```

**Example:** List global add-ons only.

```bash
claude plugin addon list --scope global
```

---

### `addon update` — Update an add-on to a new version

```bash
claude plugin addon update <source-dir> --scope <local|global>
```

- `<source-dir>`: Directory with the new `addon.json` and files.
- `--scope`: Required.

The command compares the new version (from the source `addon.json`) with the live installed version:
- If **same version**, it's a no-op: `"Already at version X.Y.Z. Nothing to update."`
- If **newer**, the prior version is snapshotted to the version store, and the new version is installed live.

The live add-on is only mutated after the snapshot succeeds, ensuring recoverability.

**Example:**

```bash
claude plugin addon update ./my-skill --scope local
```

---

### `addon remove` — Remove an installed add-on

```bash
claude plugin addon remove <name> --type <hook|skill|agent|plugin> --scope <local|global>
```

- `<name>`: The add-on's name (from its `addon.json`).
- `--type`: Required. Must match the installed add-on's type.
- `--scope`: Required.

Removes the add-on's files from the live directory and unregisters any hook entries from `settings.json`.

**Example:**

```bash
claude plugin addon remove my-hook --type hook --scope global
```

---

### `addon rollback` — Restore a prior version

```bash
claude plugin addon rollback <name> --type <hook|skill|agent|plugin> --scope <local|global> [--to <version>]
```

- `<name>`, `--type`, `--scope`: Required; identify the add-on.
- `--to <version>`: Optional. Restore to a specific semver (e.g., `1.0.5`). If omitted, restores the most recent prior snapshot.

The add-on is restored from the version store. The current live version is snapshotted first, so rollback is reversible.

**Example:**

```bash
claude plugin addon rollback my-agent --type agent --scope local --to 1.2.0
```

---

## Scopes & Installation Paths

Add-ons are installed to one of two scopes:

| Scope | Base Directory | Note |
|-------|---|---|
| **local** | `./.claude/` | Project-local; overrides global. |
| **global** | `~/.claude/` | User-wide default. |

Per-type placement within a scope:

| Type | Live Install Path | Notes |
|------|---|---|
| **agent** | `<scope>/.claude/agents/<name>.md` | Single markdown file with YAML frontmatter. No subdirs. |
| **skill** | `<scope>/.claude/skills/<name>/` | Directory tree; should include `SKILL.md`. |
| **hook** | `<scope>/.claude/hooks/<script>` + `<scope>/.claude/settings.json` | Script file(s) **plus** a merged hook entry in `settings.json` (required for activation). |
| **plugin** | `~/.claude/plugins/<name>/` | **Global only.** Local scope rejected. Bundle directory. |

**Metadata:** Add-on metadata (manifest, placed file list, stored versions) is kept in a per-scope sidecar at `.addons/<type>/<name>.json` to avoid polluting the live directories.

---

## `addon.json` Manifest Format

Every add-on is self-describing via an `addon.json` file at its source root.

### Example

```json
{
  "name": "my-auth-hook",
  "version": "1.0.0",
  "type": "hook",
  "supportedScopes": ["local", "global"],
  "files": ["hooks/auth.sh", "README.md"],
  "hook": {
    "event": "PreToolUse",
    "matcher": "Bash",
    "command": "hooks/auth.sh",
    "type": "command"
  }
}
```

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | string | Yes | Non-empty; identifies the add-on. |
| `version` | string | Yes | Strict semver: `MAJOR.MINOR.PATCH[+-prerelease]`. |
| `type` | string | Yes | One of: `hook`, `plugin`, `skill`, `agent`. |
| `supportedScopes` | array or "both" | Yes | Array of `"local"` and/or `"global"`, or shorthand `"both"` (expands to both). Plugins must be `["global"]` or equivalent. |
| `files` | array | Yes | Relative paths (no absolute, no `..`); at least one required. Per-type semantics below. |
| `hook` | object | Conditional | **Required iff `type === "hook"`; forbidden otherwise.** See below. |

### Per-Type File Semantics

**Agent:**
- `files` must contain **exactly one** entry.
- Should be a `.md` file (warning if not).
- Example: `["my-agent.md"]`

**Skill:**
- `files` is a directory tree (multiple entries OK).
- Should include `SKILL.md` or `<subdir>/SKILL.md` (warning if missing).
- Example: `["SKILL.md", "utils/helpers.ts", "prompts/default.md"]`

**Hook:**
- `files` lists script(s): `["hooks/auth.sh"]` or `["hooks/pre.sh", "hooks/post.sh"]`.
- The manifest **must** include a `hook` object (below) that references one of these files.

**Plugin:**
- `files` is the bundle tree.
- Should include `.claude-plugin/plugin.json`.
- Example: `[".claude-plugin/plugin.json", "assets/icon.png"]`

### Hook Object (required for `type: "hook"`)

```json
{
  "event": "PreToolUse",
  "matcher": "Bash",
  "command": "hooks/auth.sh",
  "type": "command"
}
```

| Field | Notes |
|-------|-------|
| `event` | Claude Code hook event name (e.g., `PreToolUse`, `PostToolUse`, `SessionStart`). |
| `matcher` | Tool name or regex pattern to match (e.g., `Bash`, `^Bash.*`). |
| `command` | Relative path to the hook script, as placed in the live directory (e.g., `hooks/auth.sh`). Must reference one of the `files` entries. |
| `type` | Hook entry type; defaults to `"command"` if absent. |

---

## Version History & Rollback

Every `update` or `add --force` snapshots the prior live version to a **version store** before mutating the live directory. This enables `rollback`.

### Version Store Location

```
~/.claude-plugins/addon-store/<scope>/<type>/<name>/<version>/
  ├── addon.json           # Manifest as installed at that version
  ├── files/…              # Snapshot of placed files
  └── settings-entry.json  # For hooks: the exact settings.json entry registered
```

### Retention Policy

By default, the CLI retains the **last 5 versions** per add-on and automatically prunes older snapshots after each successful `snapshot` or `update`.

### `list` Output

```
local scope:
  agent   my-agent   v2.1.0   [stored: 2.0.5, 2.0.1, 1.9.0]
  skill   my-skill   v1.0.0   (no prior snapshots)

global scope:
  plugin  gpt-helper   v3.0.0   [stored: 2.9.1, 2.8.0, 2.7.0]
```

---

## Flags & Validation

### Required Flags

- `--scope local|global`: Mandatory for all mutating commands (`add`, `update`, `remove`, `rollback`). Optional for `list` (defaults to both).

### Conditional Flags

- `--type`: Required for `remove` and `rollback`. Optional for `add` (triggers scaffold mode if provided without a source manifest).
- `--lang`: Optional for `add` in scaffold mode; defaults to `typescript`.
- `--force`: Optional for `add` (overwrites existing add-on, snapshotting the prior version).
- `--to`: Optional for `rollback` (target version; defaults to most recent prior snapshot).

### Validation Rules

1. **Plugin is global-only.** `--scope local --type plugin` is rejected before any I/O.
2. **Agent is single-file.** Validation rejects agents with zero or more than one file entry.
3. **Scope isolation.** `remove`, `update`, and `rollback` operate within the specified scope only.
4. **Manifest safety.** Files must be relative paths; absolute paths, `..` segments, and null bytes are rejected.

---

## Atomicity & Crash Safety

### `add` & `update` (Write Path)

1. Validate the source `addon.json`.
2. If updating an existing add-on, snapshot the current live version to the version store.
3. Stage all placed files to a temp directory (within the same scope root for atomic rename).
4. Atomically swap the staged files into the live location.
5. For hooks, merge the hook entry into `settings.json` (done last, so reverted first on failure).
6. On any failure, restore from the snapshot or `.bak`, revert the `settings.json` merge, and report.

### `remove` (Delete Path)

1. Read the add-on's sidecar metadata (which files were placed).
2. Delete only those files (never blindly delete the owner dir).
3. Unregister the hook entry from `settings.json` if present.
4. Prune now-empty owner directories.

### Crash Recovery

- **Version snapshots** are written atomically (temp + rename).
- **Hook merges** are idempotent: re-adding the same hook is a no-op (no duplicates).
- **Missing files tolerate idempotence:** remove/rollback succeed even if a file was already deleted by the user (e.g., hand-deletion of `agents/my-agent.md`).

---

## Testing

```bash
# Run all tests
npm test

# Run tests for a specific module
npm test -- addon-manifest

# Watch mode
npm test -- --watch
```

Tests use `vitest` with fake `*FsPort` implementations to avoid real filesystem I/O.

---

## Architecture

- **`cli/src/addon/manifest.ts`** — Manifest types, validation, scope normalization.
- **`cli/src/addon/scope.ts`** — Scope resolution (local `./.claude/` vs. global `~/.claude/`).
- **`cli/src/addon/placement.ts`** — Per-type placement logic (where files land, sidecar location).
- **`cli/src/addon/settings.ts`** — Hook registration in `settings.json` (idempotent merge/unmerge).
- **`cli/src/addon/store.ts`** — Version store: snapshot, list, read, rollback (port-based I/O).
- **`cli/src/addon/scaffold-source.ts`** — Scaffold generation (reuses `generatePluginTemplate` from plugin-template).
- **`cli/src/addon/lifecycle.ts`** — Core add-on lifecycle engine (`add`, `list`, `update`, `remove`, `rollback`).
- **`cli/src/commands/addon-*.ts`** — Command wrappers (parse args, call lifecycle, format output).

All modules are injection-friendly via port interfaces (`LifecycleFsPort`, `SettingsFsPort`, `VersionStore`) for testability.

---

## Examples

### Create and install a local hook

```bash
# Scaffold a TypeScript hook
claude plugin addon add my-logger --type hook --scope local --lang typescript

# Later, update it
claude plugin addon update ./path/to/my-logger --scope local

# View all local add-ons
claude plugin addon list --scope local

# Rollback to the prior version
claude plugin addon rollback my-logger --type hook --scope local
```

### Install a global skill

```bash
# Install from a directory with addon.json
claude plugin addon add ~/skills/my-analyzer --scope global

# Force-overwrite (snapshotting the prior version)
claude plugin addon add ~/skills/my-analyzer --scope global --force

# List versions
claude plugin addon list --scope global

# Restore a specific prior version
claude plugin addon rollback my-analyzer --type skill --scope global --to 1.5.0
```

---

## Notes

- **Hooks are inactive until registered.** A hook script placed in `hooks/` has no effect; the hook entry must exist in `settings.json` (the CLI handles this automatically on `add`).
- **Project-local overrides global.** If both `./.claude/agents/foo.md` and `~/.claude/agents/foo.md` exist, the local version takes precedence in Claude Code.
- **No circular dependencies.** Add-ons cannot depend on or require other add-ons; each is independent.
- **No remote registry.** The `addon` group manages local/global installs only. The separate `install`/`publish`/`search` commands talk to the ClaudeForge marketplace.

---

## Development

See [../README.md](../README.md) for setup, testing, and build commands.

### Adding a New Add-On Type

1. Update `AddonType` in `cli/src/addon/manifest.ts`.
2. Add per-type validation rules to `validatePerTypeFilesConstraints`.
3. Update `placement.ts` with the new type's file mapping.
4. Add tests for the new type in `addon-manifest.test.ts` and `command-addon-*.test.ts`.
5. Document the new type in this README (section **Per-Type File Semantics**).
