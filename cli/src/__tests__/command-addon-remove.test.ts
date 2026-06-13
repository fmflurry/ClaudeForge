/**
 * Tests for src/commands/addon-remove.ts
 *
 * Covers:
 *   - Remove installed add-on succeeds
 *   - --type required
 *   - --scope required
 *   - Removing non-installed add-on errors clearly
 *   - Error messages do not leak internal absolute paths
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';
import { runAddonRemove } from '../commands/addon-remove.js';
import type { AddonRemoveDeps } from '../commands/addon-remove.js';
import type { LifecycleDeps } from '../addon/lifecycle.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-addon-remove-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

async function installAddon(
  sourceDir: string,
  scope: 'local' | 'global',
  deps: { cwd: string; homeDir: string },
): Promise<void> {
  const { runAddonAdd } = await import('../commands/addon-add.js');
  await runAddonAdd({ source: sourceDir, scope }, deps);
}

async function writeAddonSource(dir: string, name: string, type: string): Promise<void> {
  const manifest: Record<string, unknown> = {
    name,
    version: '1.0.0',
    type,
    supportedScopes: type === 'plugin' ? ['global'] : ['local', 'global'],
    files: type === 'agent' ? [`${name}.md`] : ['SKILL.md'],
  };
  await fs.mkdir(dir, { recursive: true });
  await fs.writeFile(path.join(dir, 'addon.json'), JSON.stringify(manifest, null, 2), 'utf-8');
  const files = manifest['files'] as string[];
  for (const f of files) {
    const dest = path.join(dir, f);
    await fs.mkdir(path.dirname(dest), { recursive: true });
    await fs.writeFile(dest, `# ${name}`, 'utf-8');
  }
}

// ---------------------------------------------------------------------------
// --type required
// ---------------------------------------------------------------------------

describe('runAddonRemove – --type required', () => {
  it('returns exitCode 1 when --type is missing', async () => {
    const result = await runAddonRemove({ name: 'my-addon', scope: 'local' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/--type is required/i);
  });

  it('returns exitCode 1 for invalid --type', async () => {
    const result = await runAddonRemove({ name: 'my-addon', type: 'widget', scope: 'local' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/invalid.*type/i);
  });
});

// ---------------------------------------------------------------------------
// --scope required
// ---------------------------------------------------------------------------

describe('runAddonRemove – --scope required', () => {
  it('returns exitCode 1 when --scope is missing', async () => {
    const result = await runAddonRemove({ name: 'my-addon', type: 'agent' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/--scope is required/i);
  });

  it('returns exitCode 1 when scope is invalid', async () => {
    const result = await runAddonRemove({ name: 'my-addon', type: 'agent', scope: 'cosmos' }, {});
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/invalid scope/i);
  });
});

// ---------------------------------------------------------------------------
// Non-installed error
// ---------------------------------------------------------------------------

describe('runAddonRemove – non-installed', () => {
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

  it('returns exitCode 1 when add-on is not installed', async () => {
    const result = await runAddonRemove(
      { name: 'ghost-addon', type: 'skill', scope: 'local' },
      { cwd, homeDir },
    );
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/not installed/i);
  });

  it('error message does not contain deep internal paths', async () => {
    const result = await runAddonRemove(
      { name: 'ghost-addon', type: 'skill', scope: 'local' },
      { cwd, homeDir },
    );
    // The message from lifecycle.remove is "skill "ghost-addon" is not installed in local scope."
    // No absolute paths should appear in the output
    const absolutePathPattern = /\/[a-z]+\/[a-z]+\/[a-z]+/i;
    expect(result.output).not.toMatch(absolutePathPattern);
  });
});

// ---------------------------------------------------------------------------
// Successful remove
// ---------------------------------------------------------------------------

describe('runAddonRemove – success', () => {
  let sourceDir: string;
  let cwd: string;
  let homeDir: string;

  beforeEach(async () => {
    sourceDir = await makeTmpDir();
    cwd = await makeTmpDir();
    homeDir = await makeTmpDir();
    await writeAddonSource(sourceDir, 'my-agent', 'agent');
  });

  afterEach(async () => {
    await removeTmpDir(sourceDir);
    await removeTmpDir(cwd);
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 when removing an installed add-on', async () => {
    await installAddon(sourceDir, 'local', { cwd, homeDir });
    const result = await runAddonRemove(
      { name: 'my-agent', type: 'agent', scope: 'local' },
      { cwd, homeDir },
    );
    expect(result.exitCode).toBe(0);
  });

  it('output mentions the add-on name and scope', async () => {
    await installAddon(sourceDir, 'local', { cwd, homeDir });
    const result = await runAddonRemove(
      { name: 'my-agent', type: 'agent', scope: 'local' },
      { cwd, homeDir },
    );
    expect(result.output).toContain('my-agent');
    expect(result.output).toContain('local');
  });
});

// ---------------------------------------------------------------------------
// Injected lifecycle deps — verify isolation
// ---------------------------------------------------------------------------

describe('runAddonRemove – injected deps isolation', () => {
  it('does not leak absolute paths via injected lifecycle messages', async () => {
    const lifecycleDeps: LifecycleDeps = {
      fs: {
        mkdir: vi.fn().mockResolvedValue(undefined),
        writeFile: vi.fn().mockResolvedValue(undefined),
        readFile: vi.fn().mockResolvedValue(''),
        readFileBuffer: vi.fn().mockResolvedValue(Buffer.from('')),
        copyFile: vi.fn().mockResolvedValue(undefined),
        copyDir: vi.fn().mockResolvedValue(undefined),
        rename: vi.fn().mockResolvedValue(undefined),
        rm: vi.fn().mockResolvedValue(undefined),
        // sidecar not found
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
        snapshot: vi.fn(),
        list: vi.fn().mockResolvedValue([]),
        read: vi.fn(),
        latestPrior: vi.fn().mockResolvedValue(undefined),
        prune: vi.fn().mockResolvedValue(undefined),
      },
      cwd: '/very/deep/internal/project/path',
      homeDir: '/very/deep/internal/home/dir',
    };

    const deps: AddonRemoveDeps = {
      lifecycleDeps,
      cwd: '/very/deep/internal/project/path',
      homeDir: '/very/deep/internal/home/dir',
    };

    const result = await runAddonRemove(
      { name: 'ghost', type: 'skill', scope: 'local' },
      deps,
    );

    expect(result.exitCode).toBe(1);
    // Verify no deep absolute paths leak through
    // The sanitizer in addon-remove.ts should replace /a/b/c/d patterns
    const deepPathPattern = /\/[a-z]+\/[a-z]+\/[a-z]+\/[a-z]+/;
    expect(result.output).not.toMatch(deepPathPattern);
  });
});
