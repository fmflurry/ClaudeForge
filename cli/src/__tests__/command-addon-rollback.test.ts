/**
 * Tests for src/commands/addon-rollback.ts
 *
 * Covers:
 *   - Default rollback to latest prior version
 *   - --to specific version
 *   - Missing version error (no stored versions)
 *   - --type and --scope required
 *   - Reversible: rollback can be rolled back again
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';
import { runAddonRollback } from '../commands/addon-rollback.js';
import type { AddonRollbackDeps } from '../commands/addon-rollback.js';
import type { LifecycleDeps } from '../addon/lifecycle.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-addon-rollback-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

async function writeAddonSource(dir: string, name: string, version: string, type = 'skill'): Promise<void> {
  const manifest: Record<string, unknown> = {
    name,
    version,
    type,
    supportedScopes: ['local', 'global'],
    files: ['SKILL.md'],
  };
  await fs.mkdir(dir, { recursive: true });
  await fs.writeFile(path.join(dir, 'addon.json'), JSON.stringify(manifest, null, 2), 'utf-8');
  await fs.writeFile(path.join(dir, 'SKILL.md'), `# ${name} v${version}`, 'utf-8');
}

// ---------------------------------------------------------------------------
// --type and --scope required
// ---------------------------------------------------------------------------

describe('runAddonRollback – required flags', () => {
  it('returns exitCode 1 when --type is missing', async () => {
    const result = await runAddonRollback({ name: 'my-skill', scope: 'local' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/--type is required/i);
  });

  it('returns exitCode 1 for invalid --type', async () => {
    const result = await runAddonRollback({ name: 'my-skill', type: 'gadget', scope: 'local' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/invalid.*type/i);
  });

  it('returns exitCode 1 when --scope is missing', async () => {
    const result = await runAddonRollback({ name: 'my-skill', type: 'skill' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/--scope is required/i);
  });

  it('returns exitCode 1 for invalid --scope', async () => {
    const result = await runAddonRollback({ name: 'my-skill', type: 'skill', scope: 'nowhere' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/invalid scope/i);
  });
});

// ---------------------------------------------------------------------------
// No stored versions (missing version error)
// ---------------------------------------------------------------------------

describe('runAddonRollback – missing version', () => {
  let cwd: string;
  let homeDir: string;

  beforeEach(async () => {
    cwd = await makeTmpDir();
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(cwd);
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 1 when addon is not installed', async () => {
    const result = await runAddonRollback(
      { name: 'ghost-skill', type: 'skill', scope: 'local' },
      { cwd, homeDir },
    );
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/not installed/i);
  });

  it('returns exitCode 1 and clear error when no prior versions exist', async () => {
    const sourceDir = await makeTmpDir();
    try {
      await writeAddonSource(sourceDir, 'fresh-skill', '1.0.0');
      // Install once — no prior versions in store
      const { runAddonAdd } = await import('../commands/addon-add.js');
      await runAddonAdd({ source: sourceDir, scope: 'local' }, { cwd, homeDir });

      const result = await runAddonRollback(
        { name: 'fresh-skill', type: 'skill', scope: 'local' },
        { cwd, homeDir },
      );

      expect(result.exitCode).toBe(1);
      expect(result.output).toMatch(/no stored versions|roll back to/i);
    } finally {
      await removeTmpDir(sourceDir);
    }
  });
});

// ---------------------------------------------------------------------------
// Injected lifecycle deps — unit-level tests
// ---------------------------------------------------------------------------

describe('runAddonRollback – injected lifecycle deps', () => {
  function makeRollbackDeps(): LifecycleDeps {
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
        exists: vi.fn().mockImplementation(async (p: string) => {
          // Sidecar exists
          return p.endsWith('.json') && p.includes('.addons');
        }),
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
        list: vi.fn().mockResolvedValue(['0.9.0', '1.0.0']),
        read: vi.fn().mockImplementation(async ({ version }: { version: string }) => ({
          manifest: {
            name: 'my-skill',
            version,
            type: 'skill',
            supportedScopes: ['local', 'global'],
            files: ['SKILL.md'],
          },
          version,
          files: ['SKILL.md'],
          path: `/fake/store/local/skill/my-skill/${version}`,
        })),
        latestPrior: vi.fn().mockResolvedValue('1.0.0'),
        prune: vi.fn().mockResolvedValue(undefined),
      },
      cwd: '/fake/cwd',
      homeDir: '/fake/home',
    };
  }

  it('returns exitCode 0 on successful rollback (injected deps)', async () => {
    const lifecycleDeps = makeRollbackDeps();

    // Provide a sidecar for the installed addon
    (lifecycleDeps.fs.readFile as ReturnType<typeof vi.fn>).mockImplementation(async (p: string) => {
      if (p.includes('.addons')) {
        return JSON.stringify({
          manifest: {
            name: 'my-skill',
            version: '2.0.0',
            type: 'skill',
            supportedScopes: ['local', 'global'],
            files: ['SKILL.md'],
          },
          placedFiles: ['/fake/cwd/.claude/skills/my-skill/SKILL.md'],
        });
      }
      return '';
    });

    const deps: AddonRollbackDeps = {
      lifecycleDeps,
      cwd: '/fake/cwd',
      homeDir: '/fake/home',
    };

    const result = await runAddonRollback(
      { name: 'my-skill', type: 'skill', scope: 'local' },
      deps,
    );

    expect(result.exitCode).toBe(0);
    expect(result.output).toMatch(/rolled back|my-skill/i);
  });

  it('returns exitCode 0 with --to specific version', async () => {
    const lifecycleDeps = makeRollbackDeps();

    (lifecycleDeps.fs.readFile as ReturnType<typeof vi.fn>).mockImplementation(async (p: string) => {
      if (p.includes('.addons')) {
        return JSON.stringify({
          manifest: {
            name: 'my-skill',
            version: '2.0.0',
            type: 'skill',
            supportedScopes: ['local', 'global'],
            files: ['SKILL.md'],
          },
          placedFiles: ['/fake/cwd/.claude/skills/my-skill/SKILL.md'],
        });
      }
      return '';
    });

    const deps: AddonRollbackDeps = {
      lifecycleDeps,
      cwd: '/fake/cwd',
      homeDir: '/fake/home',
    };

    const result = await runAddonRollback(
      { name: 'my-skill', type: 'skill', scope: 'local', to: '0.9.0' },
      deps,
    );

    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('0.9.0');
  });

  it('returns exitCode 1 and clear error when --to version does not exist', async () => {
    const lifecycleDeps = makeRollbackDeps();

    (lifecycleDeps.store.read as ReturnType<typeof vi.fn>).mockRejectedValue(
      new Error('version not found'),
    );
    (lifecycleDeps.fs.readFile as ReturnType<typeof vi.fn>).mockImplementation(async (p: string) => {
      if (p.includes('.addons')) {
        return JSON.stringify({
          manifest: {
            name: 'my-skill',
            version: '2.0.0',
            type: 'skill',
            supportedScopes: ['local', 'global'],
            files: ['SKILL.md'],
          },
          placedFiles: ['/fake/cwd/.claude/skills/my-skill/SKILL.md'],
        });
      }
      return '';
    });

    const deps: AddonRollbackDeps = {
      lifecycleDeps,
      cwd: '/fake/cwd',
      homeDir: '/fake/home',
    };

    const result = await runAddonRollback(
      { name: 'my-skill', type: 'skill', scope: 'local', to: '99.0.0' },
      deps,
    );

    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/99\.0\.0|cannot roll back|not found|unchanged/i);
  });
});

// ---------------------------------------------------------------------------
// Reversible — rollback then rollback again
// ---------------------------------------------------------------------------

describe('runAddonRollback – reversible', () => {
  let cwd: string;
  let homeDir: string;

  beforeEach(async () => {
    cwd = await makeTmpDir();
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(cwd);
    await removeTmpDir(homeDir);
  });

  it('can roll back and then roll back again (reversible)', async () => {
    const sourceV1 = await makeTmpDir();
    const sourceV2 = await makeTmpDir();
    try {
      await writeAddonSource(sourceV1, 'my-skill', '1.0.0');
      await writeAddonSource(sourceV2, 'my-skill', '2.0.0');

      const { runAddonAdd } = await import('../commands/addon-add.js');
      const { runAddonUpdate } = await import('../commands/addon-update.js');

      // Install v1
      await runAddonAdd({ source: sourceV1, scope: 'local' }, { cwd, homeDir });
      // Update to v2 (snapshots v1 into store)
      await runAddonUpdate({ source: sourceV2, scope: 'local' }, { cwd, homeDir });

      // Rollback to v1
      const rollback1 = await runAddonRollback(
        { name: 'my-skill', type: 'skill', scope: 'local' },
        { cwd, homeDir },
      );
      expect(rollback1.exitCode).toBe(0);
      expect(rollback1.output).toContain('1.0.0');

      // Now at v1, rollback should have snapshotted v2 — so we can roll back to v2
      const rollback2 = await runAddonRollback(
        { name: 'my-skill', type: 'skill', scope: 'local', to: '2.0.0' },
        { cwd, homeDir },
      );
      // The store has v2 snapshotted from the rollback step, so this should succeed
      expect(rollback2.exitCode).toBe(0);
    } finally {
      await removeTmpDir(sourceV1);
      await removeTmpDir(sourceV2);
    }
  });
});
