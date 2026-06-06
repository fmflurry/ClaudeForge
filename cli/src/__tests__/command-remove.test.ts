/**
 * Tests for src/commands/remove.ts
 *
 * Production module path: src/commands/remove.ts
 * Exported functions:
 *   - runRemove(args: RemoveArgs, deps: RemoveDeps): Promise<CommandResult>
 *       args: { pluginName: string }
 *       deps: { homeDir: string; fs?: RemoveFsPort; env?: NodeJS.ProcessEnv }
 *   - RemoveFsPort: { rm(path: string): Promise<void>; exists(path: string): Promise<boolean> }
 *   - CommandResult: { exitCode: number; output: string }
 *
 * VERBATIM spec strings:
 *   - success: "Removed @namespace/plugin-name v1.2.3"
 *   - not installed: "Plugin @namespace/nonexistent is not installed"
 *   - suggest list: "claude plugin list"
 *   - non-zero exit on not installed
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

// These imports WILL FAIL until src/commands/remove.ts is created (RED state).
import { runRemove } from '../commands/remove.js';
import type { CommandResult, RemoveFsPort } from '../commands/remove.js';
import { writeRegistry, readRegistry } from '../registry/registry.js';
import type { InstalledRegistry } from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-remove-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

function makeFakeFs(overrides?: Partial<RemoveFsPort>): RemoveFsPort {
  return {
    rm: vi.fn().mockResolvedValue(undefined),
    exists: vi.fn().mockResolvedValue(true),
    ...overrides,
  };
}

function registryWith(name: string, version: string): InstalledRegistry {
  return {
    plugins: [{
      name,
      version,
      installedAt: '2024-01-01T00:00:00.000Z',
      path: `/tmp/plugins/${name}`,
    }],
  };
}

// ---------------------------------------------------------------------------
// runRemove – happy path
// ---------------------------------------------------------------------------

describe('runRemove – happy path', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await writeRegistry(homeDir, registryWith('@namespace/plugin-name', '1.2.3'));
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 on successful removal', async () => {
    const fakeFs = makeFakeFs();
    const result: CommandResult = await runRemove(
      { pluginName: '@namespace/plugin-name' },
      { homeDir, fs: fakeFs },
    );
    expect(result.exitCode).toBe(0);
  });

  it('output says "Removed @namespace/plugin-name v1.2.3"', async () => {
    const fakeFs = makeFakeFs();
    const result = await runRemove(
      { pluginName: '@namespace/plugin-name' },
      { homeDir, fs: fakeFs },
    );
    expect(result.output).toContain('Removed @namespace/plugin-name v1.2.3');
  });

  it('removes the entry from the installed registry', async () => {
    const fakeFs = makeFakeFs();
    await runRemove(
      { pluginName: '@namespace/plugin-name' },
      { homeDir, fs: fakeFs },
    );
    const registry = await readRegistry(homeDir);
    expect(registry.plugins).toHaveLength(0);
  });

  it('calls fs.rm to delete the plugin from disk', async () => {
    const rmFn = vi.fn().mockResolvedValue(undefined);
    const fakeFs = makeFakeFs({ rm: rmFn });
    await runRemove(
      { pluginName: '@namespace/plugin-name' },
      { homeDir, fs: fakeFs },
    );
    expect(rmFn).toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// runRemove – plugin not installed
// ---------------------------------------------------------------------------

describe('runRemove – plugin not installed', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    // Empty registry
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns non-zero exitCode when plugin is not in registry', async () => {
    const fakeFs = makeFakeFs();
    const result: CommandResult = await runRemove(
      { pluginName: '@namespace/nonexistent' },
      { homeDir, fs: fakeFs },
    );
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('output says "Plugin @namespace/nonexistent is not installed"', async () => {
    const fakeFs = makeFakeFs();
    const result = await runRemove(
      { pluginName: '@namespace/nonexistent' },
      { homeDir, fs: fakeFs },
    );
    expect(result.output).toContain('Plugin @namespace/nonexistent is not installed');
  });

  it('suggests "claude plugin list" when plugin not found', async () => {
    const fakeFs = makeFakeFs();
    const result = await runRemove(
      { pluginName: '@namespace/nonexistent' },
      { homeDir, fs: fakeFs },
    );
    expect(result.output).toContain('claude plugin list');
  });

  it('does not call fs.rm when plugin is not installed', async () => {
    const rmFn = vi.fn();
    const fakeFs = makeFakeFs({ rm: rmFn });
    await runRemove(
      { pluginName: '@namespace/nonexistent' },
      { homeDir, fs: fakeFs },
    );
    expect(rmFn).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// runRemove – partial failure (disk delete fails but registry is consistent)
// ---------------------------------------------------------------------------

describe('runRemove – disk delete failure', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await writeRegistry(homeDir, registryWith('@namespace/plugin-name', '1.2.3'));
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns non-zero exitCode when disk removal fails', async () => {
    const fakeFs = makeFakeFs({
      rm: vi.fn().mockRejectedValue(new Error('EACCES: permission denied')),
    });
    const result = await runRemove(
      { pluginName: '@namespace/plugin-name' },
      { homeDir, fs: fakeFs },
    );
    expect(result.exitCode).toBeGreaterThan(0);
  });
});
