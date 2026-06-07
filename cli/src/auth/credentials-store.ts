/**
 * Credentials store — secure on-disk storage for OAuth tokens.
 * Files are written at 0600, directory at 0700.
 * Any looser permissions trigger a PermissionError.
 */

import * as path from 'node:path';
import * as fs from 'node:fs/promises';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

export const CREDENTIALS_FILENAME = 'credentials.json';
export const CREDENTIALS_FILE_MODE = 0o600;
export const CREDENTIALS_DIR_MODE = 0o700;

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface Credentials {
  access: string;
  refresh: string;
  expiresAt: string; // ISO-8601
  user: string; // email
  provider: 'google' | 'microsoft';
}

export interface CredentialsFsPort {
  readFile(p: string): Promise<string>;
  writeFile(p: string, content: string, mode: number): Promise<void>;
  mkdir(p: string, options: { recursive: boolean; mode: number }): Promise<void>;
  stat(p: string): Promise<{ mode: number }>;
}

// ---------------------------------------------------------------------------
// PermissionError
// ---------------------------------------------------------------------------

export class PermissionError extends Error {
  public readonly path: string;
  public readonly expected: number;
  public readonly actual: number;

  constructor(filePath: string, expected: number, actual: number) {
    super(
      `Insecure permissions on ${filePath}: expected 0${expected.toString(8)}, got 0${actual.toString(8)}. ` +
        `Run: chmod 0${expected.toString(8)} ${filePath}`,
    );
    this.name = 'PermissionError';
    this.path = filePath;
    this.expected = expected;
    this.actual = actual;
    Object.setPrototypeOf(this, PermissionError.prototype);
  }
}

// ---------------------------------------------------------------------------
// redactToken
// ---------------------------------------------------------------------------

export function redactToken(token: string): string {
  if (token.length === 0) {
    return '';
  }
  return '***REDACTED***';
}

// ---------------------------------------------------------------------------
// Default real FS port
// ---------------------------------------------------------------------------

const realFsPort: CredentialsFsPort = {
  readFile: (p) => fs.readFile(p, 'utf-8'),
  writeFile: (p, content, mode) => fs.writeFile(p, content, { mode, encoding: 'utf-8' }),
  mkdir: (p, options) => fs.mkdir(p, options).then(() => undefined),
  stat: (p) => fs.stat(p),
};

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function credentialsFilePath(homeDir: string): string {
  return path.join(homeDir, CREDENTIALS_FILENAME);
}

/**
 * Check that the mode does not have any extra permission bits beyond the expected value.
 * "Looser" = has bits set that the expected does not allow.
 * "Stricter" = missing bits that expected allows → OK (e.g. dir at 0o600 when 0o700 expected).
 * Only checks the permission bits (bottom 9 bits, 0o777 mask).
 *
 * Rule: reject if (actual & ~expected) != 0
 */
function assertNotLooserThan(filePath: string, actual: number, expected: number): void {
  const actualPerms = actual & 0o777;
  const disallowedBits = actualPerms & ~expected;
  if (disallowedBits !== 0) {
    throw new PermissionError(filePath, expected, actualPerms);
  }
}

function isEnoent(err: unknown): boolean {
  return typeof err === 'object' && err !== null && 'code' in err && (err as { code: string }).code === 'ENOENT';
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

export async function readCredentials(
  homeDir: string,
  fsPort: CredentialsFsPort = realFsPort,
): Promise<Credentials | null> {
  const filePath = credentialsFilePath(homeDir);

  // 1. Check directory permissions
  try {
    const dirStat = await fsPort.stat(homeDir);
    assertNotLooserThan(homeDir, dirStat.mode, CREDENTIALS_DIR_MODE);
  } catch (err) {
    if (err instanceof PermissionError) {
      throw err;
    }
    if (isEnoent(err)) {
      // Dir doesn't exist → no credentials
      return null;
    }
    throw err;
  }

  // 2. Check file permissions
  try {
    const fileStat = await fsPort.stat(filePath);
    assertNotLooserThan(filePath, fileStat.mode, CREDENTIALS_FILE_MODE);
  } catch (err) {
    if (err instanceof PermissionError) {
      throw err;
    }
    if (isEnoent(err)) {
      return null;
    }
    throw err;
  }

  // 3. Read and parse
  try {
    const raw = await fsPort.readFile(filePath);
    if (!raw || raw.trim().length === 0) {
      return null;
    }
    const parsed = JSON.parse(raw) as unknown;
    if (
      parsed !== null &&
      typeof parsed === 'object' &&
      'access' in parsed &&
      'refresh' in parsed &&
      'expiresAt' in parsed &&
      'user' in parsed &&
      'provider' in parsed
    ) {
      return parsed as Credentials;
    }
    return null;
  } catch {
    return null;
  }
}

export async function writeCredentials(
  homeDir: string,
  creds: Credentials,
  fsPort: CredentialsFsPort = realFsPort,
): Promise<void> {
  const snapshot: Credentials = { ...creds };
  const filePath = credentialsFilePath(homeDir);

  await fsPort.mkdir(homeDir, { recursive: true, mode: CREDENTIALS_DIR_MODE });
  await fsPort.writeFile(filePath, JSON.stringify(snapshot, null, 2), CREDENTIALS_FILE_MODE);
}

export async function verifyCredentialsPermissions(
  homeDir: string,
  fsPort: CredentialsFsPort = realFsPort,
): Promise<void> {
  // Check directory permissions
  try {
    const dirStat = await fsPort.stat(homeDir);
    assertNotLooserThan(homeDir, dirStat.mode, CREDENTIALS_DIR_MODE);
  } catch (err) {
    if (err instanceof PermissionError) {
      throw err;
    }
    if (isEnoent(err)) {
      // Dir not present — nothing to verify
      return;
    }
    throw err;
  }

  // Check file permissions (skip if file absent)
  const filePath = credentialsFilePath(homeDir);
  try {
    const fileStat = await fsPort.stat(filePath);
    assertNotLooserThan(filePath, fileStat.mode, CREDENTIALS_FILE_MODE);
  } catch (err) {
    if (err instanceof PermissionError) {
      throw err;
    }
    if (isEnoent(err)) {
      return;
    }
    throw err;
  }
}
