/**
 * Tests for src/registry/registry.ts
 *
 * Production module path: src/registry/registry.ts
 * Exported functions / types:
 *   - InstalledRecord interface: { name: string; version: string; installedAt: string; path: string }
 *   - InstalledRegistry interface: { plugins: InstalledRecord[] }
 *   - readRegistry(homeDir: string): Promise<InstalledRegistry>
 *       → reads homeDir/installed.json; returns { plugins: [] } on missing/corrupt
 *   - writeRegistry(homeDir: string, registry: InstalledRegistry): Promise<void>
 *       → writes homeDir/installed.json; creates dir if missing
 *   - addRecord(registry: InstalledRegistry, record: InstalledRecord): InstalledRegistry
 *       → returns NEW registry with record appended (immutable, input not mutated)
 *   - removeRecord(registry: InstalledRegistry, name: string): InstalledRegistry
 *       → returns NEW registry without the matching name (immutable)
 *       → if name not found, returns registry unchanged (no throw)
 *   - findRecord(registry: InstalledRegistry, name: string): InstalledRecord | undefined
 *   - backupsDir(homeDir: string): string
 *       → returns homeDir/backups
 *   - ensureBackupsDir(homeDir: string): Promise<void>
 *       → creates homeDir/backups/ if absent
 */

import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

// These imports WILL FAIL until src/registry/registry.ts is created (RED state).
import {
  readRegistry,
  writeRegistry,
  addRecord,
  removeRecord,
  findRecord,
  backupsDir,
  ensureBackupsDir,
} from '../registry/registry.js';
import type { InstalledRecord, InstalledRegistry } from '../registry/registry.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-plugins-registry-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

function makeRecord(overrides?: Partial<InstalledRecord>): InstalledRecord {
  return {
    name: '@test/plugin-a',
    version: '1.0.0',
    installedAt: '2024-01-01T00:00:00.000Z',
    path: '/tmp/.claude-plugins/plugins/@test/plugin-a',
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// readRegistry
// ---------------------------------------------------------------------------

describe('readRegistry', () => {
  let tmpDir: string;

  beforeEach(async () => {
    tmpDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(tmpDir);
  });

  it('returns empty registry when installed.json does not exist', async () => {
    const reg = await readRegistry(tmpDir);
    expect(reg).toEqual<InstalledRegistry>({ plugins: [] });
  });

  it('returns the stored registry when installed.json is valid', async () => {
    const record = makeRecord();
    const expected: InstalledRegistry = { plugins: [record] };
    await fs.writeFile(
      path.join(tmpDir, 'installed.json'),
      JSON.stringify(expected),
      'utf-8',
    );
    const reg = await readRegistry(tmpDir);
    expect(reg).toEqual(expected);
  });

  it('returns empty registry when installed.json is corrupt JSON', async () => {
    await fs.writeFile(path.join(tmpDir, 'installed.json'), '{bad json', 'utf-8');
    const reg = await readRegistry(tmpDir);
    expect(reg).toEqual<InstalledRegistry>({ plugins: [] });
  });

  it('returns empty registry when installed.json is empty', async () => {
    await fs.writeFile(path.join(tmpDir, 'installed.json'), '', 'utf-8');
    const reg = await readRegistry(tmpDir);
    expect(reg).toEqual<InstalledRegistry>({ plugins: [] });
  });

  it('handles missing directory gracefully (no throw)', async () => {
    const nonExistent = path.join(tmpDir, 'sub-dir-not-created');
    await expect(readRegistry(nonExistent)).resolves.toEqual<InstalledRegistry>({ plugins: [] });
  });
});

// ---------------------------------------------------------------------------
// writeRegistry
// ---------------------------------------------------------------------------

describe('writeRegistry', () => {
  let tmpDir: string;

  beforeEach(async () => {
    tmpDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(tmpDir);
  });

  it('creates installed.json with the supplied registry', async () => {
    const registry: InstalledRegistry = { plugins: [makeRecord()] };
    await writeRegistry(tmpDir, registry);
    const raw = await fs.readFile(path.join(tmpDir, 'installed.json'), 'utf-8');
    expect(JSON.parse(raw)).toEqual(registry);
  });

  it('creates the home directory if absent', async () => {
    const nested = path.join(tmpDir, 'auto-nested');
    const registry: InstalledRegistry = { plugins: [] };
    await writeRegistry(nested, registry);
    const raw = await fs.readFile(path.join(nested, 'installed.json'), 'utf-8');
    expect(JSON.parse(raw)).toEqual(registry);
  });

  it('round-trips: write then read returns the same registry', async () => {
    const registry: InstalledRegistry = { plugins: [makeRecord()] };
    await writeRegistry(tmpDir, registry);
    const result = await readRegistry(tmpDir);
    expect(result).toEqual(registry);
  });
});

// ---------------------------------------------------------------------------
// addRecord (immutable)
// ---------------------------------------------------------------------------

describe('addRecord', () => {
  it('returns a new registry with the record appended', () => {
    const original: InstalledRegistry = { plugins: [] };
    const record = makeRecord();
    const result = addRecord(original, record);
    expect(result.plugins).toHaveLength(1);
    expect(result.plugins[0]).toEqual(record);
  });

  it('does not mutate the original registry', () => {
    const original: InstalledRegistry = { plugins: [] };
    const record = makeRecord();
    addRecord(original, record);
    expect(original.plugins).toHaveLength(0);
  });

  it('appends to an existing list without changing earlier records', () => {
    const first = makeRecord({ name: '@test/plugin-a' });
    const original: InstalledRegistry = { plugins: [first] };
    const second = makeRecord({ name: '@test/plugin-b' });
    const result = addRecord(original, second);
    expect(result.plugins).toHaveLength(2);
    expect(result.plugins[0]).toEqual(first);
    expect(result.plugins[1]).toEqual(second);
  });

  it('allows duplicate names (caller is responsible for deduplication)', () => {
    const rec = makeRecord();
    const original: InstalledRegistry = { plugins: [rec] };
    const result = addRecord(original, { ...rec, version: '2.0.0' });
    expect(result.plugins).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// removeRecord (immutable)
// ---------------------------------------------------------------------------

describe('removeRecord', () => {
  it('returns a new registry without the named plugin', () => {
    const record = makeRecord({ name: '@test/plugin-a' });
    const original: InstalledRegistry = { plugins: [record] };
    const result = removeRecord(original, '@test/plugin-a');
    expect(result.plugins).toHaveLength(0);
  });

  it('does not mutate the original registry', () => {
    const record = makeRecord({ name: '@test/plugin-a' });
    const original: InstalledRegistry = { plugins: [record] };
    removeRecord(original, '@test/plugin-a');
    expect(original.plugins).toHaveLength(1);
  });

  it('returns unchanged registry when name is not found', () => {
    const record = makeRecord({ name: '@test/plugin-a' });
    const original: InstalledRegistry = { plugins: [record] };
    const result = removeRecord(original, '@test/nonexistent');
    expect(result.plugins).toHaveLength(1);
    expect(result.plugins[0]).toEqual(record);
  });

  it('removes only the matching plugin, keeping others', () => {
    const a = makeRecord({ name: '@test/plugin-a' });
    const b = makeRecord({ name: '@test/plugin-b' });
    const original: InstalledRegistry = { plugins: [a, b] };
    const result = removeRecord(original, '@test/plugin-a');
    expect(result.plugins).toHaveLength(1);
    expect(result.plugins[0]).toEqual(b);
  });

  it('handles empty registry without throwing', () => {
    const original: InstalledRegistry = { plugins: [] };
    const result = removeRecord(original, '@test/anything');
    expect(result.plugins).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// findRecord
// ---------------------------------------------------------------------------

describe('findRecord', () => {
  it('returns the matching record', () => {
    const record = makeRecord({ name: '@test/plugin-a' });
    const registry: InstalledRegistry = { plugins: [record] };
    expect(findRecord(registry, '@test/plugin-a')).toEqual(record);
  });

  it('returns undefined when the name does not exist', () => {
    const registry: InstalledRegistry = { plugins: [makeRecord()] };
    expect(findRecord(registry, '@test/nonexistent')).toBeUndefined();
  });

  it('returns undefined on empty registry', () => {
    const registry: InstalledRegistry = { plugins: [] };
    expect(findRecord(registry, '@test/anything')).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// backupsDir / ensureBackupsDir
// ---------------------------------------------------------------------------

describe('backupsDir', () => {
  it('returns homeDir/backups', () => {
    expect(backupsDir('/home/user/.claude-plugins')).toBe(
      '/home/user/.claude-plugins/backups',
    );
  });
});

describe('ensureBackupsDir', () => {
  let tmpDir: string;

  beforeEach(async () => {
    tmpDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(tmpDir);
  });

  it('creates the backups directory when absent', async () => {
    await ensureBackupsDir(tmpDir);
    const stat = await fs.stat(path.join(tmpDir, 'backups'));
    expect(stat.isDirectory()).toBe(true);
  });

  it('does not throw when backups directory already exists', async () => {
    await fs.mkdir(path.join(tmpDir, 'backups'), { recursive: true });
    await expect(ensureBackupsDir(tmpDir)).resolves.toBeUndefined();
  });
});
