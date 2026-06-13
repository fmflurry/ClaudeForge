/**
 * Version store module for add-on lifecycle management.
 *
 * Persists versioned snapshots of add-ons under:
 *   <baseStorePath>/<scope>/<type>/<name>/<version>/
 *     addon.json            — manifest at that version
 *     files/…               — verbatim copies of the placed source files
 *     settings-entry.json   — (hooks only) the exact settings.json entry registered
 *
 * All I/O is delegated to an injected StoreFsPort for full testability.
 * No `any`. Immutable patterns throughout.
 */

import * as path from 'node:path';
import * as fs from 'node:fs/promises';
import type { AddonManifest, AddonScope, AddonType, HookRegistration } from './manifest.js';
import { CREDENTIALS_DIR_MODE } from '../auth/credentials-store.js';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

export const DEFAULT_VERSION_RETENTION = 5;

// ---------------------------------------------------------------------------
// Port interface
// ---------------------------------------------------------------------------

export interface StoreFsPort {
  mkdir(filePath: string, opts?: { recursive?: boolean; mode?: number }): Promise<void>;
  writeFile(filePath: string, content: string): Promise<void>;
  readFile(filePath: string): Promise<string>;
  copyFile(src: string, dest: string): Promise<void>;
  rename(src: string, dest: string): Promise<void>;
  rm(filePath: string, opts?: { recursive: boolean; force: boolean }): Promise<void>;
  exists(filePath: string): Promise<boolean>;
  readdir(filePath: string): Promise<string[]>;
}

// ---------------------------------------------------------------------------
// Real fs/promises implementation of StoreFsPort
// ---------------------------------------------------------------------------

export const realStoreFsPort: StoreFsPort = {
  mkdir: async (p, opts) => { await fs.mkdir(p, { recursive: opts?.recursive ?? true, ...(opts?.mode !== undefined ? { mode: opts.mode } : {}) }); },
  writeFile: (p, c) => fs.writeFile(p, c, 'utf-8'),
  readFile: (p) => fs.readFile(p, 'utf-8'),
  copyFile: (src, dest) => fs.copyFile(src, dest),
  rename: (src, dest) => fs.rename(src, dest),
  rm: (p, opts) => fs.rm(p, opts ?? { recursive: true, force: true }),
  exists: async (p) => {
    try {
      await fs.access(p);
      return true;
    } catch {
      return false;
    }
  },
  readdir: (p) => fs.readdir(p),
};

// ---------------------------------------------------------------------------
// Domain types
// ---------------------------------------------------------------------------

export interface StoredVersion {
  readonly manifest: AddonManifest;
  readonly version: string;
  readonly files: string[];
  readonly settingsEntry?: HookRegistration;
  readonly path: string;
}

// ---------------------------------------------------------------------------
// VersionStore interface
// ---------------------------------------------------------------------------

export interface VersionStore {
  /**
   * Snapshot the current add-on version into the store.
   * Writes to a temp dir first, then atomically renames into the final path.
   * Calls prune(keep=DEFAULT_VERSION_RETENTION) after a successful snapshot.
   * Returns the final stored path.
   */
  snapshot(args: {
    scope: AddonScope;
    type: AddonType;
    name: string;
    version: string;
    sourceFiles: readonly { rel: string; absSource: string }[];
    manifest: AddonManifest;
    settingsEntry?: HookRegistration;
  }): Promise<string>;

  /**
   * List stored versions for an add-on, sorted semver ascending.
   */
  list(args: { scope: AddonScope; type: AddonType; name: string }): Promise<string[]>;

  /**
   * Read a specific stored version.
   */
  read(args: {
    scope: AddonScope;
    type: AddonType;
    name: string;
    version: string;
  }): Promise<StoredVersion>;

  /**
   * Returns the highest stored version strictly less than currentVersion,
   * or undefined if none exists.
   */
  latestPrior(args: {
    scope: AddonScope;
    type: AddonType;
    name: string;
    currentVersion: string;
  }): Promise<string | undefined>;

  /**
   * Keep only the newest `keep` versions; remove the rest.
   */
  prune(args: { scope: AddonScope; type: AddonType; name: string; keep: number }): Promise<void>;
}

// ---------------------------------------------------------------------------
// Semver comparison utilities (pure)
// ---------------------------------------------------------------------------

interface SemverParts {
  readonly major: number;
  readonly minor: number;
  readonly patch: number;
}

function parseSemver(version: string): SemverParts {
  // Strip pre-release/build metadata for comparison purposes
  const core = version.split(/[+-]/)[0] ?? version;
  const parts = core.split('.');
  return {
    major: parseInt(parts[0] ?? '0', 10),
    minor: parseInt(parts[1] ?? '0', 10),
    patch: parseInt(parts[2] ?? '0', 10),
  };
}

/**
 * Compare two semver strings.
 * Returns negative if a < b, 0 if equal, positive if a > b.
 */
function compareSemver(a: string, b: string): number {
  const pa = parseSemver(a);
  const pb = parseSemver(b);
  if (pa.major !== pb.major) return pa.major - pb.major;
  if (pa.minor !== pb.minor) return pa.minor - pb.minor;
  return pa.patch - pb.patch;
}

// ---------------------------------------------------------------------------
// Path helpers (pure)
// ---------------------------------------------------------------------------

function addonDir(baseStorePath: string, scope: AddonScope, type: AddonType, name: string): string {
  return path.join(baseStorePath, scope, type, name);
}

function versionDir(
  baseStorePath: string,
  scope: AddonScope,
  type: AddonType,
  name: string,
  version: string,
): string {
  return path.join(addonDir(baseStorePath, scope, type, name), version);
}

function tmpDir(
  baseStorePath: string,
  scope: AddonScope,
  type: AddonType,
  name: string,
  version: string,
): string {
  const id = Math.random().toString(36).slice(2, 10);
  // Sibling of the final version dir — same parent, so rename stays on one filesystem
  return path.join(addonDir(baseStorePath, scope, type, name), `${version}.tmp-${id}`);
}

// ---------------------------------------------------------------------------
// createVersionStore factory
// ---------------------------------------------------------------------------

export function createVersionStore(deps: {
  baseStorePath: string;
  port: StoreFsPort;
  /** The plugins home directory (e.g. ~/.claude-plugins). Created with mode 0o700.
   *  Defaults to path.dirname(baseStorePath). */
  pluginsHome?: string;
}): VersionStore {
  const { baseStorePath, port } = deps;
  const pluginsHome = deps.pluginsHome ?? path.dirname(baseStorePath);

  // -------------------------------------------------------------------------
  // snapshot
  // -------------------------------------------------------------------------
  async function snapshot(args: {
    scope: AddonScope;
    type: AddonType;
    name: string;
    version: string;
    sourceFiles: readonly { rel: string; absSource: string }[];
    manifest: AddonManifest;
    settingsEntry?: HookRegistration;
  }): Promise<string> {
    const { scope, type, name, version, sourceFiles, manifest, settingsEntry } = args;

    const finalPath = versionDir(baseStorePath, scope, type, name, version);
    const tmp = tmpDir(baseStorePath, scope, type, name, version);

    // Ensure the plugins home exists with the required secure mode (0o700).
    // Passing mode only sets it on creation — no chmod on an existing dir.
    await port.mkdir(pluginsHome, { recursive: true, mode: CREDENTIALS_DIR_MODE });

    // Stage into temp dir
    await port.mkdir(tmp);

    // Write addon.json
    await port.writeFile(path.join(tmp, 'addon.json'), JSON.stringify(manifest, null, 2));

    // Copy source files into files/
    for (const { rel, absSource } of sourceFiles) {
      const destDir = path.join(tmp, 'files', path.dirname(rel));
      await port.mkdir(destDir);
      await port.copyFile(absSource, path.join(tmp, 'files', rel));
    }

    // Write settings-entry.json for hook add-ons
    if (settingsEntry !== undefined) {
      await port.writeFile(
        path.join(tmp, 'settings-entry.json'),
        JSON.stringify(settingsEntry, null, 2),
      );
    }

    // Write a manifest of stored files for reliable retrieval
    const relPaths = sourceFiles.map(({ rel }) => rel);
    await port.writeFile(path.join(tmp, 'files.json'), JSON.stringify(relPaths, null, 2));

    // Atomic rename: tmp → final
    await port.rename(tmp, finalPath);

    // Prune after successful snapshot
    await prune({ scope, type, name, keep: DEFAULT_VERSION_RETENTION });

    return finalPath;
  }

  // -------------------------------------------------------------------------
  // list
  // -------------------------------------------------------------------------
  async function list(args: { scope: AddonScope; type: AddonType; name: string }): Promise<string[]> {
    const { scope, type, name } = args;
    const dir = addonDir(baseStorePath, scope, type, name);

    const dirExists = await port.exists(dir);
    if (!dirExists) {
      return [];
    }

    const entries = await port.readdir(dir);

    // Filter to only entries that look like version dirs (have addon.json)
    const versions: string[] = [];
    for (const entry of entries) {
      // Skip temp dirs
      if (entry.includes('.tmp-')) continue;
      const addonJsonPath = path.join(dir, entry, 'addon.json');
      const exists = await port.exists(addonJsonPath);
      if (exists) {
        versions.push(entry);
      }
    }

    return [...versions].sort(compareSemver);
  }

  // -------------------------------------------------------------------------
  // read
  // -------------------------------------------------------------------------
  async function read(args: {
    scope: AddonScope;
    type: AddonType;
    name: string;
    version: string;
  }): Promise<StoredVersion> {
    const { scope, type, name, version } = args;
    const vDir = versionDir(baseStorePath, scope, type, name, version);

    const addonJsonContent = await port.readFile(path.join(vDir, 'addon.json'));
    const manifest = JSON.parse(addonJsonContent) as AddonManifest;

    // Read file list from the stored files.json manifest
    const filesJsonPath = path.join(vDir, 'files.json');
    const filesJsonExists = await port.exists(filesJsonPath);
    let files: string[] = [];
    if (filesJsonExists) {
      const raw = await port.readFile(filesJsonPath);
      files = JSON.parse(raw) as string[];
    }

    // Read settings-entry.json if present
    const settingsEntryPath = path.join(vDir, 'settings-entry.json');
    const hasSettingsEntry = await port.exists(settingsEntryPath);
    let settingsEntry: HookRegistration | undefined;
    if (hasSettingsEntry) {
      const raw = await port.readFile(settingsEntryPath);
      settingsEntry = JSON.parse(raw) as HookRegistration;
    }

    return {
      manifest,
      version,
      files,
      ...(settingsEntry !== undefined ? { settingsEntry } : {}),
      path: vDir,
    };
  }

  // -------------------------------------------------------------------------
  // latestPrior
  // -------------------------------------------------------------------------
  async function latestPrior(args: {
    scope: AddonScope;
    type: AddonType;
    name: string;
    currentVersion: string;
  }): Promise<string | undefined> {
    const { scope, type, name, currentVersion } = args;
    const versions = await list({ scope, type, name });

    // Filter: strictly less than currentVersion
    const priors = versions.filter((v) => compareSemver(v, currentVersion) < 0);

    if (priors.length === 0) return undefined;

    // priors is sorted ascending; return the last (highest) one
    return priors[priors.length - 1];
  }

  // -------------------------------------------------------------------------
  // prune
  // -------------------------------------------------------------------------
  async function prune(args: {
    scope: AddonScope;
    type: AddonType;
    name: string;
    keep: number;
  }): Promise<void> {
    const { scope, type, name, keep } = args;
    const versions = await list({ scope, type, name });

    if (versions.length <= keep) return;

    // versions is sorted ascending; remove the oldest (first N - keep)
    const toRemove = versions.slice(0, versions.length - keep);
    for (const v of toRemove) {
      const target = versionDir(baseStorePath, scope, type, name, v);
      await port.rm(target, { recursive: true, force: true });
    }
  }

  return { snapshot, list, read, latestPrior, prune };
}
