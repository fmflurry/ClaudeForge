# OpenSpec Proposal: claude-plugin-cli

## Why

Today, Claude add-ons (hooks, skills, agents, plugins) are managed ad-hoc through hand-editing files under `.claude/` and `~/.claude/`, with no uniform way to declare an add-on's type, scope, or lifecycle. The existing CLI handles remote marketplace flows (publish/search/install-from-registry) but does not provide unified local add-on lifecycle management (create, install, update, remove) across local and global scopes. This change is needed as the foundation for a consistent "npm-for-Claude" experience where developers can declaratively manage all four add-on types with simple CLI commands.

## What Changes

- **Add-on manifest schema**: Define a canonical, type-explicit structure template covering all four add-on types (hook, plugin, skill, agent), declaring the add-on's name, version, type, scope target (local or global), and files. This becomes the single source of truth for any add-on's identity and lifecycle.

- **CLI lifecycle commands**: Extend or introduce `add` (create/install), `remove`, `update`, and `list` verbs that accept a `--scope` flag to target either LOCAL (CWD `.claude/`) or GLOBAL (`~/.claude/`) scope. These commands operate over manifests, not raw files.

- **Scope resolution rules**: Define mapping logic: local scope resolves to `./.claude/` in the current working directory; global scope resolves to `~/.claude/`. Scope is explicit and opt-in to avoid accidents.

- **Reconciliation with existing commands**: The existing `install`, `remove`, `update`, `scaffold`, and `list` commands today target the remote marketplace. Design phase will resolve whether these are reused (with scope logic layered on) or whether local add-on commands become separate verbs; this proposal does not pre-decide but flags it as a design concern.

- **No breaking changes**: This change is additive. Existing marketplace commands remain unchanged until design phase decides on integration strategy.

## Capabilities

### New Capabilities

- `addon-manifest`: The explicit structure template and schema that declares an add-on's type (hook, plugin, skill, agent), name, version, target scope, and file paths.

- `addon-lifecycle`: CRUD operations (add/create, list/read, update, remove) over local and global add-ons, respecting the manifest as the contract.

- `addon-scope`: Local (CWD) vs global (`~/.claude`) scope resolution, targeting, and validation to ensure add-ons land in the correct directory.

### Modified Capabilities

None — no existing specs in `openspec/specs/` to modify.

## Impact

**Affected systems:**
- `cli/` workspace: new command modules for add-on lifecycle, manifest schema/validation module, scope resolver utility.
- `plugin-template/` generator: may emit manifests when scaffolding new add-ons (deferred to design phase).
- Filesystem layout: `.claude/` and `~/.claude/` directory structure and manifest conventions.

**Not affected:** Backend, frontend, or remote marketplace API.

**Dependencies:** TypeScript, Node 22+, existing CLI stack (vitest, yargs/commander). No new external dependencies expected. Adheres to user rule: no `any` types.
