/**
 * Publish command — validate and upload a plugin to the marketplace.
 */

import * as nodeFsPromises from 'node:fs/promises';
import * as path from 'node:path';
import type { IMarketplaceClient } from '../api/client.js';
import { MarketplaceApiError } from '../api/client.js';
import { validateManifest } from './validate.js';
import type { PluginManifest } from './validate.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface PublishFsPort {
  readFile(p: string): Promise<string>;
  exists(p: string): Promise<boolean>;
  compress(dir: string): Promise<Blob>;
}

export interface PublishArgs {
  pluginPath?: string;
}

export interface PublishDeps {
  client: IMarketplaceClient;
  homeDir: string;
  fs?: PublishFsPort;
  env?: NodeJS.ProcessEnv;
}

// ---------------------------------------------------------------------------
// Default real FS implementation
// ---------------------------------------------------------------------------

const realPublishFsPort: PublishFsPort = {
  async readFile(p) {
    return nodeFsPromises.readFile(p, 'utf-8');
  },
  async exists(p) {
    try {
      await nodeFsPromises.stat(p);
      return true;
    } catch {
      return false;
    }
  },
  async compress(_dir) {
    // Minimal implementation — real archiving is Group 19 scope
    return new Blob(['archive placeholder']);
  },
};

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runPublish(
  args: PublishArgs,
  deps: PublishDeps,
): Promise<CommandResult> {
  const { pluginPath } = args;
  const { client, fs: fsPort = realPublishFsPort } = deps;

  const dir = pluginPath ?? process.cwd();
  const manifestPath = path.join(dir, 'plugin.json');

  // ── 1. Read manifest ──────────────────────────────────────────────────────
  let rawManifest: string;
  try {
    rawManifest = await fsPort.readFile(manifestPath);
  } catch {
    return {
      exitCode: 1,
      output: [
        `plugin.json not found in ${dir}`,
        `Run \`claude plugin scaffold\` to generate a plugin template.`,
      ].join('\n'),
    };
  }

  // ── 2. Parse manifest ─────────────────────────────────────────────────────
  let manifest: unknown;
  try {
    manifest = JSON.parse(rawManifest);
  } catch {
    return {
      exitCode: 1,
      output: `Invalid plugin.json: file contains malformed JSON.`,
    };
  }

  // ── 3. Validate manifest ──────────────────────────────────────────────────
  const validation = validateManifest(manifest);
  if (!validation.valid) {
    const lines = [
      'Cannot publish: manifest validation failed.',
      ...validation.errors.map((e) => `  - ${e}`),
      '',
      'Run \`claude plugin scaffold\` to generate a valid plugin template.',
    ];
    return { exitCode: 1, output: lines.join('\n') };
  }

  const m = manifest as PluginManifest;

  // ── 4. Compress plugin directory ─────────────────────────────────────────
  const archive = await fsPort.compress(dir);

  // ── 5. Upload to marketplace ──────────────────────────────────────────────
  const formData = new FormData();
  formData.append('package', archive, 'plugin.tar.gz');
  formData.append('name', m.name);
  formData.append('version', m.version);

  try {
    const response = await client.uploadPlugin(formData);
    const marketplaceUrl = `https://plugins.claudeforge.dev/plugins/${response.slug ?? m.name}`;
    return {
      exitCode: 0,
      output: `Published ${m.name}@${m.version} at ${marketplaceUrl}`,
    };
  } catch (err) {
    if (err instanceof MarketplaceApiError && err.status === 409) {
      return {
        exitCode: 1,
        output: [
          `Version ${m.version} of ${m.name} already exists`,
          `Suggest incrementing the version in plugin.json and re-running publish.`,
          `Use --force to overwrite (if allowed by marketplace policy).`,
        ].join('\n'),
      };
    }
    const message = err instanceof Error ? err.message : String(err);
    return {
      exitCode: 1,
      output: `Publish failed: ${message}`,
    };
  }
}
