/**
 * Tests for src/auth/token-attachment.ts  (Group 11.7 — token attachment)
 *
 * Production module path: src/auth/token-attachment.ts
 *
 * This module tests the behavior of the augmented marketplace client that attaches
 * Authorization: Bearer headers for authenticated commands (publish, private pull),
 * performs local expiry pre-checks, and deliberately sends NO auth header for
 * public plugin downloads.
 *
 * Exported types:
 *   - AuthenticatedClientDeps: {
 *       credentials: Credentials | null;
 *       refreshTokens?(refreshToken: string): Promise<Credentials>;
 *       storeCredentials?(creds: Credentials): Promise<void>;
 *       clock?: { now(): Date };
 *     }
 *   - createAuthenticatedClient(
 *       baseClient: IMarketplaceClient,
 *       deps: AuthenticatedClientDeps,
 *     ): IMarketplaceClient
 *       → uploadPlugin: attaches Bearer; pre-checks expiry; 401 from backend → throws SessionExpiredError
 *       → downloadPlugin (private): attaches Bearer; 401 → SessionExpiredError; 403 → surfaced as MarketplaceApiError(403)
 *       → downloadPlugin (public): sends NO Authorization header even when credentials exist
 *       → searchPlugins: no auth header (public)
 *
 * Exported errors:
 *   - SessionExpiredError extends Error: { message: "Session expired. Please run 'claude-plugin login'" }
 *
 * Exported functions:
 *   - isTokenExpired(expiresAt: string, clock?: { now(): Date }): boolean
 *       → true when expiresAt is in the past
 *       → false when expiresAt is in the future
 *       → true when expiresAt is exactly now (treat as expired)
 *
 * NOTE: "private pull" is downloadPlugin with a plugin that requires auth.
 * The distinction is whether the server returns 401/403 or 200.
 * The public-vs-private determination for header sending is:
 *   - public download (anonymous call): NO Bearer header regardless of stored token
 *   - publish (uploadPlugin): ALWAYS attach Bearer
 *   - private download (downloadPlugin when credentials present + endpoint returns 401/403):
 *     attach Bearer
 *
 * Implementation contract for createAuthenticatedClient:
 *   - uploadPlugin: attach Bearer, check local expiry first
 *   - downloadPlugin: attach Bearer IF credentials present; if NO credentials and server returns 401 → SessionExpiredError
 *   - If local expiry check fails: throw SessionExpiredError WITHOUT making a network call
 *   - If server returns 401: throw SessionExpiredError
 *   - If server returns 403: re-throw as MarketplaceApiError(403)
 *   - searchPlugins, getPlugin, getLatestVersion, checkVersionExists: pass through, NO Bearer
 */

import { describe, it, expect, vi } from 'vitest';

// These imports WILL FAIL until src/auth/token-attachment.ts is created (RED state).
import {
  createAuthenticatedClient,
  isTokenExpired,
  SessionExpiredError,
} from '../auth/token-attachment.js';
import type { AuthenticatedClientDeps } from '../auth/token-attachment.js';
import type { Credentials } from '../auth/credentials-store.js';
import {
  MarketplaceApiError,
} from '../api/client.js';
import type {
  IMarketplaceClient,
  UploadResponse,
  ProblemDetails,
} from '../api/client.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const FUTURE_ISO = new Date(Date.now() + 3_600_000).toISOString(); // 1 hour from now
const PAST_ISO = new Date(Date.now() - 3_600_000).toISOString();   // 1 hour ago

const VALID_CREDS: Credentials = {
  access: 'eyJ.valid.access.token',
  refresh: 'opaque-refresh-token',
  expiresAt: FUTURE_ISO,
  user: 'user@example.com',
  provider: 'google',
};

const EXPIRED_CREDS: Credentials = {
  ...VALID_CREDS,
  expiresAt: PAST_ISO,
};

const SUCCESS_UPLOAD: UploadResponse = {
  id: 'abc123',
  name: '@ns/plugin',
  slug: 'ns-plugin',
  version: '1.0.0',
};

function make401Error(): MarketplaceApiError {
  const pd: ProblemDetails = { title: 'Unauthorized', status: 401 };
  return new MarketplaceApiError(pd, 401);
}

function make403Error(): MarketplaceApiError {
  const pd: ProblemDetails = { title: 'Forbidden', status: 403, detail: 'Not a member' };
  return new MarketplaceApiError(pd, 403);
}

function makeDeps(creds: Credentials | null, overrides: Partial<AuthenticatedClientDeps> = {}): AuthenticatedClientDeps {
  return {
    credentials: creds,
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// isTokenExpired
// ---------------------------------------------------------------------------

describe('isTokenExpired', () => {
  it('returns false when expiresAt is in the future', () => {
    expect(isTokenExpired(FUTURE_ISO)).toBe(false);
  });

  it('returns true when expiresAt is in the past', () => {
    expect(isTokenExpired(PAST_ISO)).toBe(true);
  });

  it('returns true when expiresAt equals exactly now', () => {
    const now = new Date();
    const clock = { now: () => now };
    expect(isTokenExpired(now.toISOString(), clock)).toBe(true);
  });

  it('uses the provided clock for now()', () => {
    const farFuture = new Date(Date.now() + 10_000_000).toISOString();
    // With a clock in the far future, the token should appear expired
    const futureClock = { now: () => new Date(Date.now() + 20_000_000) };
    expect(isTokenExpired(farFuture, futureClock)).toBe(true);
  });

  it('handles ISO strings with different timezone representations', () => {
    const utcFuture = new Date(Date.now() + 3600 * 1000).toUTCString();
    // Convert via Date to ensure ISO
    const isoFuture = new Date(utcFuture).toISOString();
    expect(isTokenExpired(isoFuture)).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// SessionExpiredError
// ---------------------------------------------------------------------------

describe('SessionExpiredError', () => {
  it('is an instance of Error', () => {
    const err = new SessionExpiredError();
    expect(err).toBeInstanceOf(Error);
  });

  it("message contains 'Session expired'", () => {
    const err = new SessionExpiredError();
    expect(err.message).toContain('Session expired');
  });

  it("message mentions 'claude-plugin login'", () => {
    const err = new SessionExpiredError();
    expect(err.message).toContain('claude-plugin login');
  });
});

// ---------------------------------------------------------------------------
// createAuthenticatedClient – uploadPlugin (publish)
// ---------------------------------------------------------------------------

describe('createAuthenticatedClient – uploadPlugin attaches Bearer', () => {
  it('attaches Authorization: Bearer header when credentials are valid', async () => {
    const capturedHeaders: Record<string, string>[] = [];
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn(),
      uploadPlugin: vi.fn().mockImplementation(async (_fd: FormData, headers?: Record<string, string>) => {
        capturedHeaders.push(headers ?? {});
        return SUCCESS_UPLOAD;
      }),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(VALID_CREDS));
    await client.uploadPlugin(new FormData());

    expect(capturedHeaders).toHaveLength(1);
    expect(capturedHeaders[0]?.['Authorization']).toBe(`Bearer ${VALID_CREDS.access}`);
  });

  it('throws SessionExpiredError when credentials are locally expired (no network call)', async () => {
    const uploadFn = vi.fn();
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn(),
      uploadPlugin: uploadFn,
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(EXPIRED_CREDS));
    await expect(client.uploadPlugin(new FormData())).rejects.toBeInstanceOf(SessionExpiredError);
    expect(uploadFn).not.toHaveBeenCalled();
  });

  it('throws SessionExpiredError when backend returns 401 on upload', async () => {
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn(),
      uploadPlugin: vi.fn().mockRejectedValue(make401Error()),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(VALID_CREDS));
    await expect(client.uploadPlugin(new FormData())).rejects.toBeInstanceOf(SessionExpiredError);
  });

  it('throws SessionExpiredError when no credentials and upload attempted', async () => {
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn(),
      uploadPlugin: vi.fn().mockRejectedValue(make401Error()),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(null));
    await expect(client.uploadPlugin(new FormData())).rejects.toBeInstanceOf(SessionExpiredError);
  });
});

// ---------------------------------------------------------------------------
// createAuthenticatedClient – downloadPlugin (private pull)
// ---------------------------------------------------------------------------

describe('createAuthenticatedClient – downloadPlugin (private pull) attaches Bearer', () => {
  it('attaches Authorization: Bearer when credentials are valid', async () => {
    const capturedHeaders: Record<string, string>[] = [];
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn().mockImplementation(async (_id: string, _v?: string, headers?: Record<string, string>) => {
        capturedHeaders.push(headers ?? {});
        return new ReadableStream();
      }),
      uploadPlugin: vi.fn(),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(VALID_CREDS));
    await client.downloadPlugin('plugin-id', '1.0.0');

    expect(capturedHeaders[0]?.['Authorization']).toBe(`Bearer ${VALID_CREDS.access}`);
  });

  it('throws SessionExpiredError when backend returns 401 on private download', async () => {
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn().mockRejectedValue(make401Error()),
      uploadPlugin: vi.fn(),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(VALID_CREDS));
    await expect(client.downloadPlugin('plugin-id', '1.0.0')).rejects.toBeInstanceOf(SessionExpiredError);
  });

  it('surfaces 403 as MarketplaceApiError (non-member)', async () => {
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn().mockRejectedValue(make403Error()),
      uploadPlugin: vi.fn(),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(VALID_CREDS));
    let caught: unknown;
    try {
      await client.downloadPlugin('private-plugin-id', '1.0.0');
    } catch (e) {
      caught = e;
    }
    expect(caught).toBeInstanceOf(MarketplaceApiError);
    expect((caught as MarketplaceApiError).status).toBe(403);
  });

  it('throws SessionExpiredError when credentials are locally expired before download', async () => {
    const downloadFn = vi.fn();
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: downloadFn,
      uploadPlugin: vi.fn(),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(EXPIRED_CREDS));
    await expect(client.downloadPlugin('plugin-id', '1.0.0')).rejects.toBeInstanceOf(SessionExpiredError);
    expect(downloadFn).not.toHaveBeenCalled();
  });
});

// ---------------------------------------------------------------------------
// createAuthenticatedClient – public downloadPlugin sends NO auth header
// ---------------------------------------------------------------------------

describe('createAuthenticatedClient – public downloadPlugin sends NO auth header', () => {
  it('passes NO Authorization header for searchPlugins even when credentials exist', async () => {
    const capturedArgs: unknown[][] = [];
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn().mockImplementation(async (...args: unknown[]) => {
        capturedArgs.push(args);
        return { data: [], totalCount: 0, page: 1, limit: 10, totalPages: 0 };
      }),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn().mockResolvedValue(new ReadableStream()),
      uploadPlugin: vi.fn(),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(VALID_CREDS));
    await client.searchPlugins('test', 10);

    // searchPlugins should be called without any auth header in its args
    const callArgs = capturedArgs[0] ?? [];
    const authHeaderArg = callArgs.find(
      (a) => typeof a === 'object' && a !== null && 'Authorization' in a,
    );
    expect(authHeaderArg).toBeUndefined();
  });

  it('public downloadPlugin (no credentials) sends NO Authorization header', async () => {
    const capturedHeaders: (Record<string, string> | undefined)[] = [];
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn().mockImplementation(async (_id: string, _v?: string, headers?: Record<string, string>) => {
        capturedHeaders.push(headers);
        return new ReadableStream();
      }),
      uploadPlugin: vi.fn(),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    // No credentials → public pull
    const client = createAuthenticatedClient(baseClient, makeDeps(null));
    await client.downloadPlugin('public-plugin-id');

    // No Authorization header should be present
    const authHeader = capturedHeaders[0]?.['Authorization'];
    expect(authHeader).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// createAuthenticatedClient – preserves non-auth methods
// ---------------------------------------------------------------------------

describe('createAuthenticatedClient – pass-through for non-auth methods', () => {
  it('getPlugin is called on base client', async () => {
    const getPluginFn = vi.fn().mockResolvedValue({ id: '1', name: 'test' });
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: getPluginFn,
      downloadPlugin: vi.fn(),
      uploadPlugin: vi.fn(),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };
    const client = createAuthenticatedClient(baseClient, makeDeps(VALID_CREDS));
    await client.getPlugin('test-plugin');
    expect(getPluginFn).toHaveBeenCalledWith('test-plugin');
  });

  it('checkVersionExists is called on base client', async () => {
    const checkFn = vi.fn().mockResolvedValue(true);
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn(),
      uploadPlugin: vi.fn(),
      getLatestVersion: vi.fn(),
      checkVersionExists: checkFn,
    };
    const client = createAuthenticatedClient(baseClient, makeDeps(VALID_CREDS));
    await client.checkVersionExists('plugin-id', '1.0.0');
    expect(checkFn).toHaveBeenCalledWith('plugin-id', '1.0.0');
  });
});

// ---------------------------------------------------------------------------
// createAuthenticatedClient – SessionExpiredError message format
// ---------------------------------------------------------------------------

describe('createAuthenticatedClient – SessionExpiredError output format', () => {
  it('SessionExpiredError message starts with "Session expired"', async () => {
    const baseClient: IMarketplaceClient = {
      searchPlugins: vi.fn(),
      getPlugin: vi.fn(),
      downloadPlugin: vi.fn(),
      uploadPlugin: vi.fn().mockRejectedValue(make401Error()),
      getLatestVersion: vi.fn(),
      checkVersionExists: vi.fn(),
    };

    const client = createAuthenticatedClient(baseClient, makeDeps(VALID_CREDS));
    let caught: SessionExpiredError | undefined;
    try {
      await client.uploadPlugin(new FormData());
    } catch (e) {
      if (e instanceof SessionExpiredError) caught = e;
    }
    expect(caught?.message).toMatch(/^Session expired/);
  });
});
