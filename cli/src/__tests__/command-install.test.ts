/**
 * Tests for src/commands/install.ts
 *
 * Production module path: src/commands/install.ts
 * Exported functions:
 *   - runInstall(args: InstallArgs, deps: InstallDeps): Promise<CommandResult>
 *       args: { pluginName: string; version?: string }
 *       deps: {
 *         client: IMarketplaceClient;
 *         homeDir: string;
 *         fs?: FsPort;       — injectable FS operations for testing
 *         env?: NodeJS.ProcessEnv;
 *       }
 *   - FsPort interface: {
 *       mkdir(dir: string): Promise<void>;
 *       writeStream(dest: string, stream: ReadableStream<Uint8Array>): Promise<void>;
 *       rm(path: string): Promise<void>;
 *     }
 *   - CommandResult: { exitCode: number; output: string }
 *
 * VERBATIM spec strings:
 *   - success: "Installed @namespace/plugin-name v1.2.3"  (includes entrypoints note)
 *   - network error: "Could not reach marketplace at https://..."
 *   - newer version available: "A newer version v1.3.0 is available"
 *   - already installed: "Plugin @namespace/plugin-name is already installed at v1.2.3"
 *
 * Key behavior: HALT before writing to registry on network error (non-zero exit)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

// These imports WILL FAIL until src/commands/install.ts is created (RED state).
import { runInstall } from '../commands/install.js';
import type { CommandResult, FsPort } from '../commands/install.js';
import type { IMarketplaceClient, PluginDetail, VersionSummary } from '../api/client.js';
import { readRegistry } from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-install-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

function makeVersion(overrides?: Partial<VersionSummary>): VersionSummary {
  return {
    version: '1.2.3',
    releasedAt: '2024-01-01T00:00:00.000Z',
    downloadCount: 42,
    isLatest: true,
    packageFormat: 'tar.gz',
    sizeBytes: 1024,
    ...overrides,
  };
}

function makePlugin(overrides?: Partial<PluginDetail>): PluginDetail {
  return {
    id: 'abc123-id',
    name: '@namespace/plugin-name',
    slug: 'namespace-plugin-name',
    description: 'A plugin',
    author: 'Test Author',
    downloadCount: 100,
    latestVersion: '1.2.3',
    allVersions: [makeVersion()],
    ...overrides,
  };
}

function makeFakeClient(overrides?: Partial<IMarketplaceClient>): IMarketplaceClient {
  return {
    searchPlugins: vi.fn(),
    getPlugin: vi.fn().mockResolvedValue(makePlugin()),
    downloadPlugin: vi.fn().mockResolvedValue(new ReadableStream()),
    uploadPlugin: vi.fn(),
    getLatestVersion: vi.fn().mockResolvedValue(makeVersion()),
    checkVersionExists: vi.fn().mockResolvedValue(false),
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
// Happy path: install by name
// ---------------------------------------------------------------------------

describe('runInstall – happy path', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 on successful install', async () => {
    const client = makeFakeClient();
    const fakeFs = makeFakeFs();
    const result: CommandResult = await runInstall(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    expect(result.exitCode).toBe(0);
  });

  it('output contains "Installed @namespace/plugin-name v1.2.3"', async () => {
    const client = makeFakeClient();
    const fakeFs = makeFakeFs();
    const result = await runInstall(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    expect(result.output).toContain('Installed @namespace/plugin-name v1.2.3');
  });

  it('records the plugin in the installed registry after success', async () => {
    const client = makeFakeClient();
    const fakeFs = makeFakeFs();
    await runInstall(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    const registry = await readRegistry(homeDir);
    const found = registry.plugins.find((p) => p.name === '@namespace/plugin-name');
    expect(found).toBeDefined();
    expect(found?.version).toBe('1.2.3');
  });

  it('calls downloadPlugin on the client', async () => {
    const downloadFn = vi.fn().mockResolvedValue(new ReadableStream());
    const client = makeFakeClient({ downloadPlugin: downloadFn });
    const fakeFs = makeFakeFs();
    await runInstall(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    expect(downloadFn).toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// Install specific version
// ---------------------------------------------------------------------------

describe('runInstall – specific version', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('installs the exact requested version', async () => {
    const plugin = makePlugin({ latestVersion: '2.0.0' });
    const client = makeFakeClient({
      getPlugin: vi.fn().mockResolvedValue(plugin),
      downloadPlugin: vi.fn().mockResolvedValue(new ReadableStream()),
    });
    const fakeFs = makeFakeFs();
    const result = await runInstall(
      { pluginName: '@namespace/plugin-name', version: '1.2.3' },
      { client, homeDir, fs: fakeFs },
    );
    expect(result.exitCode).toBe(0);
    const registry = await readRegistry(homeDir);
    const found = registry.plugins.find((p) => p.name === '@namespace/plugin-name');
    expect(found?.version).toBe('1.2.3');
  });

  it('warns when a newer version is available after specific-version install', async () => {
    const plugin = makePlugin({ latestVersion: '1.3.0' });
    const client = makeFakeClient({
      getPlugin: vi.fn().mockResolvedValue(plugin),
      downloadPlugin: vi.fn().mockResolvedValue(new ReadableStream()),
    });
    const fakeFs = makeFakeFs();
    const result = await runInstall(
      { pluginName: '@namespace/plugin-name', version: '1.2.3' },
      { client, homeDir, fs: fakeFs },
    );
    // Spec: "A newer version v1.3.0 is available"
    expect(result.output).toContain('A newer version v1.3.0 is available');
  });
});

// ---------------------------------------------------------------------------
// Network error — HALT before writing registry
// ---------------------------------------------------------------------------

describe('runInstall – network error', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns non-zero exitCode when getPlugin fails', async () => {
    const client = makeFakeClient({
      getPlugin: vi.fn().mockRejectedValue(new TypeError('fetch failed')),
    });
    const fakeFs = makeFakeFs();
    const result = await runInstall(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('does NOT write to the registry when getPlugin fails (halt before write)', async () => {
    const client = makeFakeClient({
      getPlugin: vi.fn().mockRejectedValue(new TypeError('fetch failed')),
    });
    const fakeFs = makeFakeFs();
    await runInstall(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    const registry = await readRegistry(homeDir);
    expect(registry.plugins).toHaveLength(0);
  });

  it('does NOT write to the registry when downloadPlugin fails', async () => {
    const client = makeFakeClient({
      getPlugin: vi.fn().mockResolvedValue(makePlugin()),
      downloadPlugin: vi.fn().mockRejectedValue(new TypeError('download failed')),
    });
    const fakeFs = makeFakeFs();
    await runInstall(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    const registry = await readRegistry(homeDir);
    expect(registry.plugins).toHaveLength(0);
  });

  it('output mentions "Could not reach marketplace" on network error', async () => {
    const client = makeFakeClient({
      getPlugin: vi.fn().mockRejectedValue(new TypeError('fetch failed')),
    });
    const fakeFs = makeFakeFs();
    const result = await runInstall(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    expect(result.output.toLowerCase()).toContain('could not reach marketplace');
  });

  it('suggests retry or manual config when network fails', async () => {
    const client = makeFakeClient({
      getPlugin: vi.fn().mockRejectedValue(new TypeError('fetch failed')),
    });
    const fakeFs = makeFakeFs();
    const result = await runInstall(
      { pluginName: '@namespace/plugin-name' },
      { client, homeDir, fs: fakeFs },
    );
    // Spec says: "Suggest retry or manual configuration of API URL"
    expect(result.output).toMatch(/retry|api.url|config/i);
  });
});
