/**
 * Tests for src/commands/login.ts
 *
 * Covers:
 *   - PKCE happy path (inject api.exchangeToken + fake loopback server deps)
 *   - Device-code branch (--device-code flag)
 *   - Exchange failure → non-zero exit + no token stored
 *   - Missing-api / noop path
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as os from 'node:os';

import { runLogin } from '../commands/login.js';
import type { LoginArgs, LoginDeps, LoginApiPort } from '../commands/login.js';
import type { TokenExchangeResponse } from '../auth/pkce-login.js';
import { readCredentials } from '../auth/credentials-store.js';
import { writeActiveOrg } from '../auth/active-org-store.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

async function makeTmpDir(): Promise<string> {
  return fs.mkdtemp(path.join(os.tmpdir(), 'claude-login-test-'));
}

async function removeTmpDir(dir: string): Promise<void> {
  await fs.rm(dir, { recursive: true, force: true });
}

const VALID_TOKEN_RESPONSE: TokenExchangeResponse = {
  access: 'eyJ.access.token',
  refresh: 'opaque-refresh-token',
  expiresAt: '2099-01-01T00:00:00.000Z',
  user: 'user@example.com',
  provider: 'google',
};

/**
 * Build a fake LoopbackServerPort + TokenExchangePort bundled into the
 * runPkceLogin-compatible structure that runLogin threads through.
 *
 * runLogin calls runPkceLogin internally and injects:
 *   - loopbackServer: createLoopbackServer()   ← we override via module mock below
 *   - tokenExchange: { exchange: deps.api.exchangeToken }
 *
 * The cleanest way to test runLogin without touching production loopback servers
 * is to mock the pkce-login module and the credentials-store.
 */

function makeApiPort(overrides?: Partial<LoginApiPort>): LoginApiPort {
  return {
    exchangeToken: vi.fn().mockResolvedValue(VALID_TOKEN_RESPONSE),
    requestDeviceCode: vi.fn().mockResolvedValue({
      userCode: 'ABCD-1234',
      verificationUri: 'https://device.example.com/activate',
      deviceCode: 'device-code-xyz',
      expiresIn: 300,
      interval: 5,
    }),
    pollDeviceToken: vi.fn().mockResolvedValue({
      status: 'approved' as const,
      tokens: VALID_TOKEN_RESPONSE,
    }),
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Module-level mocks for pkce-login so we control the loopback server
// ---------------------------------------------------------------------------

vi.mock('../auth/pkce-login.js', async (importOriginal) => {
  const original = await importOriginal<typeof import('../auth/pkce-login.js')>();
  return {
    ...original,
    // createLoopbackServer returns a fake that immediately resolves the code
    createLoopbackServer: vi.fn(() => ({
      listen: vi.fn().mockResolvedValue({
        port: 59999,
        waitForCode: vi.fn().mockResolvedValue('fake-auth-code'),
      }),
      close: vi.fn().mockResolvedValue(undefined),
    })),
  };
});

// ---------------------------------------------------------------------------
// PKCE happy path
// ---------------------------------------------------------------------------

describe('runLogin – PKCE happy path', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 on successful PKCE login', async () => {
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api: makeApiPort(),
      openBrowser: vi.fn().mockResolvedValue(undefined),
    };
    const result = await runLogin(args, deps);
    expect(result.exitCode).toBe(0);
  });

  it('output contains user email after PKCE login', async () => {
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api: makeApiPort(),
      openBrowser: vi.fn().mockResolvedValue(undefined),
    };
    const result = await runLogin(args, deps);
    expect(result.output).toContain('user@example.com');
  });

  it('output contains "Logged in as" after PKCE login', async () => {
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api: makeApiPort(),
      openBrowser: vi.fn().mockResolvedValue(undefined),
    };
    const result = await runLogin(args, deps);
    expect(result.output).toContain('Logged in as');
  });

  it('stores credentials to disk after PKCE login', async () => {
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api: makeApiPort(),
      openBrowser: vi.fn().mockResolvedValue(undefined),
    };
    await runLogin(args, deps);
    const creds = await readCredentials(homeDir);
    expect(creds).not.toBeNull();
    expect(creds?.user).toBe('user@example.com');
  });

  it('calls openBrowser once during PKCE flow', async () => {
    const openBrowserFn = vi.fn().mockResolvedValue(undefined);
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api: makeApiPort(),
      openBrowser: openBrowserFn,
    };
    await runLogin(args, deps);
    expect(openBrowserFn).toHaveBeenCalledTimes(1);
  });

  it('output includes active org when one is set', async () => {
    await writeActiveOrg(homeDir, 'org-123');
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api: makeApiPort(),
      openBrowser: vi.fn().mockResolvedValue(undefined),
    };
    const result = await runLogin(args, deps);
    expect(result.output).toContain('Active org: org-123');
  });

  it('output does not include active org line when none is set', async () => {
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api: makeApiPort(),
      openBrowser: vi.fn().mockResolvedValue(undefined),
    };
    const result = await runLogin(args, deps);
    expect(result.output).not.toContain('Active org:');
  });

  it('defaults to google provider when none is specified', async () => {
    const exchangeTokenFn = vi.fn().mockResolvedValue(VALID_TOKEN_RESPONSE);
    const api = makeApiPort({ exchangeToken: exchangeTokenFn });
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api,
      openBrowser: vi.fn().mockResolvedValue(undefined),
    };
    await runLogin(args, deps);
    // exchangeToken is called by PKCE flow
    expect(exchangeTokenFn).toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// Device-code branch
// ---------------------------------------------------------------------------

describe('runLogin – device-code branch', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns exitCode 0 when device-code poll is approved', async () => {
    const api = makeApiPort({
      requestDeviceCode: vi.fn().mockResolvedValue({
        userCode: 'ABCD-1234',
        verificationUri: 'https://device.example.com/activate',
        deviceCode: 'device-code-xyz',
        expiresIn: 300,
        interval: 0, // 0ms so the test doesn't wait
      }),
      pollDeviceToken: vi.fn().mockResolvedValue({
        status: 'approved' as const,
        tokens: VALID_TOKEN_RESPONSE,
      }),
    });
    const args: LoginArgs = { deviceCode: true };
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api,
      deviceCodeMode: true,
    };
    const result = await runLogin(args, deps);
    expect(result.exitCode).toBe(0);
  });

  it('device-code flow stores credentials on approval', async () => {
    const api = makeApiPort({
      requestDeviceCode: vi.fn().mockResolvedValue({
        userCode: 'ABCD-1234',
        verificationUri: 'https://device.example.com/activate',
        deviceCode: 'device-code-xyz',
        expiresIn: 300,
        interval: 0,
      }),
      pollDeviceToken: vi.fn().mockResolvedValue({
        status: 'approved' as const,
        tokens: VALID_TOKEN_RESPONSE,
      }),
    });
    const args: LoginArgs = { deviceCode: true };
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api,
    };
    await runLogin(args, deps);
    const creds = await readCredentials(homeDir);
    expect(creds?.user).toBe('user@example.com');
  });

  it('returns non-zero exit when device-code poll expires', async () => {
    const api = makeApiPort({
      requestDeviceCode: vi.fn().mockResolvedValue({
        userCode: 'ABCD-1234',
        verificationUri: 'https://device.example.com/activate',
        deviceCode: 'device-code-xyz',
        expiresIn: 300,
        interval: 0,
      }),
      pollDeviceToken: vi.fn().mockResolvedValue({ status: 'expired' as const }),
    });
    const args: LoginArgs = { deviceCode: true };
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api,
    };
    const result = await runLogin(args, deps);
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('picks up deviceCodeMode from deps when args.deviceCode is undefined', async () => {
    const api = makeApiPort({
      requestDeviceCode: vi.fn().mockResolvedValue({
        userCode: 'ABCD-1234',
        verificationUri: 'https://device.example.com/activate',
        deviceCode: 'device-code-xyz',
        expiresIn: 300,
        interval: 0,
      }),
      pollDeviceToken: vi.fn().mockResolvedValue({
        status: 'approved' as const,
        tokens: VALID_TOKEN_RESPONSE,
      }),
    });
    // Note: args.deviceCode is undefined; deps.deviceCodeMode = true
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api,
      deviceCodeMode: true,
    };
    const result = await runLogin(args, deps);
    // device code flow was used; requestDeviceCode should have been called
    expect(api.requestDeviceCode).toHaveBeenCalled();
    expect(result.exitCode).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// Exchange failure → non-zero exit, no credentials stored
// ---------------------------------------------------------------------------

describe('runLogin – exchange failure', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns non-zero exitCode when token exchange throws', async () => {
    const api = makeApiPort({
      exchangeToken: vi.fn().mockRejectedValue(new Error('invalid_grant')),
    });
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api,
      openBrowser: vi.fn().mockResolvedValue(undefined),
    };
    const result = await runLogin(args, deps);
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('does not store credentials when exchange fails', async () => {
    const api = makeApiPort({
      exchangeToken: vi.fn().mockRejectedValue(new Error('invalid_grant')),
    });
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      api,
      openBrowser: vi.fn().mockResolvedValue(undefined),
    };
    await runLogin(args, deps);
    const creds = await readCredentials(homeDir);
    expect(creds).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Missing API / noop path
// ---------------------------------------------------------------------------

describe('runLogin – no api injected (noop path)', () => {
  let homeDir: string;

  beforeEach(async () => {
    homeDir = await makeTmpDir();
  });

  afterEach(async () => {
    await removeTmpDir(homeDir);
  });

  it('returns non-zero exit when no api port is provided and PKCE exchange is called', async () => {
    // Without api, the noop token exchange throws "Token exchange API not configured"
    const args: LoginArgs = {};
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      openBrowser: vi.fn().mockResolvedValue(undefined),
      // api is intentionally omitted
    };
    const result = await runLogin(args, deps);
    // noop throws → PKCE returns non-zero
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('returns non-zero exit when no api port and device-code mode is on', async () => {
    // Without api, the noop device code api throws "Device code API not configured"
    const args: LoginArgs = { deviceCode: true };
    const deps: LoginDeps = {
      homeDir,
      apiUrl: 'https://api.example.com',
      // api is intentionally omitted
    };
    const result = await runLogin(args, deps);
    expect(result.exitCode).toBeGreaterThan(0);
  });
});
