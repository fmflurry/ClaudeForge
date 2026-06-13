/**
 * addon-list command — list installed add-ons.
 *
 * Flags:
 *   --scope <local|global>  optional; if absent, lists BOTH scopes with labels
 *
 * Output format:
 *   type  name  version  [storedVersions]
 */

import * as os from 'node:os';
import * as path from 'node:path';
import type { CommandResult } from './install.js';
import type { AddonScope } from '../addon/manifest.js';
import type { LifecycleDeps } from '../addon/lifecycle.js';
import { list } from '../addon/lifecycle.js';
import { realLifecycleFsPort } from '../addon/lifecycle.js';
import { realSettingsFsPort } from '../addon/settings.js';
import { createVersionStore, realStoreFsPort } from '../addon/store.js';
import { resolveHome } from '../config/config.js';
import type { AddonListing } from '../addon/lifecycle.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface AddonListArgs {
  scope?: string;
}

export interface AddonListDeps {
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

function formatListing(listing: AddonListing): string {
  const stored =
    listing.storedVersions.length > 0
      ? `  [stored: ${listing.storedVersions.join(', ')}]`
      : '';
  return `  ${listing.type}  ${listing.name}  v${listing.version}${stored}`;
}

function formatScopeBlock(scope: AddonScope, listings: AddonListing[]): string {
  const header = `${scope} scope:`;
  if (listings.length === 0) {
    return `${header}\n  No add-ons found in ${scope} scope`;
  }
  return [header, ...listings.map(formatListing)].join('\n');
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runAddonList(args: AddonListArgs, deps: AddonListDeps = {}): Promise<CommandResult> {
  const cwd = deps.cwd ?? process.cwd();
  const homeDir = deps.homeDir ?? os.homedir();
  const lifecycleDeps = deps.lifecycleDeps ?? buildLifecycleDeps(cwd, homeDir);

  if (args.scope !== undefined) {
    // Validate scope
    if (args.scope !== 'local' && args.scope !== 'global') {
      return {
        exitCode: 1,
        output: `Invalid scope "${args.scope}". Use --scope local or --scope global.`,
      };
    }

    const scope = args.scope as AddonScope;
    const listings = await list({ scope }, lifecycleDeps);

    if (listings.length === 0) {
      return { exitCode: 0, output: `No add-ons found in ${scope} scope` };
    }

    const lines = listings.map(formatListing);
    return { exitCode: 0, output: lines.join('\n') };
  }

  // No scope — list both
  const [localListings, globalListings] = await Promise.all([
    list({ scope: 'local' }, lifecycleDeps),
    list({ scope: 'global' }, lifecycleDeps),
  ]);

  const blocks = [
    formatScopeBlock('local', localListings),
    formatScopeBlock('global', globalListings),
  ];

  return { exitCode: 0, output: blocks.join('\n\n') };
}
