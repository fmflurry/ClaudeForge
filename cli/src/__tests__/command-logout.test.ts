/**
 * Tests for src/commands/logout.ts
 *
 * Covers:
 *   - No credentials → "Already logged out" exit 0
 *   - With credentials → revoke called + credentials file removed + activeOrg cleared
 *   - Revoke throwing is swallowed (still removes local creds)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

import { runLogout } from '../commands/logout.js';
import type { LogoutArgs, LogoutDeps, LogoutApiPort } from '../commands/logout.js';
import { writeCredentials, readCredentials, CREDENTIALS_FILENAME } from '../auth/credentials-store.js';
import { writeActiveOrg, readActiveOrg } from '../auth/active-org-store.js';
import type { Credentials } from '../auth/credentials-store.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-logout-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

const VALID_CREDS: Credentials = {
  access: 'eyJ.access.token',
  refresh: 'opaque-refresh-token',
  expiresAt: '2099-01-01T00:00:00.000Z',
  user: 'user@example.com',
  provider: 'google',
};

/**
 * Writes VALID_CREDS into the homeDir so it looks like the user is logged in.
 * Uses the real credentials-store helpers which write with correct permissions.
 */
async function seedCredentials(homeDir: string): Promise<void> {
  await writeCredentials(homeDir, VALID_CREDS);
}

// ---------------------------------------------------------------------------
// No credentials — already logged out
// ---------------------------------------------------------------------------

describe('runLogout – no credentials (already logged out)', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 when no credentials file exists', async () => {
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir };
    const result = await runLogout(args, deps);
    expect(result.exitCode).toBe(0);
  });

  it('output says "Already logged out" when no credentials exist', async () => {
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir };
    const result = await runLogout(args, deps);
    expect(result.output).toBe('Already logged out');
  });

  it('does not call revokeToken when already logged out', async () => {
    const revokeTokenFn = vi.fn().mockResolvedValue(undefined);
    const api: LogoutApiPort = { revokeToken: revokeTokenFn };
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir, api };
    await runLogout(args, deps);
    expect(revokeTokenFn).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// With credentials — full logout flow
// ---------------------------------------------------------------------------

describe('runLogout – with credentials', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await seedCredentials(homeDir);
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 after successful logout', async () => {
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir };
    const result = await runLogout(args, deps);
    expect(result.exitCode).toBe(0);
  });

  it('output contains the user email in the confirmation message', async () => {
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir };
    const result = await runLogout(args, deps);
    expect(result.output).toContain('user@example.com');
  });

  it('output contains "Logged out"', async () => {
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir };
    const result = await runLogout(args, deps);
    expect(result.output.toLowerCase()).toContain('logged out');
  });

  it('removes the credentials.json file', async () => {
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir };
    await runLogout(args, deps);
    const creds = await readCredentials(homeDir);
    expect(creds).toBeNull();
  });

  it('calls revokeToken with the refresh token', async () => {
    const revokeTokenFn = vi.fn().mockResolvedValue(undefined);
    const api: LogoutApiPort = { revokeToken: revokeTokenFn };
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir, api };
    await runLogout(args, deps);
    expect(revokeTokenFn).toHaveBeenCalledWith('opaque-refresh-token');
  });

  it('clears the active org after logout', async () => {
    await writeActiveOrg(homeDir, 'org-abc');
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir };
    await runLogout(args, deps);
    const org = await readActiveOrg(homeDir);
    expect(org).toBeNull();
  });

  it('credentials file is physically absent after logout', async () => {
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir };
    await runLogout(args, deps);
    const credPath = path.join(homeDir, CREDENTIALS_FILENAME);
    await expect(fs.access(credPath)).rejects.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Revoke throwing is swallowed
// ---------------------------------------------------------------------------

describe('runLogout – revoke throwing is swallowed', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await seedCredentials(homeDir);
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('still returns exitCode 0 when revokeToken throws', async () => {
    const api: LogoutApiPort = {
      revokeToken: vi.fn().mockRejectedValue(new Error('Network error')),
    };
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir, api };
    const result = await runLogout(args, deps);
    expect(result.exitCode).toBe(0);
  });

  it('still removes credentials when revokeToken throws', async () => {
    const api: LogoutApiPort = {
      revokeToken: vi.fn().mockRejectedValue(new Error('Network error')),
    };
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir, api };
    await runLogout(args, deps);
    const creds = await readCredentials(homeDir);
    expect(creds).toBeNull();
  });

  it('still outputs the logged-out message when revokeToken throws', async () => {
    const api: LogoutApiPort = {
      revokeToken: vi.fn().mockRejectedValue(new Error('Network error')),
    };
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir, api };
    const result = await runLogout(args, deps);
    expect(result.output.toLowerCase()).toContain('logged out');
  });

  it('works correctly without any api port provided', async () => {
    const args: LogoutArgs = {};
    const deps: LogoutDeps = { homeDir };
    const result = await runLogout(args, deps);
    expect(result.exitCode).toBe(0);
    const creds = await readCredentials(homeDir);
    expect(creds).toBeNull();
  });
});
