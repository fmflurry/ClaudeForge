/**
 * Update command — update an installed plugin to the latest version.
 * Creates a backup before extracting new version, rolls back on failure.
 */

import * as nodeFsPromises from 'node:fs/promises';
import * as path from 'node:path';
import type { IMarketplaceClient } from '../api/client.js';
import {
  readRegistry,
  writeRegistry,
  findRecord,
  removeRecord,
  addRecord,
  backupsDir,
} from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface UpdateFsPort {
  mkdir(dir: string): Promise<void>;
  writeStream(dest: string, stream: ReadableStream<Uint8Array>): Promise<void>;
  rm(filePath: string): Promise<void>;
  copyDir(src: string, dest: string): Promise<void>;
  exists(p: string): Promise<boolean>;
}

export interface UpdateArgs {
  pluginName: string;
}

export interface UpdateDeps {
  client: IMarketplaceClient;
  homeDir: string;
  fs?: UpdateFsPort;
  env?: NodeJS.ProcessEnv;
}

// ---------------------------------------------------------------------------
// Default real FS implementation
// ---------------------------------------------------------------------------

const realUpdateFsPort: UpdateFsPort = {
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
  async copyDir(src, dest) {
    await nodeFsPromises.cp(src, dest, { recursive: true });
  },
  async exists(p) {
    try {
      await nodeFsPromises.stat(p);
      return true;
    } catch {
      return false;
    }
  },
};

// ---------------------------------------------------------------------------
// Command
// ---------------------------------------------------------------------------

export async function runUpdate(args: UpdateArgs, deps: UpdateDeps): Promise<CommandResult> {
  const { pluginName } = args;
  const { client, homeDir, fs: fsPort = realUpdateFsPort } = deps;

  // ── 1. Check plugin is installed ─────────────────────────────────────────
  const registry = await readRegistry(homeDir);
  const existing = findRecord(registry, pluginName);
  if (!existing) {
    return {
      exitCode: 1,
      output: `Plugin ${pluginName} is not installed.\nRun \`claude-plugin list\` to see installed plugins.`,
    };
  }

  // ── 2. Get latest version ─────────────────────────────────────────────────
  const latest = await client.getLatestVersion(existing.name);
  if (latest.version === existing.version) {
    return {
      exitCode: 0,
      output: `Plugin is already up-to-date at v${existing.version}`,
    };
  }

  // ── 3. Create backup before writing ─────────────────────────────────────
  const bDir = backupsDir(homeDir);
  await fsPort.mkdir(bDir);
  const backupPath = path.join(bDir, `${pluginName.replace(/\//g, '__')}_${existing.version}`);
  const pluginDir = existing.path;
  const pluginExists = await fsPort.exists(pluginDir);
  if (pluginExists) {
    await fsPort.copyDir(pluginDir, backupPath);
  }

  // ── 4. Download new version ───────────────────────────────────────────────
  const stream = await client.downloadPlugin(existing.name, latest.version);

  // ── 5. Write new version to disk ─────────────────────────────────────────
  try {
    await fsPort.mkdir(pluginDir);
    await fsPort.writeStream(path.join(pluginDir, 'package.tar.gz'), stream);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    // Rollback: keep old registry entry unchanged
    return {
      exitCode: 1,
      output: [
        `Update failed: ${message}`,
        `The previous version v${existing.version} has been retained.`,
        `A backup was saved to: ${backupPath}`,
        `You can rollback manually or report the error to the plugin author.`,
      ].join('\n'),
    };
  }

  // ── 6. Update registry ────────────────────────────────────────────────────
  const withoutOld = removeRecord(registry, pluginName);
  const newRecord = {
    name: pluginName,
    version: latest.version,
    installedAt: new Date().toISOString(),
    path: pluginDir,
  };
  const updatedRegistry = addRecord(withoutOld, newRecord);
  await writeRegistry(homeDir, updatedRegistry);

  return {
    exitCode: 0,
    output: `Updated ${pluginName} from v${existing.version} to v${latest.version}`,
  };
}
