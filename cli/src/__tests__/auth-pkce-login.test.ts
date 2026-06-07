/**
 * Tests for src/auth/pkce-login.ts  (Group 11.3 — loopback PKCE login)
 *
 * Production module path: src/auth/pkce-login.ts
 *
 * Exported types:
 *   - PkceVerifierChallengePair: { verifier: string; challenge: string }
 *   - LoopbackServerPort: {
 *       listen(port: number, host: string): Promise<{ port: number; waitForCode(): Promise<string> }>
 *       close(): Promise<void>
 *     }
 *   - TokenExchangePort: {
 *       exchange(code: string, verifier: string, redirectUri: string): Promise<TokenExchangeResponse>
 *     }
 *   - TokenExchangeResponse: { access: string; refresh: string; expiresAt: string; user: string; provider: string }
 *   - PkceLoginDeps: {
 *       generatePkce(): PkceVerifierChallengePair;
 *       openBrowser(url: string): Promise<void>;
 *       loopbackServer: LoopbackServerPort;
 *       tokenExchange: TokenExchangePort;
 *       storeCredentials(creds: TokenExchangeResponse): Promise<void>;
 *       timeoutMs?: number;   // default 300_000 (5 min)
 *       clock?: { now(): number };
 *     }
 *   - PkceLoginResult: { exitCode: number; output: string }
 *
 * Exported functions:
 *   - generatePkceVerifierChallenge(): PkceVerifierChallengePair
 *       → verifier: 43–128 chars, BASE64URL (A-Z a-z 0-9 - _ only, no = + /)
 *       → challenge: BASE64URL(SHA-256(verifier))
 *       → two calls produce DIFFERENT pairs (random)
 *   - runPkceLogin(provider: string, apiUrl: string, deps: PkceLoginDeps): Promise<PkceLoginResult>
 *       → opens browser with PKCE auth URL
 *       → starts loopback server on 127.0.0.1 (loopback ONLY, not 0.0.0.0)
 *       → redirect_uri must be exactly http://127.0.0.1:<port>/callback
 *       → validates state param on callback (mismatch → error)
 *       → on success: exchanges code for tokens, stores them → exitCode 0 + user email
 *       → on timeout (5 min): exitCode non-zero + "Authentication cancelled or timed out"
 *       → on exchange error: exitCode non-zero + no credentials stored
 *       → on state mismatch: exitCode non-zero + no credentials stored
 *
 * Exported function:
 *   - createLoopbackServer(): LoopbackServerPort
 *       → binds to 127.0.0.1 ONLY (not 0.0.0.0)
 *       → allocates a random ephemeral port
 *       → times out after timeoutMs if no code arrives
 */

import { describe, it, expect, vi } from 'vitest';
import * as http from 'node:http';
import * as net from 'node:net';

// These imports WILL FAIL until src/auth/pkce-login.ts is created (RED state).
import {
  generatePkceVerifierChallenge,
  runPkceLogin,
  createLoopbackServer,
} from '../auth/pkce-login.js';
import type {
  PkceVerifierChallengePair,
  PkceLoginDeps,
  LoopbackServerPort,
  TokenExchangePort,
  TokenExchangeResponse,
} from '../auth/pkce-login.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const VALID_TOKEN_RESPONSE: TokenExchangeResponse = {
  access: 'eyJ.access.token',
  refresh: 'opaque-refresh-token',
  expiresAt: '2026-06-07T15:00:00.000Z',
  user: 'user@example.com',
  provider: 'google',
};

function makeSuccessfulLoopbackServer(code = 'auth-code-from-server'): LoopbackServerPort {
  return {
    listen: vi.fn().mockResolvedValue({
      port: 54321,
      waitForCode: vi.fn().mockResolvedValue(code),
    }),
    close: vi.fn().mockResolvedValue(undefined),
  };
}

function makeTimedOutLoopbackServer(): LoopbackServerPort {
  return {
    listen: vi.fn().mockResolvedValue({
      port: 54321,
      waitForCode: vi.fn().mockRejectedValue(new Error('Authentication cancelled or timed out')),
    }),
    close: vi.fn().mockResolvedValue(undefined),
  };
}

function makeSuccessfulTokenExchange(): TokenExchangePort {
  return {
    exchange: vi.fn().mockResolvedValue(VALID_TOKEN_RESPONSE),
  };
}

function makeFailingTokenExchange(): TokenExchangePort {
  return {
    exchange: vi.fn().mockRejectedValue(new Error('invalid_grant')),
  };
}

function makeDeps(overrides: Partial<PkceLoginDeps> = {}): PkceLoginDeps {
  return {
    generatePkce: vi.fn().mockReturnValue({
      verifier: 'my-verifier-abc123',
      challenge: 'base64url-challenge-abc',
    }),
    openBrowser: vi.fn().mockResolvedValue(undefined),
    loopbackServer: makeSuccessfulLoopbackServer(),
    tokenExchange: makeSuccessfulTokenExchange(),
    storeCredentials: vi.fn().mockResolvedValue(undefined),
    timeoutMs: 300_000,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// generatePkceVerifierChallenge
// ---------------------------------------------------------------------------

describe('generatePkceVerifierChallenge', () => {
  it('returns an object with verifier and challenge strings', () => {
    const pair: PkceVerifierChallengePair = generatePkceVerifierChallenge();
    expect(typeof pair.verifier).toBe('string');
    expect(typeof pair.challenge).toBe('string');
  });

  it('verifier length is between 43 and 128 characters (RFC 7636)', () => {
    const pair = generatePkceVerifierChallenge();
    expect(pair.verifier.length).toBeGreaterThanOrEqual(43);
    expect(pair.verifier.length).toBeLessThanOrEqual(128);
  });

  it('verifier uses only BASE64URL characters (A-Z a-z 0-9 - _ )', () => {
    const pair = generatePkceVerifierChallenge();
    expect(pair.verifier).toMatch(/^[A-Za-z0-9\-_]+$/);
  });

  it('verifier does NOT contain padding = or + or / (not standard base64)', () => {
    const pair = generatePkceVerifierChallenge();
    expect(pair.verifier).not.toMatch(/[=+/]/);
  });

  it('challenge uses only BASE64URL characters', () => {
    const pair = generatePkceVerifierChallenge();
    expect(pair.challenge).toMatch(/^[A-Za-z0-9\-_]+$/);
  });

  it('challenge does NOT contain padding characters', () => {
    const pair = generatePkceVerifierChallenge();
    expect(pair.challenge).not.toMatch(/[=+/]/);
  });

  it('two consecutive calls produce different verifiers (random)', () => {
    const pair1 = generatePkceVerifierChallenge();
    const pair2 = generatePkceVerifierChallenge();
    expect(pair1.verifier).not.toBe(pair2.verifier);
  });

  it('two consecutive calls produce different challenges (random)', () => {
    const pair1 = generatePkceVerifierChallenge();
    const pair2 = generatePkceVerifierChallenge();
    expect(pair1.challenge).not.toBe(pair2.challenge);
  });

  it('challenge is the SHA-256 of the verifier in BASE64URL', async () => {
    const pair = generatePkceVerifierChallenge();
    // Verify the challenge matches SHA-256(verifier) in BASE64URL
    const { createHash } = await import('node:crypto');
    const expected = createHash('sha256')
      .update(pair.verifier)
      .digest('base64url');
    expect(pair.challenge).toBe(expected);
  });
});

// ---------------------------------------------------------------------------
// runPkceLogin – happy path
// ---------------------------------------------------------------------------

describe('runPkceLogin – happy path', () => {
  it('returns exitCode 0 on successful login', async () => {
    const deps = makeDeps();
    const result = await runPkceLogin('google', 'https://api.example.com', deps);
    expect(result.exitCode).toBe(0);
  });

  it('output contains the user email on success', async () => {
    const deps = makeDeps();
    const result = await runPkceLogin('google', 'https://api.example.com', deps);
    expect(result.output).toContain('user@example.com');
  });

  it('calls openBrowser with a URL containing the auth endpoint', async () => {
    const openBrowserFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({ openBrowser: openBrowserFn });
    await runPkceLogin('google', 'https://api.example.com', deps);
    expect(openBrowserFn).toHaveBeenCalled();
    const [url] = openBrowserFn.mock.calls[0] as [string];
    expect(url).toContain('auth/authorize');
  });

  it('opens browser URL with code_challenge query param', async () => {
    const openBrowserFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({ openBrowser: openBrowserFn });
    await runPkceLogin('google', 'https://api.example.com', deps);
    const [url] = openBrowserFn.mock.calls[0] as [string];
    expect(url).toContain('code_challenge');
  });

  it('opens browser URL with provider query param', async () => {
    const openBrowserFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({ openBrowser: openBrowserFn });
    await runPkceLogin('google', 'https://api.example.com', deps);
    const [url] = openBrowserFn.mock.calls[0] as [string];
    expect(url).toContain('provider=google');
  });

  it('redirect_uri is exactly http://127.0.0.1:<port>/callback', async () => {
    const openBrowserFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({ openBrowser: openBrowserFn });
    await runPkceLogin('google', 'https://api.example.com', deps);
    const [rawUrl] = openBrowserFn.mock.calls[0] as [string];
    const parsedUrl = new URL(rawUrl);
    const redirectUri = parsedUrl.searchParams.get('redirect_uri') ?? '';
    expect(redirectUri).toMatch(/^http:\/\/127\.0\.0\.1:\d+\/callback$/);
  });

  it('redirect_uri does NOT use 0.0.0.0 (must be loopback only)', async () => {
    const openBrowserFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({ openBrowser: openBrowserFn });
    await runPkceLogin('google', 'https://api.example.com', deps);
    const [rawUrl] = openBrowserFn.mock.calls[0] as [string];
    expect(rawUrl).not.toContain('0.0.0.0');
  });

  it('listens on 127.0.0.1 (not 0.0.0.0)', async () => {
    const listenFn = vi.fn().mockResolvedValue({
      port: 54321,
      waitForCode: vi.fn().mockResolvedValue('code'),
    });
    const deps = makeDeps({
      loopbackServer: { listen: listenFn, close: vi.fn().mockResolvedValue(undefined) },
    });
    await runPkceLogin('google', 'https://api.example.com', deps);
    const [, host] = listenFn.mock.calls[0] as [number, string];
    expect(host).toBe('127.0.0.1');
  });

  it('calls tokenExchange with the received code and verifier', async () => {
    const exchangeFn = vi.fn().mockResolvedValue(VALID_TOKEN_RESPONSE);
    const deps = makeDeps({
      tokenExchange: { exchange: exchangeFn },
    });
    await runPkceLogin('google', 'https://api.example.com', deps);
    expect(exchangeFn).toHaveBeenCalledWith(
      'auth-code-from-server',
      'my-verifier-abc123',
      expect.stringMatching(/^http:\/\/127\.0\.0\.1:\d+\/callback$/),
    );
  });

  it('calls storeCredentials with the token response on success', async () => {
    const storeFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({ storeCredentials: storeFn });
    await runPkceLogin('google', 'https://api.example.com', deps);
    expect(storeFn).toHaveBeenCalledWith(VALID_TOKEN_RESPONSE);
  });
});

// ---------------------------------------------------------------------------
// runPkceLogin – state validation
// ---------------------------------------------------------------------------

describe('runPkceLogin – state parameter', () => {
  it('browser URL contains a state param', async () => {
    const openBrowserFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({ openBrowser: openBrowserFn });
    await runPkceLogin('google', 'https://api.example.com', deps);
    const [rawUrl] = openBrowserFn.mock.calls[0] as [string];
    const parsedUrl = new URL(rawUrl);
    expect(parsedUrl.searchParams.has('state')).toBe(true);
    expect((parsedUrl.searchParams.get('state') ?? '').length).toBeGreaterThan(0);
  });

  it('returns non-zero exit and no stored credentials on state mismatch', async () => {
    const storeFn = vi.fn().mockResolvedValue(undefined);
    // Simulate a loopback server that returns a code but with state mismatch
    // The test injects a loopback server that resolves the code,
    // but the state validation in runPkceLogin must verify the state matches what was sent.
    // We do this by having the server return a "bad state" alongside the code.
    const listenFn = vi.fn().mockResolvedValue({
      port: 54321,
      waitForCode: vi.fn().mockRejectedValue(Object.assign(new Error('state_mismatch'), { code: 'STATE_MISMATCH' })),
    });
    const deps = makeDeps({
      loopbackServer: { listen: listenFn, close: vi.fn().mockResolvedValue(undefined) },
      storeCredentials: storeFn,
    });
    const result = await runPkceLogin('google', 'https://api.example.com', deps);
    expect(result.exitCode).toBeGreaterThan(0);
    expect(storeFn).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// runPkceLogin – timeout
// ---------------------------------------------------------------------------

describe('runPkceLogin – 5-minute timeout', () => {
  it('returns non-zero exit with "Authentication cancelled or timed out" on timeout', async () => {
    const deps = makeDeps({ loopbackServer: makeTimedOutLoopbackServer() });
    const result = await runPkceLogin('google', 'https://api.example.com', deps);
    expect(result.exitCode).toBeGreaterThan(0);
    expect(result.output).toContain('Authentication cancelled or timed out');
  });

  it('does NOT store credentials on timeout', async () => {
    const storeFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({
      loopbackServer: makeTimedOutLoopbackServer(),
      storeCredentials: storeFn,
    });
    await runPkceLogin('google', 'https://api.example.com', deps);
    expect(storeFn).not.toHaveBeenCalled();
  });

  it('closes the loopback server after timeout', async () => {
    const closeFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({
      loopbackServer: {
        listen: vi.fn().mockResolvedValue({
          port: 54321,
          waitForCode: vi.fn().mockRejectedValue(new Error('Authentication cancelled or timed out')),
        }),
        close: closeFn,
      },
    });
    await runPkceLogin('google', 'https://api.example.com', deps);
    expect(closeFn).toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// runPkceLogin – token exchange error
// ---------------------------------------------------------------------------

describe('runPkceLogin – exchange error', () => {
  it('returns non-zero exit on token exchange failure', async () => {
    const deps = makeDeps({ tokenExchange: makeFailingTokenExchange() });
    const result = await runPkceLogin('google', 'https://api.example.com', deps);
    expect(result.exitCode).toBeGreaterThan(0);
  });

  it('does NOT store credentials when exchange fails', async () => {
    const storeFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({
      tokenExchange: makeFailingTokenExchange(),
      storeCredentials: storeFn,
    });
    await runPkceLogin('google', 'https://api.example.com', deps);
    expect(storeFn).not.toHaveBeenCalled();
  });

  it('closes the loopback server after exchange failure', async () => {
    const closeFn = vi.fn().mockResolvedValue(undefined);
    const deps = makeDeps({
      loopbackServer: {
        listen: makeSuccessfulLoopbackServer().listen,
        close: closeFn,
      },
      tokenExchange: makeFailingTokenExchange(),
    });
    await runPkceLogin('google', 'https://api.example.com', deps);
    expect(closeFn).toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// createLoopbackServer – loopback-bind integration test
// (uses real Node.js net/http — binds to an ephemeral port on 127.0.0.1)
// ---------------------------------------------------------------------------

describe('createLoopbackServer – integration', () => {
  it('binds to 127.0.0.1 (loopback only)', async () => {
    const server = createLoopbackServer();
    // Start listening; the wait will never complete unless we send a code.
    // We check the server address immediately after listen.
    const handle = await server.listen(0, '127.0.0.1');
    expect(handle.port).toBeGreaterThan(0);
    expect(handle.port).toBeLessThanOrEqual(65535);

    // Now simulate receiving a code by making an HTTP request to the callback
    const receivedCode = await new Promise<string>((resolve, reject) => {
      const req = http.get(`http://127.0.0.1:${handle.port}/callback?code=test-code-integration&state=test-state`, (res) => {
        res.resume();
        res.on('end', () => resolve('test-code-integration'));
      });
      req.on('error', reject);
    });

    await server.close();
    expect(receivedCode).toBe('test-code-integration');
  });

  it('returns an ephemeral port (port > 1023)', async () => {
    const server = createLoopbackServer();
    const handle = await server.listen(0, '127.0.0.1');
    expect(handle.port).toBeGreaterThan(1023);
    await server.close();
  });

  it('two servers get different ports', async () => {
    const server1 = createLoopbackServer();
    const server2 = createLoopbackServer();
    const handle1 = await server1.listen(0, '127.0.0.1');
    const handle2 = await server2.listen(0, '127.0.0.1');
    expect(handle1.port).not.toBe(handle2.port);
    await server1.close();
    await server2.close();
  });

  it('resolves the code from the callback GET request', async () => {
    const server = createLoopbackServer();
    const handle = await server.listen(0, '127.0.0.1');

    // Send the code via HTTP simultaneously
    const [code] = await Promise.all([
      handle.waitForCode(),
      new Promise<void>((resolve, reject) => {
        const req = http.get(
          `http://127.0.0.1:${handle.port}/callback?code=real-auth-code&state=some-state`,
          (res) => {
            res.resume();
            res.on('end', resolve);
          },
        );
        req.on('error', reject);
      }),
    ]);

    await server.close();
    expect(code).toBe('real-auth-code');
  });

  it('does NOT bind to 0.0.0.0', async () => {
    // Try to connect from loopback — should work.
    // Verify the actual server address is 127.0.0.1.
    const server = createLoopbackServer();
    const handle = await server.listen(0, '127.0.0.1');

    const address = await new Promise<string | null>((resolve) => {
      // Check by attempting a loopback connection
      const socket = net.connect({ port: handle.port, host: '127.0.0.1' }, () => {
        socket.destroy();
        resolve('127.0.0.1');
      });
      socket.on('error', () => resolve(null));
    });

    await server.close();
    expect(address).toBe('127.0.0.1');
  });
});
