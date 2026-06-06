/**
 * Tests for src/commands/list.ts
 *
 * Production module path: src/commands/list.ts
 * Exported functions:
 *   - runList(args: ListArgs, deps: ListDeps): Promise<CommandResult>
 *       args: { checkUpdates?: boolean }
 *       deps: { client: IMarketplaceClient; homeDir: string; env?: NodeJS.ProcessEnv }
 *   - CommandResult: { exitCode: number; output: string }
 *
 * VERBATIM spec strings:
 *   - empty: "No plugins installed"
 *   - table columns: Name, Version, Installed Date, Status
 *   - status values: "up-to-date" | "update-available"
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

// These imports WILL FAIL until src/commands/list.ts is created (RED state).
import { runList } from '../commands/list.js';
import type { CommandResult } from '../commands/list.js';
import type { IMarketplaceClient, VersionSummary } from '../api/client.js';
import { writeRegistry } from '../registry/registry.js';
import type { InstalledRegistry } from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-list-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

function makeFakeClient(overrides?: Partial<IMarketplaceClient>): IMarketplaceClient {
  return {
    searchPlugins: vi.fn(),
    getPlugin: vi.fn(),
    downloadPlugin: vi.fn(),
    uploadPlugin: vi.fn(),
    getLatestVersion: vi.fn(),
    checkVersionExists: vi.fn(),
    ...overrides,
  };
}

function makeLatestVersion(version: string): VersionSummary {
  return {
    version,
    releasedAt: '2024-01-01T00:00:00.000Z',
    downloadCount: 0,
    isLatest: true,
    packageFormat: 'tar.gz',
    sizeBytes: 1024,
  };
}

const INSTALLED_REGISTRY: InstalledRegistry = {
  plugins: [
    {
      name: '@auth/plugin',
      version: '1.0.0',
      installedAt: '2024-03-01T12:00:00.000Z',
      path: '/tmp/plugins/@auth/plugin',
    },
  ],
};

// ---------------------------------------------------------------------------
// runList – empty case
// ---------------------------------------------------------------------------

describe('runList – empty registry', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 when no plugins are installed', async () => {
    const client = makeFakeClient();
    const result: CommandResult = await runList({ }, { client, homeDir });
    expect(result.exitCode).toBe(0);
  });

  it('output contains "No plugins installed" verbatim', async () => {
    const client = makeFakeClient();
    const result = await runList({}, { client, homeDir });
    expect(result.output).toContain('No plugins installed');
  });
});

// ---------------------------------------------------------------------------
// runList – installed plugins
// ---------------------------------------------------------------------------

describe('runList – with installed plugins', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0', async () => {
    await writeRegistry(homeDir, INSTALLED_REGISTRY);
    const client = makeFakeClient();
    const result = await runList({}, { client, homeDir });
    expect(result.exitCode).toBe(0);
  });

  it('output contains table headers: Name, Version, Installed Date, Status', async () => {
    await writeRegistry(homeDir, INSTALLED_REGISTRY);
    const client = makeFakeClient();
    const result = await runList({}, { client, homeDir });
    expect(result.output).toContain('Name');
    expect(result.output).toContain('Version');
    expect(result.output).toContain('Installed Date');
    expect(result.output).toContain('Status');
  });

  it('output contains installed plugin name', async () => {
    await writeRegistry(homeDir, INSTALLED_REGISTRY);
    const client = makeFakeClient();
    const result = await runList({}, { client, homeDir });
    expect(result.output).toContain('@auth/plugin');
  });

  it('output contains installed version', async () => {
    await writeRegistry(homeDir, INSTALLED_REGISTRY);
    const client = makeFakeClient();
    const result = await runList({}, { client, homeDir });
    expect(result.output).toContain('1.0.0');
  });
});

// ---------------------------------------------------------------------------
// runList – --check-updates
// ---------------------------------------------------------------------------

describe('runList – --check-updates', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('calls getLatestVersion for each installed plugin when --check-updates is set', async () => {
    await writeRegistry(homeDir, INSTALLED_REGISTRY);
    const getLatest = vi.fn().mockResolvedValue(makeLatestVersion('1.0.0'));
    const client = makeFakeClient({ getLatestVersion: getLatest });
    await runList({ checkUpdates: true }, { client, homeDir });
    expect(getLatest).toHaveBeenCalled();
  });

  it('shows "update-available" status when marketplace has newer version', async () => {
    await writeRegistry(homeDir, INSTALLED_REGISTRY);
    const client = makeFakeClient({
      getLatestVersion: vi.fn().mockResolvedValue(makeLatestVersion('2.0.0')),
    });
    const result = await runList({ checkUpdates: true }, { client, homeDir });
    expect(result.output).toContain('update-available');
  });

  it('shows "up-to-date" status when already at latest', async () => {
    await writeRegistry(homeDir, INSTALLED_REGISTRY);
    const client = makeFakeClient({
      getLatestVersion: vi.fn().mockResolvedValue(makeLatestVersion('1.0.0')),
    });
    const result = await runList({ checkUpdates: true }, { client, homeDir });
    expect(result.output).toContain('up-to-date');
  });

  it('does not call getLatestVersion when --check-updates is not set', async () => {
    await writeRegistry(homeDir, INSTALLED_REGISTRY);
    const getLatest = vi.fn();
    const client = makeFakeClient({ getLatestVersion: getLatest });
    await runList({}, { client, homeDir });
    expect(getLatest).not.toHaveBeenCalled();
  });

  it('handles API error during update check gracefully (still lists plugins)', async () => {
    await writeRegistry(homeDir, INSTALLED_REGISTRY);
    const client = makeFakeClient({
      getLatestVersion: vi.fn().mockRejectedValue(new Error('Network error')),
    });
    const result = await runList({ checkUpdates: true }, { client, homeDir });
    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('@auth/plugin');
  });
});
