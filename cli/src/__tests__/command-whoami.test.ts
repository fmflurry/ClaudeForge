/**
 * Tests for src/commands/whoami.ts
 *
 * Covers:
 *   - Unauthenticated → exit 1 message
 *   - Expired-token branch (isTokenExpired)
 *   - Authenticated with api → prints email + orgs + active org
 *   - Offline mode (no api port) → prints local user info
 *   - API error → non-zero exit with message
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

import { runWhoami } from '../commands/whoami.js';
import type { WhoamiArgs, WhoamiDeps, WhoamiApiPort, MeResponse } from '../commands/whoami.js';
import { writeCredentials } from '../auth/credentials-store.js';
import { writeActiveOrg } from '../auth/active-org-store.js';
import type { Credentials } from '../auth/credentials-store.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-whoami-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

const VALID_CREDS: Credentials = {
  access: 'eyJ.access.token',
  refresh: 'opaque-refresh-token',
  expiresAt: '2099-01-01T00:00:00.000Z', // far future — not expired
  user: 'user@example.com',
  provider: 'google',
};

const EXPIRED_CREDS: Credentials = {
  access: 'eyJ.expired.token',
  refresh: 'opaque-expired-refresh',
  expiresAt: '2000-01-01T00:00:00.000Z', // in the past — expired
  user: 'user@example.com',
  provider: 'google',
};

const ME_RESPONSE: MeResponse = {
  email: 'user@example.com',
  orgs: [
    { id: 'org-1', name: 'Acme Corp' },
    { id: 'org-2', name: 'Beta Inc' },
  ],
};

function makeApi(overrides?: Partial<WhoamiApiPort>): WhoamiApiPort {
  return {
    getMe: vi.fn().mockResolvedValue(ME_RESPONSE),
    ...overrides,
  };
}

async function seedCredentials(homeDir: string, creds: Credentials = VALID_CREDS): Promise<void> {
  await writeCredentials(homeDir, creds);
}

// ---------------------------------------------------------------------------
// Unauthenticated
// ---------------------------------------------------------------------------

describe('runWhoami – unauthenticated', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 1 when no credentials exist', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.exitCode).toBe(1);
  });

  it('output mentions "Not logged in" when no credentials exist', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.output.toLowerCase()).toContain('not logged in');
  });

  it('output suggests running login command', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('login');
  });
});

// ---------------------------------------------------------------------------
// Expired token
// ---------------------------------------------------------------------------

describe('runWhoami – expired token', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await seedCredentials(homeDir, EXPIRED_CREDS);
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 1 when token is expired', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.exitCode).toBe(1);
  });

  it('output mentions "Session expired" when token is expired', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('Session expired');
  });

  it('output suggests re-authenticating when token is expired', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('login');
  });

  it('does not call api.getMe when token is expired', async () => {
    const getMeFn = vi.fn().mockResolvedValue(ME_RESPONSE);
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api: { getMe: getMeFn } };
    await runWhoami(args, deps);
    expect(getMeFn).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// Authenticated — with api port
// ---------------------------------------------------------------------------

describe('runWhoami – authenticated with api', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await seedCredentials(homeDir);
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 when authenticated', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api: makeApi() };
    const result = await runWhoami(args, deps);
    expect(result.exitCode).toBe(0);
  });

  it('output contains the user email', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api: makeApi() };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('user@example.com');
  });

  it('output lists the orgs', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api: makeApi() };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('Acme Corp');
    expect(result.output).toContain('Beta Inc');
  });

  it('output contains org ids in brackets', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api: makeApi() };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('[org-1]');
    expect(result.output).toContain('[org-2]');
  });

  it('marks the active org in output', async () => {
    await writeActiveOrg(homeDir, 'org-1');
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api: makeApi() };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('(active)');
  });

  it('calls api.getMe with the access token', async () => {
    const getMeFn = vi.fn().mockResolvedValue(ME_RESPONSE);
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api: { getMe: getMeFn } };
    await runWhoami(args, deps);
    expect(getMeFn).toHaveBeenCalledWith('eyJ.access.token');
  });

  it('shows a note when active org is not in the org list', async () => {
    await writeActiveOrg(homeDir, 'org-999');
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api: makeApi() };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('org-999');
    expect(result.output.toLowerCase()).toContain('not in org list');
  });

  it('output contains "Orgs:" section when user has orgs', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api: makeApi() };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('Orgs:');
  });

  it('does not show Orgs section when user has no orgs', async () => {
    const api = makeApi({
      getMe: vi.fn().mockResolvedValue({ email: 'user@example.com', orgs: [] }),
    });
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api };
    const result = await runWhoami(args, deps);
    expect(result.output).not.toContain('Orgs:');
  });
});

// ---------------------------------------------------------------------------
// Offline mode (no api port)
// ---------------------------------------------------------------------------

describe('runWhoami – offline mode (no api)', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await seedCredentials(homeDir);
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 in offline mode', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.exitCode).toBe(0);
  });

  it('output contains the stored user email in offline mode', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('user@example.com');
  });

  it('output shows active org when set in offline mode', async () => {
    await writeActiveOrg(homeDir, 'org-xyz');
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('org-xyz');
  });

  it('output does not show active org when none is set in offline mode', async () => {
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir };
    const result = await runWhoami(args, deps);
    expect(result.output).not.toContain('Active org:');
  });
});

// ---------------------------------------------------------------------------
// API error
// ---------------------------------------------------------------------------

describe('runWhoami – api error', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
    await seedCredentials(homeDir);
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 1 when api.getMe throws', async () => {
    const api = makeApi({
      getMe: vi.fn().mockRejectedValue(new Error('Internal server error')),
    });
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api };
    const result = await runWhoami(args, deps);
    expect(result.exitCode).toBe(1);
  });

  it('output contains "Failed to get user info" when api throws', async () => {
    const api = makeApi({
      getMe: vi.fn().mockRejectedValue(new Error('Internal server error')),
    });
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api };
    const result = await runWhoami(args, deps);
    expect(result.output.toLowerCase()).toContain('failed to get user info');
  });

  it('output includes the error message when api throws', async () => {
    const api = makeApi({
      getMe: vi.fn().mockRejectedValue(new Error('Internal server error')),
    });
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api };
    const result = await runWhoami(args, deps);
    expect(result.output).toContain('Internal server error');
  });

  it('handles non-Error thrown objects gracefully', async () => {
    const api = makeApi({
      getMe: vi.fn().mockRejectedValue('string-error'),
    });
    const args: WhoamiArgs = {};
    const deps: WhoamiDeps = { homeDir, api };
    const result = await runWhoami(args, deps);
    expect(result.exitCode).toBe(1);
    expect(result.output).toContain('string-error');
  });
});
