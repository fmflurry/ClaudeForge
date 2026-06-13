/**
 * Tests for cli/src/addon/store.ts
 *
 * Covers:
 *  - snapshot creates versioned dir with addon.json + files (+ settings-entry.json for hooks)
 *  - snapshot is atomic: writes to temp dir first, then renames into place
 *  - list returns semver-sorted versions ascending
 *  - latestPrior excludes current version and returns highest prior
 *  - prune keeps N newest and removes older
 *  - read returns stored manifest/files/settingsEntry
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { AddonManifest, HookRegistration } from '../addon/manifest.js';
import type { StoreFsPort, StoredVersion, VersionStore } from '../addon/store.js';
import { createVersionStore } from '../addon/store.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeManifest(name: string, version: string, type: AddonManifest['type'] = 'skill'): AddonManifest {
  return {
    name,
    version,
    type,
    supportedScopes: ['local', 'global'],
    files: ['SKILL.md', 'scripts/run.sh'],
  };
}

function makeHookManifest(name: string, version: string): AddonManifest {
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

function makeHookRegistration(): HookRegistration {
  return {
    event: 'PreToolUse',
    matcher: 'Bash',
    command: 'hooks/auth.sh',
    type: 'command',
  };
}

// ---------------------------------------------------------------------------
// Fake StoreFsPort
// ---------------------------------------------------------------------------

interface MkdirCall {
  path: string;
  opts?: { recursive?: boolean; mode?: number };
}

interface FakeFs {
  files: Map<string, string>;
  dirs: Set<string>;
  renames: { src: string; dest: string }[];
  rms: string[];
  mkdirCalls: MkdirCall[];
}

function makeFakePort(fakeFs: FakeFs): StoreFsPort {
  return {
    mkdir: vi.fn(async (p: string, opts?: { recursive?: boolean; mode?: number }) => {
      fakeFs.dirs.add(p);
      fakeFs.mkdirCalls.push({ path: p, opts });
    }),
    writeFile: vi.fn(async (p: string, content: string) => {
      fakeFs.files.set(p, content);
    }),
    readFile: vi.fn(async (p: string) => {
      const content = fakeFs.files.get(p);
      if (content === undefined) {
        throw new Error(`ENOENT: no such file or directory, open '${p}'`);
      }
      return content;
    }),
    copyFile: vi.fn(async (src: string, dest: string) => {
      const content = fakeFs.files.get(src);
      if (content === undefined) {
        throw new Error(`ENOENT: no such file or directory, open '${src}'`);
      }
      fakeFs.files.set(dest, content);
    }),
    rename: vi.fn(async (src: string, dest: string) => {
      // Move all files with prefix src → dest
      for (const [k, v] of fakeFs.files.entries()) {
        if (k === src || k.startsWith(src + '/')) {
          fakeFs.files.set(dest + k.slice(src.length), v);
          fakeFs.files.delete(k);
        }
      }
      for (const d of fakeFs.dirs) {
        if (d === src || d.startsWith(src + '/')) {
          fakeFs.dirs.add(dest + d.slice(src.length));
          fakeFs.dirs.delete(d);
        }
      }
      fakeFs.renames.push({ src, dest });
    }),
    rm: vi.fn(async (p: string, _opts?: { recursive: boolean; force: boolean }) => {
      fakeFs.rms.push(p);
      // Remove matching files
      for (const k of fakeFs.files.keys()) {
        if (k === p || k.startsWith(p + '/')) {
          fakeFs.files.delete(k);
        }
      }
      for (const d of fakeFs.dirs) {
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
      // Return direct children of p
      for (const k of fakeFs.dirs) {
        if (k.startsWith(p + '/')) {
          const rest = k.slice(p.length + 1);
          const seg = rest.split('/')[0];
          if (seg && !results.includes(seg)) {
            results.push(seg);
          }
        }
      }
      for (const k of fakeFs.files.keys()) {
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
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('createVersionStore — snapshot', () => {
  let fakeFs: FakeFs;
  let port: StoreFsPort;
  let store: VersionStore;
  const baseStorePath = '/fake/home/.claude-plugins/addon-store';

  beforeEach(() => {
    fakeFs = { files: new Map(), dirs: new Set(), renames: [], rms: [], mkdirCalls: [] };
    port = makeFakePort(fakeFs);
    store = createVersionStore({ baseStorePath, port });
  });

  it('creates versioned directory with addon.json after snapshot', async () => {
    const manifest = makeManifest('my-skill', '1.0.0');
    const sourceFiles = [
      { rel: 'SKILL.md', absSource: '/project/SKILL.md' },
      { rel: 'scripts/run.sh', absSource: '/project/scripts/run.sh' },
    ];
    fakeFs.files.set('/project/SKILL.md', '# My Skill');
    fakeFs.files.set('/project/scripts/run.sh', '#!/bin/bash');

    await store.snapshot({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      version: '1.0.0',
      sourceFiles,
      manifest,
    });

    const storedManifestPath = `${baseStorePath}/local/skill/my-skill/1.0.0/addon.json`;
    expect(fakeFs.files.has(storedManifestPath)).toBe(true);
    const storedManifest = JSON.parse(fakeFs.files.get(storedManifestPath)!) as AddonManifest;
    expect(storedManifest.name).toBe('my-skill');
    expect(storedManifest.version).toBe('1.0.0');
  });

  it('copies source files into files/ subdirectory', async () => {
    const manifest = makeManifest('my-skill', '2.0.0');
    fakeFs.files.set('/project/SKILL.md', '# My Skill v2');
    fakeFs.files.set('/project/scripts/run.sh', '#!/bin/bash\necho v2');

    await store.snapshot({
      scope: 'global',
      type: 'skill',
      name: 'my-skill',
      version: '2.0.0',
      sourceFiles: [
        { rel: 'SKILL.md', absSource: '/project/SKILL.md' },
        { rel: 'scripts/run.sh', absSource: '/project/scripts/run.sh' },
      ],
      manifest,
    });

    const filesDir = `${baseStorePath}/global/skill/my-skill/2.0.0/files`;
    expect(fakeFs.files.has(`${filesDir}/SKILL.md`)).toBe(true);
    expect(fakeFs.files.has(`${filesDir}/scripts/run.sh`)).toBe(true);
  });

  it('writes settings-entry.json for hook add-ons', async () => {
    const manifest = makeHookManifest('my-hook', '1.0.0');
    const settingsEntry = makeHookRegistration();
    fakeFs.files.set('/project/hooks/auth.sh', '#!/bin/bash');

    await store.snapshot({
      scope: 'local',
      type: 'hook',
      name: 'my-hook',
      version: '1.0.0',
      sourceFiles: [{ rel: 'hooks/auth.sh', absSource: '/project/hooks/auth.sh' }],
      manifest,
      settingsEntry,
    });

    const settingsEntryPath = `${baseStorePath}/local/hook/my-hook/1.0.0/settings-entry.json`;
    expect(fakeFs.files.has(settingsEntryPath)).toBe(true);
    const stored = JSON.parse(fakeFs.files.get(settingsEntryPath)!) as HookRegistration;
    expect(stored.event).toBe('PreToolUse');
    expect(stored.matcher).toBe('Bash');
    expect(stored.command).toBe('hooks/auth.sh');
  });

  it('does NOT write settings-entry.json for non-hook add-ons', async () => {
    const manifest = makeManifest('my-skill', '1.0.0');
    fakeFs.files.set('/project/SKILL.md', '# skill');

    await store.snapshot({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      version: '1.0.0',
      sourceFiles: [{ rel: 'SKILL.md', absSource: '/project/SKILL.md' }],
      manifest,
    });

    const settingsEntryPath = `${baseStorePath}/local/skill/my-skill/1.0.0/settings-entry.json`;
    expect(fakeFs.files.has(settingsEntryPath)).toBe(false);
  });

  it('returns the stored path', async () => {
    const manifest = makeManifest('my-skill', '3.0.0');
    fakeFs.files.set('/project/SKILL.md', '# skill');

    const stored = await store.snapshot({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      version: '3.0.0',
      sourceFiles: [{ rel: 'SKILL.md', absSource: '/project/SKILL.md' }],
      manifest,
    });

    expect(stored).toBe(`${baseStorePath}/local/skill/my-skill/3.0.0`);
  });
});

describe('createVersionStore — atomic write (temp then rename)', () => {
  let fakeFs: FakeFs;
  let port: StoreFsPort;
  let store: VersionStore;
  const baseStorePath = '/fake/store';

  beforeEach(() => {
    fakeFs = { files: new Map(), dirs: new Set(), renames: [], rms: [], mkdirCalls: [] };
    port = makeFakePort(fakeFs);
    store = createVersionStore({ baseStorePath, port });
  });

  it('writes to a temp dir first, then renames to final path', async () => {
    const manifest = makeManifest('my-skill', '1.0.0');
    fakeFs.files.set('/project/SKILL.md', '# skill');

    const renameCallsBefore = fakeFs.renames.length;

    await store.snapshot({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      version: '1.0.0',
      sourceFiles: [{ rel: 'SKILL.md', absSource: '/project/SKILL.md' }],
      manifest,
    });

    // At least one rename should have occurred after the snapshot
    expect(fakeFs.renames.length).toBeGreaterThan(renameCallsBefore);

    // The destination of the last rename should be the final version path
    const lastRename = fakeFs.renames[fakeFs.renames.length - 1];
    expect(lastRename.dest).toBe(`${baseStorePath}/local/skill/my-skill/1.0.0`);

    // The source should be a temp path (contains .tmp-)
    expect(lastRename.src).toMatch(/\.tmp-/);
  });

  it('temp dir is within the same parent directory (for same-filesystem rename)', async () => {
    const manifest = makeManifest('my-agent', '1.0.0', 'agent');
    fakeFs.files.set('/project/my-agent.md', '# agent');

    await store.snapshot({
      scope: 'global',
      type: 'agent',
      name: 'my-agent',
      version: '1.0.0',
      sourceFiles: [{ rel: 'my-agent.md', absSource: '/project/my-agent.md' }],
      manifest,
    });

    const lastRename = fakeFs.renames[fakeFs.renames.length - 1];
    const expectedParent = `${baseStorePath}/global/agent/my-agent`;
    expect(lastRename.src).toContain(expectedParent);
    expect(lastRename.dest).toContain(expectedParent);
  });
});

describe('createVersionStore — list', () => {
  let fakeFs: FakeFs;
  let port: StoreFsPort;
  let store: VersionStore;
  const baseStorePath = '/fake/store';

  beforeEach(() => {
    fakeFs = { files: new Map(), dirs: new Set(), renames: [], rms: [], mkdirCalls: [] };
    port = makeFakePort(fakeFs);
    store = createVersionStore({ baseStorePath, port });
  });

  it('returns an empty array when no versions are stored', async () => {
    const versions = await store.list({ scope: 'local', type: 'skill', name: 'nonexistent' });
    expect(versions).toEqual([]);
  });

  it('returns stored versions in semver ascending order', async () => {
    // Pre-populate fake dirs to simulate already-stored versions
    const addonDir = `${baseStorePath}/local/skill/my-skill`;
    fakeFs.dirs.add(`${addonDir}/2.0.0`);
    fakeFs.dirs.add(`${addonDir}/1.0.0`);
    fakeFs.dirs.add(`${addonDir}/1.10.0`);
    fakeFs.dirs.add(`${addonDir}/1.2.0`);
    // Add addon.json so exists() returns true for each
    fakeFs.files.set(`${addonDir}/2.0.0/addon.json`, '{}');
    fakeFs.files.set(`${addonDir}/1.0.0/addon.json`, '{}');
    fakeFs.files.set(`${addonDir}/1.10.0/addon.json`, '{}');
    fakeFs.files.set(`${addonDir}/1.2.0/addon.json`, '{}');

    const versions = await store.list({ scope: 'local', type: 'skill', name: 'my-skill' });

    expect(versions).toEqual(['1.0.0', '1.2.0', '1.10.0', '2.0.0']);
  });

  it('semver sorts correctly: 1.9.0 < 1.10.0', async () => {
    const addonDir = `${baseStorePath}/global/hook/my-hook`;
    fakeFs.dirs.add(`${addonDir}/1.9.0`);
    fakeFs.dirs.add(`${addonDir}/1.10.0`);
    fakeFs.files.set(`${addonDir}/1.9.0/addon.json`, '{}');
    fakeFs.files.set(`${addonDir}/1.10.0/addon.json`, '{}');

    const versions = await store.list({ scope: 'global', type: 'hook', name: 'my-hook' });
    expect(versions).toEqual(['1.9.0', '1.10.0']);
  });
});

describe('createVersionStore — latestPrior', () => {
  let fakeFs: FakeFs;
  let port: StoreFsPort;
  let store: VersionStore;
  const baseStorePath = '/fake/store';

  beforeEach(() => {
    fakeFs = { files: new Map(), dirs: new Set(), renames: [], rms: [], mkdirCalls: [] };
    port = makeFakePort(fakeFs);
    store = createVersionStore({ baseStorePath, port });
  });

  function seedVersions(type: AddonManifest['type'], name: string, versions: string[]): void {
    const addonDir = `${baseStorePath}/local/${type}/${name}`;
    for (const v of versions) {
      fakeFs.dirs.add(`${addonDir}/${v}`);
      fakeFs.files.set(`${addonDir}/${v}/addon.json`, '{}');
    }
  }

  it('returns undefined when no versions are stored', async () => {
    const prior = await store.latestPrior({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      currentVersion: '1.0.0',
    });
    expect(prior).toBeUndefined();
  });

  it('returns undefined when only the current version is stored', async () => {
    seedVersions('skill', 'my-skill', ['1.0.0']);

    const prior = await store.latestPrior({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      currentVersion: '1.0.0',
    });
    expect(prior).toBeUndefined();
  });

  it('returns the highest version strictly less than currentVersion', async () => {
    seedVersions('skill', 'my-skill', ['1.0.0', '1.2.0', '2.0.0', '2.1.0']);

    const prior = await store.latestPrior({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      currentVersion: '2.0.0',
    });
    expect(prior).toBe('1.2.0');
  });

  it('excludes currentVersion even when it is stored', async () => {
    seedVersions('hook', 'my-hook', ['1.0.0', '1.1.0', '1.2.0']);

    const prior = await store.latestPrior({
      scope: 'local',
      type: 'hook',
      name: 'my-hook',
      currentVersion: '1.2.0',
    });
    expect(prior).toBe('1.1.0');
  });

  it('returns undefined if all stored versions equal or exceed currentVersion', async () => {
    seedVersions('agent', 'my-agent', ['2.0.0', '3.0.0']);

    const prior = await store.latestPrior({
      scope: 'local',
      type: 'agent',
      name: 'my-agent',
      currentVersion: '1.0.0',
    });
    expect(prior).toBeUndefined();
  });

  it('handles single prior version correctly', async () => {
    seedVersions('skill', 'my-skill', ['1.0.0', '2.0.0']);

    const prior = await store.latestPrior({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      currentVersion: '2.0.0',
    });
    expect(prior).toBe('1.0.0');
  });
});

describe('createVersionStore — prune', () => {
  let fakeFs: FakeFs;
  let port: StoreFsPort;
  let store: VersionStore;
  const baseStorePath = '/fake/store';

  beforeEach(() => {
    fakeFs = { files: new Map(), dirs: new Set(), renames: [], rms: [], mkdirCalls: [] };
    port = makeFakePort(fakeFs);
    store = createVersionStore({ baseStorePath, port });
  });

  function seedVersions(name: string, versions: string[]): void {
    const addonDir = `${baseStorePath}/local/skill/${name}`;
    for (const v of versions) {
      fakeFs.dirs.add(`${addonDir}/${v}`);
      fakeFs.files.set(`${addonDir}/${v}/addon.json`, '{}');
    }
  }

  it('keeps the N newest versions and removes the rest', async () => {
    seedVersions('my-skill', ['1.0.0', '1.1.0', '1.2.0', '2.0.0', '2.1.0', '3.0.0']);

    await store.prune({ scope: 'local', type: 'skill', name: 'my-skill', keep: 3 });

    // Should keep 2.0.0, 2.1.0, 3.0.0 (the 3 newest)
    expect(fakeFs.rms).toContain(`${baseStorePath}/local/skill/my-skill/1.0.0`);
    expect(fakeFs.rms).toContain(`${baseStorePath}/local/skill/my-skill/1.1.0`);
    expect(fakeFs.rms).toContain(`${baseStorePath}/local/skill/my-skill/1.2.0`);
    // 2.0.0, 2.1.0, 3.0.0 should NOT be removed
    expect(fakeFs.rms).not.toContain(`${baseStorePath}/local/skill/my-skill/2.0.0`);
    expect(fakeFs.rms).not.toContain(`${baseStorePath}/local/skill/my-skill/2.1.0`);
    expect(fakeFs.rms).not.toContain(`${baseStorePath}/local/skill/my-skill/3.0.0`);
  });

  it('does not remove anything when stored count <= keep', async () => {
    seedVersions('my-skill', ['1.0.0', '2.0.0']);

    await store.prune({ scope: 'local', type: 'skill', name: 'my-skill', keep: 5 });

    expect(fakeFs.rms).toHaveLength(0);
  });

  it('removes all but keep=1 newest', async () => {
    seedVersions('my-skill', ['1.0.0', '2.0.0', '3.0.0']);

    await store.prune({ scope: 'local', type: 'skill', name: 'my-skill', keep: 1 });

    expect(fakeFs.rms).toContain(`${baseStorePath}/local/skill/my-skill/1.0.0`);
    expect(fakeFs.rms).toContain(`${baseStorePath}/local/skill/my-skill/2.0.0`);
    expect(fakeFs.rms).not.toContain(`${baseStorePath}/local/skill/my-skill/3.0.0`);
  });

  it('removes with recursive: true, force: true', async () => {
    seedVersions('my-skill', ['1.0.0', '2.0.0', '3.0.0']);

    await store.prune({ scope: 'local', type: 'skill', name: 'my-skill', keep: 2 });

    expect(port.rm).toHaveBeenCalledWith(
      `${baseStorePath}/local/skill/my-skill/1.0.0`,
      { recursive: true, force: true },
    );
  });
});

describe('createVersionStore — read', () => {
  let fakeFs: FakeFs;
  let port: StoreFsPort;
  let store: VersionStore;
  const baseStorePath = '/fake/store';

  beforeEach(() => {
    fakeFs = { files: new Map(), dirs: new Set(), renames: [], rms: [], mkdirCalls: [] };
    port = makeFakePort(fakeFs);
    store = createVersionStore({ baseStorePath, port });
  });

  function seedStoredVersion(
    scope: 'local' | 'global',
    type: AddonManifest['type'],
    name: string,
    version: string,
    manifest: AddonManifest,
    files: Record<string, string>,
    settingsEntry?: HookRegistration,
  ): void {
    const vDir = `${baseStorePath}/${scope}/${type}/${name}/${version}`;
    fakeFs.dirs.add(vDir);
    fakeFs.files.set(`${vDir}/addon.json`, JSON.stringify(manifest));
    // Write files.json listing all relative paths
    fakeFs.files.set(`${vDir}/files.json`, JSON.stringify(Object.keys(files)));
    for (const [rel, content] of Object.entries(files)) {
      fakeFs.files.set(`${vDir}/files/${rel}`, content);
    }
    if (settingsEntry !== undefined) {
      fakeFs.files.set(`${vDir}/settings-entry.json`, JSON.stringify(settingsEntry));
    }
  }

  it('returns stored manifest and file list', async () => {
    const manifest = makeManifest('my-skill', '1.0.0');
    seedStoredVersion('local', 'skill', 'my-skill', '1.0.0', manifest, {
      'SKILL.md': '# skill',
      'scripts/run.sh': '#!/bin/bash',
    });

    const result: StoredVersion = await store.read({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      version: '1.0.0',
    });

    expect(result.manifest.name).toBe('my-skill');
    expect(result.manifest.version).toBe('1.0.0');
    expect(result.version).toBe('1.0.0');
    expect(result.files).toContain('SKILL.md');
    expect(result.files).toContain('scripts/run.sh');
    expect(result.path).toBe(`${baseStorePath}/local/skill/my-skill/1.0.0`);
  });

  it('returns settingsEntry for hook add-ons', async () => {
    const manifest = makeHookManifest('my-hook', '1.0.0');
    const settingsEntry = makeHookRegistration();
    seedStoredVersion('local', 'hook', 'my-hook', '1.0.0', manifest, {
      'hooks/auth.sh': '#!/bin/bash',
    }, settingsEntry);

    const result = await store.read({
      scope: 'local',
      type: 'hook',
      name: 'my-hook',
      version: '1.0.0',
    });

    expect(result.settingsEntry).toBeDefined();
    expect(result.settingsEntry?.event).toBe('PreToolUse');
    expect(result.settingsEntry?.matcher).toBe('Bash');
    expect(result.settingsEntry?.command).toBe('hooks/auth.sh');
  });

  it('settingsEntry is undefined for non-hook add-ons', async () => {
    const manifest = makeManifest('my-skill', '2.0.0');
    seedStoredVersion('global', 'skill', 'my-skill', '2.0.0', manifest, {
      'SKILL.md': '# v2',
    });

    const result = await store.read({
      scope: 'global',
      type: 'skill',
      name: 'my-skill',
      version: '2.0.0',
    });

    expect(result.settingsEntry).toBeUndefined();
  });

  it('throws when version does not exist', async () => {
    await expect(
      store.read({ scope: 'local', type: 'skill', name: 'nonexistent', version: '1.0.0' }),
    ).rejects.toThrow();
  });
});

describe('createVersionStore — plugins home created with mode 0o700', () => {
  let fakeFs: FakeFs;
  let port: StoreFsPort;
  const pluginsHome = '/fake/home/.claude-plugins';
  const baseStorePath = `${pluginsHome}/addon-store`;

  beforeEach(() => {
    fakeFs = { files: new Map(), dirs: new Set(), renames: [], rms: [], mkdirCalls: [] };
    port = makeFakePort(fakeFs);
  });

  it('creates the plugins home with mode 0o700 on snapshot', async () => {
    const store = createVersionStore({ baseStorePath, port, pluginsHome });
    const manifest = makeManifest('my-skill', '1.0.0');
    fakeFs.files.set('/project/SKILL.md', '# My Skill');

    await store.snapshot({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      version: '1.0.0',
      sourceFiles: [{ rel: 'SKILL.md', absSource: '/project/SKILL.md' }],
      manifest,
    });

    const homeCall = fakeFs.mkdirCalls.find((c) => c.path === pluginsHome);
    expect(homeCall).toBeDefined();
    expect(homeCall?.opts?.mode).toBe(0o700);
  });

  it('derives pluginsHome from baseStorePath when not supplied', async () => {
    // No pluginsHome passed — store derives it as path.dirname(baseStorePath)
    const store = createVersionStore({ baseStorePath, port });
    const manifest = makeManifest('my-skill', '1.0.0');
    fakeFs.files.set('/project/SKILL.md', '# My Skill');

    await store.snapshot({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      version: '1.0.0',
      sourceFiles: [{ rel: 'SKILL.md', absSource: '/project/SKILL.md' }],
      manifest,
    });

    const homeCall = fakeFs.mkdirCalls.find((c) => c.path === pluginsHome);
    expect(homeCall).toBeDefined();
    expect(homeCall?.opts?.mode).toBe(0o700);
  });
});

describe('createVersionStore — snapshot auto-prunes', () => {
  let fakeFs: FakeFs;
  let port: StoreFsPort;
  let store: VersionStore;
  const baseStorePath = '/fake/store';

  beforeEach(() => {
    fakeFs = { files: new Map(), dirs: new Set(), renames: [], rms: [], mkdirCalls: [] };
    port = makeFakePort(fakeFs);
    store = createVersionStore({ baseStorePath, port });
  });

  it('prunes after snapshot so only DEFAULT_VERSION_RETENTION versions remain', async () => {
    // Pre-seed 5 versions (the default retention)
    const addonDir = `${baseStorePath}/local/skill/my-skill`;
    for (const v of ['1.0.0', '1.1.0', '1.2.0', '1.3.0', '1.4.0']) {
      fakeFs.dirs.add(`${addonDir}/${v}`);
      fakeFs.files.set(`${addonDir}/${v}/addon.json`, '{}');
    }

    // Snapshot a 6th version — should trigger prune keeping only 5 newest
    const manifest = makeManifest('my-skill', '2.0.0');
    fakeFs.files.set('/project/SKILL.md', '# v6');

    await store.snapshot({
      scope: 'local',
      type: 'skill',
      name: 'my-skill',
      version: '2.0.0',
      sourceFiles: [{ rel: 'SKILL.md', absSource: '/project/SKILL.md' }],
      manifest,
    });

    // 1.0.0 should have been pruned (oldest)
    expect(fakeFs.rms).toContain(`${addonDir}/1.0.0`);
    // 2.0.0 (newly added), 1.4.0, 1.3.0, 1.2.0, 1.1.0 should remain (5 newest)
    expect(fakeFs.rms).not.toContain(`${addonDir}/1.1.0`);
    expect(fakeFs.rms).not.toContain(`${addonDir}/2.0.0`);
  });
});
