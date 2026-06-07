/**
 * Active org store — reads/writes the `activeOrg` field in config.json.
 * Cooperates non-destructively with the existing apiUrl field.
 */

import * as path from 'node:path';
import * as fs from 'node:fs/promises';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ActiveOrgFsPort {
  readFile(p: string): Promise<string>;
  writeFile(p: string, content: string): Promise<void>;
  mkdir(p: string, options: { recursive: boolean }): Promise<void>;
}

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const CONFIG_FILENAME = 'config.json';

// ---------------------------------------------------------------------------
// Default real FS port
// ---------------------------------------------------------------------------

const realFsPort: ActiveOrgFsPort = {
  readFile: (p) => fs.readFile(p, 'utf-8'),
  writeFile: (p, content) => fs.writeFile(p, content, 'utf-8'),
  mkdir: (p, options) => fs.mkdir(p, options).then(() => undefined),
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function configFilePath(homeDir: string): string {
  return path.join(homeDir, CONFIG_FILENAME);
}

function isEnoent(err: unknown): boolean {
  return (
    typeof err === 'object' &&
    err !== null &&
    'code' in err &&
    (err as { code: string }).code === 'ENOENT'
  );
}

async function readRawConfig(
  homeDir: string,
  fsPort: ActiveOrgFsPort,
): Promise<Record<string, unknown>> {
  try {
    const raw = await fsPort.readFile(configFilePath(homeDir));
    if (!raw || raw.trim().length === 0) {
      return {};
    }
    const parsed = JSON.parse(raw) as unknown;
    if (parsed !== null && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, unknown>;
    }
    return {};
  } catch (err) {
    if (isEnoent(err)) {
      return {};
    }
    // Corrupt JSON or unexpected error → treat as empty
    return {};
  }
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

export async function readActiveOrg(
  homeDir: string,
  fsPort: ActiveOrgFsPort = realFsPort,
): Promise<string | null> {
  const config = await readRawConfig(homeDir, fsPort);
  const activeOrg = config['activeOrg'];
  if (typeof activeOrg === 'string') {
    return activeOrg;
  }
  return null;
}

export async function writeActiveOrg(
  homeDir: string,
  orgId: string | null,
  fsPort: ActiveOrgFsPort = realFsPort,
): Promise<void> {
  const existing = await readRawConfig(homeDir, fsPort);

  let updated: Record<string, unknown>;
  if (orgId === null) {
    // Remove activeOrg key
    const { activeOrg: _removed, ...rest } = existing;
    void _removed;
    updated = { ...rest };
  } else {
    updated = { ...existing, activeOrg: orgId };
  }

  await fsPort.mkdir(homeDir, { recursive: true });
  await fsPort.writeFile(configFilePath(homeDir), JSON.stringify(updated, null, 2));
}
