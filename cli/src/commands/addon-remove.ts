/**
 * addon-remove command — remove an installed add-on.
 *
 * Flags:
 *   --type <agent|skill|hook|plugin>  REQUIRED
 *   --scope <local|global>            REQUIRED
 *
 * Error messages must NOT leak absolute internal paths.
 */

import * as os from 'node:os';
import * as path from 'node:path';
import type { CommandResult } from './install.js';
import type { AddonScope, AddonType } from '../addon/manifest.js';
import type { LifecycleDeps } from '../addon/lifecycle.js';
import { remove } from '../addon/lifecycle.js';
import { realLifecycleFsPort } from '../addon/lifecycle.js';
import { realSettingsFsPort } from '../addon/settings.js';
import { createVersionStore, realStoreFsPort } from '../addon/store.js';
import { resolveHome } from '../config/config.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface AddonRemoveArgs {
  name: string;
  type?: string;
  scope?: string;
}

export interface AddonRemoveDeps {
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

export async function runAddonRemove(args: AddonRemoveArgs, deps: AddonRemoveDeps = {}): Promise<CommandResult> {
  const { name } = args;
  const cwd = deps.cwd ?? process.cwd();
  const homeDir = deps.homeDir ?? os.homedir();

  // ── Validate --type ────────────────────────────────────────────────────────
  if (!args.type) {
    return {
      exitCode: 1,
      output: '--type is required. Use --type agent, skill, hook, or plugin.',
    };
  }

  const validTypes: AddonType[] = ['agent', 'skill', 'hook', 'plugin'];
  if (!validTypes.includes(args.type as AddonType)) {
    return {
      exitCode: 1,
      output: `Invalid --type "${args.type}". Valid types: ${validTypes.join(', ')}.`,
    };
  }

  const type = args.type as AddonType;

  // ── Validate --scope ───────────────────────────────────────────────────────
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

  const result = await remove({ name, type, scope }, lifecycleDeps);

  if (!result.success) {
    // Sanitize: strip any absolute paths from the error message to avoid leaking internals.
    // The lifecycle engine already avoids them in most cases, but we strip as a final guard.
    const sanitized = result.message.replace(/\/[^\s"']+/g, (match) => {
      // Only strip paths that look like absolute internal paths (contain multiple segments)
      if (match.split('/').length > 3) {
        return '<path>';
      }
      return match;
    });
    return { exitCode: 1, output: sanitized };
  }

  return { exitCode: 0, output: result.message };
}
