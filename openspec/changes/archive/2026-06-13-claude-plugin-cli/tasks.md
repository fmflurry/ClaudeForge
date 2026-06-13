# Tasks: Claude Plugin CLI Add-on Lifecycle

## 1. Add-on manifest module

- [x] 1.1 Write unit tests for `validateAddonManifest` in `cli/src/__tests__/addon-manifest.test.ts` covering: valid manifest with all required identity fields is accepted; unknown `type` value is rejected with clear error naming the type and listing valid types; manifest missing required field (`name`, `version`, `type`, or `files`) is rejected; manifest with empty `files` array is rejected; manifest declaring absolute paths in `files` is rejected; manifest declaring `..` segments in `files` is rejected; manifest with `\0` in `files` is rejected; manifest with non-semver `version` is rejected; manifest with unsupported `supportedScopes` value is rejected (RED).
- [x] 1.2 Implement `cli/src/addon/manifest.ts` exporting: `AddonType` (union of `'hook' | 'plugin' | 'skill' | 'agent'`), `AddonScope` (union of `'local' | 'global'`), `AddonManifest` (typed interface with `name`, `version`, `type`, `supportedScopes`, `files`), and pure function `validateAddonManifest(input: unknown): AddonValidationResult` returning `{ valid: boolean; errors: string[]; warnings: string[] }`. Reuse `SEMVER_RE` from existing `validate.ts`; reuse path-traversal guard logic from `install.ts` (lines 144–158) to validate `files` entries reject absolute paths, `..` segments, and `\0` characters. No `any` types (GREEN).

## 2. Scope resolution module

- [x] 2.1 Write unit tests for `cli/src/__tests__/addon-scope.test.ts` covering: `resolveScopeRoot` for `'local'` scope resolves to `./.claude/` relative to provided `cwd`; `resolveScopeRoot` for `'global'` scope resolves to `~/.claude/` using provided `homeDir` (not the marketplace `resolveHome`); `typeSubdir` returns correct subdir for each type (`'skills/'` for `'skill'`, `'agents/'` for `'agent'`, `'hooks/'` for `'hook'`, `'plugins/'` for `'plugin'`); placement path is deterministic (`<scopeRoot>/<typeSubdir>/<name>/`); scope isolation (local and global never touch each other); ambiguous/missing scope handling (RED).
- [x] 2.2 Implement `cli/src/addon/scope.ts` exporting: pure functions `resolveScopeRoot(scope: AddonScope, deps: { cwd: string; homeDir: string }): string` resolving `'local'` to `path.resolve(cwd, '.claude')` and `'global'` to `path.resolve(homeDir, '.claude')`; `typeSubdir(type: AddonType): string` returning type-specific subdirectory name; helper to compute placement path as `<scopeRoot>/<typeSubdir>/<name>/`. No file I/O (GREEN).

## 3. Lifecycle engine

- [x] 3.1 Define `LifecycleFsPort` interface in `cli/src/addon/lifecycle.ts` with methods: `mkdir(path: string): Promise<void>`, `writeFile(path: string, content: string | Buffer): Promise<void>`, `readFile(path: string): Promise<string>`, `readFileBuffer(path: string): Promise<Buffer>`, `copyDir(src: string, dest: string): Promise<void>`, `rename(src: string, dest: string): Promise<void>`, `rm(path: string, options?: { recursive: boolean; force: boolean }): Promise<void>`, `exists(path: string): Promise<boolean>`, `readdir(path: string): Promise<string[]>`. Create a real implementation using `fs/promises` and a test fake using `vi.fn()` for injection.
- [x] 3.2 Write unit tests in `cli/src/__tests__/addon-lifecycle.test.ts` covering: `add` writes all declared `files` to resolved placement path; `add` with existing installation without `force` flag is rejected with error suggesting `--force`; `add` with `--force` flag overwrites existing files; `update` to newer version succeeds and writes new files; `update` when installed version equals target version is a no-op with explanatory message; `update` failure (e.g., mid-write) leaves prior version intact and reports backup location (atomicity); `remove` deletes only manifest-declared `files` entries plus the manifest itself; `remove` leaves non-owned files in the directory untouched; `remove` for non-installed add-on errors clearly; `remove` prunes the add-on directory only if empty after deletion; re-running `add` after interruption detects partial state and either completes cleanly or errors without scope corruption (RED).
- [x] 3.3 Implement `cli/src/addon/lifecycle.ts` core functions: `add(sourceDir: string, scope: AddonScope, name: string, options: { force: boolean; cwd: string; homeDir: string }, port: LifecycleFsPort): Promise<{ success: boolean; message: string }>` staging all declared `files` into sibling temp dir `<addonDir>.tmp-<randomId>`, backing up existing installation to `backups/<scope>__<type>__<name>_<version>` (via `ensureBackupsDir` from `registry.ts` pattern), atomically swapping via `rename(addonDir → addonDir.bak)`, `rename(tmp → addonDir)`, cleaning up `.bak`; `update(...)` similar but detecting when installed version equals target version (no-op); `remove(name: string, scope: AddonScope, ...)` reading installed `addon.json`, deleting only `files[]` entries plus manifest, pruning empty directory, leaving unowned files intact. All I/O via `LifecycleFsPort` (GREEN).

## 4. Command wrappers

- [x] 4.1 Write unit tests for `cli/src/__tests__/command-addon-add.test.ts` covering `runAddonAdd`: valid add succeeds and returns `CommandResult` with exit code 0 and message naming type/name/scope; missing `--scope` flag errors; invalid scope value errors; missing `--force` on existing add-on errors; `--force` overwrites. All via injected `DispatcherDeps` and temp directories.
- [x] 4.2 Implement `cli/src/commands/addon-add.ts` exporting `runAddonAdd(args: ParsedAddonAddArgs, deps: DispatcherDeps): Promise<CommandResult>` parsing `<source>` path, reading `addon.json`, validating manifest, resolving scope, calling `lifecycle.add(...)`, returning `{ exitCode: 0, output: "Installed <type> <name> to <scope> scope" }` on success or appropriate error result on failure.
- [x] 4.3 Write unit tests for `cli/src/__tests__/command-addon-list.test.ts` covering `runAddonList`: list local scope shows type/name/version for all installed add-ons; list global scope shows type/name/version for all installed add-ons; list with no `--scope` flag lists both scopes with clear labeling; empty scope reports "No add-ons found in <scope> scope".
- [x] 4.4 Implement `cli/src/commands/addon-list.ts` exporting `runAddonList(args: ParsedAddonListArgs, deps: DispatcherDeps): Promise<CommandResult>` optionally filtering by scope, scanning `<scopeRoot>/<typeSubdir>/` directories, reading each `addon.json`, formatting output with type/name/version columns, returning `{ exitCode: 0, output: ... }`.
- [x] 4.5 Write unit tests for `cli/src/__tests__/command-addon-update.test.ts` covering `runAddonUpdate`: update to newer version succeeds; update to same version is reported as no-op; update failure leaves prior version intact; missing `--scope` flag errors.
- [x] 4.6 Implement `cli/src/commands/addon-update.ts` exporting `runAddonUpdate(args: ParsedAddonUpdateArgs, deps: DispatcherDeps): Promise<CommandResult>` parsing `<source>`, validating new manifest, detecting installed version, calling `lifecycle.update(...)`, returning `{ exitCode: 0, output: "Updated <name> from <old> to <new>" }` on success.
- [x] 4.7 Write unit tests for `cli/src/__tests__/command-addon-remove.test.ts` covering `runAddonRemove`: remove installed add-on succeeds; `--type` and `--scope` required flags are enforced; removing non-installed add-on errors clearly; error messages do not leak internal paths.
- [x] 4.8 Implement `cli/src/commands/addon-remove.ts` exporting `runAddonRemove(args: ParsedAddonRemoveArgs, deps: DispatcherDeps): Promise<CommandResult>` requiring `--type` and `--scope`, reading installed `addon.json` at resolved placement, calling `lifecycle.remove(...)`, returning `{ exitCode: 0, output: "Removed <name> from <scope> scope" }` on success.

## 5. Dispatcher wiring

- [x] 5.1 Extend `cli/src/dispatcher.ts` by creating a new `Command('addon')` group with four sub-commands (`add`, `list`, `update`, `remove`), mirroring the existing `config` sub-command group structure (lines 255–277). Add four new optional fields to `DispatcherDeps` type: `runAddonAdd?`, `runAddonList?`, `runAddonUpdate?`, `runAddonRemove?`, each defaulting to the real `run*` function if not provided. Resolve `cwd` from `process.cwd()` and `homeDir` from `os.homedir()`, passing both to all injected command deps. Register the `addon` command group via `program.addCommand(addonCmd)`. Do not modify existing marketplace verbs (`install`, `remove`, `update`, `list`, `search`, `publish`, `scaffold`, `validate`), `index.ts`, or `createProgram` signature.
- [x] 5.2 Write unit tests in `cli/src/__tests__/dispatcher.test.ts` (or extend existing) covering: `program.parse(['addon', 'add', ...])` routes to injected `runAddonAdd` with correct `DispatcherDeps`; similar for `list`, `update`, `remove`; missing required `--scope` flag is caught by command validation; default injection uses real run functions.

## 6. Verification & documentation

- [x] 6.1 Run `npm run build:cli` (tsc type-check) — no errors.
- [x] 6.2 Run `npm run lint:cli` — no errors.
- [x] 6.3 Run `npm run test:cli` — all tests pass, including new `addon-*` and `command-addon-*` tests; confirm ≥80% coverage on `cli/src/addon/` modules.
- [x] 6.4 Update `cli/README.md` with new `addon` command group documentation, including usage examples for `addon add|list|update|remove`, description of `addon.json` manifest format (schema, field explanations, example), supported scopes (`local`, `global`), and notes on `--force` flag semantics.

## 7. Per-type placement strategy

- [x] 7.1 Implement `cli/src/addon/placement.ts`: `resolvePlacement(manifest, scopeRoot)` returning `Placement { type, liveTargets, ownerPath, settingsEntry? }` with per-type mapping (agent→agents/<name>.md file; skill→skills/<name>/ dir tree; hook→hooks/ scripts + settingsEntry; plugin→~/.claude/plugins/<name>/ dir).
- [x] 7.2 Sidecar index design under `<scopeRoot>/.addons/<type>/<name>.json` (placedFiles[], settingsEntry).
- [x] 7.3 Tests `cli/src/__tests__/addon-placement.test.ts`: each type maps correctly; agent rejects >1 file; plugin resolves to home plugins dir; no `..`/absolute escapes.

## 8. Manifest: supportedScopes array, "both" sugar, plugin-global-only, per-type files

- [x] 8.1 Update `cli/src/addon/manifest.ts`: store `supportedScopes` as `AddonScope[]`; add `normalizeSupportedScopes()` expanding "both"; reject empty.
- [x] 8.2 Add cross-field validation: plugin must not include local; hook requires `hook` object with `command` referencing a managed file; non-hook must not declare `hook`; agent must declare exactly one file.
- [x] 8.3 Tests `cli/src/__tests__/addon-manifest.test.ts`: array + "both" expansion; plugin-local rejection; hook-entry-required; hook.command-must-match-file; agent-one-file; non-hook-no-hook-entry.

## 9. settings.json hook merge module + tests

- [x] 9.1 Implement `cli/src/addon/settings.ts`: pure `mergeHookEntry()` / `removeHookEntry()` (immutable, idempotent, dedupe by type+command); `readSettings()` (missing/empty→{}, malformed→error); `writeSettingsAtomic()` (temp + rename).
- [x] 9.2 Wire a `SettingsFsPort` (no `any`) consistent with existing port pattern.
- [x] 9.3 Tests `cli/src/__tests__/addon-settings.test.ts`: merge under event/matcher; idempotent re-add; preserve unrelated keys/events; unmerge drops emptied groups/keys; malformed JSON aborts; atomic write to temp+rename.

## 10. Version-store module + tests

- [x] 10.1 Implement `cli/src/addon/store.ts`: `VersionStore` with `snapshot/list/read/latestPrior/prune` rooted at `resolveHome()/addon-store/<scope>/<type>/<name>/<version>/`; reuse backups/temp conventions; `DEFAULT_VERSION_RETENTION = 5` pruned after snapshot.
- [x] 10.2 Persist `addon.json`, `files/`, and `settings-entry.json` (hooks) per stored version.
- [x] 10.3 Tests `cli/src/__tests__/addon-store.test.ts`: snapshot creates versioned dir; list semver-sorted; latestPrior excludes current; prune keeps N; atomic temp+rename.

## 11. Generator-reuse scaffold path + tests

- [x] 11.1 Implement `cli/src/addon/scaffold-source.ts`: `buildAddonScaffold()` calling `generatePluginTemplate`, adapting output to addon shape (synthesize addon.json; remap per-type paths; hook stub; plugin→global-only scopes).
- [x] 11.2 Refactor `cli/src/commands/scaffold.ts` `runScaffold` to delegate to `generatePluginTemplate` via shared helpers (characterization tests first to keep output stable).
- [x] 11.3 Tests `cli/src/__tests__/addon-scaffold-source.test.ts`: each type produces a valid addon.json + canonical files; plugin defaults to ["global"]; no template-body duplication (shared generator invoked).

## 12. Lifecycle engine (atomicity + hooks + store) + tests

- [x] 12.1 Implement `cli/src/addon/lifecycle.ts`: add/update/remove/list/rollback over `LifecycleFsPort`; temp-stage + atomic rename swap; backup/snapshot-on-overwrite; hook settings merge last on add and first revert on failure; remove deletes only placedFiles + unregisters settings entry + prunes empty owner dirs.

## 13. addon commands + dispatcher wiring + tests

- [x] 13.1 Implement `cli/src/commands/addon-add.ts` (install + scaffold paths), `addon-list.ts`, `addon-update.ts`, `addon-remove.ts`, `addon-rollback.ts`, each returning `CommandResult`.
- [x] 13.2 Reject `--scope local --type plugin` before I/O in the command layer.
- [x] 13.3 Wire `addonCmd = new Command('addon')` group + 5 sub-verbs into `cli/src/dispatcher.ts`; add 5 optional injectable `DispatcherDeps` fields defaulting to real `run*`; resolve cwd, `~/.claude` (scope root) and `resolveHome()` (version store) deps; leave `index.ts` unchanged.
- [x] 13.4 Command tests: `command-addon-add.test.ts` (install + scaffold + plugin-local rejection + hook registers settings), `command-addon-list.test.ts` (live + stored versions, both scopes), `command-addon-update.test.ts` (snapshot prior, no-op when current), `command-addon-remove.test.ts` (only placedFiles + unregister + isolation), `command-addon-rollback.test.ts` (default latest prior, --to specific, missing version errors, reversible).

## 14. Rollback command (explicit) + tests

- [x] 14.1 Implement `runAddonRollback(args, deps)` resolving stored version (--to exact else latestPrior), snapshotting current first, then atomic swap + settings re-merge for hooks.
- [x] 14.2 Tests covered under `command-addon-rollback.test.ts` above.
