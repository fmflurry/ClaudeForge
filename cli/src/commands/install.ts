/**
 * Install command — install a plugin from the marketplace.
 * Halts before writing registry on any network/FS error.
 */

import * as nodeFsPromises from 'node:fs/promises';
import * as path from 'node:path';
import type { IMarketplaceClient } from '../api/client.js';
import { MarketplaceApiError } from '../api/client.js';
import { SessionExpiredError } from '../auth/token-attachment.js';
import { readRegistry, addRecord, writeRegistry } from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface FsPort {
  mkdir(dir: string): Promise<void>;
  writeStream(dest: string, stream: ReadableStream<Uint8Array>): Promise<void>;
  rm(filePath: string): Promise<void>;
}

export interface InstallArgs {
  pluginName: string;
  version?: string;
}

export interface InstallDeps {
  client: IMarketplaceClient;
  homeDir: string;
  fs?: FsPort;
  env?: NodeJS.ProcessEnv;
}

// ---------------------------------------------------------------------------
// Default real FS implementation
// ---------------------------------------------------------------------------

const realFsPort: FsPort = {
  async mkdir(dir) {
    await nodeFsPromises.mkdir(dir, { recursive: true });
  },
  async writeStream(dest, stream) {
    const { createWriteStream } = await import('node:fs');
    await new Promise<void>((resolve, reject) => {
      const writer = createWriteStream(dest);
      const reader = stream.getReader();

      function pump(): void {
        reader
          .read()
          .then(({ done, value }) => {
            if (done) {
              writer.end();
              return;
            }
            writer.write(Buffer.from(value), (err) => {
              if (err) {
                reject(err);
                return;
              }
              pump();
            });
          })
          .catch(reject);
      }

      writer.on('finish', resolve);
      writer.on('error', reject);
      pump();
    });
  },
  async rm(filePath) {
    await nodeFsPromises.rm(filePath, { recursive: true, force: true });
  },
};

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runInstall(
  args: InstallArgs,
  deps: InstallDeps,
): Promise<CommandResult> {
  const { pluginName, version } = args;
  const { client, homeDir, fs: fsPort = realFsPort } = deps;

  // ── 1. Fetch plugin metadata (halts here on network error) ──────────────
  let pluginDetail;
  try {
    pluginDetail = await client.getPlugin(pluginName);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return {
      exitCode: 1,
      output: [
        `Could not reach marketplace: ${message}`,
        `Try again or configure a different API URL with:`,
        `  claude-plugin config set api-url <url>`,
      ].join('\n'),
    };
  }

  const targetVersion = version ?? pluginDetail.latestVersion ?? '0.0.0';

  // ── 2. Download plugin package (halts here on download error) ───────────
  let stream: ReadableStream<Uint8Array>;
  try {
    stream = await client.downloadPlugin(pluginDetail.id, targetVersion);
  } catch (err) {
    if (err instanceof SessionExpiredError) {
      return {
        exitCode: 1,
        output: err.message,
      };
    }
    if (err instanceof MarketplaceApiError && err.status === 403) {
      return {
        exitCode: 1,
        output: [
          `Access denied: you are not a member of the org that owns this plugin.`,
          `Contact the plugin owner or run 'claude-plugin login' with the correct account.`,
        ].join('\n'),
      };
    }
    const message = err instanceof Error ? err.message : String(err);
    return {
      exitCode: 1,
      output: [
        `Could not reach marketplace: ${message}`,
        `Try again or configure a different API URL with:`,
        `  claude-plugin config set api-url <url>`,
      ].join('\n'),
    };
  }

  // ── 3. Write files to disk ───────────────────────────────────────────────
  // Guard against path traversal: reject names that are absolute paths or
  // contain '..' segments, then assert the resolved path stays inside the
  // plugins root (defense-in-depth).
  const hasDotDot = pluginName.split(/[\\/]/).some((seg) => seg === '..');
  if (path.isAbsolute(pluginName) || hasDotDot || pluginName.includes('\0')) {
    return {
      exitCode: 1,
      output: `Invalid plugin name: "${pluginName}" contains unsafe path components.`,
    };
  }
  const pluginsRoot = path.resolve(homeDir, 'plugins');
  const pluginDir = path.resolve(pluginsRoot, pluginName);
  if (!pluginDir.startsWith(pluginsRoot + path.sep)) {
    return {
      exitCode: 1,
      output: `Invalid plugin name: "${pluginName}" escapes the plugins directory.`,
    };
  }
  await fsPort.mkdir(pluginDir);
  await fsPort.writeStream(path.join(pluginDir, 'package.tar.gz'), stream);

  // ── 4. Write registry (only after successful disk write) ────────────────
  const registry = await readRegistry(homeDir);
  const record = {
    name: pluginName,
    version: targetVersion,
    installedAt: new Date().toISOString(),
    path: pluginDir,
  };
  const updated = addRecord(registry, record);
  await writeRegistry(homeDir, updated);

  // ── 5. Build success output ──────────────────────────────────────────────
  const lines = [`Installed ${pluginName} v${targetVersion}`];
  if (
    version &&
    pluginDetail.latestVersion &&
    pluginDetail.latestVersion !== version
  ) {
    lines.push(`A newer version v${pluginDetail.latestVersion} is available`);
  }

  return { exitCode: 0, output: lines.join('\n') };
}
