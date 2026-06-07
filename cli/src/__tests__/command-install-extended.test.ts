/**
 * Extended tests for src/commands/install.ts
 *
 * Covers the previously uncovered branches:
 *   - Path traversal rejection (absolute paths, "..", null bytes)
 *   - Plugin name that escapes the plugins root (path.sep edge)
 *   - latestVersion = null → falls back to '0.0.0'
 *   - Version pinned and matches latest → no "newer version" notice
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

import { runInstall } from '../commands/install.js';
import type { FsPort } from '../commands/install.js';
import type { IMarketplaceClient, PluginDetail } from '../api/client.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-install-ext-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

function makePlugin(overrides?: Partial<PluginDetail>): PluginDetail {
  return {
    id: 'abc123',
    name: 'safe-plugin',
    slug: 'safe-plugin',
    description: 'A plugin',
    author: 'Author',
    downloadCount: 0,
    latestVersion: '1.0.0',
    allVersions: [],
    ...overrides,
  };
}

function makeFakeClient(overrides?: Partial<IMarketplaceClient>): IMarketplaceClient {
  return {
    searchPlugins: vi.fn(),
    getPlugin: vi.fn().mockResolvedValue(makePlugin()),
    downloadPlugin: vi.fn().mockResolvedValue(new ReadableStream()),
    uploadPlugin: vi.fn(),
    getLatestVersion: vi.fn(),
    checkVersionExists: vi.fn(),
    ...overrides,
  };
}

function makeFakeFs(overrides?: Partial<FsPort>): FsPort {
  return {
    mkdir: vi.fn().mockResolvedValue(undefined),
    writeStream: vi.fn().mockResolvedValue(undefined),
    rm: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Path traversal rejection
// ---------------------------------------------------------------------------

describe('runInstall – path traversal rejection', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('rejects absolute plugin names (unix-style)', async () => {
    const client = makeFakeClient();
    const fakeFs = makeFakeFs();
    const result = await runInstall({ pluginName: '/etc/passwd' }, { client, homeDir, fs: fakeFs });
    expect(result.exitCode).toBe(1);
    expect(result.output.toLowerCase()).toMatch(/invalid plugin name|unsafe/i);
  });

  it('rejects plugin names containing ".." segments', async () => {
    const client = makeFakeClient();
    const fakeFs = makeFakeFs();
    const result = await runInstall({ pluginName: '../../../etc/shadow' }, { client, homeDir, fs: fakeFs });
    expect(result.exitCode).toBe(1);
    expect(result.output.toLowerCase()).toMatch(/invalid plugin name|unsafe/i);
  });

  it('rejects plugin names containing null bytes', async () => {
    const client = makeFakeClient();
    const fakeFs = makeFakeFs();
    const result = await runInstall({ pluginName: 'safe\x00evil' }, { client, homeDir, fs: fakeFs });
    expect(result.exitCode).toBe(1);
    expect(result.output.toLowerCase()).toMatch(/invalid plugin name|unsafe/i);
  });

  it('does not call mkdir when a path traversal is detected', async () => {
    const mkdirFn = vi.fn().mockResolvedValue(undefined);
    const client = makeFakeClient();
    const fakeFs = makeFakeFs({ mkdir: mkdirFn });
    await runInstall({ pluginName: '../escape' }, { client, homeDir, fs: fakeFs });
    expect(mkdirFn).not.toHaveBeenCalled();
  });

  it('does not write to registry when a path traversal is detected', async () => {
    const { readRegistry } = await import('../registry/registry.js');
    const client = makeFakeClient();
    const fakeFs = makeFakeFs();
    await runInstall({ pluginName: '../escape' }, { client, homeDir, fs: fakeFs });
    const registry = await readRegistry(homeDir);
    expect(registry.plugins).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// latestVersion = null → fallback to '0.0.0'
// ---------------------------------------------------------------------------

describe('runInstall – latestVersion null fallback', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('installs v0.0.0 when plugin latestVersion is null and no version specified', async () => {
    const plugin = makePlugin({ latestVersion: null });
    const client = makeFakeClient({
      getPlugin: vi.fn().mockResolvedValue(plugin),
      downloadPlugin: vi.fn().mockResolvedValue(new ReadableStream()),
    });
    const fakeFs = makeFakeFs();
    const result = await runInstall({ pluginName: 'safe-plugin' }, { client, homeDir, fs: fakeFs });
    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('0.0.0');
  });
});

// ---------------------------------------------------------------------------
// Version pinned and matches latest → no "newer version" notice
// ---------------------------------------------------------------------------

describe('runInstall – version matches latest, no notice', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('does not show "newer version available" when installed version matches latest', async () => {
    const plugin = makePlugin({ latestVersion: '1.0.0' });
    const client = makeFakeClient({
      getPlugin: vi.fn().mockResolvedValue(plugin),
      downloadPlugin: vi.fn().mockResolvedValue(new ReadableStream()),
    });
    const fakeFs = makeFakeFs();
    const result = await runInstall({ pluginName: 'safe-plugin', version: '1.0.0' }, { client, homeDir, fs: fakeFs });
    expect(result.output).not.toContain('A newer version');
  });

  it('does not show "newer version available" when no specific version is requested', async () => {
    const plugin = makePlugin({ latestVersion: '2.0.0' });
    const client = makeFakeClient({
      getPlugin: vi.fn().mockResolvedValue(plugin),
      downloadPlugin: vi.fn().mockResolvedValue(new ReadableStream()),
    });
    const fakeFs = makeFakeFs();
    const result = await runInstall({ pluginName: 'safe-plugin' }, { client, homeDir, fs: fakeFs });
    // No `version` arg → installs latest; no "newer version" notice
    expect(result.output).not.toContain('A newer version');
  });
});

// ---------------------------------------------------------------------------
// Path escapes plugins directory check (defense-in-depth)
// This tests the secondary check after the explicit ".." / absolute guard.
// On most OSes a name like "." can resolve to a path that doesn't start with
// pluginsRoot + sep — verifying the contains check catches it.
// ---------------------------------------------------------------------------

describe('runInstall – path escapes plugins directory', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('rejects a plugin named "." which resolves to pluginsRoot itself', async () => {
    // path.resolve(pluginsRoot, ".") === pluginsRoot (no trailing sep)
    // so pluginDir.startsWith(pluginsRoot + sep) is false → rejected
    const client = makeFakeClient();
    const fakeFs = makeFakeFs();
    const result = await runInstall({ pluginName: '.' }, { client, homeDir, fs: fakeFs });
    // Either rejected by the explicit checks or the startsWith check
    expect(result.exitCode).toBe(1);
  });
});

// ---------------------------------------------------------------------------
// mkdir + writeStream called with expected paths
// ---------------------------------------------------------------------------

describe('runInstall – fs port called with correct paths', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('calls mkdir with a path inside the plugins root', async () => {
    const mkdirFn = vi.fn().mockResolvedValue(undefined);
    const client = makeFakeClient();
    const fakeFs = makeFakeFs({ mkdir: mkdirFn });
    await runInstall({ pluginName: 'safe-plugin' }, { client, homeDir, fs: fakeFs });
    expect(mkdirFn).toHaveBeenCalled();
    const [dirArg] = mkdirFn.mock.calls[0] as [string];
    expect(dirArg).toContain('plugins');
    expect(dirArg).toContain('safe-plugin');
  });

  it('calls writeStream with package.tar.gz filename', async () => {
    const writeStreamFn = vi.fn().mockResolvedValue(undefined);
    const client = makeFakeClient();
    const fakeFs = makeFakeFs({ writeStream: writeStreamFn });
    await runInstall({ pluginName: 'safe-plugin' }, { client, homeDir, fs: fakeFs });
    expect(writeStreamFn).toHaveBeenCalled();
    const [destArg] = writeStreamFn.mock.calls[0] as [string, ReadableStream<Uint8Array>];
    expect(destArg).toContain('package.tar.gz');
  });
});
