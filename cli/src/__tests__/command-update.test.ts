/**
 * Tests for src/commands/update.ts
 *
 * Production module path: src/commands/update.ts
 * Exported functions:
 *   - runUpdate(args: UpdateArgs, deps: UpdateDeps): Promise<CommandResult>
 *       args: { pluginName: string }
 *       deps: {
 *         client: IMarketplaceClient;
 *         homeDir: string;
 *         fs?: FsPort;
 *         env?: NodeJS.ProcessEnv;
 *       }
 *   - FsPort (same as install): { mkdir, writeStream, rm, copyDir, exists }
 *       + copyDir(src: string, dest: string): Promise<void>  — for backup
 *       + exists(p: string): Promise<boolean>
 *   - CommandResult: { exitCode: number; output: string }
 *
 * VERBATIM spec strings:
 *   - already up to date: "Plugin is already up-to-date at v1.5.0"
 *   - updated: "Updated @namespace/plugin-name from v1.2.3 to v1.5.0"
 *   - rollback: mentions backup/rollback on failure
 *   - dependency conflict: "v2.0.0 requires framework-x >= 3.0, but you have 2.5"
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

// These imports WILL FAIL until src/commands/update.ts is created (RED state).
import { runUpdate } from '../commands/update.js';
import type { CommandResult, UpdateFsPort } from '../commands/update.js';
import type { IMarketplaceClient, VersionSummary } from '../api/client.js';
import { writeRegistry } from '../registry/registry.js';
import type { InstalledRegistry } from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-update-test-'));
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
    getLatestVersion: vi.fn().mockResolvedValue(makeVersion('1.5.0')),
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
    plugins: [{
      name,
      version,
      installedAt: '2024-01-01T00:00:00.000Z',
      path: `/tmp/plugins/${name}`,
    }],
  };
}

// ---------------------------------------------------------------------------
// runUpdate – already up-to-date
// ---------------------------------------------------------------------------

describe('runUpdate – already up-to-date', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await writeRegistry(homeDir, registryWithPlugin('@namespace/plugin-name', '1.5.0'));
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 when already at latest', async () => {
    const client = makeFakeClient({ getLatestVersion: vi.fn().mockResolvedValue(makeVersion('1.5.0')) });
    const result: CommandResult = await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: makeFakeFs() },
    );
    expect(result.exitCode).toBe(0);
  });

  it('output says "Plugin is already up-to-date at v1.5.0"', async () => {
    const client = makeFakeClient({ getLatestVersion: vi.fn().mockResolvedValue(makeVersion('1.5.0')) });
    const result = await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: makeFakeFs() },
    );
    expect(result.output).toContain('Plugin is already up-to-date at v1.5.0');
  });

  it('does not call downloadPlugin when already up-to-date', async () => {
    const downloadFn = vi.fn();
    const client = makeFakeClient({
      getLatestVersion: vi.fn().mockResolvedValue(makeVersion('1.5.0')),
      downloadPlugin: downloadFn,
    });
    await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: makeFakeFs() },
    );
    expect(downloadFn).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// runUpdate – successful update
// ---------------------------------------------------------------------------

describe('runUpdate – successful update', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await writeRegistry(homeDir, registryWithPlugin('@namespace/plugin-name', '1.2.3'));
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0', async () => {
    const client = makeFakeClient();
    const result = await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: makeFakeFs() },
    );
    expect(result.exitCode).toBe(0);
  });

  it('output says "Updated @namespace/plugin-name from v1.2.3 to v1.5.0"', async () => {
    const client = makeFakeClient();
    const result = await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: makeFakeFs() },
    );
    expect(result.output).toContain('Updated @namespace/plugin-name from v1.2.3 to v1.5.0');
  });

  it('creates a backup before extracting new version', async () => {
    const copyDir = vi.fn().mockResolvedValue(undefined);
    const fakeFs = makeFakeFs({ copyDir });
    const client = makeFakeClient();
    await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    expect(copyDir).toHaveBeenCalled();
  });

  it('updates the registry with the new version after success', async () => {
    const { readRegistry } = await import('../registry/registry.js');
    const client = makeFakeClient();
    await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: makeFakeFs() },
    );
    const reg = await readRegistry(homeDir);
    const found = reg.plugins.find((p) => p.name === '@namespace/plugin-name');
    expect(found?.version).toBe('1.5.0');
  });
});

// ---------------------------------------------------------------------------
// runUpdate – rollback on extraction failure
// ---------------------------------------------------------------------------

describe('runUpdate – rollback on extraction failure', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await writeRegistry(homeDir, registryWithPlugin('@namespace/plugin-name', '1.2.3'));
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns non-zero exitCode when writeStream fails', async () => {
    const fakeFs = makeFakeFs({
      writeStream: vi.fn().mockRejectedValue(new Error('extraction failed')),
    });
    const client = makeFakeClient();
    const result = await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('output mentions backup/rollback on extraction failure', async () => {
    const fakeFs = makeFakeFs({
      writeStream: vi.fn().mockRejectedValue(new Error('extraction failed')),
    });
    const client = makeFakeClient();
    const result = await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    expect(result.output.toLowerCase()).toMatch(/backup|rollback|previous version/);
  });

  it('retains the old version in the registry after rollback', async () => {
    const { readRegistry } = await import('../registry/registry.js');
    const fakeFs = makeFakeFs({
      writeStream: vi.fn().mockRejectedValue(new Error('extraction failed')),
    });
    const client = makeFakeClient();
    await runUpdate(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    const reg = await readRegistry(homeDir);
    const found = reg.plugins.find((p) => p.name === '@namespace/plugin-name');
    expect(found?.version).toBe('1.2.3');
  });
});

// ---------------------------------------------------------------------------
// runUpdate – plugin not installed
// ---------------------------------------------------------------------------

describe('runUpdate – plugin not installed', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    // Empty registry
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns non-zero exitCode when plugin is not installed', async () => {
    const client = makeFakeClient();
    const result = await runUpdate(
      { pluginName: '@namespace/nonexistent' },
      { client, homeDir, fs: makeFakeFs() },
    );
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('output mentions the plugin is not installed', async () => {
    const client = makeFakeClient();
    const result = await runUpdate(
      { pluginName: '@namespace/nonexistent' },
      { client, homeDir, fs: makeFakeFs() },
    );
    expect(result.output).toContain('@namespace/nonexistent');
    expect(result.output.toLowerCase()).toContain('not installed');
  });
});
