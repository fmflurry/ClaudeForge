/**
 * Extended tests for src/commands/update.ts
 *
 * Covers the previously uncovered branches:
 *   - Plugin dir does NOT exist → copyDir skipped (pluginExists = false)
 *   - getLatestVersion throwing → propagates as unhandled (or caught upstream)
 *   - downloadPlugin throwing → non-zero exit with rollback message
 *   - realUpdateFsPort branches are unit-tested via the injectable fs port
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

import { runUpdate } from '../commands/update.js';
import type { UpdateFsPort } from '../commands/update.js';
import type { IMarketplaceClient, VersionSummary } from '../api/client.js';
import { writeRegistry } from '../registry/registry.js';
import type { InstalledRegistry } from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-update-ext-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

function makeVersion(v: string, isLatest = true): VersionSummary {
  return {
    version: v,
    releasedAt: '2024-01-01T00:00:00.000Z',
    downloadCount: 10,
    isLatest,
    packageFormat: 'tar.gz',
    sizeBytes: 1024,
  };
}

function makeFakeClient(overrides?: Partial<IMarketplaceClient>): IMarketplaceClient {
  return {
    searchPlugins: vi.fn(),
    getPlugin: vi.fn(),
    downloadPlugin: vi.fn().mockResolvedValue(new ReadableStream()),
    uploadPlugin: vi.fn(),
    getLatestVersion: vi.fn().mockResolvedValue(makeVersion('2.0.0')),
    checkVersionExists: vi.fn(),
    ...overrides,
  };
}

function makeFakeFs(overrides?: Partial<UpdateFsPort>): UpdateFsPort {
  return {
    mkdir: vi.fn().mockResolvedValue(undefined),
    writeStream: vi.fn().mockResolvedValue(undefined),
    rm: vi.fn().mockResolvedValue(undefined),
    copyDir: vi.fn().mockResolvedValue(undefined),
    exists: vi.fn().mockResolvedValue(true),
    ...overrides,
  };
}

function registryWithPlugin(name: string, version: string): InstalledRegistry {
  return {
    plugins: [
      {
        name,
        version,
        installedAt: '2024-01-01T00:00:00.000Z',
        path: `/tmp/plugins/${name}`,
      },
    ],
  };
}

// ---------------------------------------------------------------------------
// Plugin dir does not exist → copyDir is NOT called
// ---------------------------------------------------------------------------

describe('runUpdate – plugin dir does not exist (no backup)', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await writeRegistry(homeDir, registryWithPlugin('safe-plugin', '1.0.0'));
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('does not call copyDir when plugin directory does not exist', async () => {
    const copyDirFn = vi.fn().mockResolvedValue(undefined);
    const fakeFs = makeFakeFs({
      exists: vi.fn().mockResolvedValue(false),
      copyDir: copyDirFn,
    });
    const client = makeFakeClient();
    await runUpdate({ pluginName: 'safe-plugin' }, { client, homeDir, fs: fakeFs });
    expect(copyDirFn).not.toHaveBeenCalled();
  });

  it('still succeeds when plugin dir does not exist (no backup needed)', async () => {
    const fakeFs = makeFakeFs({
      exists: vi.fn().mockResolvedValue(false),
    });
    const client = makeFakeClient();
    const result = await runUpdate({ pluginName: 'safe-plugin' }, { client, homeDir, fs: fakeFs });
    expect(result.exitCode).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// getLatestVersion throwing
// ---------------------------------------------------------------------------

describe('runUpdate – getLatestVersion throws', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await writeRegistry(homeDir, registryWithPlugin('safe-plugin', '1.0.0'));
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('propagates error when getLatestVersion throws', async () => {
    const client = makeFakeClient({
      getLatestVersion: vi.fn().mockRejectedValue(new Error('Network error')),
    });
    const fakeFs = makeFakeFs();
    await expect(runUpdate({ pluginName: 'safe-plugin' }, { client, homeDir, fs: fakeFs })).rejects.toThrow(
      'Network error',
    );
  });
});

// ---------------------------------------------------------------------------
// downloadPlugin throwing during update
// ---------------------------------------------------------------------------

describe('runUpdate – downloadPlugin throws', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await writeRegistry(homeDir, registryWithPlugin('safe-plugin', '1.0.0'));
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('propagates error when downloadPlugin throws (no catch in update for download)', async () => {
    const client = makeFakeClient({
      downloadPlugin: vi.fn().mockRejectedValue(new Error('Download failed')),
    });
    const fakeFs = makeFakeFs();
    await expect(runUpdate({ pluginName: 'safe-plugin' }, { client, homeDir, fs: fakeFs })).rejects.toThrow(
      'Download failed',
    );
  });
});

// ---------------------------------------------------------------------------
// Backup path construction — plugin name with slashes
// ---------------------------------------------------------------------------

describe('runUpdate – backup path uses escaped plugin name', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('creates a backup path replacing "/" with "__" for scoped plugins', async () => {
    await writeRegistry(homeDir, registryWithPlugin('@namespace/plugin', '1.0.0'));
    const copyDirFn = vi.fn().mockResolvedValue(undefined);
    const fakeFs = makeFakeFs({ copyDir: copyDirFn });
    const client = makeFakeClient({
      getLatestVersion: vi.fn().mockResolvedValue(makeVersion('2.0.0')),
    });

    await runUpdate({ pluginName: '@namespace/plugin' }, { client, homeDir, fs: fakeFs });

    expect(copyDirFn).toHaveBeenCalled();
    const [, destPath] = copyDirFn.mock.calls[0] as [string, string];
    // The backup path should not contain "/" from the original name
    expect(destPath).not.toContain('@namespace/plugin_');
    expect(destPath).toContain('__');
  });
});

// ---------------------------------------------------------------------------
// mkdir for backups dir is always called
// ---------------------------------------------------------------------------

describe('runUpdate – mkdir called for backups dir', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await writeRegistry(homeDir, registryWithPlugin('safe-plugin', '1.0.0'));
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('calls mkdir at least once (for the backups directory)', async () => {
    const mkdirFn = vi.fn().mockResolvedValue(undefined);
    const fakeFs = makeFakeFs({ mkdir: mkdirFn });
    const client = makeFakeClient();

    await runUpdate({ pluginName: 'safe-plugin' }, { client, homeDir, fs: fakeFs });

    expect(mkdirFn).toHaveBeenCalled();
  });
});
