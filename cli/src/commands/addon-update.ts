/**
 * addon-update command — update an installed add-on to a newer version from a source directory.
 *
 * Flags:
 *   --scope <local|global>  REQUIRED
 *
 * Success:  "Updated <name> from <old> to <new>"
 * No-op:    "Already at version <v>. Nothing to update."
 */

import * as os from 'node:os';
import * as path from 'node:path';
import type { CommandResult } from './install.js';
import type { AddonScope } from '../addon/manifest.js';
import type { LifecycleDeps } from '../addon/lifecycle.js';
import { update } from '../addon/lifecycle.js';
import { realLifecycleFsPort } from '../addon/lifecycle.js';
import { realSettingsFsPort } from '../addon/settings.js';
import { createVersionStore, realStoreFsPort } from '../addon/store.js';
import { resolveHome } from '../config/config.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface AddonUpdateArgs {
  source: string;
  scope?: string;
}

export interface AddonUpdateDeps {
  lifecycleDeps?: LifecycleDeps;
  cwd?: string;
  homeDir?: string;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function buildLifecycleDeps(cwd: string, homeDir: string): LifecycleDeps {
  const storeBasePath = path.join(resolveHome(), 'addon-store');
  return {
    fs: realLifecycleFsPort,
    settingsPort: realSettingsFsPort,
    store: createVersionStore({ baseStorePath: storeBasePath, port: realStoreFsPort }),
    cwd,
    homeDir,
  };
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runAddonUpdate(args: AddonUpdateArgs, deps: AddonUpdateDeps = {}): Promise<CommandResult> {
  const { source } = args;
  const cwd = deps.cwd ?? process.cwd();
  const homeDir = deps.homeDir ?? os.homedir();

  // ── Validate scope ─────────────────────────────────────────────────────────
  if (!args.scope) {
    return {
      exitCode: 1,
      output: '--scope is required. Use --scope local or --scope global.',
    };
  }

  if (args.scope !== 'local' && args.scope !== 'global') {
    return {
      exitCode: 1,
      output: `Invalid scope "${args.scope}". Use --scope local or --scope global.`,
    };
  }

  const scope = args.scope as AddonScope;
  const lifecycleDeps = deps.lifecycleDeps ?? buildLifecycleDeps(cwd, homeDir);
  const resolvedSource = path.isAbsolute(source) ? source : path.resolve(cwd, source);

  const result = await update({ sourceDir: resolvedSource, scope }, lifecycleDeps);

  if (!result.success) {
    return { exitCode: 1, output: result.message };
  }

  return { exitCode: 0, output: result.message };
}
