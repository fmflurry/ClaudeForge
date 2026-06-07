/**
 * Remove command — uninstall a plugin.
 */

import * as nodeFsPromises from 'node:fs/promises';
import { readRegistry, writeRegistry, findRecord, removeRecord } from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface CommandResult {
  exitCode: number;
  output: string;
}

export interface RemoveFsPort {
  rm(filePath: string): Promise<void>;
  exists(p: string): Promise<boolean>;
}

export interface RemoveArgs {
  pluginName: string;
}

export interface RemoveDeps {
  homeDir: string;
  fs?: RemoveFsPort;
  env?: NodeJS.ProcessEnv;
}

// ---------------------------------------------------------------------------
// Default real FS implementation
// ---------------------------------------------------------------------------

const realRemoveFsPort: RemoveFsPort = {
  async rm(filePath) {
    await nodeFsPromises.rm(filePath, { recursive: true, force: true });
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

export async function runRemove(args: RemoveArgs, deps: RemoveDeps): Promise<CommandResult> {
  const { pluginName } = args;
  const { homeDir, fs: fsPort = realRemoveFsPort } = deps;

  const registry = await readRegistry(homeDir);
  const record = findRecord(registry, pluginName);

  if (!record) {
    return {
      exitCode: 1,
      output: [`Plugin ${pluginName} is not installed`, `Run \`claude plugin list\` to see installed plugins.`].join(
        '\n',
      ),
    };
  }

  // Delete from disk
  try {
    await fsPort.rm(record.path);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    return {
      exitCode: 1,
      output: `Failed to remove plugin files: ${message}`,
    };
  }

  // Remove from registry
  const updated = removeRecord(registry, pluginName);
  await writeRegistry(homeDir, updated);

  return {
    exitCode: 0,
    output: `Removed ${pluginName} v${record.version}`,
  };
}
