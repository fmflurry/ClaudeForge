/**
 * Add-on scope resolution module.
 *
 * Resolves the on-disk root directory for a given add-on scope.
 *
 * IMPORTANT: This module intentionally does NOT use `resolveHome` from
 * cli/src/config/config.ts, which points at `~/.claude-plugins` (the
 * marketplace home). The scope roots here target Claude Code's own
 * config dirs: `./.claude/` (local) and `~/.claude/` (global).
 *
 * Pure module — no file I/O.
 */

import * as path from 'node:path';

import type { AddonScope } from './manifest.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** Injected dependencies for scope resolution — enables testing without I/O. */
export interface ScopeResolutionDeps {
  /** The current working directory (used for local scope). */
  readonly cwd: string;
  /** The user's home directory (used for global scope). NOT the marketplace home. */
  readonly homeDir: string;
}

// ---------------------------------------------------------------------------
// resolveScopeRoot
// ---------------------------------------------------------------------------

/**
 * Resolves the root `.claude` directory for the given scope.
 *
 * - `'local'`  → `path.resolve(deps.cwd, '.claude')`   (project-local)
 * - `'global'` → `path.resolve(deps.homeDir, '.claude')` (user home)
 *
 * The returned path is always absolute. No I/O is performed.
 */
export function resolveScopeRoot(scope: AddonScope, deps: ScopeResolutionDeps): string {
  if (scope === 'local') {
    return path.resolve(deps.cwd, '.claude');
  }
  return path.resolve(deps.homeDir, '.claude');
}
