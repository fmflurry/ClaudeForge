/**
 * Token attachment — wraps IMarketplaceClient to inject Bearer tokens.
 * - uploadPlugin: always attaches Bearer; local expiry precheck
 * - downloadPlugin: attaches Bearer when credentials present; no header when null
 * - searchPlugins, getPlugin, getLatestVersion, checkVersionExists: pass-through (no auth)
 */

import type {
  IMarketplaceClient,
  PaginatedResponse,
  SearchResult,
  PluginDetail,
  VersionSummary,
  UploadResponse,
} from '../api/client.js';
import { MarketplaceApiError } from '../api/client.js';
import type { Credentials } from './credentials-store.js';

// ---------------------------------------------------------------------------
// SessionExpiredError
// ---------------------------------------------------------------------------

export class SessionExpiredError extends Error {
  constructor() {
    super("Session expired. Please run 'claude-plugin login'");
    this.name = 'SessionExpiredError';
    Object.setPrototypeOf(this, SessionExpiredError.prototype);
  }
}

// ---------------------------------------------------------------------------
// isTokenExpired
// ---------------------------------------------------------------------------

export function isTokenExpired(expiresAt: string, clock: { now(): Date } = { now: () => new Date() }): boolean {
  const expiryTime = new Date(expiresAt).getTime();
  const nowTime = clock.now().getTime();
  return nowTime >= expiryTime;
}

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface AuthenticatedClientDeps {
  credentials: Credentials | null;
  refreshTokens?(refreshToken: string): Promise<Credentials>;
  storeCredentials?(creds: Credentials): Promise<void>;
  clock?: { now(): Date };
}

// ---------------------------------------------------------------------------
// createAuthenticatedClient
// ---------------------------------------------------------------------------

export function createAuthenticatedClient(
  baseClient: IMarketplaceClient,
  deps: AuthenticatedClientDeps,
): IMarketplaceClient {
  const { credentials, clock } = deps;

  function getAuthHeader(): Record<string, string> {
    if (!credentials) {
      return {};
    }
    return { Authorization: `Bearer ${credentials.access}` };
  }

  function checkLocalExpiry(): void {
    if (!credentials) {
      return;
    }
    if (isTokenExpired(credentials.expiresAt, clock)) {
      throw new SessionExpiredError();
    }
  }

  function handleError(err: unknown): never {
    if (err instanceof MarketplaceApiError && err.status === 401) {
      throw new SessionExpiredError();
    }
    throw err;
  }

  return {
    searchPlugins(q: string, limit: number): Promise<PaginatedResponse<SearchResult>> {
      return baseClient.searchPlugins(q, limit);
    },

    getPlugin(name: string): Promise<PluginDetail> {
      return baseClient.getPlugin(name);
    },

    getLatestVersion(pluginId: string): Promise<VersionSummary> {
      return baseClient.getLatestVersion(pluginId);
    },

    checkVersionExists(pluginId: string, version: string): Promise<boolean> {
      return baseClient.checkVersionExists(pluginId, version);
    },

    async uploadPlugin(formData: FormData, extraHeaders?: Record<string, string>): Promise<UploadResponse> {
      // No credentials → treat as expired
      if (!credentials) {
        throw new SessionExpiredError();
      }
      checkLocalExpiry();
      const headers = { ...getAuthHeader(), ...(extraHeaders ?? {}) };
      try {
        return await baseClient.uploadPlugin(formData, headers);
      } catch (err) {
        handleError(err);
      }
    },

    async downloadPlugin(
      pluginId: string,
      version?: string,
      extraHeaders?: Record<string, string>,
    ): Promise<ReadableStream<Uint8Array>> {
      if (!credentials) {
        // Public download — no auth header
        return baseClient.downloadPlugin(pluginId, version, extraHeaders);
      }
      checkLocalExpiry();
      const headers = { ...getAuthHeader(), ...(extraHeaders ?? {}) };
      try {
        return await baseClient.downloadPlugin(pluginId, version, headers);
      } catch (err) {
        handleError(err);
      }
    },
  };
}
