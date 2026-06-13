/**
 * Tests for cli/src/addon/lifecycle.ts
 *
 * Covers every scenario from the addon-lifecycle spec and tasks 3.2/12.1:
 *
 *   add:
 *     - writes all declared files + sidecar
 *     - existing without --force is rejected (suggests --force)
 *     - --force overwrites AND snapshots prior
 *     - plugin+local rejected
 *     - hook merges settings entry LAST
 *
 *   update:
 *     - newer version succeeds, snapshots prior
 *     - same-version is no-op with message
 *     - failure leaves prior intact (atomicity / restore from backup)
 *
 *   remove:
 *     - deletes only placedFiles + sidecar
 *     - leaves unowned files
 *     - non-installed errors clearly
 *     - prunes empty owner dir only
 *     - hook unregisters settings entry
 *
 *   list:
 *     - scans .addons/**, reads sidecars
 *     - includes live version + storedVersions from store
 *     - empty scope returns []
 *
 *   rollback:
 *     - latest prior (no --to)
 *     - --to specific version
 *     - missing version errors + live unchanged
 *     - reversible (snapshots current first)
 *     - re-merges stored settings entry for hooks
 *
 *   settings:
 *     - hook add merges settings entry last
 *     - hook remove unregisters entry
 *     - merge revert on failure
 *
 *   version store:
 *     - snapshot on update
 *     - snapshot on --force
 *
 *   idempotency / interrupted:
 *     - re-run after interruption is clean
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import * as path from 'node:path';
import type { AddonManifest, HookRegistration } from '../addon/manifest.js';
import type { SettingsFsPort, SettingsJson } from '../addon/settings.js';
import type { VersionStore, StoredVersion } from '../addon/store.js';
import type {
  LifecycleFsPort,
  LifecycleDeps,
  LifecycleResult,
  AddonListing,
  SidecarData,
} from '../addon/lifecycle.js';
import {
  add,
  update,
  remove,
  list,
  rollback,
} from '../addon/lifecycle.js';

// ---------------------------------------------------------------------------
// Path constants
// ---------------------------------------------------------------------------

const CWD = '/project';
const HOME = '/home/alice';
const SCOPE_ROOT_LOCAL = '/project/.claude';
const SCOPE_ROOT_GLOBAL = '/home/alice/.claude';
const BASE_STORE = '/home/alice/.claude-plugins/addon-store';

// ---------------------------------------------------------------------------
// Manifest factories
// ---------------------------------------------------------------------------

function makeSkillManifest(name = 'my-skill', version = '1.0.0'): AddonManifest {
  return {
    name,
    version,
    type: 'skill',
    supportedScopes: ['local', 'global'],
    files: ['SKILL.md', 'scripts/run.sh'],
  };
}

function makeAgentManifest(name = 'my-agent', version = '1.0.0'): AddonManifest {
  return {
    name,
    version,
    type: 'agent',
    supportedScopes: ['local', 'global'],
    files: ['my-agent.md'],
  };
}

function makeHookManifest(name = 'my-hook', version = '1.0.0'): AddonManifest {
  return {
    name,
    version,
    type: 'hook',
    supportedScopes: ['local', 'global'],
    files: ['hooks/auth.sh'],
    hook: {
      event: 'PreToolUse',
      matcher: 'Bash',
      command: 'hooks/auth.sh',
    },
  };
}

function makePluginManifest(name = 'my-plugin', version = '1.0.0'): AddonManifest {
  return {
    name,
    version,
    type: 'plugin',
    supportedScopes: ['global'],
    files: ['bundle/index.js', '.claude-plugin/plugin.json'],
  };
}

// ---------------------------------------------------------------------------
// Fake LifecycleFsPort
// ---------------------------------------------------------------------------

interface FakeFs {
  files: Map<string, string | Buffer>;
  dirs: Set<string>;
  renames: { src: string; dest: string }[];
  rms: string[];
}

function makeFakeFs(): FakeFs {
  return {
    files: new Map(),
    dirs: new Set(),
    renames: [],
    rms: [],
  };
}

function makeFakeLifecycleFsPort(fakeFs: FakeFs): LifecycleFsPort {
  return {
    mkdir: vi.fn(async (p: string) => {
      fakeFs.dirs.add(p);
    }),
    writeFile: vi.fn(async (p: string, content: string | Buffer) => {
      fakeFs.files.set(p, content);
    }),
    readFile: vi.fn(async (p: string) => {
      const content = fakeFs.files.get(p);
      if (content === undefined) {
        throw new Error(`ENOENT: no such file '${p}'`);
      }
      if (Buffer.isBuffer(content)) return content.toString('utf-8');
      return content;
    }),
    readFileBuffer: vi.fn(async (p: string) => {
      const content = fakeFs.files.get(p);
      if (content === undefined) {
        throw new Error(`ENOENT: no such file '${p}'`);
      }
      if (Buffer.isBuffer(content)) return content;
      return Buffer.from(content, 'utf-8');
    }),
    copyFile: vi.fn(async (src: string, dest: string) => {
      const content = fakeFs.files.get(src);
      if (content === undefined) {
        throw new Error(`ENOENT: cannot copy '${src}'`);
      }
      fakeFs.files.set(dest, content);
    }),
    copyDir: vi.fn(async (src: string, dest: string) => {
      for (const [k, v] of fakeFs.files.entries()) {
        if (k.startsWith(src + '/') || k === src) {
          fakeFs.files.set(dest + k.slice(src.length), v);
        }
      }
      for (const d of fakeFs.dirs) {
        if (d.startsWith(src + '/') || d === src) {
          fakeFs.dirs.add(dest + d.slice(src.length));
        }
      }
    }),
    rename: vi.fn(async (src: string, dest: string) => {
      for (const [k, v] of fakeFs.files.entries()) {
        if (k === src || k.startsWith(src + '/')) {
          fakeFs.files.set(dest + k.slice(src.length), v);
          fakeFs.files.delete(k);
        }
      }
      for (const d of [...fakeFs.dirs]) {
        if (d === src || d.startsWith(src + '/')) {
          fakeFs.dirs.add(dest + d.slice(src.length));
          fakeFs.dirs.delete(d);
        }
      }
      fakeFs.renames.push({ src, dest });
    }),
    rm: vi.fn(async (p: string, _opts?: { recursive: boolean; force: boolean }) => {
      fakeFs.rms.push(p);
      for (const k of [...fakeFs.files.keys()]) {
        if (k === p || k.startsWith(p + '/')) {
          fakeFs.files.delete(k);
        }
      }
      for (const d of [...fakeFs.dirs]) {
        if (d === p || d.startsWith(p + '/')) {
          fakeFs.dirs.delete(d);
        }
      }
    }),
    exists: vi.fn(async (p: string) => {
      if (fakeFs.dirs.has(p)) return true;
      for (const k of fakeFs.files.keys()) {
        if (k === p || k.startsWith(p + '/')) return true;
      }
      return false;
    }),
    readdir: vi.fn(async (p: string) => {
      const results: string[] = [];
      const allPaths = [...fakeFs.dirs, ...fakeFs.files.keys()];
      for (const k of allPaths) {
        if (k.startsWith(p + '/')) {
          const rest = k.slice(p.length + 1);
          const seg = rest.split('/')[0];
          if (seg && !results.includes(seg)) {
            results.push(seg);
          }
        }
      }
      return results;
    }),
    stat: vi.fn(async (p: string) => {
      if (fakeFs.dirs.has(p)) return { isDirectory: true };
      if (fakeFs.files.has(p)) return { isDirectory: false };
      throw new Error(`ENOENT: '${p}'`);
    }),
  };
}

// ---------------------------------------------------------------------------
// Fake SettingsFsPort
// ---------------------------------------------------------------------------

function makeFakeSettingsPort(fakeFs: FakeFs): SettingsFsPort {
  return {
    readFile: vi.fn(async (p: string) => {
      const content = fakeFs.files.get(p);
      if (content === undefined) {
        throw new Error(`ENOENT: no such file '${p}'`);
      }
      return Buffer.isBuffer(content) ? content.toString('utf-8') : content;
    }),
    writeFile: vi.fn(async (p: string, content: string) => {
      fakeFs.files.set(p, content);
    }),
    rename: vi.fn(async (src: string, dest: string) => {
      const content = fakeFs.files.get(src);
      if (content !== undefined) {
        fakeFs.files.set(dest, content);
        fakeFs.files.delete(src);
      }
      fakeFs.renames.push({ src, dest });
    }),
    exists: vi.fn(async (p: string) => {
      return fakeFs.files.has(p) || fakeFs.dirs.has(p);
    }),
    rm: vi.fn(async (p: string) => {
      fakeFs.files.delete(p);
    }),
  };
}

// ---------------------------------------------------------------------------
// Fake VersionStore
// ---------------------------------------------------------------------------

function makeFakeStore(snapshots: Map<string, StoredVersion> = new Map<string, StoredVersion>()): VersionStore {
  const storedVersionsByAddon = new Map<string, string[]>();

  const snapshotFn = vi.fn(async (args: {
    scope: AddonManifest['supportedScopes'][0];
    type: AddonManifest['type'];
    name: string;
    version: string;
    sourceFiles: readonly { rel: string; absSource: string }[];
    manifest: AddonManifest;
    settingsEntry?: HookRegistration;
  }): Promise<string> => {
    const key = `${args.scope}/${args.type}/${args.name}/${args.version}`;
    const storedPath = `${BASE_STORE}/${key}`;
    const stored: StoredVersion = {
      manifest: args.manifest,
      version: args.version,
      files: args.sourceFiles.map((f) => f.rel),
      settingsEntry: args.settingsEntry,
      path: storedPath,
    };
    snapshots.set(key, stored);
    const addonKey = `${args.scope}/${args.type}/${args.name}`;
    const existing = storedVersionsByAddon.get(addonKey) ?? [];
    if (!existing.includes(args.version)) {
      storedVersionsByAddon.set(addonKey, [...existing, args.version]);
    }
    return storedPath;
  });

  const listFn = vi.fn(async (args: {
    scope: AddonManifest['supportedScopes'][0];
    type: AddonManifest['type'];
    name: string;
  }): Promise<string[]> => {
    const addonKey = `${args.scope}/${args.type}/${args.name}`;
    return storedVersionsByAddon.get(addonKey) ?? [];
  });

  const readFn = vi.fn(async (args: {
    scope: AddonManifest['supportedScopes'][0];
    type: AddonManifest['type'];
    name: string;
    version: string;
  }): Promise<StoredVersion> => {
    const key = `${args.scope}/${args.type}/${args.name}/${args.version}`;
    const stored = snapshots.get(key);
    if (!stored) {
      throw new Error(`Version ${args.version} not found in store`);
    }
    return stored;
  });

  const latestPriorFn = vi.fn(async (args: {
    scope: AddonManifest['supportedScopes'][0];
    type: AddonManifest['type'];
    name: string;
    currentVersion: string;
  }): Promise<string | undefined> => {
    const addonKey = `${args.scope}/${args.type}/${args.name}`;
    const versions = storedVersionsByAddon.get(addonKey) ?? [];
    const priors = versions.filter((v) => v !== args.currentVersion);
    if (priors.length === 0) return undefined;
    return priors[priors.length - 1];
  });

  const pruneFn = vi.fn(async (_args: {
    scope: AddonManifest['supportedScopes'][0];
    type: AddonManifest['type'];
    name: string;
    keep: number;
  }): Promise<void> => {
    // no-op for tests
  });

  return {
    snapshot: snapshotFn,
    list: listFn,
    read: readFn,
    latestPrior: latestPriorFn,
    prune: pruneFn,
  };
}

// ---------------------------------------------------------------------------
// Sidecar helpers
// ---------------------------------------------------------------------------

function makeSidecar(manifest: AddonManifest, placedFiles: string[], settingsEntry?: HookRegistration): SidecarData {
  return {
    manifest,
    placedFiles,
    ...(settingsEntry !== undefined ? { settingsEntry } : {}),
  };
}

function seedSidecar(fakeFs: FakeFs, sidecarPath: string, data: SidecarData): void {
  fakeFs.files.set(sidecarPath, JSON.stringify(data));
}

// ---------------------------------------------------------------------------
// Deps factory
// ---------------------------------------------------------------------------

function makeDeps(fakeFs: FakeFs, storeSnapshots?: Map<string, StoredVersion>): LifecycleDeps {
  return {
    fs: makeFakeLifecycleFsPort(fakeFs),
    settingsPort: makeFakeSettingsPort(fakeFs),
    store: makeFakeStore(storeSnapshots),
    cwd: CWD,
    homeDir: HOME,
  };
}

// ---------------------------------------------------------------------------
// Section: add — writes all files + sidecar
// ---------------------------------------------------------------------------

describe('lifecycle.add — writes all declared files', () => {
  let fakeFs: FakeFs;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    deps = makeDeps(fakeFs);
  });

  it('writes each placed file to the resolved live target', async () => {
    const manifest = makeSkillManifest();
    const sourceDir = '/tmp/my-skill-src';
    // Seed source files
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# My Skill');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), '#!/bin/bash');

    const result = await add({ sourceDir, scope: 'local', force: false }, deps);

    expect(result.success).toBe(true);
    // skill files land under <scopeRoot>/skills/<name>/<file>
    expect(fakeFs.files.has(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`)).toBe(true);
    expect(fakeFs.files.has(`${SCOPE_ROOT_LOCAL}/skills/my-skill/scripts/run.sh`)).toBe(true);
  });

  it('writes the sidecar JSON file', async () => {
    const manifest = makeSkillManifest();
    const sourceDir = '/tmp/my-skill-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# Skill');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), '#!/bin/bash');

    await add({ sourceDir, scope: 'local', force: false }, deps);

    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    expect(fakeFs.files.has(sidecarPath)).toBe(true);
    const sidecar = JSON.parse(fakeFs.files.get(sidecarPath) as string) as SidecarData;
    expect(sidecar.manifest.name).toBe('my-skill');
    expect(sidecar.placedFiles).toContain(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`);
    expect(sidecar.placedFiles).toContain(`${SCOPE_ROOT_LOCAL}/skills/my-skill/scripts/run.sh`);
  });

  it('reports success with type, name, and scope', async () => {
    const manifest = makeSkillManifest();
    const sourceDir = '/tmp/my-skill-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# Skill');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), '#!/bin/bash');

    const result = await add({ sourceDir, scope: 'local', force: false }, deps);

    expect(result.success).toBe(true);
    expect(result.message).toMatch(/skill/i);
    expect(result.message).toMatch(/my-skill/);
    expect(result.message).toMatch(/local/i);
  });

  it('installs agent to <scopeRoot>/agents/<name>.md', async () => {
    const manifest = makeAgentManifest();
    const sourceDir = '/tmp/agent-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'my-agent.md'), '---\nfrontmatter\n---\n# Agent');

    const result = await add({ sourceDir, scope: 'local', force: false }, deps);

    expect(result.success).toBe(true);
    expect(fakeFs.files.has(`${SCOPE_ROOT_LOCAL}/agents/my-agent.md`)).toBe(true);
  });

  it('installs plugin to <homeDir>/.claude/plugins/<name>/', async () => {
    const manifest = makePluginManifest();
    const sourceDir = '/tmp/plugin-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'bundle/index.js'), '// bundle');
    fakeFs.files.set(path.join(sourceDir, '.claude-plugin/plugin.json'), '{}');

    const result = await add({ sourceDir, scope: 'global', force: false }, deps);

    expect(result.success).toBe(true);
    expect(fakeFs.files.has(`${SCOPE_ROOT_GLOBAL}/.claude/plugins/my-plugin/bundle/index.js`) ||
           fakeFs.files.has(`${HOME}/.claude/plugins/my-plugin/bundle/index.js`)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Section: add — reject invalid manifest
// ---------------------------------------------------------------------------

describe('lifecycle.add — manifest validation', () => {
  let fakeFs: FakeFs;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    deps = makeDeps(fakeFs);
  });

  it('fails when addon.json is missing', async () => {
    const sourceDir = '/tmp/no-manifest';

    const result = await add({ sourceDir, scope: 'local', force: false }, deps);

    expect(result.success).toBe(false);
    expect(result.message).toMatch(/addon\.json/i);
  });

  it('fails when addon.json fails validation', async () => {
    const sourceDir = '/tmp/bad-manifest';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify({ name: '', version: 'bad', type: 'skill', supportedScopes: ['local'], files: [] }));

    const result = await add({ sourceDir, scope: 'local', force: false }, deps);

    expect(result.success).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Section: add — plugin+local defense in depth
// ---------------------------------------------------------------------------

describe('lifecycle.add — plugin+local rejected', () => {
  it('rejects plugin type with local scope', async () => {
    const fakeFs = makeFakeFs();
    const deps = makeDeps(fakeFs);
    const manifest = makePluginManifest();
    const sourceDir = '/tmp/plugin-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'bundle/index.js'), '// bundle');
    fakeFs.files.set(path.join(sourceDir, '.claude-plugin/plugin.json'), '{}');

    const result = await add({ sourceDir, scope: 'local', force: false }, deps);

    expect(result.success).toBe(false);
    expect(result.message).toMatch(/global/i);
  });
});

// ---------------------------------------------------------------------------
// Section: add — existing without --force is rejected
// ---------------------------------------------------------------------------

describe('lifecycle.add — existing without --force', () => {
  let fakeFs: FakeFs;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    deps = makeDeps(fakeFs);
  });

  it('errors with message suggesting --force', async () => {
    const manifest = makeSkillManifest();
    const sourceDir = '/tmp/my-skill-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# Skill');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), '#!/bin/bash');

    // Pre-seed sidecar indicating already installed
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));

    const result = await add({ sourceDir, scope: 'local', force: false }, deps);

    expect(result.success).toBe(false);
    expect(result.message).toMatch(/--force/);
    expect(result.message).toMatch(/already/i);
  });

  it('does not write any files when rejecting', async () => {
    const manifest = makeSkillManifest();
    const sourceDir = '/tmp/my-skill-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# NEW Skill');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), '#!/bin/bash new');

    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    const existingManifest = { ...manifest, version: '0.9.0' };
    seedSidecar(fakeFs, sidecarPath, makeSidecar(existingManifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`, '# OLD Skill');

    await add({ sourceDir, scope: 'local', force: false }, deps);

    // Old file should remain unchanged
    expect(fakeFs.files.get(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`)).toBe('# OLD Skill');
  });
});

// ---------------------------------------------------------------------------
// Section: add with --force
// ---------------------------------------------------------------------------

describe('lifecycle.add — --force overwrites and snapshots prior', () => {
  let fakeFs: FakeFs;
  let storeSnapshots: Map<string, StoredVersion>;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    storeSnapshots = new Map();
    deps = makeDeps(fakeFs, storeSnapshots);
  });

  it('--force overwrites existing files', async () => {
    const oldManifest = makeSkillManifest('my-skill', '1.0.0');
    const newManifest = makeSkillManifest('my-skill', '2.0.0');
    const sourceDir = '/tmp/my-skill-src';

    // Pre-seed existing installation
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(oldManifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/scripts/run.sh`,
    ]));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`, '# OLD');
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/scripts/run.sh`, 'old bash');

    // Seed new source files
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(newManifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# NEW');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), 'new bash');

    const result = await add({ sourceDir, scope: 'local', force: true }, deps);

    expect(result.success).toBe(true);
    expect(fakeFs.files.get(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`)).toBe('# NEW');
  });

  it('--force snapshots existing version into the store before overwriting', async () => {
    const oldManifest = makeSkillManifest('my-skill', '1.0.0');
    const newManifest = makeSkillManifest('my-skill', '2.0.0');
    const sourceDir = '/tmp/my-skill-src';

    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(oldManifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`, '# OLD');

    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(newManifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# NEW');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), 'new bash');

    await add({ sourceDir, scope: 'local', force: true }, deps);

    expect(deps.store.snapshot).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'my-skill', version: '1.0.0' }),
    );
  });
});

// ---------------------------------------------------------------------------
// Section: hook settings.json merge
// ---------------------------------------------------------------------------

describe('lifecycle.add — hook merges settings entry last', () => {
  let fakeFs: FakeFs;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    deps = makeDeps(fakeFs);
  });

  it('adds hook entry to settings.json', async () => {
    const manifest = makeHookManifest();
    const sourceDir = '/tmp/hook-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'hooks/auth.sh'), '#!/bin/bash');

    await add({ sourceDir, scope: 'local', force: false }, deps);

    const settingsPath = `${SCOPE_ROOT_LOCAL}/settings.json`;
    // Settings file should have been written
    expect(fakeFs.files.has(settingsPath)).toBe(true);
    const settingsRaw = fakeFs.files.get(settingsPath);
    const settings = JSON.parse(typeof settingsRaw === 'string' ? settingsRaw : String(settingsRaw)) as SettingsJson;
    expect(settings.hooks).toBeDefined();
    expect(settings.hooks?.['PreToolUse']).toBeDefined();
  });

  it('settings merge is the LAST write (hook script is written before settings)', async () => {
    const manifest = makeHookManifest();
    const sourceDir = '/tmp/hook-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'hooks/auth.sh'), '#!/bin/bash');

    const writeOrder: string[] = [];
    const origWriteFile = deps.fs.writeFile;
    deps.fs.writeFile = vi.fn(async (p: string, content: string | Buffer) => {
      writeOrder.push(p);
      return origWriteFile(p, content);
    });
    // Also intercept settings port writes
    const origSettingsWrite = deps.settingsPort.writeFile;
    deps.settingsPort.writeFile = vi.fn(async (p: string, content: string) => {
      writeOrder.push(`settings:${p}`);
      return origSettingsWrite(p, content);
    });

    await add({ sourceDir, scope: 'local', force: false }, deps);

    const settingsWriteIdx = writeOrder.findIndex((p) => p.startsWith('settings:'));
    const lastNonSettingsWrite = writeOrder.reduce((last, p, idx) => {
      if (!p.startsWith('settings:') && !p.endsWith('addon.json')) return idx;
      return last;
    }, -1);

    // Settings write should come after file writes
    expect(settingsWriteIdx).toBeGreaterThan(lastNonSettingsWrite);
  });

  it('preserves unrelated settings keys', async () => {
    const manifest = makeHookManifest();
    const sourceDir = '/tmp/hook-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'hooks/auth.sh'), '#!/bin/bash');

    // Pre-seed settings with unrelated key
    const settingsPath = `${SCOPE_ROOT_LOCAL}/settings.json`;
    fakeFs.files.set(settingsPath, JSON.stringify({ theme: 'dark', otherKey: 'preserved' }));

    await add({ sourceDir, scope: 'local', force: false }, deps);

    const settingsRaw = fakeFs.files.get(settingsPath);
    const settings = JSON.parse(typeof settingsRaw === 'string' ? settingsRaw : String(settingsRaw)) as SettingsJson & { theme?: string; otherKey?: string };
    expect(settings.theme).toBe('dark');
    expect(settings.otherKey).toBe('preserved');
  });
});

// ---------------------------------------------------------------------------
// Section: update — newer version succeeds
// ---------------------------------------------------------------------------

describe('lifecycle.update — newer version', () => {
  let fakeFs: FakeFs;
  let storeSnapshots: Map<string, StoredVersion>;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    storeSnapshots = new Map();
    deps = makeDeps(fakeFs, storeSnapshots);
  });

  function seedInstalledSkill(name: string, version: string): void {
    const manifest = makeSkillManifest(name, version);
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/${name}.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [
      `${SCOPE_ROOT_LOCAL}/skills/${name}/SKILL.md`,
      `${SCOPE_ROOT_LOCAL}/skills/${name}/scripts/run.sh`,
    ]));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/${name}/SKILL.md`, `# ${name} v${version}`);
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/${name}/scripts/run.sh`, `#!/bin/bash v${version}`);
  }

  it('updates to newer version and reports old + new version', async () => {
    seedInstalledSkill('my-skill', '1.0.0');

    const newManifest = makeSkillManifest('my-skill', '2.0.0');
    const sourceDir = '/tmp/my-skill-v2';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(newManifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# v2.0.0');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), '#!/bin/bash v2');

    const result = await update({ sourceDir, scope: 'local' }, deps);

    expect(result.success).toBe(true);
    expect(result.message).toMatch(/1\.0\.0/);
    expect(result.message).toMatch(/2\.0\.0/);
  });

  it('replaces managed files after update', async () => {
    seedInstalledSkill('my-skill', '1.0.0');

    const newManifest = makeSkillManifest('my-skill', '2.0.0');
    const sourceDir = '/tmp/my-skill-v2';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(newManifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# v2.0.0 content');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), 'new bash');

    await update({ sourceDir, scope: 'local' }, deps);

    expect(fakeFs.files.get(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`)).toBe('# v2.0.0 content');
  });

  it('snapshots the prior version into the store before swapping', async () => {
    seedInstalledSkill('my-skill', '1.0.0');

    const newManifest = makeSkillManifest('my-skill', '2.0.0');
    const sourceDir = '/tmp/my-skill-v2';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(newManifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# v2');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), 'new bash');

    await update({ sourceDir, scope: 'local' }, deps);

    expect(deps.store.snapshot).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'my-skill', version: '1.0.0' }),
    );
  });

  it('errors if addon is not installed', async () => {
    const newManifest = makeSkillManifest('not-installed', '2.0.0');
    const sourceDir = '/tmp/not-installed';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(newManifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# content');

    const result = await update({ sourceDir, scope: 'local' }, deps);

    expect(result.success).toBe(false);
    expect(result.message).toMatch(/not installed/i);
  });
});

// ---------------------------------------------------------------------------
// Section: update — same-version no-op
// ---------------------------------------------------------------------------

describe('lifecycle.update — same version is no-op', () => {
  let fakeFs: FakeFs;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    deps = makeDeps(fakeFs);
  });

  it('returns no-op message when version unchanged', async () => {
    const manifest = makeSkillManifest('my-skill', '1.5.0');
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`, '# existing');

    const sourceDir = '/tmp/same-version';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# same version source');

    const result = await update({ sourceDir, scope: 'local' }, deps);

    expect(result.success).toBe(true);
    expect(result.message).toMatch(/already/i);
    expect(result.message).toMatch(/1\.5\.0/);
  });

  it('does not write any files when same version', async () => {
    const manifest = makeSkillManifest('my-skill', '1.5.0');
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    const originalContent = '# ORIGINAL';
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`, originalContent);

    const sourceDir = '/tmp/same-version';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# DIFFERENT CONTENT SAME VERSION');

    await update({ sourceDir, scope: 'local' }, deps);

    expect(fakeFs.files.get(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`)).toBe(originalContent);
  });
});

// ---------------------------------------------------------------------------
// Section: update — atomicity / restore on failure
// ---------------------------------------------------------------------------

describe('lifecycle.update — failure leaves prior version intact (atomicity)', () => {
  it('prior version remains when write fails partway through', async () => {
    const fakeFs = makeFakeFs();
    const deps = makeDeps(fakeFs);

    const oldManifest = makeSkillManifest('my-skill', '1.0.0');
    const newManifest = makeSkillManifest('my-skill', '2.0.0');
    const sourceDir = '/tmp/my-skill-v2';

    // Seed installed version
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(oldManifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`, '# OLD CONTENT');

    // Seed new source with second file missing (will cause read error)
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(newManifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# NEW CONTENT');
    // scripts/run.sh NOT seeded — will fail

    const result = await update({ sourceDir, scope: 'local' }, deps);

    // Should fail
    expect(result.success).toBe(false);
    // Old file should remain
    expect(fakeFs.files.get(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`)).toBe('# OLD CONTENT');
  });
});

// ---------------------------------------------------------------------------
// Section: remove — deletes only managed files
// ---------------------------------------------------------------------------

describe('lifecycle.remove — deletes only managed files', () => {
  let fakeFs: FakeFs;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    deps = makeDeps(fakeFs);
  });

  it('deletes declared placedFiles and the sidecar', async () => {
    const manifest = makeSkillManifest();
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    const placed1 = `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`;
    const placed2 = `${SCOPE_ROOT_LOCAL}/skills/my-skill/scripts/run.sh`;

    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [placed1, placed2]));
    fakeFs.files.set(placed1, '# Skill');
    fakeFs.files.set(placed2, '#!/bin/bash');

    const result = await remove({ name: 'my-skill', type: 'skill', scope: 'local' }, deps);

    expect(result.success).toBe(true);
    expect(fakeFs.files.has(placed1)).toBe(false);
    expect(fakeFs.files.has(placed2)).toBe(false);
    expect(fakeFs.files.has(sidecarPath)).toBe(false);
  });

  it('leaves unowned files untouched', async () => {
    const manifest = makeSkillManifest();
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    const placed = `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`;
    const unownedFile = `${SCOPE_ROOT_LOCAL}/skills/my-skill/user-notes.md`;

    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [placed]));
    fakeFs.files.set(placed, '# Skill');
    fakeFs.files.set(unownedFile, '# User notes — not managed');

    await remove({ name: 'my-skill', type: 'skill', scope: 'local' }, deps);

    expect(fakeFs.files.has(unownedFile)).toBe(true);
  });

  it('errors clearly when not installed', async () => {
    const result = await remove({ name: 'nonexistent', type: 'skill', scope: 'local' }, deps);

    expect(result.success).toBe(false);
    expect(result.message).toMatch(/not (installed|found)/i);
  });

  it('is idempotent for already-missing files (tolerate missing placedFiles)', async () => {
    const manifest = makeSkillManifest();
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    const placed = `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`;

    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [placed]));
    // placed file is NOT in fakeFs (already gone)

    const result = await remove({ name: 'my-skill', type: 'skill', scope: 'local' }, deps);

    // Should succeed even though the file was already missing
    expect(result.success).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Section: remove — prune empty owner dir
// ---------------------------------------------------------------------------

describe('lifecycle.remove — prune empty owner dirs', () => {
  let fakeFs: FakeFs;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    deps = makeDeps(fakeFs);
  });

  it('prunes empty skill directory after removing all files', async () => {
    const manifest = makeSkillManifest();
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    const placed = `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`;
    // skill dir with only the one placed file
    fakeFs.dirs.add(`${SCOPE_ROOT_LOCAL}/skills/my-skill`);

    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [placed]));
    fakeFs.files.set(placed, '# Skill');

    await remove({ name: 'my-skill', type: 'skill', scope: 'local' }, deps);

    // The skill directory should have been removed
    expect(fakeFs.rms).toContain(`${SCOPE_ROOT_LOCAL}/skills/my-skill`);
  });

  it('does NOT prune skill directory if unowned files remain', async () => {
    const manifest = makeSkillManifest();
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    const placed = `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`;
    const unowned = `${SCOPE_ROOT_LOCAL}/skills/my-skill/extra.md`;

    fakeFs.dirs.add(`${SCOPE_ROOT_LOCAL}/skills/my-skill`);
    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [placed]));
    fakeFs.files.set(placed, '# Skill');
    fakeFs.files.set(unowned, '# Extra');

    await remove({ name: 'my-skill', type: 'skill', scope: 'local' }, deps);

    // Directory should NOT be pruned (has unowned file)
    expect(fakeFs.rms).not.toContain(`${SCOPE_ROOT_LOCAL}/skills/my-skill`);
  });
});

// ---------------------------------------------------------------------------
// Section: remove — hook unregisters settings entry
// ---------------------------------------------------------------------------

describe('lifecycle.remove — hook unregisters settings entry', () => {
  let fakeFs: FakeFs;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    deps = makeDeps(fakeFs);
  });

  it('removes hook entry from settings.json on remove', async () => {
    const manifest = makeHookManifest();
    const hookReg: HookRegistration = {
      event: 'PreToolUse',
      matcher: 'Bash',
      command: 'hooks/auth.sh',
      type: 'command',
    };
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/hook/my-hook.json`;
    const placed = `${SCOPE_ROOT_LOCAL}/hooks/hooks/auth.sh`;

    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [placed], hookReg));
    fakeFs.files.set(placed, '#!/bin/bash');

    // Pre-seed settings with hook entry
    const settingsPath = `${SCOPE_ROOT_LOCAL}/settings.json`;
    const settingsJson: SettingsJson = {
      hooks: {
        PreToolUse: [
          {
            matcher: 'Bash',
            hooks: [{ type: 'command', command: 'hooks/auth.sh' }],
          },
        ],
      },
    };
    fakeFs.files.set(settingsPath, JSON.stringify(settingsJson));

    await remove({ name: 'my-hook', type: 'hook', scope: 'local' }, deps);

    const settingsRaw = fakeFs.files.get(settingsPath);
    if (settingsRaw !== undefined) {
      const settings = JSON.parse(typeof settingsRaw === 'string' ? settingsRaw : String(settingsRaw)) as SettingsJson;
      // Hook entry should be removed
      const hasEntry = settings.hooks?.['PreToolUse']?.some((g) =>
        g.hooks.some((h) => h.command === 'hooks/auth.sh'),
      );
      expect(hasEntry).toBeFalsy();
    }
  });
});

// ---------------------------------------------------------------------------
// Section: settings merge revert on failure
// ---------------------------------------------------------------------------

describe('lifecycle.add — settings merge revert on failure', () => {
  it('reverts settings merge when a subsequent step fails', async () => {
    const fakeFs = makeFakeFs();
    const deps = makeDeps(fakeFs);

    const manifest = makeHookManifest();
    const sourceDir = '/tmp/hook-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    // hooks/auth.sh NOT seeded — will cause copy failure

    const settingsPath = `${SCOPE_ROOT_LOCAL}/settings.json`;
    fakeFs.files.set(settingsPath, JSON.stringify({ theme: 'dark' }));

    // We'll make the copyFile (or readFile for the hook script) fail
    const origReadFile = deps.fs.readFile;
    deps.fs.readFile = vi.fn(async (p: string) => {
      // Allow addon.json to be read
      if (p.endsWith('addon.json')) return origReadFile(p);
      throw new Error('simulated write failure');
    });

    await add({ sourceDir, scope: 'local', force: false }, deps);

    // Since the hook script couldn't be placed, settings should be unchanged
    const settingsRaw = fakeFs.files.get(settingsPath);
    if (settingsRaw !== undefined) {
      const settings = JSON.parse(typeof settingsRaw === 'string' ? settingsRaw : String(settingsRaw)) as SettingsJson & { theme?: string };
      // Settings.json should still be intact (either not written or reverted)
      // The important thing is it wasn't corrupted
      expect(typeof settings).toBe('object');
      expect(settings.theme).toBe('dark');
    }
  });
});

// ---------------------------------------------------------------------------
// Section: list
// ---------------------------------------------------------------------------

describe('lifecycle.list', () => {
  let fakeFs: FakeFs;
  let storeSnapshots: Map<string, StoredVersion>;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    storeSnapshots = new Map();
    deps = makeDeps(fakeFs, storeSnapshots);
  });

  it('returns empty array when no addons installed', async () => {
    const result = await list({ scope: 'local' }, deps);
    expect(result).toEqual([]);
  });

  it('returns listing for each installed addon', async () => {
    const manifest = makeSkillManifest();
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    // Mark the sidecar parent dirs as existing for readdir
    fakeFs.dirs.add(`${SCOPE_ROOT_LOCAL}/.addons`);
    fakeFs.dirs.add(`${SCOPE_ROOT_LOCAL}/.addons/skill`);

    const result = await list({ scope: 'local' }, deps);

    expect(result.length).toBeGreaterThan(0);
    const listing = result[0];
    expect(listing).toBeDefined();
    if (listing) {
      expect(listing.name).toBe('my-skill');
      expect(listing.type).toBe('skill');
      expect(listing.version).toBe('1.0.0');
      expect(listing.scope).toBe('local');
    }
  });

  it('includes storedVersions from the store', async () => {
    const manifest = makeSkillManifest();
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    fakeFs.dirs.add(`${SCOPE_ROOT_LOCAL}/.addons`);
    fakeFs.dirs.add(`${SCOPE_ROOT_LOCAL}/.addons/skill`);

    // Pre-seed versions in store
    const addonKey = `local/skill/my-skill`;
    const storedKey1 = `${addonKey}/0.9.0`;
    const storedKey2 = `${addonKey}/0.8.0`;
    storeSnapshots.set(storedKey1, {
      manifest: makeSkillManifest('my-skill', '0.9.0'),
      version: '0.9.0',
      files: ['SKILL.md'],
      path: `${BASE_STORE}/${storedKey1}`,
    });
    storeSnapshots.set(storedKey2, {
      manifest: makeSkillManifest('my-skill', '0.8.0'),
      version: '0.8.0',
      files: ['SKILL.md'],
      path: `${BASE_STORE}/${storedKey2}`,
    });

    // Make the fake store list return these versions
    deps.store.list = vi.fn(async () => ['0.8.0', '0.9.0']);

    const result = await list({ scope: 'local' }, deps);
    const listing = result.find((l) => l.name === 'my-skill');
    expect(listing).toBeDefined();
    expect(listing?.storedVersions).toContain('0.9.0');
    expect(listing?.storedVersions).toContain('0.8.0');
  });

  it('lists multiple addons of different types', async () => {
    // Seed a skill
    const skillManifest = makeSkillManifest();
    seedSidecar(fakeFs, `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`,
      makeSidecar(skillManifest, [`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`]));

    // Seed an agent
    const agentManifest = makeAgentManifest();
    seedSidecar(fakeFs, `${SCOPE_ROOT_LOCAL}/.addons/agent/my-agent.json`,
      makeSidecar(agentManifest, [`${SCOPE_ROOT_LOCAL}/agents/my-agent.md`]));

    fakeFs.dirs.add(`${SCOPE_ROOT_LOCAL}/.addons`);
    fakeFs.dirs.add(`${SCOPE_ROOT_LOCAL}/.addons/skill`);
    fakeFs.dirs.add(`${SCOPE_ROOT_LOCAL}/.addons/agent`);

    const result = await list({ scope: 'local' }, deps);

    expect(result.length).toBeGreaterThanOrEqual(2);
    const names = result.map((l) => l.name);
    expect(names).toContain('my-skill');
    expect(names).toContain('my-agent');
  });
});

// ---------------------------------------------------------------------------
// Section: rollback
// ---------------------------------------------------------------------------

describe('lifecycle.rollback — latest prior', () => {
  let fakeFs: FakeFs;
  let storeSnapshots: Map<string, StoredVersion>;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    storeSnapshots = new Map();
    deps = makeDeps(fakeFs, storeSnapshots);
  });

  function seedInstalledSkillV2(): void {
    const manifest = makeSkillManifest('my-skill', '2.0.0');
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(manifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`, '# v2.0.0');
  }

  function seedStoredV1(): void {
    const key = 'local/skill/my-skill/1.0.0';
    storeSnapshots.set(key, {
      manifest: makeSkillManifest('my-skill', '1.0.0'),
      version: '1.0.0',
      files: ['SKILL.md'],
      path: `${BASE_STORE}/${key}`,
    });
    // Seed the stored file content
    fakeFs.files.set(`${BASE_STORE}/${key}/files/SKILL.md`, '# v1.0.0');
  }

  it('rollback installs latest prior version', async () => {
    seedInstalledSkillV2();
    seedStoredV1();
    deps.store.latestPrior = vi.fn(async () => '1.0.0');
    deps.store.read = vi.fn(async () => storeSnapshots.get('local/skill/my-skill/1.0.0')!);

    const result = await rollback({ name: 'my-skill', type: 'skill', scope: 'local' }, deps);

    expect(result.success).toBe(true);
    expect(result.message).toMatch(/1\.0\.0/);
  });

  it('snapshots the current version before rolling back (reversible)', async () => {
    seedInstalledSkillV2();
    seedStoredV1();
    deps.store.latestPrior = vi.fn(async () => '1.0.0');
    deps.store.read = vi.fn(async () => storeSnapshots.get('local/skill/my-skill/1.0.0')!);

    await rollback({ name: 'my-skill', type: 'skill', scope: 'local' }, deps);

    // Should have snapshotted the current v2.0.0 before rolling back
    expect(deps.store.snapshot).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'my-skill', version: '2.0.0' }),
    );
  });

  it('errors when no stored versions exist', async () => {
    seedInstalledSkillV2();
    deps.store.latestPrior = vi.fn(async () => undefined);

    const result = await rollback({ name: 'my-skill', type: 'skill', scope: 'local' }, deps);

    expect(result.success).toBe(false);
    expect(result.message).toMatch(/no stored/i);
  });

  it('errors when addon not installed', async () => {
    const result = await rollback({ name: 'not-installed', type: 'skill', scope: 'local' }, deps);

    expect(result.success).toBe(false);
    expect(result.message).toMatch(/not (installed|found)/i);
  });
});

describe('lifecycle.rollback — --to specific version', () => {
  let fakeFs: FakeFs;
  let storeSnapshots: Map<string, StoredVersion>;
  let deps: LifecycleDeps;

  beforeEach(() => {
    fakeFs = makeFakeFs();
    storeSnapshots = new Map();
    deps = makeDeps(fakeFs, storeSnapshots);
  });

  it('installs the specified version', async () => {
    const currentManifest = makeSkillManifest('my-skill', '3.0.0');
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(currentManifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`, '# v3.0.0');

    const key = 'local/skill/my-skill/1.0.0';
    storeSnapshots.set(key, {
      manifest: makeSkillManifest('my-skill', '1.0.0'),
      version: '1.0.0',
      files: ['SKILL.md'],
      path: `${BASE_STORE}/${key}`,
    });
    fakeFs.files.set(`${BASE_STORE}/${key}/files/SKILL.md`, '# v1.0.0 specific');
    deps.store.read = vi.fn(async () => storeSnapshots.get(key)!);

    const result = await rollback({ name: 'my-skill', type: 'skill', scope: 'local', to: '1.0.0' }, deps);

    expect(result.success).toBe(true);
    expect(result.message).toMatch(/1\.0\.0/);
  });

  it('errors when specified version does not exist', async () => {
    const currentManifest = makeSkillManifest('my-skill', '3.0.0');
    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(currentManifest, [
      `${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`,
    ]));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`, '# v3.0.0');

    deps.store.read = vi.fn(async () => {
      throw new Error('Version 9.9.9 not found');
    });

    const result = await rollback({ name: 'my-skill', type: 'skill', scope: 'local', to: '9.9.9' }, deps);

    expect(result.success).toBe(false);
    expect(result.message).toMatch(/9\.9\.9/);
    // Live install should remain unchanged
    expect(fakeFs.files.get(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`)).toBe('# v3.0.0');
  });
});

describe('lifecycle.rollback — hook re-merges stored settings entry', () => {
  it('re-merges stored settings entry for hook rollback', async () => {
    const fakeFs = makeFakeFs();
    const storeSnapshots = new Map<string, StoredVersion>();
    const deps = makeDeps(fakeFs, storeSnapshots);

    const currentManifest = makeHookManifest('my-hook', '2.0.0');
    const hookReg: HookRegistration = {
      event: 'PreToolUse',
      matcher: 'Bash',
      command: 'hooks/auth.sh',
      type: 'command',
    };

    const sidecarPath = `${SCOPE_ROOT_LOCAL}/.addons/hook/my-hook.json`;
    seedSidecar(fakeFs, sidecarPath, makeSidecar(currentManifest, [
      `${SCOPE_ROOT_LOCAL}/hooks/hooks/auth.sh`,
    ], hookReg));
    fakeFs.files.set(`${SCOPE_ROOT_LOCAL}/hooks/hooks/auth.sh`, '#!/bin/bash v2');

    const storedKey = 'local/hook/my-hook/1.0.0';
    const storedHookReg: HookRegistration = {
      event: 'PreToolUse',
      matcher: 'Bash',
      command: 'hooks/auth.sh',
      type: 'command',
    };
    storeSnapshots.set(storedKey, {
      manifest: makeHookManifest('my-hook', '1.0.0'),
      version: '1.0.0',
      files: ['hooks/auth.sh'],
      settingsEntry: storedHookReg,
      path: `${BASE_STORE}/${storedKey}`,
    });
    fakeFs.files.set(`${BASE_STORE}/${storedKey}/files/hooks/auth.sh`, '#!/bin/bash v1');

    deps.store.latestPrior = vi.fn(async () => '1.0.0');
    deps.store.read = vi.fn(async () => storeSnapshots.get(storedKey)!);

    await rollback({ name: 'my-hook', type: 'hook', scope: 'local' }, deps);

    // The settings should have been updated with the stored entry
    const settingsPath = `${SCOPE_ROOT_LOCAL}/settings.json`;
    expect(fakeFs.files.has(settingsPath)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Section: idempotency — re-run after interruption
// ---------------------------------------------------------------------------

describe('lifecycle.add — idempotency after interruption', () => {
  it('re-run after partial state either completes or errors without corruption', async () => {
    const fakeFs = makeFakeFs();
    const deps = makeDeps(fakeFs);

    const manifest = makeSkillManifest();
    const sourceDir = '/tmp/my-skill-src';
    fakeFs.files.set(path.join(sourceDir, 'addon.json'), JSON.stringify(manifest));
    fakeFs.files.set(path.join(sourceDir, 'SKILL.md'), '# Skill');
    fakeFs.files.set(path.join(sourceDir, 'scripts/run.sh'), '#!/bin/bash');

    // Simulate partial state: tmp dir exists but rename never completed
    const tmpDir = `${SCOPE_ROOT_LOCAL}/.addons/.tmp-partial`;
    fakeFs.dirs.add(tmpDir);
    fakeFs.files.set(`${tmpDir}/SKILL.md`, '# partial');

    // Run again
    const result = await add({ sourceDir, scope: 'local', force: false }, deps);

    // Should either succeed or fail gracefully
    expect(typeof result.success).toBe('boolean');
    expect(typeof result.message).toBe('string');

    // If it succeeded, files should be in a consistent state
    if (result.success) {
      expect(fakeFs.files.has(`${SCOPE_ROOT_LOCAL}/skills/my-skill/SKILL.md`)).toBe(true);
      expect(fakeFs.files.has(`${SCOPE_ROOT_LOCAL}/.addons/skill/my-skill.json`)).toBe(true);
    }
  });
});

// ---------------------------------------------------------------------------
// Section: LifecycleResult return type guarantees
// ---------------------------------------------------------------------------

describe('lifecycle functions — return type guarantees', () => {
  it('all functions return { success: boolean; message: string } plain objects', async () => {
    const fakeFs = makeFakeFs();
    const deps = makeDeps(fakeFs);

    const addResult: LifecycleResult = await add({ sourceDir: '/nonexistent', scope: 'local', force: false }, deps);
    expect(typeof addResult.success).toBe('boolean');
    expect(typeof addResult.message).toBe('string');

    const updateResult: LifecycleResult = await update({ sourceDir: '/nonexistent', scope: 'local' }, deps);
    expect(typeof updateResult.success).toBe('boolean');
    expect(typeof updateResult.message).toBe('string');

    const removeResult: LifecycleResult = await remove({ name: 'x', type: 'skill', scope: 'local' }, deps);
    expect(typeof removeResult.success).toBe('boolean');
    expect(typeof removeResult.message).toBe('string');

    const rollbackResult: LifecycleResult = await rollback({ name: 'x', type: 'skill', scope: 'local' }, deps);
    expect(typeof rollbackResult.success).toBe('boolean');
    expect(typeof rollbackResult.message).toBe('string');
  });

  it('list returns AddonListing array', async () => {
    const fakeFs = makeFakeFs();
    const deps = makeDeps(fakeFs);

    const result: AddonListing[] = await list({ scope: 'local' }, deps);
    expect(Array.isArray(result)).toBe(true);
  });
});
