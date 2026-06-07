/**
 * Tests for src/auth/device-code.ts  (Group 11.5 — device-code fallback)
 *
 * Production module path: src/auth/device-code.ts
 *
 * Exported types:
 *   - DeviceCodeResponse: {
 *       userCode: string;
 *       verificationUri: string;
 *       deviceCode: string;
 *       expiresIn: number;      // seconds
 *       interval: number;       // polling interval seconds
 *     }
 *   - DeviceTokenPollResult:
 *     | { status: 'approved'; tokens: TokenExchangeResponse }
 *     | { status: 'pending' }
 *     | { status: 'slow_down' }
 *     | { status: 'expired' }
 *   - DeviceCodeApiPort: {
 *       requestDeviceCode(provider: string): Promise<DeviceCodeResponse>
 *       pollDeviceToken(deviceCode: string, provider: string): Promise<DeviceTokenPollResult>
 *     }
 *   - DeviceCodeDeps: {
 *       api: DeviceCodeApiPort;
 *       storeCredentials(creds: TokenExchangeResponse): Promise<void>;
 *       displayInstructions(verificationUri: string, userCode: string): void;
 *       pollIntervalMs?: number;  // default: interval * 1000 from server
 *       maxAttempts?: number;     // default: 180 (15 min with 5s interval)
 *       clock?: { now(): number; sleep(ms: number): Promise<void> };
 *     }
 *   - DeviceCodeResult: { exitCode: number; output: string }
 *
 * Exported functions:
 *   - runDeviceCodeLogin(provider: string, deps: DeviceCodeDeps): Promise<DeviceCodeResult>
 *       → requests device code
 *       → displays verification URL + user code
 *       → polls until approved → stores tokens → exitCode 0
 *       → on slow_down: increases poll interval
 *       → on expired (or max retries): exitCode non-zero
 */

import { describe, it, expect, vi } from 'vitest';

// These imports WILL FAIL until src/auth/device-code.ts is created (RED state).
import { runDeviceCodeLogin } from '../auth/device-code.js';
import type {
  DeviceCodeResponse,
  DeviceCodeApiPort,
  DeviceCodeDeps,
  DeviceTokenPollResult,
  TokenExchangeResponse,
} from '../auth/device-code.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const SAMPLE_DEVICE_CODE_RESPONSE: DeviceCodeResponse = {
  userCode: 'ABCD-1234',
  verificationUri: 'https://api.example.com/activate',
  deviceCode: 'device-code-opaque-string',
  expiresIn: 300,
  interval: 5,
};

const SAMPLE_TOKENS: TokenExchangeResponse = {
  access: 'eyJ.access.token',
  refresh: 'opaque-refresh-token',
  expiresAt: '2026-06-07T15:00:00.000Z',
  user: 'user@example.com',
  provider: 'google',
};

function makeApi(pollResponses: DeviceTokenPollResult[]): DeviceCodeApiPort {
  let callCount = 0;
  return {
    requestDeviceCode: vi.fn().mockResolvedValue(SAMPLE_DEVICE_CODE_RESPONSE),
    pollDeviceToken: vi.fn().mockImplementation(async () => {
      const response = pollResponses[callCount] ?? { status: 'expired' };
      callCount++;
      return response;
    }),
  };
}

function makeDeps(overrides: Partial<DeviceCodeDeps> = {}): DeviceCodeDeps {
  const fastClock = {
    now: vi.fn().mockReturnValue(Date.now()),
    sleep: vi.fn().mockResolvedValue(undefined),
  };
  return {
    api: makeApi([{ status: 'approved', tokens: SAMPLE_TOKENS }]),
    storeCredentials: vi.fn().mockResolvedValue(undefined),
    displayInstructions: vi.fn(),
    pollIntervalMs: 0, // instant for tests
    maxAttempts: 5,
    clock: fastClock,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// runDeviceCodeLogin – happy path (immediate approval)
// ---------------------------------------------------------------------------

describe('runDeviceCodeLogin – immediate approval', () => {
  it('returns exitCode 0 on approval', async () => {
    const deps = makeDeps();
    const result = await runDeviceCodeLogin('google', deps);
    expect(result.exitCode).toBe(0);
  });

  it('calls displayInstructions with verification URI and user code', async () => {
    const displayFn = vi.fn();
    const deps = makeDeps({ displayInstructions: displayFn });
    await runDeviceCodeLogin('google', deps);
    expect(displayFn).toHaveBeenCalledWith(
      SAMPLE_DEVICE_CODE_RESPONSE.verificationUri,
      SAMPLE_DEVICE_CODE_RESPONSE.userCode,
    );
  });

  it('output includes the verification URI', async () => {
    const deps = makeDeps();
    const result = await runDeviceCodeLogin('google', deps);
    expect(result.output).toContain(SAMPLE_DEVICE_CODE_RESPONSE.verificationUri);
  });

  it('output includes the user code', async () => {
    const deps = makeDeps();
    const result = await runDeviceCodeLogin('google', deps);
    expect(result.output).toContain(SAMPLE_DEVICE_CODE_RESPONSE.userCode);
  });

  it('stores credentials on approval', async () => {
    const storeFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({ storeCredentials: storeFn });
    await runDeviceCodeLogin('google', deps);
    expect(storeFn).toHaveBeenCalledWith(SAMPLE_TOKENS);
  });

  it('requests device code for the specified provider', async () => {
    const api = makeApi([{ status: 'approved', tokens: SAMPLE_TOKENS }]);
    const deps = makeDeps({ api });
    await runDeviceCodeLogin('microsoft', deps);
    expect(api.requestDeviceCode).toHaveBeenCalledWith('microsoft');
  });
});

// ---------------------------------------------------------------------------
// runDeviceCodeLogin – polling: pending then approval
// ---------------------------------------------------------------------------

describe('runDeviceCodeLogin – polling: pending → approved', () => {
  it('polls multiple times and succeeds on eventual approval', async () => {
    const pollResults: DeviceTokenPollResult[] = [
      { status: 'pending' },
      { status: 'pending' },
      { status: 'approved', tokens: SAMPLE_TOKENS },
    ];
    const api = makeApi(pollResults);
    const deps = makeDeps({ api, maxAttempts: 10 });
    const result = await runDeviceCodeLogin('google', deps);
    expect(result.exitCode).toBe(0);
    expect(api.pollDeviceToken).toHaveBeenCalledTimes(3);
  });

  it('stores credentials after polling delay', async () => {
    const pollResults: DeviceTokenPollResult[] = [{ status: 'pending' }, { status: 'approved', tokens: SAMPLE_TOKENS }];
    const storeFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({
      api: makeApi(pollResults),
      storeCredentials: storeFn,
      maxAttempts: 10,
    });
    await runDeviceCodeLogin('google', deps);
    expect(storeFn).toHaveBeenCalledWith(SAMPLE_TOKENS);
  });
});

// ---------------------------------------------------------------------------
// runDeviceCodeLogin – slow_down
// ---------------------------------------------------------------------------

describe('runDeviceCodeLogin – slow_down handling', () => {
  it('continues polling after slow_down and succeeds on approval', async () => {
    const pollResults: DeviceTokenPollResult[] = [
      { status: 'slow_down' },
      { status: 'approved', tokens: SAMPLE_TOKENS },
    ];
    const api = makeApi(pollResults);
    const deps = makeDeps({ api, maxAttempts: 10 });
    const result = await runDeviceCodeLogin('google', deps);
    expect(result.exitCode).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// runDeviceCodeLogin – expiry / max attempts
// ---------------------------------------------------------------------------

describe('runDeviceCodeLogin – expiry', () => {
  it('returns non-zero exit when server returns expired', async () => {
    const api = makeApi([{ status: 'expired' }]);
    const deps = makeDeps({ api });
    const result = await runDeviceCodeLogin('google', deps);
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('returns non-zero exit when maxAttempts exhausted with pending', async () => {
    const pending: DeviceTokenPollResult = { status: 'pending' };
    const api = makeApi([pending, pending, pending, pending, pending]);
    const deps = makeDeps({ api, maxAttempts: 3 });
    const result = await runDeviceCodeLogin('google', deps);
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('does NOT store credentials on expiry', async () => {
    const storeFn = vi.fn().mockResolvedValue(undefined);
    const api = makeApi([{ status: 'expired' }]);
    const deps = makeDeps({ api, storeCredentials: storeFn });
    await runDeviceCodeLogin('google', deps);
    expect(storeFn).not.toHaveBeenCalled();
  });

  it('does NOT store credentials when maxAttempts exhausted', async () => {
    const pending: DeviceTokenPollResult = { status: 'pending' };
    const storeFn = vi.fn().mockResolvedValue(undefined);
    const api = makeApi([pending, pending, pending]);
    const deps = makeDeps({ api, storeCredentials: storeFn, maxAttempts: 2 });
    await runDeviceCodeLogin('google', deps);
    expect(storeFn).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// runDeviceCodeLogin – API error
// ---------------------------------------------------------------------------

describe('runDeviceCodeLogin – API error on requestDeviceCode', () => {
  it('returns non-zero exit when requestDeviceCode throws', async () => {
    const failingApi: DeviceCodeApiPort = {
      requestDeviceCode: vi.fn().mockRejectedValue(new Error('network error')),
      pollDeviceToken: vi.fn(),
    };
    const deps = makeDeps({ api: failingApi });
    const result = await runDeviceCodeLogin('google', deps);
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('does NOT store credentials when requestDeviceCode throws', async () => {
    const storeFn = vi.fn();
    const failingApi: DeviceCodeApiPort = {
      requestDeviceCode: vi.fn().mockRejectedValue(new Error('network error')),
      pollDeviceToken: vi.fn(),
    };
    const deps = makeDeps({ api: failingApi, storeCredentials: storeFn });
    await runDeviceCodeLogin('google', deps);
    expect(storeFn).not.toHaveBeenCalled();
  });
});
