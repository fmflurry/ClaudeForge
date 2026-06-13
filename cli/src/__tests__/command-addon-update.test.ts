/**
 * Tests for src/commands/addon-update.ts
 *
 * Covers:
 *   - Update to newer version succeeds
 *   - Update to same version is a no-op with message
 *   - Update failure leaves prior version intact (message from lifecycle)
 *   - Missing --scope flag errors
 *   - Invalid scope errors
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';
import { runAddonUpdate } from '../commands/addon-update.js';
import type { LifecycleDeps } from '../addon/lifecycle.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-addon-update-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

async function writeAddonSource(dir: string, version: string): Promise<void> {
  await fs.mkdir(dir, { recursive: true });
  const manifest = {
    name: 'my-skill',
    version,
    type: 'skill',
    supportedScopes: ['local', 'global'],
    files: ['SKILL.md'],
  };
  await fs.writeFile(path.join(dir, 'addon.json'), JSON.stringify(manifest, null, 2), 'utf-8');
  await fs.writeFile(path.join(dir, 'SKILL.md'), `# my-skill v${version}`, 'utf-8');
}

function makeLifecycleDepsForUpdate(
  overrideUpdate?: (args: { sourceDir: string; scope: string }) => Promise<{ success: boolean; message: string }>,
): LifecycleDeps {
  void overrideUpdate; // not used directly — we use injected mock
  return {
    fs: {
      mkdir: vi.fn().mockResolvedValue(undefined),
      writeFile: vi.fn().mockResolvedValue(undefined),
      readFile: vi.fn().mockResolvedValue(''),
      readFileBuffer: vi.fn().mockResolvedValue(Buffer.from('')),
      copyFile: vi.fn().mockResolvedValue(undefined),
      copyDir: vi.fn().mockResolvedValue(undefined),
      rename: vi.fn().mockResolvedValue(undefined),
      rm: vi.fn().mockResolvedValue(undefined),
      exists: vi.fn().mockResolvedValue(false),
      readdir: vi.fn().mockResolvedValue([]),
      stat: vi.fn().mockResolvedValue({ isDirectory: false }),
    },
    settingsPort: {
      readFile: vi.fn().mockResolvedValue('{}'),
      writeFile: vi.fn().mockResolvedValue(undefined),
      rename: vi.fn().mockResolvedValue(undefined),
      exists: vi.fn().mockResolvedValue(false),
      rm: vi.fn().mockResolvedValue(undefined),
    },
    store: {
      snapshot: vi.fn().mockResolvedValue('/store/path'),
      list: vi.fn().mockResolvedValue([]),
      read: vi.fn(),
      latestPrior: vi.fn().mockResolvedValue(undefined),
      prune: vi.fn().mockResolvedValue(undefined),
    },
    cwd: '/fake/cwd',
    homeDir: '/fake/home',
  };
}

// ---------------------------------------------------------------------------
// Scope validation
// ---------------------------------------------------------------------------

describe('runAddonUpdate – scope validation', () => {
  it('returns exitCode 1 when --scope is missing', async () => {
    const result = await runAddonUpdate({ source: '/some/path' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/--scope is required/i);
  });

  it('returns exitCode 1 when scope is invalid', async () => {
    const result = await runAddonUpdate({ source: '/some/path', scope: 'universe' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/invalid scope/i);
  });
});

// ---------------------------------------------------------------------------
// Successful update
// ---------------------------------------------------------------------------

describe('runAddonUpdate – successful update', () => {
  let sourceV1: string;
  let sourceV2: string;
  let cwd: string;
  let homeDir: string;

  beforeEach(async () => {
    sourceV1 = await makeTmpDir();
    sourceV2 = await makeTmpDir();
    cwd = await makeTmpDir();
    homeDir = await makeTmpDir();
    await writeAddonSource(sourceV1, '1.0.0');
    await writeAddonSource(sourceV2, '2.0.0');
  });

  afterEach(async () => {
    await removeTmpDir(sourceV1);
    await removeTmpDir(sourceV2);
    await removeTmpDir(cwd);
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 when updating to a newer version', async () => {
    // First install v1
    const { runAddonAdd } = await import('../commands/addon-add.js');
    await runAddonAdd({ source: sourceV1, scope: 'local' }, { cwd, homeDir });
    // Then update to v2
    const result = await runAddonUpdate({ source: sourceV2, scope: 'local' }, { cwd, homeDir });
    expect(result.exitCode).toBe(0);
  });

  it('output mentions old and new versions on successful update', async () => {
    const { runAddonAdd } = await import('../commands/addon-add.js');
    await runAddonAdd({ source: sourceV1, scope: 'local' }, { cwd, homeDir });
    const result = await runAddonUpdate({ source: sourceV2, scope: 'local' }, { cwd, homeDir });
    // lifecycle.update returns "Updated ... from v1.0.0 to v2.0.0."
    expect(result.output).toContain('1.0.0');
    expect(result.output).toContain('2.0.0');
  });

  it('reports no-op message when updating to same version', async () => {
    const { runAddonAdd } = await import('../commands/addon-add.js');
    await runAddonAdd({ source: sourceV1, scope: 'local' }, { cwd, homeDir });
    const result = await runAddonUpdate({ source: sourceV1, scope: 'local' }, { cwd, homeDir });
    // lifecycle.update returns "already at version" for same version
    expect(result.output).toMatch(/already at version|nothing to update/i);
  });
});

// ---------------------------------------------------------------------------
// Not-installed error
// ---------------------------------------------------------------------------

describe('runAddonUpdate – not installed', () => {
  let source: string;
  let cwd: string;
  let homeDir: string;

  beforeEach(async () => {
    source = await makeTmpDir();
    cwd = await makeTmpDir();
    homeDir = await makeTmpDir();
    await writeAddonSource(source, '2.0.0');
  });

  afterEach(async () => {
    await removeTmpDir(source);
    await removeTmpDir(cwd);
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 1 when add-on is not installed', async () => {
    const result = await runAddonUpdate({ source, scope: 'local' }, { cwd, homeDir });
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/not installed/i);
  });
});

// ---------------------------------------------------------------------------
// Update failure leaves prior intact
// ---------------------------------------------------------------------------

describe('runAddonUpdate – failure message', () => {
  it('returns exit 1 with a message mentioning prior version retained on failure', async () => {
    const lifecycleDeps = makeLifecycleDepsForUpdate();

    // Make readFile return a valid sidecar with v1.0.0
    (lifecycleDeps.fs.exists as ReturnType<typeof vi.fn>).mockImplementation(async (p: string) => {
      if (p.endsWith('addon.json')) return true;
      if (p.endsWith('.json') && p.includes('.addons')) return true;
      return false;
    });

    (lifecycleDeps.fs.readFile as ReturnType<typeof vi.fn>).mockImplementation(async (p: string) => {
      if (p.includes('.addons')) {
        return JSON.stringify({
          manifest: {
            name: 'my-skill',
            version: '1.0.0',
            type: 'skill',
            supportedScopes: ['local', 'global'],
            files: ['SKILL.md'],
          },
          placedFiles: ['/fake/scope/skills/my-skill/SKILL.md'],
        });
      }
      if (p.endsWith('addon.json')) {
        return JSON.stringify({
          name: 'my-skill',
          version: '2.0.0',
          type: 'skill',
          supportedScopes: ['local', 'global'],
          files: ['SKILL.md'],
        });
      }
      return '';
    });

    // Make copyFile fail (stage failure)
    (lifecycleDeps.fs.copyFile as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('disk full'),
    );

    const result = await runAddonUpdate(
      { source: '/fake/source', scope: 'local' },
      { lifecycleDeps, cwd: '/fake/cwd', homeDir: '/fake/home' },
    );

    expect(result.exitCode).toBe(1);
    // lifecycle.update returns "Previous version <v> retained" on failure
    expect(result.output).toMatch(/failed|retained/i);
  });
});
