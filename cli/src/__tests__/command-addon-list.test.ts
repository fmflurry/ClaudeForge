/**
 * Tests for src/commands/addon-list.ts
 *
 * Covers:
 *   - List local scope shows installed add-ons
 *   - List global scope shows installed add-ons
 *   - No --scope flag lists both scopes with labels
 *   - Empty scope reports "No add-ons found in <scope> scope"
 *   - Live version and stored versions appear in output
 *   - Invalid scope error
 */

import { describe, it, expect, vi } from 'vitest';
import { runAddonList } from '../commands/addon-list.js';
import type { AddonListDeps } from '../commands/addon-list.js';
import type { LifecycleDeps, AddonListing } from '../addon/lifecycle.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeLifecycleDeps(
  localListings: AddonListing[],
  globalListings: AddonListing[],
): LifecycleDeps {
  const mockStore = {
    snapshot: vi.fn(),
    list: vi.fn(),
    read: vi.fn(),
    latestPrior: vi.fn(),
    prune: vi.fn(),
  };

  const listingMap: Record<string, AddonListing[]> = {
    local: localListings,
    global: globalListings,
  };

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
      ...mockStore,
      list: vi.fn().mockImplementation(({ scope }: { scope: string }) => {
        const found = listingMap[scope];
        return Promise.resolve(found?.map((l) => l.storedVersions) ?? []);
      }),
    },
    cwd: '/fake/cwd',
    homeDir: '/fake/home',
  };
}

function makeDepsWithListings(
  localListings: AddonListing[],
  globalListings: AddonListing[],
): AddonListDeps {
  // We can't directly inject the list() function result without mocking the whole lifecycle,
  // so we use a mock FS that drives addons dir scanning.
  // For simplicity, build a deps object that has a custom lifecycle that drives list().
  const base = makeLifecycleDeps(localListings, globalListings);

  // Make exists return true for .addons dir and sidecar files
  const localSidecars = localListings.map((l) => `/fake/cwd/.claude/.addons/${l.type}/${l.name}.json`);
  const globalSidecars = globalListings.map((l) => `/fake/home/.claude/.addons/${l.type}/${l.name}.json`);

  const allDirs = new Set([
    '/fake/cwd/.claude/.addons',
    ...localListings.map((l) => `/fake/cwd/.claude/.addons/${l.type}`),
    '/fake/home/.claude/.addons',
    ...globalListings.map((l) => `/fake/home/.claude/.addons/${l.type}`),
  ]);

  const allFiles = new Set([...localSidecars, ...globalSidecars]);

  const sidecarContent: Record<string, AddonListing> = {};
  for (const l of [...localListings, ...globalListings]) {
    const localPath = `/fake/cwd/.claude/.addons/${l.type}/${l.name}.json`;
    const globalPath = `/fake/home/.claude/.addons/${l.type}/${l.name}.json`;
    sidecarContent[localPath] = l;
    sidecarContent[globalPath] = l;
  }

  (base.fs.exists as ReturnType<typeof vi.fn>).mockImplementation(async (p: string) => {
    return allDirs.has(p) || allFiles.has(p);
  });

  (base.fs.readdir as ReturnType<typeof vi.fn>).mockImplementation(async (p: string) => {
    // Return type subdirs for .addons dirs
    if (p.endsWith('/.addons')) {
      const scope = p.includes('/fake/cwd') ? 'local' : 'global';
      const listings = scope === 'local' ? localListings : globalListings;
      return [...new Set(listings.map((l) => l.type))];
    }
    // Return sidecar files for type dirs
    for (const [, listing] of Object.entries(sidecarContent)) {
      const dir = listing.scope === 'local'
        ? `/fake/cwd/.claude/.addons/${listing.type}`
        : `/fake/home/.claude/.addons/${listing.type}`;
      if (p === dir) {
        const scopeListings = listing.scope === 'local' ? localListings : globalListings;
        return scopeListings.filter((l) => l.type === listing.type).map((l) => `${l.name}.json`);
      }
    }
    return [];
  });

  (base.fs.readFile as ReturnType<typeof vi.fn>).mockImplementation(async (p: string) => {
    const listing = sidecarContent[p];
    if (listing) {
      return JSON.stringify({
        manifest: {
          name: listing.name,
          version: listing.version,
          type: listing.type,
          supportedScopes: ['local', 'global'],
          files: [],
        },
        placedFiles: [],
      });
    }
    return '{}';
  });

  // Mock store.list to return stored versions
  (base.store.list as ReturnType<typeof vi.fn>).mockImplementation(
    async ({ type, name }: { scope: string; type: string; name: string }) => {
      const all = [...localListings, ...globalListings];
      const found = all.find((l) => l.type === type && l.name === name);
      return found?.storedVersions ?? [];
    },
  );

  return {
    lifecycleDeps: base,
    cwd: '/fake/cwd',
    homeDir: '/fake/home',
  };
}

// ---------------------------------------------------------------------------
// Empty scopes
// ---------------------------------------------------------------------------

describe('runAddonList – empty scope', () => {
  it('reports "No add-ons found in local scope" when local scope is empty and --scope local', async () => {
    const deps = makeDepsWithListings([], []);
    const result = await runAddonList({ scope: 'local' }, deps);
    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('No add-ons found in local scope');
  });

  it('reports "No add-ons found in global scope" when global scope is empty and --scope global', async () => {
    const deps = makeDepsWithListings([], []);
    const result = await runAddonList({ scope: 'global' }, deps);
    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('No add-ons found in global scope');
  });
});

// ---------------------------------------------------------------------------
// Single scope listing
// ---------------------------------------------------------------------------

describe('runAddonList – single scope', () => {
  it('lists add-ons in local scope', async () => {
    const localListings: AddonListing[] = [
      { type: 'agent', name: 'my-agent', version: '1.0.0', storedVersions: [], scope: 'local' },
    ];
    const deps = makeDepsWithListings(localListings, []);
    const result = await runAddonList({ scope: 'local' }, deps);
    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('my-agent');
    expect(result.output).toContain('1.0.0');
    expect(result.output).toContain('agent');
  });

  it('lists add-ons in global scope', async () => {
    const globalListings: AddonListing[] = [
      { type: 'skill', name: 'my-skill', version: '2.3.0', storedVersions: ['1.0.0'], scope: 'global' },
    ];
    const deps = makeDepsWithListings([], globalListings);
    const result = await runAddonList({ scope: 'global' }, deps);
    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('my-skill');
    expect(result.output).toContain('2.3.0');
  });

  it('shows stored versions in the output', async () => {
    const localListings: AddonListing[] = [
      { type: 'skill', name: 'versioned-skill', version: '2.0.0', storedVersions: ['0.9.0', '1.0.0'], scope: 'local' },
    ];
    const deps = makeDepsWithListings(localListings, []);
    const result = await runAddonList({ scope: 'local' }, deps);
    expect(result.output).toContain('0.9.0');
    expect(result.output).toContain('1.0.0');
  });

  it('returns exitCode 1 on invalid scope', async () => {
    const deps = makeDepsWithListings([], []);
    const result = await runAddonList({ scope: 'galaxy' }, deps);
    expect(result.exitCode).toBe(1);
    expect(result.output).toMatch(/invalid scope/i);
  });
});

// ---------------------------------------------------------------------------
// Both scopes (no --scope flag)
// ---------------------------------------------------------------------------

describe('runAddonList – both scopes', () => {
  it('lists both scopes with "local scope:" and "global scope:" labels', async () => {
    const localListings: AddonListing[] = [
      { type: 'agent', name: 'local-agent', version: '1.0.0', storedVersions: [], scope: 'local' },
    ];
    const globalListings: AddonListing[] = [
      { type: 'skill', name: 'global-skill', version: '2.0.0', storedVersions: [], scope: 'global' },
    ];
    const deps = makeDepsWithListings(localListings, globalListings);
    const result = await runAddonList({}, deps);
    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('local scope:');
    expect(result.output).toContain('global scope:');
    expect(result.output).toContain('local-agent');
    expect(result.output).toContain('global-skill');
  });

  it('shows "No add-ons found in local scope" and global items when local is empty', async () => {
    const globalListings: AddonListing[] = [
      { type: 'hook', name: 'my-hook', version: '0.5.0', storedVersions: [], scope: 'global' },
    ];
    const deps = makeDepsWithListings([], globalListings);
    const result = await runAddonList({}, deps);
    expect(result.exitCode).toBe(0);
    expect(result.output).toContain('No add-ons found in local scope');
    expect(result.output).toContain('my-hook');
  });
});
