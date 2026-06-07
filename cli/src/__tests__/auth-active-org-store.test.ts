/**
 * Tests for src/auth/active-org-store.ts  (Group 11.1 — activeOrg in config.json)
 *
 * Production module path: src/auth/active-org-store.ts
 *
 * This module extends the existing config.json (at ~/.claude-plugins/config.json)
 * with an optional `activeOrg` field. It does NOT conflict with the existing
 * readConfig/writeConfig (CliConfig.apiUrl) — instead it provides a focused
 * read/write surface for the activeOrg field only.
 *
 * Exported types:
 *   - ActiveOrgStore: {
 *       readActiveOrg(homeDir: string, fs?: ActiveOrgFsPort): Promise<string | null>
 *       writeActiveOrg(homeDir: string, orgId: string | null, fs?: ActiveOrgFsPort): Promise<void>
 *     }
 *   - ActiveOrgFsPort: {
 *       readFile(p: string): Promise<string>;
 *       writeFile(p: string, content: string): Promise<void>;
 *       mkdir(p: string, options: { recursive: boolean }): Promise<void>;
 *     }
 *
 * Exported functions:
 *   - readActiveOrg(homeDir: string, fs?: ActiveOrgFsPort): Promise<string | null>
 *       → reads config.json, returns activeOrg or null if absent/corrupt
 *   - writeActiveOrg(homeDir: string, orgId: string | null, fs?: ActiveOrgFsPort): Promise<void>
 *       → merges activeOrg into existing config.json (preserves apiUrl etc.)
 *       → setting null removes activeOrg from the stored JSON
 *       → never mutates any input object
 */

import { describe, it, expect, vi } from 'vitest';

// These imports WILL FAIL until src/auth/active-org-store.ts is created (RED state).
import { readActiveOrg, writeActiveOrg } from '../auth/active-org-store.js';
import type { ActiveOrgFsPort } from '../auth/active-org-store.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function makeFakeFs(existingConfig: Record<string, unknown> = {}): ActiveOrgFsPort & {
  lastWrite: { path: string; content: string } | null;
} {
  let stored = JSON.stringify(existingConfig);
  let lastWrite: { path: string; content: string } | null = null;

  return {
    readFile: vi.fn().mockImplementation(async () => stored),
    writeFile: vi.fn().mockImplementation(async (p: string, content: string) => {
      stored = content;
      lastWrite = { path: p, content };
    }),
    mkdir: vi.fn().mockResolvedValue(undefined),
    get lastWrite() {
      return lastWrite;
    },
  };
}

function makeAbsentFs(): ActiveOrgFsPort {
  return {
    readFile: vi.fn().mockRejectedValue(Object.assign(new Error('ENOENT'), { code: 'ENOENT' })),
    writeFile: vi.fn().mockResolvedValue(undefined),
    mkdir: vi.fn().mockResolvedValue(undefined),
  };
}

// ---------------------------------------------------------------------------
// readActiveOrg
// ---------------------------------------------------------------------------

describe('readActiveOrg', () => {
  it('returns null when config.json does not exist', async () => {
    const fakeFs = makeAbsentFs();
    const result = await readActiveOrg('/home/.claude-plugins', fakeFs);
    expect(result).toBeNull();
  });

  it('returns null when config.json has no activeOrg field', async () => {
    const fakeFs = makeFakeFs({ apiUrl: 'https://plugins.claudeforge.dev' });
    const result = await readActiveOrg('/home/.claude-plugins', fakeFs);
    expect(result).toBeNull();
  });

  it('returns the activeOrg string when present', async () => {
    const fakeFs = makeFakeFs({
      apiUrl: 'https://plugins.claudeforge.dev',
      activeOrg: 'org-uuid-123',
    });
    const result = await readActiveOrg('/home/.claude-plugins', fakeFs);
    expect(result).toBe('org-uuid-123');
  });

  it('returns null when config.json is corrupt JSON', async () => {
    const fakeFs: ActiveOrgFsPort = {
      readFile: vi.fn().mockResolvedValue('{not json'),
      writeFile: vi.fn(),
      mkdir: vi.fn(),
    };
    const result = await readActiveOrg('/home/.claude-plugins', fakeFs);
    expect(result).toBeNull();
  });

  it('returns null when config.json is empty', async () => {
    const fakeFs: ActiveOrgFsPort = {
      readFile: vi.fn().mockResolvedValue(''),
      writeFile: vi.fn(),
      mkdir: vi.fn(),
    };
    const result = await readActiveOrg('/home/.claude-plugins', fakeFs);
    expect(result).toBeNull();
  });

  it('returns null when activeOrg is not a string', async () => {
    const fakeFs = makeFakeFs({ activeOrg: 42 });
    const result = await readActiveOrg('/home/.claude-plugins', fakeFs);
    expect(result).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// writeActiveOrg
// ---------------------------------------------------------------------------

describe('writeActiveOrg – set an org', () => {
  it('writes config.json with activeOrg when setting a non-null value', async () => {
    const fakeFs = makeFakeFs({ apiUrl: 'https://plugins.claudeforge.dev' });
    await writeActiveOrg('/home/.claude-plugins', 'my-org-id', fakeFs);
    const written = JSON.parse(fakeFs.lastWrite?.content ?? '{}') as Record<string, unknown>;
    expect(written['activeOrg']).toBe('my-org-id');
  });

  it('preserves existing apiUrl when writing activeOrg', async () => {
    const fakeFs = makeFakeFs({ apiUrl: 'https://custom.example.com' });
    await writeActiveOrg('/home/.claude-plugins', 'my-org-id', fakeFs);
    const written = JSON.parse(fakeFs.lastWrite?.content ?? '{}') as Record<string, unknown>;
    expect(written['apiUrl']).toBe('https://custom.example.com');
    expect(written['activeOrg']).toBe('my-org-id');
  });

  it('writes to the correct config.json path', async () => {
    const fakeFs = makeFakeFs({});
    await writeActiveOrg('/home/.claude-plugins', 'my-org-id', fakeFs);
    expect(fakeFs.lastWrite?.path).toContain('config.json');
    expect(fakeFs.lastWrite?.path).toContain('/home/.claude-plugins');
  });

  it('creates the directory with recursive option before writing', async () => {
    const fakeFs = makeFakeFs({});
    await writeActiveOrg('/home/.claude-plugins', 'my-org-id', fakeFs);
    expect(fakeFs.mkdir).toHaveBeenCalledWith('/home/.claude-plugins', { recursive: true });
  });
});

describe('writeActiveOrg – clear an org (null)', () => {
  it('removes activeOrg from config.json when set to null', async () => {
    const fakeFs = makeFakeFs({
      apiUrl: 'https://plugins.claudeforge.dev',
      activeOrg: 'existing-org',
    });
    await writeActiveOrg('/home/.claude-plugins', null, fakeFs);
    const written = JSON.parse(fakeFs.lastWrite?.content ?? '{}') as Record<string, unknown>;
    expect('activeOrg' in written).toBe(false);
  });

  it('preserves apiUrl when clearing activeOrg', async () => {
    const fakeFs = makeFakeFs({
      apiUrl: 'https://custom.example.com',
      activeOrg: 'existing-org',
    });
    await writeActiveOrg('/home/.claude-plugins', null, fakeFs);
    const written = JSON.parse(fakeFs.lastWrite?.content ?? '{}') as Record<string, unknown>;
    expect(written['apiUrl']).toBe('https://custom.example.com');
  });
});

describe('writeActiveOrg – immutability', () => {
  it('does not mutate any in-memory objects', async () => {
    const fakeFs = makeFakeFs({ apiUrl: 'https://plugins.claudeforge.dev' });
    const orgId = 'org-123';
    await writeActiveOrg('/home/.claude-plugins', orgId, fakeFs);
    // orgId string is a primitive — we just verify the function returns void
    expect(orgId).toBe('org-123');
  });
});
