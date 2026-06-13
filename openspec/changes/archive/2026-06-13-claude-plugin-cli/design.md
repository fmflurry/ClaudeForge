## Context

The `cli/` workspace (TypeScript, Node 22+, vitest, commander) today implements a **remote-marketplace** client. Commands are registered in `cli/src/dispatcher.ts` via `createProgram(deps)` using commander, with each verb delegating to a `run*` function in `cli/src/commands/*.ts`. Every command returns the uniform `CommandResult { exitCode: number; output: string }` and takes injected dependency objects (`{ client, homeDir, fs? }`) so the real FS/network is swappable in tests via hand-rolled `*FsPort` interfaces (see `realFsPort` in `cli/src/commands/install.ts`, `realRemoveFsPort` in `remove.ts`). `cli/src/index.ts` is the only place that calls `process.exit`.

Key existing behaviors and conventions that constrain this design:

- **Remote verbs already exist**: `install <pluginName>`, `remove <pluginName>`, `update <pluginName>`, `list`, `search`, `publish`, `scaffold`, `validate`, plus auth/config/org. These all talk to `IMarketplaceClient` (`cli/src/api/client.ts`) and store state in a **plugins home** (`~/.claude-plugins/`, resolved by `resolveHome` in `cli/src/config/config.ts` — note: NOT `~/.claude/`).
- **Registry pattern**: `cli/src/registry/registry.ts` persists `installed.json` with `InstalledRecord { name, version, installedAt, path }`, exposes pure immutable record ops (`addRecord`/`removeRecord`/`findRecord`), and already has a `backups/` convention (`backupsDir`, `ensureBackupsDir`) rooted under a passed-in `homeDir`.
- **Manifest already exists but is marketplace-shaped**: `cli/src/commands/validate.ts` validates `plugin.json` with `PluginManifest { name, version, description, author, types[], languages[], entrypoints[], ... }`. `VALID_TYPES = {skill, hook, agent, command, plugin}`, `SEMVER_RE`. Validation is **hand-rolled** (`validateManifest(manifest: unknown): ValidationResult`) returning `{ valid, errors[], warnings[] }` — no Zod in the CLI dependency tree.
- **Scaffold** (`cli/src/commands/scaffold.ts`) emits a `plugin.json` via a local `buildManifest` and one minimal entrypoint; the richer **pure** generator lives in `plugin-template/src/generator.ts` (`generatePluginTemplate(opts: GeneratorOptions): GeneratedFileMap`, a relative-path→content map, performs no I/O). The scaffold command currently does **not** use that generator — it duplicates a thinner version inline.
- **Update atomicity precedent**: `update.ts` copies the existing dir to `backups/<name>_<version>` before writing and, on write failure, retains the prior version and reports the backup path.
- **Test convention**: one test file per module in `cli/src/__tests__/command-*.test.ts`, real `mkdtemp` temp dirs plus `vi.fn()` fakes implementing the port interfaces.

The new capability — declarative local/global add-on lifecycle — is a **distinct concern** from the marketplace client. It targets Claude Code's own config dirs (`./.claude/` and `~/.claude/`), not the plugins home, and is driven by an add-on manifest rather than a registry record fetched from the API.

### Authoritative Claude Code on-disk layout (replaces prior assumptions)

Base dirs: global = `~/.claude/`, project-local = `./.claude/`. Project overrides global.

| Type | Live install shape | Notes |
| ---- | ------------------ | ----- |
| **agent** | `<scope>/.claude/agents/<name>.md` | A single markdown file with YAML frontmatter. Recursive discovery; subdirs allowed. **Not a directory.** |
| **skill** | `<scope>/.claude/skills/<name>/SKILL.md` (+ supporting files) | A **directory** tree. |
| **hook** | script at `<scope>/.claude/hooks/<script>.sh` **plus** a JSON entry merged into `<scope>/.claude/settings.json` under the `hooks` key | File presence alone does NOT activate a hook; the `settings.json` entry does. |
| **plugin** | bundle dir under `~/.claude/plugins/` with `.claude-plugin/plugin.json` | **GLOBAL ONLY.** No project-local install path exists. |

`settings.json` hooks schema:

```jsonc
{
  "hooks": {
    "<Event>": [                       // PreToolUse | PostToolUse | SessionStart | ...
      {
        "matcher": "<ToolNameOrRegex>",
        "hooks": [
          { "type": "command", "command": "<path-to-script>" }
        ]
      }
    ]
  }
}
```

A personal override file `<scope>/.claude/settings.local.json` shares the same schema. Legacy slash commands (`<scope>/.claude/commands/<name>.md`) are **out of scope** — our four types are agent/skill/hook/plugin only.

This authoritative layout invalidates the prior design's uniform `<typeSubdir>/<name>/` claim: agents are single files, hooks require a `settings.json` mutation, and plugins are global-only. The revision below corrects each.

## Goals / Non-Goals

**Goals:**

- Define a canonical, type-explicit **add-on manifest** (`addon.json`) as the single source of truth for an add-on's identity, supported scopes, and owned files — with per-type file semantics (agent = one `.md`, skill = a dir tree, hook = script(s) + a declared `settings.json` entry, plugin = a bundle dir).
- Provide full lifecycle (`add`/install, `list`, `update`, `remove`, `rollback`) over the four add-on types in **local** (`./.claude/`) or **global** (`~/.claude/`) scope, against the authoritative live paths above.
- For **hooks**, idempotently register the hook entry in the target scope's `settings.json` on `add`, and unregister it on `remove`, with safe atomic read-modify-write that preserves unrelated settings.
- Enforce explicit scope selection and strict scope isolation, and enforce the hard constraint that **type `plugin` is global-only**.
- Retain **version history**: snapshot the live version into a CLI-managed version store on `update`, and support `rollback` to a stored version. `list` surfaces the live version and available stored versions.
- Reuse existing conventions: hand-rolled validation (matching `validate.ts`), `CommandResult`, injected `*FsPort` ports, immutable record ops, the `backups/` + temp-swap atomicity pattern, and the **pure** `generatePluginTemplate` generator for the scaffold/create path (no duplication).
- Make placement deterministic from the manifest alone (no file-content inspection).
- Land additively — zero changes to existing marketplace verbs.

**Non-Goals:**

- **No remote-marketplace changes.** `install/remove/update/list/scaffold/publish/search` against `IMarketplaceClient` and `~/.claude-plugins/` are untouched.
- **No GUI / TUI.**
- **No dependency resolution between add-ons.**
- **No deep plugin bundle semantics.** For `plugin` we place the bundle dir under `~/.claude/plugins/<name>/`; we do not parse/execute its inner `.claude-plugin/plugin.json` beyond placement and version-store snapshotting.
- **No migration** of existing `~/.claude-plugins/` installs into the add-on model.
- **No `settings.local.json` writes in this change** — we register hooks into the shared `settings.json` only (the local file is noted as a future option).

## Decisions

### Decision 1 — Manifest format & filename: `addon.json` (JSON, hand-rolled validation)

**Decision.** Use a dedicated JSON file named **`addon.json`** colocated with the add-on's source, distinct from the marketplace `plugin.json`. Field set:

```jsonc
{
  "name": "my-hook",                         // string, required, non-empty
  "version": "1.0.0",                        // string, required, strict semver (reuse SEMVER_RE)
  "type": "hook",                            // closed enum: hook | plugin | skill | agent
  "supportedScopes": ["local", "global"],    // explicit non-empty array; "both" sugar expands to ["local","global"]
  "files": ["hooks/auth.sh"],                // relative POSIX paths; per-type semantics below
  "hook": {                                   // REQUIRED iff type === "hook"; forbidden otherwise
    "event": "PreToolUse",                   // Claude Code hook event name
    "matcher": "Bash",                       // tool name or regex
    "command": "hooks/auth.sh",              // path to the script entry, relative to scope root after install
    "type": "command"                        // hook entry type; default "command"
  }
}
```

`supportedScopes` is stored as an **explicit array** of `"local" | "global"`. The input shorthand string `"both"` is accepted and **expanded on read** to `["local","global"]` (it is never persisted as the literal `"both"`). An empty array is rejected.

**Per-type `files` semantics** (placement is fully derived from `type` + `name` + `files` — see Decision 2):

- **agent**: `files` MUST resolve to exactly one markdown file; it lands as `agents/<name>.md`. If `files` contains a single non-`.md` file the validator warns; if it contains more than one entry the validator errors (an agent is one file).
- **skill**: `files` is a directory tree (one or more relative paths, all rooted notionally under the skill dir). They land under `skills/<name>/…` preserving relative structure; the tree SHOULD contain a `SKILL.md`. Missing `SKILL.md` is a warning, not an error.
- **hook**: `files` enumerates the script(s) (e.g. `hooks/auth.sh`); they land under `hooks/`. The manifest MUST additionally carry the `hook` object (event/matcher/command) that is merged into `settings.json` (Decision 6). `hook.command` MUST reference a path that the placement produces.
- **plugin**: `files` is the bundle tree; it lands under `~/.claude/plugins/<name>/…`. SHOULD contain `.claude-plugin/plugin.json`.

A new module `cli/src/addon/manifest.ts` exports `AddonManifest`, `AddonType = 'hook' | 'plugin' | 'skill' | 'agent'`, `AddonScope = 'local' | 'global'`, `HookRegistration` (the `hook` object type), and a pure `validateAddonManifest(input: unknown): AddonValidationResult` returning `{ valid, errors[], warnings[] }` — **mirroring** `validateManifest` in `validate.ts`. It also exports `normalizeSupportedScopes(input: unknown): AddonScope[]` (the `"both"` expander) and `resolveSupportedScopes` reused by the scope validator. No `any` — all narrowing is via `unknown` + type guards.

**Cross-field validation rules enforced by `validateAddonManifest`:**

1. `type === "plugin"` ⇒ `supportedScopes` MUST NOT include `"local"` (plugin is global-only). Reject with a clear message if it does, or if a `plugin` declares `supportedScopes: ["local"]`.
2. `type === "hook"` ⇒ the `hook` object is REQUIRED and `hook.command` MUST be one of the placed paths derivable from `files`. For all other types the `hook` field MUST be absent (warn-and-ignore vs error: **error**, to keep manifests honest).
3. `files` validation rejects `path.isAbsolute(f)`, any `..` segment, and `\0` — reusing the traversal guard proven in `install.ts`.
4. `type === "agent"` ⇒ exactly one entry in `files`.

**Alternatives considered.** Reuse `plugin.json`/`PluginManifest` (rejected — marketplace-shaped, multi-`type` array including `command`, no `supportedScopes`/`hook`); YAML/TOML (rejected — no parser in CLI deps); Zod (rejected for CLI — no Zod dependency; established pattern is hand-rolled `unknown`-narrowing validators).

**Rationale.** A separate minimal JSON manifest with explicit per-type semantics satisfies "type-explicit, self-describing, deterministic placement" while staying consistent with `validate.ts` and encoding the real Claude Code constraints (plugin-global-only, hook-needs-settings-entry).

### Decision 2 — Scope resolution & per-type placement (authoritative layout)

**Decision.** `cli/src/addon/scope.ts` exports `resolveScopeRoot(scope: AddonScope, deps: { cwd: string; homeDir: string }): string`:

- `local` → `path.resolve(cwd, '.claude')`
- `global` → `path.resolve(homeDir, '.claude')`, where `homeDir` derives from `os.homedir()` (NOT the marketplace `resolveHome`, which points at `~/.claude-plugins`).

Placement is **per-type** and is owned by a new strategy module `cli/src/addon/placement.ts` exporting `resolvePlacement(manifest: AddonManifest, scopeRoot: string): Placement`. `Placement` describes the canonical live target(s) and how each manifest `file` maps onto disk:

```ts
interface Placement {
  readonly type: AddonType;
  readonly liveTargets: readonly PlacedFile[];   // source-relative path -> absolute live destination
  readonly ownerPath: string;                    // the canonical "thing" list/remove keys on (file or dir)
  readonly settingsEntry?: HookRegistration;     // present iff type === 'hook'
}
interface PlacedFile { readonly fromRel: string; readonly toAbs: string; }
```

Per-type mapping:

| type | ownerPath | file mapping | settings.json |
| ---- | --------- | ------------ | ------------- |
| **agent** | `<scopeRoot>/agents/<name>.md` (a **file**) | the single `files[0]` → `agents/<name>.md` | — |
| **skill** | `<scopeRoot>/skills/<name>/` (a **dir**) | each `files[i]` → `skills/<name>/<files[i]>` preserving structure | — |
| **hook** | `<scopeRoot>/hooks/` scripts (files), keyed by add-on `name` via a sidecar (below) | each `files[i]` → `hooks/<files[i]>` (basename preserved) | merge `hook` entry into `<scopeRoot>/settings.json` |
| **plugin** | `<homeDir>/.claude/plugins/<name>/` (a **dir**, global only) | each `files[i]` → `plugins/<name>/<files[i]>` | — |

Because the authoritative live paths cannot hold a colocated `addon.json` for every type (an agent is a single `.md` file; a hook's scripts share a flat `hooks/` dir), the CLI maintains a small **per-scope sidecar index** for `list`/`remove`/`rollback` to know what it owns:

- Sidecar location: `<scopeRoot>/.addons/<type>/<name>.json` — a copy of the installed `addon.json` plus a `placedFiles: string[]` array (scope-relative paths actually written) and the resolved `settingsEntry` for hooks. This keeps the live dirs pristine (no stray manifests next to a user's hand-authored agents) while preserving the manifest-as-source-of-truth principle. `list` scans `<scopeRoot>/.addons/**`; `remove` reads the sidecar to know exactly which files and which `settings.json` entry to delete.

**Alternatives considered.**
- *Colocate `addon.json` in the live dir (prior design).* Rejected: impossible for agents (single file) and messy for hooks (flat shared `hooks/`); would also pollute Claude Code's discovery dirs.
- *Central `installed.json` registry like `registry.ts`.* Rejected as the live index for the same drift reasons as before, but the **per-scope `.addons/` sidecar** is a localized, scope-isolated variant that does not centralize across scopes.
- *Reuse `resolveHome` for global.* Rejected — wrong target (`~/.claude-plugins`).

**Rationale.** Placement now matches Claude Code's real layout exactly, the sidecar gives deterministic remove/list/rollback without polluting live dirs, and all path knowledge is isolated behind `placement.ts` + `scope.ts`.

### Decision 3 — Command surface: `addon` sub-command group (additive), with scaffold/create path

**Decision.** A dedicated `addon` command namespace (mirroring the existing `config` group), with verbs:

```
claude-plugin addon add <source>    --scope <local|global> [--force]
                                     # <source> = dir/file containing addon.json (install path)
claude-plugin addon add <name>      --type <hook|plugin|skill|agent> --scope <local|global> [--lang <ts|py|go|rust>]
                                     # no source manifest -> SCAFFOLD/CREATE path (Decision 7)
claude-plugin addon list            [--scope <local|global>]      # omitting --scope lists both
claude-plugin addon update <source> --scope <local|global>
claude-plugin addon remove <name>   --type <hook|plugin|skill|agent> --scope <local|global>
claude-plugin addon rollback <name> --type <hook|plugin|skill|agent> --scope <local|global> [--to <version>]
```

- `add` has two paths, disambiguated by whether `<source>` resolves to a path containing an `addon.json`:
  - **install path** — `<source>` is a dir/file with an `addon.json`: read + validate, place per Decision 2, register hook entry if applicable.
  - **scaffold/create path** — `<source>` is a bare name with no existing manifest and `--type` is supplied: generate starter files via `generatePluginTemplate` (Decision 7) into the resolved scope, emitting an `addon.json`.
- `--scope` is **required** for all mutating verbs (`add`/`update`/`remove`/`rollback`); `list` defaults to both scopes and labels each.
- `--scope local` combined with `--type plugin` is **rejected** before any I/O (plugin is global-only).
- `rollback` restores a stored version from the version store (Decision 5). `--to` selects a specific stored version; omitted ⇒ the most recent prior snapshot.
- Each verb maps to a `run*` in `cli/src/commands/addon-*.ts` returning `CommandResult`, registered in `dispatcher.ts` and exposed as injectable `DispatcherDeps` entries.

**Alternatives considered.** Extend marketplace `install/remove/update/list` with `--scope` (rejected — entangles two storage models + two manifest formats); top-level `add`/`rm` (rejected — collides with existing top-level `remove`).

**Rationale.** Additive, lowest blast radius, mirrors the proven `config` sub-command idiom.

### Decision 4 — Atomicity: stage-to-temp, atomic swap, backup-and-restore

**Decision.** Centralize in `cli/src/addon/lifecycle.ts`. Only **placed files** (and, for hooks, the one `settings.json` entry) are ever touched. Crash-safety combines temp-staging with the repo's backup convention:

1. **Add / Update (write path):**
   - Compute `Placement`. Stage all placed files into a sibling temp dir **inside the same scope root** (`<scopeRoot>/.addons/.tmp-<rand>/…` for sidecar + a parallel staging of live targets) so `rename` stays on one filesystem.
   - If an install already exists, snapshot the live version into the **version store** (Decision 5) before mutating.
   - **Atomic swap per owner**: for a dir owner (skill/plugin) `rename(live → live.bak)`, `rename(staged → live)`, drop `.bak`; for a file owner (agent) write staged then atomic `rename` over the destination; for hooks, write scripts then perform the `settings.json` merge (Decision 6) last, after scripts are in place.
   - On any failure at any step: restore from `.bak` / version-store snapshot, revert the `settings.json` merge, and report (mirrors `update.ts` messaging: "previous version retained, snapshot saved to …").
   - `update` is a **no-op** when staged `version` equals installed `version`.
2. **Remove (delete path):** read the sidecar, delete **only** its `placedFiles`, unregister the hook `settings.json` entry if present, then delete the sidecar. Prune now-empty owner dirs (`skills/<name>/`, `plugins/<name>/`). Never touch unlisted files.

A `LifecycleFsPort { mkdir, writeFile, readFile, rm, rename, copyDir, exists, readdir, stat }` extends the existing port pattern so it is fully fakeable.

**Alternatives considered.** Backup-only without temp staging (rejected — partial-live-state window); per-file `.bak` in place (rejected — more failure permutations, no all-or-nothing).

**Rationale.** All-or-nothing semantics + recoverable artifact, reusing the repo's backup convention and messaging, extended to cover the `settings.json` mutation ordering.

### Decision 5 (NEW) — Version store + rollback

**Decision.** Because the live paths are fixed by Claude Code (a single `agents/<name>.md`, a single `skills/<name>/`, etc.) and cannot hold multiple versions in place, the CLI maintains a **version store separate from the live install**, rooted under the existing plugins home (reusing `resolveHome` + the `backups/` convention from `registry.ts`):

```
~/.claude-plugins/addon-store/<scope>/<type>/<name>/<version>/
    addon.json            # the manifest as installed at that version
    files/…               # verbatim copy of the placed files for that version
    settings-entry.json   # for hooks: the exact settings.json entry that was registered (for accurate rollback)
```

`cli/src/addon/store.ts` exports a pure-ish store API over an injected port:

```ts
interface VersionStore {
  snapshot(args: { scope; type; name; version; sourceFiles; settingsEntry? }): Promise<string>; // returns stored path
  list(args: { scope; type; name }): Promise<string[]>;            // available versions, semver-sorted
  read(args: { scope; type; name; version }): Promise<StoredVersion>;
  latestPrior(args: { scope; type; name; currentVersion }): Promise<string | undefined>;
  prune(args: { scope; type; name; keep: number }): Promise<void>; // bounded retention
}
```

- **On `update`:** before swapping in the new version, `snapshot()` the current live version into the store. Then install the new version live (Decision 4).
- **On `add` over an existing add-on with `--force`:** snapshot the existing version first (same as update).
- **`rollback <name> --type --scope [--to <version>]`:** resolve the stored version (`--to` exact, else `latestPrior`), validate it still exists in the store, then run the same atomic swap path (Decision 4) to install the stored files live and re-merge the stored `settings-entry.json` for hooks. The pre-rollback live version is itself snapshotted first, so rollback is reversible.
- **`list`** shows, per add-on: the live version (from the sidecar) **and** the set of stored versions (from `store.list`).
- **Retention:** default keep last **N = 5** versions per add-on; `prune` runs after each successful `snapshot`. N is a constant (`DEFAULT_VERSION_RETENTION`) — see Open Questions for configurability.
- **Atomicity:** snapshots are written to `…/<version>.tmp-<rand>/` then `rename`d into place; rollback uses the same temp-stage + swap + restore-on-failure as Decision 4.

**Alternatives considered.** Keep versions in the live dir (impossible — fixed single-slot paths); rely on `backups/` flat dir only (rejected — no structured version lookup, no rollback-to-specific-version).

**Rationale.** Satisfies the REQUIRED version-history + rollback while respecting Claude Code's fixed live layout, reusing the plugins-home + backup conventions, and staying atomic.

### Decision 6 (NEW) — Hook registration in `settings.json` (idempotent merge / unmerge, atomic write)

**Decision.** `cli/src/addon/settings.ts` owns all `settings.json` mutation. Write target: **`<scopeRoot>/settings.json`** for both local and global (i.e. `./.claude/settings.json` and `~/.claude/settings.json`). `settings.local.json` is explicitly NOT written in this change (future option, Open Questions).

API (pure transform + thin atomic I/O):

```ts
function mergeHookEntry(settings: SettingsJson, reg: HookRegistration): SettingsJson; // pure, immutable
function removeHookEntry(settings: SettingsJson, reg: HookRegistration): SettingsJson; // pure, immutable
async function readSettings(path, port): Promise<SettingsJson>;   // missing/empty -> {}
async function writeSettingsAtomic(path, settings, port): Promise<void>; // temp + rename
```

- **Merge semantics (idempotent):** locate `settings.hooks[reg.event]` (create the event array if absent); within it, find the matcher group whose `matcher === reg.matcher` (create if absent); within that group's `hooks` array, add `{ type: reg.type ?? 'command', command: reg.command }` **only if an identical entry is not already present** (dedupe by `type` + `command`). Re-adding the same hook is a no-op — no duplicate entries.
- **Unmerge semantics:** remove the matching `{ type, command }` entry; if its matcher group's `hooks` becomes empty, drop the matcher group; if the event array becomes empty, drop the event key. Never touch unrelated events/matchers/keys.
- **Preservation:** all transforms are immutable spreads; unrelated top-level settings keys and unrelated hook events are carried through untouched.
- **Atomic write:** serialize with 2-space JSON, write to `<path>.tmp-<rand>`, `rename` over the target (same dir ⇒ same filesystem ⇒ atomic). Never truncate-in-place. Malformed existing JSON ⇒ fail the operation with a clear error rather than overwrite (do not silently clobber a hand-edited file).
- **Ordering in lifecycle:** scripts are placed first; the `settings.json` merge is the **last** write on `add` and is reverted first on rollback/failure.

**Alternatives considered.** Write to `settings.local.json` by default (rejected for this change — shared `settings.json` is the team-visible activation point; local file deferred); naive `JSON.parse`→mutate→`writeFile` (rejected — non-atomic, can corrupt on crash, and risks duplicate entries on re-add).

**Rationale.** Hooks are only active once registered in `settings.json`; an idempotent immutable merge with atomic write is the safe way to do that without corrupting unrelated settings.

### Decision 7 (NEW) — Scaffold/create path reuses `generatePluginTemplate` (no duplication)

**Decision.** The scaffold/create path of `addon add <name> --type …` MUST reuse the **pure** `generatePluginTemplate` generator from `plugin-template/src/generator.ts` rather than duplicating scaffolding. To avoid duplication between the existing `scaffold` command and the new addon path, factor the shared logic into a new `cli/src/addon/scaffold-source.ts`:

```ts
function buildAddonScaffold(args: {
  name: string; type: AddonType; scope: AddonScope; language: TemplateLanguage;
}): { addonJson: string; files: GeneratedFileMap };
```

- It calls `generatePluginTemplate({ name, language, types: [mapAddonTypeToPluginType(type)] })` to obtain the `GeneratedFileMap`, then **adapts** the output for the addon model:
  - Drop/replace the marketplace `plugin.json` entry; synthesize an `addon.json` (the addon manifest) with the correct `type`, `supportedScopes` (defaulting to the type's eligibility — `plugin` ⇒ `["global"]`, others ⇒ `["local","global"]`), `files`, and a `hook` stub when `type === 'hook'`.
  - Remap generated relative paths onto the per-type canonical shape (agent ⇒ a single `<name>.md`; skill ⇒ a tree with `SKILL.md`; hook ⇒ a `hooks/<name>.sh` + the `hook` entry; plugin ⇒ a bundle with `.claude-plugin/plugin.json`).
- The existing `runScaffold` command is refactored to delegate its manifest/file generation to `generatePluginTemplate` as well (removing its inline `buildManifest`/`generateEntrypoint` duplication where reasonable), so there is exactly one generator of truth. The addon scaffold path and `runScaffold` share `scaffold-source.ts` helpers; neither re-implements template bodies.
- After generation the addon scaffold path runs the normal `add` install path (validate the synthesized `addon.json` → place → register), so scaffolding and installing share one code path.

**Alternatives considered.** A bespoke addon scaffolder (rejected — duplicates `plugin-template`); leaving `scaffold` untouched and only wiring the addon path to the generator (acceptable fallback if refactoring `runScaffold` proves risky — captured as a non-blocking note).

**Rationale.** One generator, no duplicated template bodies; the scaffold/create path becomes "generate → validate → install" over the same lifecycle.

### Decision 8 — Module layout in `cli/src` (updated)

```
cli/src/addon/
  manifest.ts          # AddonManifest, AddonType, AddonScope, HookRegistration,
                       # validateAddonManifest(), normalizeSupportedScopes() (pure)
  scope.ts             # resolveScopeRoot(), AddonScope helpers (pure)
  placement.ts         # resolvePlacement(): per-type live targets + sidecar + settingsEntry (pure)
  settings.ts          # mergeHookEntry()/removeHookEntry() (pure) + atomic read/write (port)
  store.ts             # VersionStore: snapshot/list/read/latestPrior/prune (port)
  scaffold-source.ts   # buildAddonScaffold() reusing generatePluginTemplate (pure-ish)
  lifecycle.ts         # add/update/remove/list/rollback engine + LifecycleFsPort (I/O via port)
cli/src/commands/
  addon-add.ts         # runAddonAdd(args, deps): CommandResult  (install + scaffold paths)
  addon-list.ts        # runAddonList(args, deps)
  addon-update.ts      # runAddonUpdate(args, deps)
  addon-remove.ts      # runAddonRemove(args, deps)
  addon-rollback.ts    # runAddonRollback(args, deps)
cli/src/__tests__/
  addon-manifest.test.ts
  addon-scope.test.ts
  addon-placement.test.ts
  addon-settings.test.ts
  addon-store.test.ts
  addon-scaffold-source.test.ts
  command-addon-add.test.ts
  command-addon-list.test.ts
  command-addon-update.test.ts
  command-addon-remove.test.ts
  command-addon-rollback.test.ts
```

`dispatcher.ts` gains an `addonCmd = new Command('addon')` group with five sub-verbs, five new optional `DispatcherDeps` fields (`runAddonAdd?`, `runAddonList?`, `runAddonUpdate?`, `runAddonRemove?`, `runAddonRollback?`) defaulting to the real `run*`, and `program.addCommand(addonCmd)` — identical to how `configCmd` is wired. The dispatcher resolves `cwd` (local), `os.homedir()`-based `~/.claude` (global scope root), and `resolveHome()` (`~/.claude-plugins`, for the version store) and passes them as deps; `index.ts` is unchanged.

**Rationale.** Matches existing domain structure, keeps files small and testable, isolates every new concern (settings merge, version store, placement) behind its own module.

## Risks / Trade-offs

- **[Risk] `settings.json` corruption / merge conflict.** A bad merge or crash mid-write could damage a hand-edited `settings.json`. → **Mitigation:** all merges are pure immutable transforms; writes are temp-stage + atomic `rename`; malformed existing JSON aborts the op (never clobbered); merge/unmerge are dedupe-idempotent; the merge is the last write on `add` and first revert on failure.
- **[Risk] Plugin-global-only violated.** A `plugin` add-on targeting local scope has no valid live path. → **Mitigation:** enforced in two places — `validateAddonManifest` (manifest `supportedScopes` must not include `local` for `plugin`) and the command layer (`--scope local --type plugin` rejected before I/O).
- **[Risk] Version-store unbounded growth.** Every update/rollback snapshots files. → **Mitigation:** bounded retention (`DEFAULT_VERSION_RETENTION = 5`) pruned after each snapshot; store lives under the plugins home, not in the user's `.claude/`. Pruning policy configurability is an Open Question.
- **[Risk] Agent-file vs skill-dir asymmetry.** An agent owner is a single file, a skill/plugin owner is a dir, a hook owner is scripts-in-a-shared-dir-plus-a-settings-entry. A naive "rm the owner dir" would be wrong for agents/hooks. → **Mitigation:** `placement.ts` returns an explicit `ownerPath` + `placedFiles`; remove deletes exactly `placedFiles` (never a blanket dir wipe) and prunes empty dirs only.
- **[Risk] Sidecar drift vs live dirs.** A user hand-deletes a live agent `.md` while the `.addons/` sidecar still lists it. → **Mitigation:** `remove`/`rollback` tolerate already-missing placed files (idempotent unlink); `list` flags an add-on as "drifted" if a placed file is absent. (Detection depth is bounded — see Open Questions.)
- **[Risk] Cross-filesystem `rename` not atomic.** → **Mitigation:** stage inside the same scope root (and inside the same store dir for snapshots), never the OS temp dir.
- **[Risk] Scaffold refactor regresses existing `scaffold`.** Refactoring `runScaffold` onto the shared generator could change its output. → **Mitigation:** keep `runScaffold`'s observable output stable, add characterization tests before refactor; the addon scaffold path is the primary consumer of `scaffold-source.ts`.
- **[Risk] Name collision with marketplace verbs.** → **Mitigation:** namespaced `addon` group keeps the surface distinct.

## Migration Plan

Additive rollout, no data migration:

1. Land pure modules first (RED→GREEN): `manifest.ts`, `scope.ts`, `placement.ts`, `settings.ts` (pure transforms), `store.ts` (port-backed), `scaffold-source.ts`, then the `lifecycle.ts` engine.
2. Land the five `commands/addon-*.ts` wrappers with command tests.
3. Wire the `addon` sub-command group into `dispatcher.ts` (new injectable deps, `addCommand`). No changes to `index.ts`, `registry.ts`, `config.ts`, or the marketplace client. Optionally refactor `runScaffold` onto `generatePluginTemplate` via `scaffold-source.ts` behind characterization tests.
4. Existing `~/.claude-plugins/` installs are unaffected; the version store adds a new `addon-store/` subtree under that home but does not touch `installed.json` or existing plugin dirs.

**Rollback:** remove the `addon` group registration, the five command modules, and the `addon/` domain. The feature writes under user-targeted `.claude/` dirs, their `.addons/` sidecars, the registered `settings.json` hook entries, and the `addon-store/` subtree — all of which a user can remove via `addon remove` before uninstalling, or by deleting those paths. No impact on marketplace behavior.

## Open Questions

- **Version-store pruning policy.** `DEFAULT_VERSION_RETENTION = 5` is hardcoded. Should retention be configurable (per-addon, via `config`, or a `--keep` flag), and should `prune` be exposed as an explicit command?
- **`settings.local.json` vs `settings.json` default.** This change writes hooks into the shared `settings.json`. Should a `--personal`/`--local-settings` flag (writing `settings.local.json`) be offered, and what is the right default for global vs project scope?
- **Plugin bundle install semantics depth.** We place the plugin bundle dir under `~/.claude/plugins/<name>/` without parsing its inner `.claude-plugin/plugin.json`. Is verbatim placement sufficient, or must we validate/normalize the bundle (and snapshot its inner manifest separately) for correct Claude Code activation?
