/**
 * Plugin installation registry module.
 * Manages installed.json in the home directory.
 * All record operations are pure and immutable.
 */

import * as fs from 'node:fs/promises';
import * as path from 'node:path';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface InstalledRecord {
  name: string;
  version: string;
  installedAt: string;
  path: string;
}

export interface InstalledRegistry {
  plugins: InstalledRecord[];
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const REGISTRY_FILENAME = 'installed.json';
const BACKUPS_DIRNAME = 'backups';
const EMPTY_REGISTRY: InstalledRegistry = { plugins: [] };

// ---------------------------------------------------------------------------
// I/O
// ---------------------------------------------------------------------------

export async function readRegistry(homeDir: string): Promise<InstalledRegistry> {
  try {
    const raw = await fs.readFile(path.join(homeDir, REGISTRY_FILENAME), 'utf-8');
    if (!raw || raw.trim().length === 0) {
      return { plugins: [] };
    }
    const parsed = JSON.parse(raw) as unknown;
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      'plugins' in parsed &&
      Array.isArray((parsed as { plugins: unknown }).plugins)
    ) {
      return { plugins: [...(parsed as { plugins: InstalledRecord[] }).plugins] };
    }
    return { plugins: [] };
  } catch {
    return { plugins: [] };
  }
}

export async function writeRegistry(
  homeDir: string,
  registry: InstalledRegistry,
): Promise<void> {
  await fs.mkdir(homeDir, { recursive: true });
  const snapshot: InstalledRegistry = { plugins: [...registry.plugins] };
  await fs.writeFile(
    path.join(homeDir, REGISTRY_FILENAME),
    JSON.stringify(snapshot, null, 2),
    'utf-8',
  );
}

// ---------------------------------------------------------------------------
// Pure record operations (immutable)
// ---------------------------------------------------------------------------

export function addRecord(
  registry: InstalledRegistry,
  record: InstalledRecord,
): InstalledRegistry {
  return { plugins: [...registry.plugins, record] };
}

export function removeRecord(
  registry: InstalledRegistry,
  name: string,
): InstalledRegistry {
  return { plugins: registry.plugins.filter((p) => p.name !== name) };
}

export function findRecord(
  registry: InstalledRegistry,
  name: string,
): InstalledRecord | undefined {
  return registry.plugins.find((p) => p.name === name);
}

// ---------------------------------------------------------------------------
// Backups
// ---------------------------------------------------------------------------

export function backupsDir(homeDir: string): string {
  return path.join(homeDir, BACKUPS_DIRNAME);
}

export async function ensureBackupsDir(homeDir: string): Promise<void> {
  await fs.mkdir(path.join(homeDir, BACKUPS_DIRNAME), { recursive: true });
}

// Suppress unused warning — EMPTY_REGISTRY exported for consumers
export { EMPTY_REGISTRY };
