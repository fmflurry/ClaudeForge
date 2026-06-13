/**
 * addon-add command — install an add-on from a source directory or scaffold from a bare name.
 *
 * Two paths:
 *   INSTALL path: <source> resolves to a dir/file containing addon.json → call lifecycle.add
 *   SCAFFOLD path: <source> is a bare name with no manifest AND --type given → buildAddonScaffold + lifecycle.add
 *
 * Flags:
 *   --scope <local|global>  REQUIRED
 *   --force                 optional, overwrite existing
 *   --type                  optional, for scaffold path
 *   --lang                  optional, for scaffold path
 *
 * Guards:
 *   --scope local --type plugin → rejected before any I/O
 */

import * as nodeFsPromises from 'node:fs/promises';
import * as os from 'node:os';
import * as path from 'node:path';
import type { CommandResult } from './install.js';
import type { AddonScope, AddonType } from '../addon/manifest.js';
import type { LifecycleDeps, LifecycleFsPort } from '../addon/lifecycle.js';
import { add } from '../addon/lifecycle.js';
import { realLifecycleFsPort } from '../addon/lifecycle.js';
import { realSettingsFsPort } from '../addon/settings.js';
import { createVersionStore, realStoreFsPort } from '../addon/store.js';
import { resolveHome } from '../config/config.js';
import { buildAddonScaffold } from '../addon/scaffold-source.js';
import type { TemplateLanguage } from '../addon/scaffold-source.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface AddonAddArgs {
  source: string;
  scope?: string;
  force?: boolean;
  type?: string;
  lang?: string;
}

export interface AddonAddDeps {
  lifecycleDeps?: LifecycleDeps;
  cwd?: string;
  homeDir?: string;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function buildLifecycleDeps(cwd: string, homeDir: string, fsPort?: LifecycleFsPort): LifecycleDeps {
  const storeBasePath = path.join(resolveHome(), 'addon-store');
  return {
    fs: fsPort ?? realLifecycleFsPort,
    settingsPort: realSettingsFsPort,
    store: createVersionStore({ baseStorePath: storeBasePath, port: realStoreFsPort }),
    cwd,
    homeDir,
  };
}

async function hasAddonJson(sourceDir: string): Promise<boolean> {
  try {
    await nodeFsPromises.stat(path.join(sourceDir, 'addon.json'));
    return true;
  } catch {
    return false;
  }
}

async function writeTempScaffold(files: Record<string, string>): Promise<string> {
  const tmpDir = await nodeFsPromises.mkdtemp(path.join(os.tmpdir(), 'claude-addon-scaffold-'));
  for (const [rel, content] of Object.entries(files)) {
    const dest = path.join(tmpDir, rel);
    await nodeFsPromises.mkdir(path.dirname(dest), { recursive: true });
    await nodeFsPromises.writeFile(dest, content, 'utf-8');
  }
  return tmpDir;
}

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runAddonAdd(args: AddonAddArgs, deps: AddonAddDeps = {}): Promise<CommandResult> {
  const { source, force = false, type: typeArg, lang } = args;
  const cwd = deps.cwd ?? process.cwd();
  const homeDir = deps.homeDir ?? os.homedir();

  // ── 1. Validate scope ──────────────────────────────────────────────────────
  if (!args.scope) {
    return {
      exitCode: 1,
      output: '--scope is required. Use --scope local or --scope global.',
    };
  }

  const scope = args.scope as AddonScope;
  if (scope !== 'local' && scope !== 'global') {
    return {
      exitCode: 1,
      output: `Invalid scope "${args.scope}". Use --scope local or --scope global.`,
    };
  }

  // ── 2. Reject plugin+local before any I/O ────────────────────────────────
  if (typeArg === 'plugin' && scope === 'local') {
    return {
      exitCode: 1,
      output: 'plugins are global-only and cannot be installed to local scope. Use --scope global instead.',
    };
  }

  // ── 3. Determine path: INSTALL vs SCAFFOLD ────────────────────────────────
  const lifecycleDeps = deps.lifecycleDeps ?? buildLifecycleDeps(cwd, homeDir);
  const resolvedSource = path.isAbsolute(source) ? source : path.resolve(cwd, source);

  let sourceDir: string;
  let tmpDirToClean: string | undefined;

  const sourceHasManifest = await hasAddonJson(resolvedSource);

  if (sourceHasManifest) {
    // INSTALL path: source dir has addon.json
    sourceDir = resolvedSource;
  } else {
    // SCAFFOLD path: bare name, --type required
    if (!typeArg) {
      return {
        exitCode: 1,
        output: `No addon.json found in "${source}". Provide --type to scaffold a new add-on.`,
      };
    }

    const validTypes: AddonType[] = ['agent', 'skill', 'hook', 'plugin'];
    if (!validTypes.includes(typeArg as AddonType)) {
      return {
        exitCode: 1,
        output: `Invalid --type "${typeArg}". Valid types: ${validTypes.join(', ')}.`,
      };
    }

    const addonType = typeArg as AddonType;
    const language = (lang ?? 'typescript') as TemplateLanguage;

    let scaffold;
    try {
      scaffold = buildAddonScaffold({ name: source, type: addonType, language });
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      return { exitCode: 1, output: `Failed to scaffold add-on: ${msg}` };
    }

    try {
      tmpDirToClean = await writeTempScaffold(scaffold.files);
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      return { exitCode: 1, output: `Failed to write scaffold to temp dir: ${msg}` };
    }

    sourceDir = tmpDirToClean;
  }

  // ── 4. Call lifecycle.add ─────────────────────────────────────────────────
  let result;
  try {
    result = await add({ sourceDir, scope, force }, lifecycleDeps);
  } finally {
    if (tmpDirToClean !== undefined) {
      await nodeFsPromises.rm(tmpDirToClean, { recursive: true, force: true });
    }
  }

  if (!result.success) {
    return { exitCode: 1, output: result.message };
  }

  return { exitCode: 0, output: result.message };
}
