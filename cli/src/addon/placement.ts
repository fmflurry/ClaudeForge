/**
 * Add-on placement module.
 *
 * Computes the canonical live install targets and sidecar index path for an
 * add-on, given its manifest and the resolved scope root.
 *
 * Per-type layout (from design.md Decision 2 — authoritative):
 *
 *   agent  → ownerPath = <scopeRoot>/agents/<name>.md  (a FILE, not a dir)
 *            liveTargets[0]: files[0] → agents/<name>.md
 *
 *   skill  → ownerPath = <scopeRoot>/skills/<name>/     (a DIR)
 *            liveTargets[i]: files[i] → skills/<name>/<files[i]>  (preserves tree)
 *
 *   hook   → ownerPath = <scopeRoot>/hooks/<name>/      (keyed by add-on name via sidecar)
 *            liveTargets[i]: files[i] → hooks/<files[i]>
 *            settingsEntry = manifest.hook               (required)
 *
 *   plugin → ownerPath = <homeDir>/.claude/plugins/<name>/  (GLOBAL ONLY — never scopeRoot)
 *            liveTargets[i]: files[i] → plugins/<name>/<files[i]>
 *
 *   sidecarPath (ALL types) = <scopeRoot>/.addons/<type>/<name>.json
 *
 * Pure module — no file I/O.  No `any` types.
 */

import * as path from 'node:path';

import type { AddonManifest, AddonType, HookRegistration } from './manifest.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** A single file to be copied from the add-on source to a live install target. */
export interface PlacedFile {
  /** Relative path as declared in the manifest's `files` array. */
  readonly fromRel: string;
  /** Absolute path where the file will live after installation. */
  readonly toAbs: string;
}

/**
 * Fully resolved placement for an add-on.
 *
 * - `liveTargets`: one entry per `manifest.files` entry.
 * - `ownerPath`:   canonical "handle" for list/remove (a file for agents, a dir for others).
 * - `sidecarPath`: where the lifecycle engine writes the sidecar JSON index.
 * - `settingsEntry`: present only when `type === 'hook'`.
 */
export interface Placement {
  readonly type: AddonType;
  readonly liveTargets: readonly PlacedFile[];
  readonly ownerPath: string;
  readonly sidecarPath: string;
  readonly settingsEntry?: HookRegistration;
}

/** Injected dependencies for placement resolution. */
export interface PlacementDeps {
  /** The resolved `.claude` scope root (output of `resolveScopeRoot`). */
  readonly scopeRoot: string;
  /** The user's home directory — used to compute the plugin global root. */
  readonly homeDir: string;
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

/**
 * Asserts that `targetPath` starts with `expectedRoot`, providing a defensive
 * containment check against any path that managed to slip through.
 *
 * Throws if containment is violated.
 */
function assertContained(targetPath: string, expectedRoot: string): void {
  // Normalise both so trailing slashes and `.` segments cannot fool the check.
  const normalTarget = path.normalize(targetPath);
  const normalRoot = path.normalize(expectedRoot);
  // Root must end with separator for prefix check to be reliable.
  const rootWithSep = normalRoot.endsWith(path.sep)
    ? normalRoot
    : normalRoot + path.sep;

  if (!normalTarget.startsWith(rootWithSep) && normalTarget !== normalRoot) {
    throw new Error(
      `Path containment violation: "${targetPath}" escapes its intended root "${expectedRoot}".`,
    );
  }
}

function buildPlacedFile(fromRel: string, toAbs: string, containmentRoot: string): PlacedFile {
  assertContained(toAbs, containmentRoot);
  return { fromRel, toAbs };
}

function sidecarPath(scopeRoot: string, type: AddonType, name: string): string {
  return path.join(scopeRoot, '.addons', type, `${name}.json`);
}

// ---------------------------------------------------------------------------
// Per-type placement strategies
// ---------------------------------------------------------------------------

function placeAgent(manifest: AddonManifest, scopeRoot: string): Placement {
  const { name, files } = manifest;
  const ownerPath = path.join(scopeRoot, 'agents', `${name}.md`);
  const liveTargets: PlacedFile[] = [
    buildPlacedFile(files[0], ownerPath, path.join(scopeRoot, 'agents')),
  ];
  return {
    type: 'agent',
    liveTargets,
    ownerPath,
    sidecarPath: sidecarPath(scopeRoot, 'agent', name),
  };
}

function placeSkill(manifest: AddonManifest, scopeRoot: string): Placement {
  const { name, files } = manifest;
  const ownerPath = path.join(scopeRoot, 'skills', name) + path.sep;
  const skillRoot = path.join(scopeRoot, 'skills', name);
  const liveTargets: PlacedFile[] = files.map((fromRel) =>
    buildPlacedFile(fromRel, path.join(skillRoot, fromRel), skillRoot),
  );
  return {
    type: 'skill',
    liveTargets,
    ownerPath,
    sidecarPath: sidecarPath(scopeRoot, 'skill', name),
  };
}

function placeHook(manifest: AddonManifest, scopeRoot: string): Placement {
  const { name, files, hook } = manifest;
  // hook is guaranteed by manifest validation for type === 'hook', but we
  // guard defensively to satisfy TypeScript strict null checks.
  if (hook === undefined) {
    throw new Error(
      `Manifest "${name}" has type "hook" but is missing the required "hook" registration object.`,
    );
  }
  const hooksRoot = path.join(scopeRoot, 'hooks');
  const ownerPath = path.join(hooksRoot, name) + path.sep;
  const liveTargets: PlacedFile[] = files.map((fromRel) =>
    buildPlacedFile(fromRel, path.join(hooksRoot, fromRel), hooksRoot),
  );
  return {
    type: 'hook',
    liveTargets,
    ownerPath,
    sidecarPath: sidecarPath(scopeRoot, 'hook', name),
    settingsEntry: { ...hook },
  };
}

function placePlugin(manifest: AddonManifest, scopeRoot: string, homeDir: string): Placement {
  const { name, files } = manifest;
  // Plugin is GLOBAL ONLY — always placed under homeDir/.claude/plugins/, never under scopeRoot.
  const pluginRoot = path.join(homeDir, '.claude', 'plugins', name);
  const ownerPath = pluginRoot + path.sep;
  const liveTargets: PlacedFile[] = files.map((fromRel) =>
    buildPlacedFile(fromRel, path.join(pluginRoot, fromRel), pluginRoot),
  );
  return {
    type: 'plugin',
    liveTargets,
    ownerPath,
    // Sidecar lives in scopeRoot, not the live plugin dir, so list/remove can
    // find it regardless of whether scopeRoot is local or global.
    sidecarPath: sidecarPath(scopeRoot, 'plugin', name),
  };
}

// ---------------------------------------------------------------------------
// resolvePlacement — public API
// ---------------------------------------------------------------------------

/**
 * Computes the full placement for the given manifest within a resolved scope.
 *
 * The `deps.scopeRoot` must be the output of `resolveScopeRoot` from `scope.ts`.
 * The `deps.homeDir` must be the raw OS home directory (e.g. `os.homedir()`).
 *
 * @throws if any computed `toAbs` path escapes its intended containment root.
 */
export function resolvePlacement(manifest: AddonManifest, deps: PlacementDeps): Placement {
  const { scopeRoot, homeDir } = deps;

  switch (manifest.type) {
    case 'agent':
      return placeAgent(manifest, scopeRoot);
    case 'skill':
      return placeSkill(manifest, scopeRoot);
    case 'hook':
      return placeHook(manifest, scopeRoot);
    case 'plugin':
      return placePlugin(manifest, scopeRoot, homeDir);
  }
}
