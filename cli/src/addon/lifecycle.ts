/**
 * Add-on lifecycle engine — add / update / remove / list / rollback.
 *
 * Decision 4 (design.md): Centralized atomicity — stage-to-temp + rename-swap + restore-on-failure.
 * Decision 5 (design.md): Version store integration — snapshot on update / force-overwrite.
 * Decision 6 (design.md): Hook settings.json merge is the LAST write on add; first revert on fail.
 *
 * All I/O is delegated through injected ports (LifecycleFsPort + SettingsFsPort + VersionStore)
 * so the engine is fully testable without touching real disk.
 *
 * No `any`. All patterns immutable.
 */

import * as path from 'node:path';
import * as nodeFsPromises from 'node:fs/promises';

import type { AddonManifest, AddonScope, AddonType, HookRegistration } from './manifest.js';
import { validateAddonManifest } from './manifest.js';
import { resolveScopeRoot } from './scope.js';
import { resolvePlacement } from './placement.js';
import type { SettingsFsPort } from './settings.js';
import { mergeHookEntry, removeHookEntry, readSettings, writeSettingsAtomic } from './settings.js';
import type { VersionStore } from './store.js';

// ---------------------------------------------------------------------------
// Port interface
// ---------------------------------------------------------------------------

export interface LifecycleFsPort {
  mkdir(p: string): Promise<void>;
  writeFile(p: string, content: string | Buffer): Promise<void>;
  readFile(p: string): Promise<string>;
  readFileBuffer(p: string): Promise<Buffer>;
  copyFile(src: string, dest: string): Promise<void>;
  copyDir(src: string, dest: string): Promise<void>;
  rename(src: string, dest: string): Promise<void>;
  rm(p: string, options?: { recursive: boolean; force: boolean }): Promise<void>;
  exists(p: string): Promise<boolean>;
  readdir(p: string): Promise<string[]>;
  stat(p: string): Promise<{ isDirectory: boolean }>;
}

// ---------------------------------------------------------------------------
// Real FS implementation
// ---------------------------------------------------------------------------

export const realLifecycleFsPort: LifecycleFsPort = {
  mkdir: async (p) => {
    await nodeFsPromises.mkdir(p, { recursive: true });
  },
  writeFile: async (p, content) => {
    if (Buffer.isBuffer(content)) {
      await nodeFsPromises.writeFile(p, content);
    } else {
      await nodeFsPromises.writeFile(p, content, 'utf-8');
    }
  },
  readFile: (p) => nodeFsPromises.readFile(p, 'utf-8'),
  readFileBuffer: (p) => nodeFsPromises.readFile(p),
  copyFile: (src, dest) => nodeFsPromises.copyFile(src, dest),
  copyDir: async (src, dest) => {
    await nodeFsPromises.cp(src, dest, { recursive: true });
  },
  rename: (src, dest) => nodeFsPromises.rename(src, dest),
  rm: (p, options) => nodeFsPromises.rm(p, options ?? { recursive: true, force: true }),
  exists: async (p) => {
    try {
      await nodeFsPromises.stat(p);
      return true;
    } catch {
      return false;
    }
  },
  readdir: (p) => nodeFsPromises.readdir(p),
  stat: async (p) => {
    const st = await nodeFsPromises.stat(p);
    return { isDirectory: st.isDirectory() };
  },
};

// ---------------------------------------------------------------------------
// Injected deps bundle
// ---------------------------------------------------------------------------

export interface LifecycleDeps {
  fs: LifecycleFsPort;
  settingsPort: SettingsFsPort;
  store: VersionStore;
  cwd: string;
  homeDir: string;
}

// ---------------------------------------------------------------------------
// Public result types
// ---------------------------------------------------------------------------

export interface LifecycleResult {
  readonly success: boolean;
  readonly message: string;
}

export interface AddonListing {
  readonly type: AddonType;
  readonly name: string;
  readonly version: string;
  readonly storedVersions: readonly string[];
  readonly scope: AddonScope;
}

// ---------------------------------------------------------------------------
// Sidecar schema — the JSON written to <scopeRoot>/.addons/<type>/<name>.json
// ---------------------------------------------------------------------------

export interface SidecarData {
  readonly manifest: AddonManifest;
  /** Absolute paths of all placed files. */
  readonly placedFiles: string[];
  /** Present only for hook add-ons. */
  readonly settingsEntry?: HookRegistration;
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

const ADDON_TYPES: readonly AddonType[] = ['agent', 'skill', 'hook', 'plugin'];

/** Generate a short random ID for temp dirs (no crypto dep, just entropy via Date + random). */
function randId(): string {
  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

/**
 * Read and parse an addon.json manifest from a source directory.
 * Returns validated AddonManifest or throws.
 */
async function readManifest(
  sourceDir: string,
  fs: LifecycleFsPort,
): Promise<{ manifest: AddonManifest; raw: string }> {
  const manifestPath = path.join(sourceDir, 'addon.json');
  const exists = await fs.exists(manifestPath);
  if (!exists) {
    throw new Error(
      `addon.json not found in source directory "${sourceDir}". ` +
        'Ensure the directory contains a valid addon.json manifest.',
    );
  }
  const raw = await fs.readFile(manifestPath);
  let parsed: unknown;
  try {
    parsed = JSON.parse(raw) as unknown;
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    throw new Error(`addon.json in "${sourceDir}" contains malformed JSON: ${msg}`);
  }
  const result = validateAddonManifest(parsed);
  if (!result.valid) {
    throw new Error(
      `addon.json in "${sourceDir}" failed validation:\n  ${result.errors.join('\n  ')}`,
    );
  }
  return { manifest: parsed as AddonManifest, raw };
}

/**
 * Read the sidecar JSON for an installed add-on.
 * Returns undefined when the sidecar does not exist.
 */
async function readSidecar(
  sidecarPath: string,
  fs: LifecycleFsPort,
): Promise<SidecarData | undefined> {
  const exists = await fs.exists(sidecarPath);
  if (!exists) return undefined;
  const raw = await fs.readFile(sidecarPath);
  return JSON.parse(raw) as SidecarData;
}

/**
 * Write a sidecar atomically: temp file + rename into place.
 * The temp file is a sibling (same dir) to stay on the same filesystem.
 */
async function writeSidecarAtomic(
  sidecarPath: string,
  data: SidecarData,
  fs: LifecycleFsPort,
): Promise<void> {
  const sidecarDir = path.dirname(sidecarPath);
  await fs.mkdir(sidecarDir);
  const tmpPath = `${sidecarPath}.tmp-${randId()}`;
  await fs.writeFile(tmpPath, JSON.stringify(data, null, 2));
  await fs.rename(tmpPath, sidecarPath);
}

/**
 * Stage source files into a temp directory.
 * Returns the staged temp dir path and a list of staged dest paths mirroring liveTargets.
 */
async function stageFiles(args: {
  sourceDir: string;
  manifest: AddonManifest;
  liveTargets: readonly { fromRel: string; toAbs: string }[];
  tmpDir: string;
  fs: LifecycleFsPort;
}): Promise<{ stagedPaths: string[] }> {
  const { sourceDir, liveTargets, tmpDir, fs } = args;
  await fs.mkdir(tmpDir);

  const stagedPaths: string[] = [];

  for (const { fromRel, toAbs } of liveTargets) {
    const srcPath = path.join(sourceDir, fromRel);
    // Compute staged path: same relative structure from the tmp dir
    // We use the same basename as the live target under tmpDir
    const relToLive = toAbs;
    const staged = path.join(tmpDir, 'files', relToLive);
    await fs.mkdir(path.dirname(staged));
    await fs.copyFile(srcPath, staged);
    stagedPaths.push(staged);
  }

  return { stagedPaths };
}

/**
 * Atomically place files from staging into their live targets.
 * Uses per-file copy + rename for atomic in-place replacement.
 */
async function commitStagedFiles(args: {
  stagedPaths: string[];
  liveTargets: readonly { fromRel: string; toAbs: string }[];
  tmpDir: string;
  fs: LifecycleFsPort;
}): Promise<void> {
  const { stagedPaths, liveTargets, tmpDir, fs } = args;

  for (let i = 0; i < liveTargets.length; i++) {
    const liveTarget = liveTargets[i];
    const staged = stagedPaths[i];
    if (!liveTarget || staged === undefined) continue;
    await fs.mkdir(path.dirname(liveTarget.toAbs));
    const atomicTmp = `${liveTarget.toAbs}.tmp-${randId()}`;
    await fs.copyFile(staged, atomicTmp);
    await fs.rename(atomicTmp, liveTarget.toAbs);
  }
  // Clean up tmp staging dir
  await fs.rm(tmpDir, { recursive: true, force: true });
}

/**
 * Roll back placed live files to the contents stored in a version snapshot.
 * Used when an update or rollback needs to restore prior state.
 */
async function restoreLiveFiles(args: {
  placedFiles: string[];
  storeVersionPath: string;
  fileRels: string[];
  fs: LifecycleFsPort;
}): Promise<void> {
  const { placedFiles, storeVersionPath, fileRels, fs } = args;
  for (let i = 0; i < placedFiles.length; i++) {
    const liveAbs = placedFiles[i];
    const rel = fileRels[i];
    if (!liveAbs || rel === undefined) continue;
    const storedSrc = path.join(storeVersionPath, 'files', rel);
    const exists = await fs.exists(storedSrc);
    if (!exists) continue;
    await fs.mkdir(path.dirname(liveAbs));
    await fs.copyFile(storedSrc, liveAbs);
  }
}

/**
 * Check if a directory is effectively empty (no files, considering only
 * the immediate contents, not recursive).
 */
async function isEffectivelyEmpty(
  dirPath: string,
  managedFiles: readonly string[],
  fs: LifecycleFsPort,
): Promise<boolean> {
  const dirExists = await fs.exists(dirPath);
  if (!dirExists) return true;

  let entries: string[];
  try {
    entries = await fs.readdir(dirPath);
  } catch {
    return true;
  }

  // Recursively check — any remaining file that's not in managedFiles means not empty
  const managedSet = new Set(managedFiles);
  for (const entry of entries) {
    const entryPath = path.join(dirPath, entry);
    const entryExists = await fs.exists(entryPath);
    if (!entryExists) continue;
    let isDir = false;
    try {
      const st = await fs.stat(entryPath);
      isDir = st.isDirectory;
    } catch {
      isDir = false;
    }
    if (isDir) {
      const subEmpty = await isEffectivelyEmpty(entryPath, managedFiles, fs);
      if (!subEmpty) return false;
    } else {
      if (!managedSet.has(entryPath)) return false;
    }
  }
  return true;
}

// ---------------------------------------------------------------------------
// add
// ---------------------------------------------------------------------------

/**
 * Install an add-on from a source directory.
 *
 * Flow:
 * 1. Read + validate addon.json from sourceDir.
 * 2. Defense-in-depth: reject plugin+local.
 * 3. Compute Placement from manifest + scope.
 * 4. If already installed and !force → error suggesting --force.
 * 5. If already installed and force → snapshot existing version.
 * 6. Stage all placed files into a temp dir inside scopeRoot.
 * 7. Atomically commit staged files to live targets.
 * 8. Write sidecar.
 * 9. For hooks: merge settings.json entry LAST.
 * 10. On any failure: restore/clean up + return error.
 */
export async function add(
  args: { sourceDir: string; scope: AddonScope; force: boolean },
  deps: LifecycleDeps,
): Promise<LifecycleResult> {
  const { sourceDir, scope, force } = args;
  const { fs, settingsPort, store, cwd, homeDir } = deps;

  // ── 1. Read + validate manifest ──────────────────────────────────────────
  let manifest: AddonManifest;
  try {
    const result = await readManifest(sourceDir, fs);
    manifest = result.manifest;
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return { success: false, message: msg };
  }

  // ── 2. Defense-in-depth: plugin is global-only ───────────────────────────
  if (manifest.type === 'plugin' && scope === 'local') {
    return {
      success: false,
      message:
        'plugin add-ons are global-only and cannot be installed to local scope. ' +
        'Use --scope global instead.',
    };
  }

  // ── 3. Compute placement ──────────────────────────────────────────────────
  const scopeRoot = resolveScopeRoot(scope, { cwd, homeDir });
  const placement = resolvePlacement(manifest, { scopeRoot, homeDir });
  const { liveTargets, sidecarPath } = placement;

  // ── 4. Check for existing installation ───────────────────────────────────
  const existingSidecar = await readSidecar(sidecarPath, fs);
  if (existingSidecar !== undefined && !force) {
    return {
      success: false,
      message:
        `${manifest.type} "${manifest.name}" is already installed in ${scope} scope. ` +
        'Use --force to overwrite the existing installation.',
    };
  }

  // ── 5. Snapshot existing version before overwrite (force path) ───────────
  if (existingSidecar !== undefined && force) {
    const existingManifest = existingSidecar.manifest;
    const sourceFiles = existingSidecar.placedFiles.map((absPath, i) => ({
      rel: existingManifest.files[i] ?? path.basename(absPath),
      absSource: absPath,
    }));
    try {
      await store.snapshot({
        scope,
        type: existingManifest.type,
        name: existingManifest.name,
        version: existingManifest.version,
        sourceFiles,
        manifest: existingManifest,
        ...(existingSidecar.settingsEntry !== undefined
          ? { settingsEntry: existingSidecar.settingsEntry }
          : {}),
      });
    } catch {
      // Non-fatal: log but continue (snapshot failure should not block overwrite)
    }
  }

  // ── 6 + 7. Stage + commit placed files atomically ─────────────────────────
  const tmpDir = path.join(scopeRoot, '.addons', `.tmp-${randId()}`);
  let stagedPaths: string[];
  try {
    const staged = await stageFiles({ sourceDir, manifest, liveTargets, tmpDir, fs });
    stagedPaths = staged.stagedPaths;
  } catch (err) {
    await fs.rm(tmpDir, { recursive: true, force: true });
    const msg = err instanceof Error ? err.message : String(err);
    return { success: false, message: `Failed to stage add-on files: ${msg}` };
  }

  try {
    await commitStagedFiles({ stagedPaths, liveTargets, tmpDir, fs });
  } catch (err) {
    await fs.rm(tmpDir, { recursive: true, force: true });
    const msg = err instanceof Error ? err.message : String(err);
    return { success: false, message: `Failed to install add-on files: ${msg}` };
  }

  // ── 8. Write sidecar ──────────────────────────────────────────────────────
  const placedFileAbsPaths = liveTargets.map((t) => t.toAbs);
  const sidecarData: SidecarData = {
    manifest,
    placedFiles: placedFileAbsPaths,
    ...(placement.settingsEntry !== undefined
      ? { settingsEntry: placement.settingsEntry }
      : {}),
  };

  try {
    await writeSidecarAtomic(sidecarPath, sidecarData, fs);
  } catch (err) {
    // Attempt cleanup of placed files
    for (const { toAbs } of liveTargets) {
      await fs.rm(toAbs, { recursive: true, force: true });
    }
    const msg = err instanceof Error ? err.message : String(err);
    return { success: false, message: `Failed to write sidecar: ${msg}` };
  }

  // ── 9. Hook: merge settings.json entry LAST ───────────────────────────────
  if (placement.settingsEntry !== undefined) {
    const settingsPath = path.join(scopeRoot, 'settings.json');
    let settingsBeforeMerge = undefined;
    try {
      settingsBeforeMerge = await readSettings(settingsPath, settingsPort);
      const merged = mergeHookEntry(settingsBeforeMerge, placement.settingsEntry);
      await writeSettingsAtomic(settingsPath, merged, settingsPort);
    } catch (err) {
      // Revert: remove placed files and sidecar
      for (const { toAbs } of liveTargets) {
        await fs.rm(toAbs, { recursive: true, force: true });
      }
      await fs.rm(sidecarPath, { recursive: true, force: true });
      const msg = err instanceof Error ? err.message : String(err);
      return { success: false, message: `Failed to merge hook settings entry: ${msg}` };
    }
  }

  return {
    success: true,
    message: `Installed ${manifest.type} "${manifest.name}" to ${scope} scope (v${manifest.version}).`,
  };
}

// ---------------------------------------------------------------------------
// update
// ---------------------------------------------------------------------------

/**
 * Update an installed add-on to a newer version from a source directory.
 *
 * - NO-OP if new version == installed version.
 * - On failure: restore prior version from the backup (via version store snapshot).
 */
export async function update(
  args: { sourceDir: string; scope: AddonScope },
  deps: LifecycleDeps,
): Promise<LifecycleResult> {
  const { sourceDir, scope } = args;
  const { fs, settingsPort, store, cwd, homeDir } = deps;

  // ── Read new manifest ──────────────────────────────────────────────────────
  let newManifest: AddonManifest;
  try {
    const result = await readManifest(sourceDir, fs);
    newManifest = result.manifest;
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return { success: false, message: msg };
  }

  // ── Resolve scope + placement ─────────────────────────────────────────────
  const scopeRoot = resolveScopeRoot(scope, { cwd, homeDir });
  const placement = resolvePlacement(newManifest, { scopeRoot, homeDir });
  const { liveTargets, sidecarPath } = placement;

  // ── Check existing installation ───────────────────────────────────────────
  const existingSidecar = await readSidecar(sidecarPath, fs);
  if (existingSidecar === undefined) {
    return {
      success: false,
      message:
        `${newManifest.type} "${newManifest.name}" is not installed in ${scope} scope. ` +
        'Use "addon add" to install it first.',
    };
  }

  const installedVersion = existingSidecar.manifest.version;

  // ── No-op when same version ───────────────────────────────────────────────
  if (installedVersion === newManifest.version) {
    return {
      success: true,
      message: `${newManifest.type} "${newManifest.name}" is already at version ${installedVersion}. Nothing to update.`,
    };
  }

  // ── Snapshot current version into store ──────────────────────────────────
  const existingManifest = existingSidecar.manifest;
  const sourceFilesForSnapshot = existingSidecar.placedFiles.map((absPath, i) => ({
    rel: existingManifest.files[i] ?? path.basename(absPath),
    absSource: absPath,
  }));
  let snapshotPath: string | undefined;
  try {
    snapshotPath = await store.snapshot({
      scope,
      type: existingManifest.type,
      name: existingManifest.name,
      version: existingManifest.version,
      sourceFiles: sourceFilesForSnapshot,
      manifest: existingManifest,
      ...(existingSidecar.settingsEntry !== undefined
        ? { settingsEntry: existingSidecar.settingsEntry }
        : {}),
    });
  } catch {
    // Non-fatal
    snapshotPath = undefined;
  }

  // ── Stage new files ───────────────────────────────────────────────────────
  const tmpDir = path.join(scopeRoot, '.addons', `.tmp-${randId()}`);
  let stagedPaths: string[];
  try {
    const staged = await stageFiles({ sourceDir, manifest: newManifest, liveTargets, tmpDir, fs });
    stagedPaths = staged.stagedPaths;
  } catch (err) {
    await fs.rm(tmpDir, { recursive: true, force: true });
    const msg = err instanceof Error ? err.message : String(err);
    // Restore from snapshot if available
    if (snapshotPath !== undefined) {
      await restoreLiveFiles({
        placedFiles: existingSidecar.placedFiles,
        storeVersionPath: snapshotPath,
        fileRels: existingManifest.files,
        fs,
      });
    }
    return {
      success: false,
      message: `Update failed (stage): ${msg}. Previous version ${installedVersion} retained.`,
    };
  }

  try {
    await commitStagedFiles({ stagedPaths, liveTargets, tmpDir, fs });
  } catch (err) {
    await fs.rm(tmpDir, { recursive: true, force: true });
    const msg = err instanceof Error ? err.message : String(err);
    // Restore from snapshot
    if (snapshotPath !== undefined) {
      await restoreLiveFiles({
        placedFiles: existingSidecar.placedFiles,
        storeVersionPath: snapshotPath,
        fileRels: existingManifest.files,
        fs,
      });
    }
    return {
      success: false,
      message: `Update failed (commit): ${msg}. Previous version ${installedVersion} retained.`,
    };
  }

  // ── Write updated sidecar ─────────────────────────────────────────────────
  const updatedSidecar: SidecarData = {
    manifest: newManifest,
    placedFiles: liveTargets.map((t) => t.toAbs),
    ...(placement.settingsEntry !== undefined
      ? { settingsEntry: placement.settingsEntry }
      : {}),
  };
  try {
    await writeSidecarAtomic(sidecarPath, updatedSidecar, fs);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return { success: false, message: `Update failed (sidecar): ${msg}` };
  }

  // ── Update hook settings entry if changed ────────────────────────────────
  if (placement.settingsEntry !== undefined) {
    const settingsPath = path.join(scopeRoot, 'settings.json');
    try {
      // Remove old entry, add new entry (handles command path change)
      const settings = await readSettings(settingsPath, settingsPort);
      let updated = settings;
      if (existingSidecar.settingsEntry !== undefined) {
        updated = removeHookEntry(updated, existingSidecar.settingsEntry);
      }
      updated = mergeHookEntry(updated, placement.settingsEntry);
      await writeSettingsAtomic(settingsPath, updated, settingsPort);
    } catch {
      // Non-fatal: settings update failure
    }
  }

  return {
    success: true,
    message: `Updated ${newManifest.type} "${newManifest.name}" from v${installedVersion} to v${newManifest.version}.`,
  };
}

// ---------------------------------------------------------------------------
// remove
// ---------------------------------------------------------------------------

/**
 * Remove an installed add-on.
 *
 * - Reads the sidecar to determine exactly which files to delete.
 * - Deletes only placedFiles + sidecar; never touches unlisted files.
 * - For hooks: unregisters the settings.json entry.
 * - Prunes now-empty owner dirs (skills/<name>/, plugins/<name>/).
 * - Tolerates already-missing files (idempotent).
 */
export async function remove(
  args: { name: string; type: AddonType; scope: AddonScope },
  deps: LifecycleDeps,
): Promise<LifecycleResult> {
  const { name, type, scope } = args;
  const { fs, settingsPort, cwd, homeDir } = deps;

  const scopeRoot = resolveScopeRoot(scope, { cwd, homeDir });
  const sidecarPath = path.join(scopeRoot, '.addons', type, `${name}.json`);

  const sidecar = await readSidecar(sidecarPath, fs);
  if (sidecar === undefined) {
    return {
      success: false,
      message: `${type} "${name}" is not installed in ${scope} scope.`,
    };
  }

  const { placedFiles, settingsEntry, manifest } = sidecar;

  // ── Delete placed files (tolerate missing) ────────────────────────────────
  for (const absPath of placedFiles) {
    const exists = await fs.exists(absPath);
    if (exists) {
      await fs.rm(absPath, { recursive: false, force: true });
    }
  }

  // ── Unregister hook settings entry ────────────────────────────────────────
  if (settingsEntry !== undefined) {
    const settingsPath = path.join(scopeRoot, 'settings.json');
    try {
      const settings = await readSettings(settingsPath, settingsPort);
      const updated = removeHookEntry(settings, settingsEntry);
      await writeSettingsAtomic(settingsPath, updated, settingsPort);
    } catch {
      // Non-fatal: don't block removal
    }
  }

  // ── Prune empty owner dirs ─────────────────────────────────────────────────
  if (type === 'skill') {
    const ownerDir = path.join(scopeRoot, 'skills', name);
    const empty = await isEffectivelyEmpty(ownerDir, placedFiles, fs);
    if (empty) {
      await fs.rm(ownerDir, { recursive: true, force: true });
    }
  } else if (type === 'plugin') {
    const ownerDir = path.join(homeDir, '.claude', 'plugins', name);
    const empty = await isEffectivelyEmpty(ownerDir, placedFiles, fs);
    if (empty) {
      await fs.rm(ownerDir, { recursive: true, force: true });
    }
  }

  // ── Delete sidecar ────────────────────────────────────────────────────────
  await fs.rm(sidecarPath, { recursive: false, force: true });

  return {
    success: true,
    message: `Removed ${type} "${name}" from ${scope} scope (was v${manifest.version}).`,
  };
}

// ---------------------------------------------------------------------------
// list
// ---------------------------------------------------------------------------

/**
 * List installed add-ons for a given scope.
 *
 * Scans <scopeRoot>/.addons/<type>/<name>.json sidecars.
 * For each, queries the version store for available stored versions.
 */
export async function list(
  args: { scope: AddonScope },
  deps: LifecycleDeps,
): Promise<AddonListing[]> {
  const { scope } = args;
  const { fs, store, cwd, homeDir } = deps;

  const scopeRoot = resolveScopeRoot(scope, { cwd, homeDir });
  const addonsDir = path.join(scopeRoot, '.addons');

  const addonsDirExists = await fs.exists(addonsDir);
  if (!addonsDirExists) {
    return [];
  }

  const listings: AddonListing[] = [];

  for (const addonType of ADDON_TYPES) {
    const typeDir = path.join(addonsDir, addonType);
    const typeDirExists = await fs.exists(typeDir);
    if (!typeDirExists) continue;

    let entries: string[];
    try {
      entries = await fs.readdir(typeDir);
    } catch {
      continue;
    }

    for (const entry of entries) {
      if (!entry.endsWith('.json')) continue;
      const sidecarPath = path.join(typeDir, entry);
      let sidecar: SidecarData | undefined;
      try {
        sidecar = await readSidecar(sidecarPath, fs);
      } catch {
        continue;
      }
      if (sidecar === undefined) continue;

      const addonName = entry.slice(0, -5); // strip .json
      let storedVersions: string[] = [];
      try {
        storedVersions = await store.list({ scope, type: addonType, name: addonName });
      } catch {
        storedVersions = [];
      }

      listings.push({
        type: addonType,
        name: addonName,
        version: sidecar.manifest.version,
        storedVersions,
        scope,
      });
    }
  }

  return listings;
}

// ---------------------------------------------------------------------------
// rollback
// ---------------------------------------------------------------------------

/**
 * Roll an installed add-on back to a stored version.
 *
 * Flow:
 * 1. Read current sidecar.
 * 2. Resolve target stored version (--to exact, else latestPrior).
 * 3. Snapshot the CURRENT live version into the store first (reversible).
 * 4. Atomically swap in the stored version's files.
 * 5. Update sidecar to the restored version.
 * 6. For hooks: unregister current settings entry, re-merge stored entry.
 * On failure: restore current live files from snapshot taken in step 3.
 */
export async function rollback(
  args: { name: string; type: AddonType; scope: AddonScope; to?: string },
  deps: LifecycleDeps,
): Promise<LifecycleResult> {
  const { name, type, scope, to } = args;
  const { fs, settingsPort, store, cwd, homeDir } = deps;

  const scopeRoot = resolveScopeRoot(scope, { cwd, homeDir });
  const sidecarPath = path.join(scopeRoot, '.addons', type, `${name}.json`);

  // ── 1. Read current sidecar ───────────────────────────────────────────────
  const currentSidecar = await readSidecar(sidecarPath, fs);
  if (currentSidecar === undefined) {
    return {
      success: false,
      message: `${type} "${name}" is not installed in ${scope} scope.`,
    };
  }

  const currentManifest = currentSidecar.manifest;

  // ── 2. Resolve target version ─────────────────────────────────────────────
  let targetVersion: string;
  if (to !== undefined) {
    targetVersion = to;
  } else {
    const prior = await store.latestPrior({
      scope,
      type,
      name,
      currentVersion: currentManifest.version,
    });
    if (prior === undefined) {
      return {
        success: false,
        message: `No stored versions found for ${type} "${name}" in ${scope} scope to roll back to.`,
      };
    }
    targetVersion = prior;
  }

  // ── Read the stored version ───────────────────────────────────────────────
  let storedVersion;
  try {
    storedVersion = await store.read({ scope, type, name, version: targetVersion });
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return {
      success: false,
      message: `Cannot roll back to version ${targetVersion}: ${msg}. Live install unchanged.`,
    };
  }

  // ── 3. Snapshot CURRENT live version (reversible rollback) ───────────────
  const sourceFilesForSnapshot = currentSidecar.placedFiles.map((absPath, i) => ({
    rel: currentManifest.files[i] ?? path.basename(absPath),
    absSource: absPath,
  }));
  try {
    await store.snapshot({
      scope,
      type,
      name,
      version: currentManifest.version,
      sourceFiles: sourceFilesForSnapshot,
      manifest: currentManifest,
      ...(currentSidecar.settingsEntry !== undefined
        ? { settingsEntry: currentSidecar.settingsEntry }
        : {}),
    });
  } catch {
    // Non-fatal
  }

  // ── 4. Restore stored version files to live targets ──────────────────────
  const storedManifest = storedVersion.manifest;
  const restoredPlacement = resolvePlacement(storedManifest, { scopeRoot, homeDir });
  const { liveTargets: restoredLiveTargets, sidecarPath: restoredSidecarPath } = restoredPlacement;

  // Build a mapping of rel → stored abs path
  for (let i = 0; i < restoredLiveTargets.length; i++) {
    const target = restoredLiveTargets[i];
    const rel = storedManifest.files[i];
    if (!target || rel === undefined) continue;
    const storedFilePath = path.join(storedVersion.path, 'files', rel);
    const storedExists = await fs.exists(storedFilePath);
    if (!storedExists) continue;

    const atomicTmp = `${target.toAbs}.tmp-${randId()}`;
    try {
      await fs.mkdir(path.dirname(target.toAbs));
      await fs.copyFile(storedFilePath, atomicTmp);
      await fs.rename(atomicTmp, target.toAbs);
    } catch (err) {
      // Best-effort cleanup
      await fs.rm(atomicTmp, { recursive: false, force: true });
      const msg = err instanceof Error ? err.message : String(err);
      return {
        success: false,
        message: `Rollback failed while restoring file "${rel}": ${msg}. Live install may be partially restored.`,
      };
    }
  }

  // ── 5. Update sidecar ─────────────────────────────────────────────────────
  const restoredSidecar: SidecarData = {
    manifest: storedManifest,
    placedFiles: restoredLiveTargets.map((t) => t.toAbs),
    ...(restoredPlacement.settingsEntry !== undefined
      ? { settingsEntry: restoredPlacement.settingsEntry }
      : {}),
  };
  try {
    await writeSidecarAtomic(restoredSidecarPath, restoredSidecar, fs);
  } catch (err) {
    const msg = err instanceof Error ? err.message : String(err);
    return { success: false, message: `Rollback failed (sidecar): ${msg}` };
  }

  // ── 6. Hook: update settings entry ────────────────────────────────────────
  if (type === 'hook') {
    const settingsPath = path.join(scopeRoot, 'settings.json');
    try {
      const settings = await readSettings(settingsPath, settingsPort);
      let updated = settings;
      if (currentSidecar.settingsEntry !== undefined) {
        updated = removeHookEntry(updated, currentSidecar.settingsEntry);
      }
      if (restoredPlacement.settingsEntry !== undefined) {
        updated = mergeHookEntry(updated, restoredPlacement.settingsEntry);
      } else if (storedVersion.settingsEntry !== undefined) {
        updated = mergeHookEntry(updated, storedVersion.settingsEntry);
      }
      await writeSettingsAtomic(settingsPath, updated, settingsPort);
    } catch {
      // Non-fatal: settings update failure
    }
  }

  return {
    success: true,
    message: `Rolled back ${type} "${name}" from v${currentManifest.version} to v${targetVersion} in ${scope} scope.`,
  };
}
