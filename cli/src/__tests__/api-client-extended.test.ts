/**
 * Extended tests for src/api/client.ts — covering the previously uncovered branches:
 *
 * - getLatestVersion: 404 branch (empty data array → MarketplaceApiError 404)
 * - checkVersionExists: network-error catch path → returns false
 * - downloadPlugin: with optional headers, without headers, error paths
 * - uploadPlugin: with optional headers, without headers
 * - handleResponse: non-JSON 4xx body (falls back to generic HTTP error title)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import {
  createMarketplaceClient,
  MarketplaceApiError,
} from '../api/client.js';
import type {
  PaginatedResponse,
  VersionSummary,
  ProblemDetails,
  UploadResponse,
} from '../api/client.js';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const SAMPLE_VERSION: VersionSummary = {
  version: '1.2.3',
  releasedAt: '2024-01-01T00:00:00.000Z',
  downloadCount: 42,
  isLatest: true,
  packageFormat: 'tar.gz',
  sizeBytes: 102400,
};

function makeVersionPage(versions: VersionSummary[]): PaginatedResponse<VersionSummary> {
  return {
    data: versions,
    totalCount: versions.length,
    page: 1,
    limit: 1,
    totalPages: versions.length > 0 ? 1 : 0,
  };
}

// ---------------------------------------------------------------------------
// getLatestVersion — 404 branch (empty data)
// ---------------------------------------------------------------------------

describe('createMarketplaceClient – getLatestVersion 404 branch', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('throws MarketplaceApiError with status 404 when data is empty', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => makeVersionPage([]),
    });

    const client = createMarketplaceClient('https://api.example.com');
    await expect(client.getLatestVersion('plugin-id')).rejects.toBeInstanceOf(MarketplaceApiError);
  });

  it('thrown error has status 404 when no versions found', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => makeVersionPage([]),
    });

    const client = createMarketplaceClient('https://api.example.com');
    let caught: MarketplaceApiError | undefined;
    try {
      await client.getLatestVersion('plugin-id');
    } catch (e) {
      if (e instanceof MarketplaceApiError) caught = e;
    }
    expect(caught?.status).toBe(404);
  });

  it('error detail mentions the pluginId when no versions found', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => makeVersionPage([]),
    });

    const client = createMarketplaceClient('https://api.example.com');
    let caught: MarketplaceApiError | undefined;
    try {
      await client.getLatestVersion('my-plugin-id');
    } catch (e) {
      if (e instanceof MarketplaceApiError) caught = e;
    }
    expect(caught?.problemDetails.detail).toContain('my-plugin-id');
  });

  it('returns the first version summary when data has entries', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => makeVersionPage([SAMPLE_VERSION]),
    });

    const client = createMarketplaceClient('https://api.example.com');
    const result = await client.getLatestVersion('plugin-id');
    expect(result).toEqual(SAMPLE_VERSION);
  });

  it('URL includes the pluginId when calling getLatestVersion', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => makeVersionPage([SAMPLE_VERSION]),
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.getLatestVersion('my-specific-plugin');

    const [calledUrl] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, ...unknown[]];
    expect(calledUrl).toContain('my-specific-plugin');
  });
});

// ---------------------------------------------------------------------------
// checkVersionExists — catch path (network error → false)
// ---------------------------------------------------------------------------

describe('createMarketplaceClient – checkVersionExists catch path', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('returns false when fetch throws (network error)', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
      new TypeError('fetch failed'),
    );

    const client = createMarketplaceClient('https://unreachable.example.com');
    const result = await client.checkVersionExists('plugin-id', '1.0.0');
    expect(result).toBe(false);
  });

  it('returns true when server responds 200', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
    });

    const client = createMarketplaceClient('https://api.example.com');
    const result = await client.checkVersionExists('plugin-id', '1.0.0');
    expect(result).toBe(true);
  });

  it('returns false when server responds 404', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 404,
    });

    const client = createMarketplaceClient('https://api.example.com');
    const result = await client.checkVersionExists('plugin-id', '1.0.0');
    expect(result).toBe(false);
  });

  it('URL includes pluginId and version in checkVersionExists request', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.checkVersionExists('my-plugin', '2.3.4');

    const [calledUrl] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, ...unknown[]];
    expect(calledUrl).toContain('my-plugin');
    expect(calledUrl).toContain('2.3.4');
  });
});

// ---------------------------------------------------------------------------
// downloadPlugin — optional headers paths
// ---------------------------------------------------------------------------

describe('createMarketplaceClient – downloadPlugin optional headers', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('calls fetch without headers option when no headers provided', async () => {
    const mockBody = new ReadableStream();
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      body: mockBody,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.downloadPlugin('plugin-id');

    const [, fetchOptions] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, RequestInit | undefined];
    // Without headers, fetch is called with undefined options (no { headers } key)
    expect(fetchOptions).toBeUndefined();
  });

  it('calls fetch with headers when headers are provided', async () => {
    const mockBody = new ReadableStream();
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      body: mockBody,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.downloadPlugin('plugin-id', undefined, { Authorization: 'Bearer token123' });

    const [, fetchOptions] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, RequestInit | undefined];
    expect(fetchOptions).toBeDefined();
    expect((fetchOptions as RequestInit).headers).toMatchObject({ Authorization: 'Bearer token123' });
  });

  it('uses /latest segment when no version is provided', async () => {
    const mockBody = new ReadableStream();
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      body: mockBody,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.downloadPlugin('plugin-id');

    const [calledUrl] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, ...unknown[]];
    expect(calledUrl).toContain('/latest');
  });

  it('uses the specified version segment when version is provided', async () => {
    const mockBody = new ReadableStream();
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      body: mockBody,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.downloadPlugin('plugin-id', '2.0.0');

    const [calledUrl] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, ...unknown[]];
    expect(calledUrl).toContain('2.0.0');
  });

  it('throws MarketplaceApiError when download response is not ok', async () => {
    const pd: ProblemDetails = { title: 'Not Found', status: 404 };
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 404,
      json: async () => pd,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await expect(client.downloadPlugin('plugin-id')).rejects.toBeInstanceOf(MarketplaceApiError);
  });

  it('throws MarketplaceApiError with fallback title when download error body is not JSON', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 503,
      json: async () => { throw new Error('not json'); },
    });

    const client = createMarketplaceClient('https://api.example.com');
    let caught: MarketplaceApiError | undefined;
    try {
      await client.downloadPlugin('plugin-id');
    } catch (e) {
      if (e instanceof MarketplaceApiError) caught = e;
    }
    expect(caught?.status).toBe(503);
    expect(caught?.problemDetails.title).toContain('HTTP Error 503');
  });

  it('throws MarketplaceApiError with status 500 when body is null (empty response)', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      body: null,
    });

    const client = createMarketplaceClient('https://api.example.com');
    let caught: MarketplaceApiError | undefined;
    try {
      await client.downloadPlugin('plugin-id');
    } catch (e) {
      if (e instanceof MarketplaceApiError) caught = e;
    }
    expect(caught?.status).toBe(500);
    expect(caught?.problemDetails.title).toContain('Empty Response');
  });
});

// ---------------------------------------------------------------------------
// uploadPlugin — optional headers paths
// ---------------------------------------------------------------------------

describe('createMarketplaceClient – uploadPlugin optional headers', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('calls fetch with POST method', async () => {
    const uploadResponse: UploadResponse = { id: 'p1', name: 'plugin', slug: 'plugin', version: '1.0.0' };
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => uploadResponse,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.uploadPlugin(new FormData());

    const [, fetchOptions] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, RequestInit];
    expect(fetchOptions.method).toBe('POST');
  });

  it('calls fetch without headers key when no headers provided', async () => {
    const uploadResponse: UploadResponse = { id: 'p1', name: 'plugin', slug: 'plugin', version: '1.0.0' };
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => uploadResponse,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.uploadPlugin(new FormData());

    const [, fetchOptions] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, RequestInit];
    expect(fetchOptions.headers).toBeUndefined();
  });

  it('calls fetch with headers when headers are provided', async () => {
    const uploadResponse: UploadResponse = { id: 'p1', name: 'plugin', slug: 'plugin', version: '1.0.0' };
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => uploadResponse,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.uploadPlugin(new FormData(), { Authorization: 'Bearer mytoken' });

    const [, fetchOptions] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, RequestInit];
    expect(fetchOptions.headers).toMatchObject({ Authorization: 'Bearer mytoken' });
  });

  it('returns the UploadResponse from server', async () => {
    const uploadResponse: UploadResponse = { id: 'uuid-abc', name: 'myplugin', slug: 'myplugin', version: '3.0.0' };
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => uploadResponse,
    });

    const client = createMarketplaceClient('https://api.example.com');
    const result = await client.uploadPlugin(new FormData());
    expect(result).toEqual(uploadResponse);
  });
});

// ---------------------------------------------------------------------------
// handleResponse — non-JSON error body fallback
// ---------------------------------------------------------------------------

describe('createMarketplaceClient – handleResponse non-JSON fallback', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('uses generic HTTP Error title when response body is not valid JSON', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 502,
      json: async () => { throw new Error('invalid json'); },
    });

    const client = createMarketplaceClient('https://api.example.com');
    let caught: MarketplaceApiError | undefined;
    try {
      await client.searchPlugins('test', 10);
    } catch (e) {
      if (e instanceof MarketplaceApiError) caught = e;
    }
    expect(caught?.status).toBe(502);
    expect(caught?.problemDetails.title).toBe('HTTP Error 502');
  });
});

// ---------------------------------------------------------------------------
// Trailing slash stripping in base URL
// ---------------------------------------------------------------------------

describe('createMarketplaceClient – base URL normalisation', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('strips trailing slash from the API URL', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ data: [], totalCount: 0, page: 1, limit: 10, totalPages: 0 }),
    });

    // Pass URL with trailing slash
    const client = createMarketplaceClient('https://api.example.com/');
    await client.searchPlugins('test', 10);

    const [calledUrl] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, ...unknown[]];
    // Should not have double slash
    expect(calledUrl).not.toContain('//api/');
    expect(calledUrl).toContain('/api/v1/plugins/search');
  });
});
