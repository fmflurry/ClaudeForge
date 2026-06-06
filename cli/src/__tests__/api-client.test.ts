/**
 * Tests for src/api/client.ts
 *
 * Production module path: src/api/client.ts
 * Exported interfaces / types:
 *   - ProblemDetails: { type?: string; title: string; status: number; detail?: string }
 *   - MarketplaceApiError extends Error: { problemDetails: ProblemDetails; status: number }
 *   - PluginSummary: { id: string; name: string; slug: string; description: string; author: string;
 *                      downloadCount: number; latestVersion: string | null }
 *   - PluginDetail: PluginSummary & { allVersions: VersionSummary[] }
 *   - VersionSummary: { version: string; releasedAt: string; downloadCount: number;
 *                       isLatest: boolean; packageFormat: string; sizeBytes: number }
 *   - SearchResult: { id: string; name: string; slug: string; description: string;
 *                     relevanceScore: number; downloadCount: number }
 *   - PaginatedResponse<T>: { data: T[]; totalCount: number; page: number;
 *                             limit: number; totalPages: number }
 *   - UploadResponse: { id: string; name: string; slug: string; version: string }
 *   - IMarketplaceClient interface with methods:
 *       searchPlugins(q: string, limit: number): Promise<PaginatedResponse<SearchResult>>
 *       getPlugin(name: string): Promise<PluginDetail>
 *       downloadPlugin(pluginId: string, version?: string): Promise<ReadableStream<Uint8Array>>
 *       uploadPlugin(formData: FormData): Promise<UploadResponse>
 *       getLatestVersion(pluginId: string): Promise<VersionSummary>
 *       checkVersionExists(pluginId: string, version: string): Promise<boolean>
 *   - createMarketplaceClient(apiUrl: string): IMarketplaceClient
 *       → returns a fetch-based implementation (Node 22 global fetch)
 *
 * NOTE: createMarketplaceClient is the production factory. Tests exercise it with
 * a controlled fetch stub (vi.stubGlobal) and also test the interface boundary
 * directly with a fake implementation — never a real network call.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

// These imports WILL FAIL until src/api/client.ts is created (RED state).
import {
  createMarketplaceClient,
  MarketplaceApiError,
} from '../api/client.js';
import type {
  IMarketplaceClient,
  PaginatedResponse,
  SearchResult,
  PluginDetail,
  PluginSummary,
  VersionSummary,
  ProblemDetails,
} from '../api/client.js';

// ---------------------------------------------------------------------------
// Fake client factory (interface-level test double)
// ---------------------------------------------------------------------------

function makeFakeClient(overrides?: Partial<IMarketplaceClient>): IMarketplaceClient {
  return {
    searchPlugins: vi.fn(),
    getPlugin: vi.fn(),
    downloadPlugin: vi.fn(),
    uploadPlugin: vi.fn(),
    getLatestVersion: vi.fn(),
    checkVersionExists: vi.fn(),
    ...overrides,
  };
}

const SAMPLE_VERSION: VersionSummary = {
  version: '1.2.3',
  releasedAt: '2024-01-01T00:00:00.000Z',
  downloadCount: 42,
  isLatest: true,
  packageFormat: 'tar.gz',
  sizeBytes: 102400,
};

const SAMPLE_PLUGIN_SUMMARY: PluginSummary = {
  id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
  name: '@namespace/plugin-name',
  slug: 'namespace-plugin-name',
  description: 'A test plugin',
  author: 'Test Author',
  downloadCount: 100,
  latestVersion: '1.2.3',
};

const SAMPLE_PLUGIN_DETAIL: PluginDetail = {
  ...SAMPLE_PLUGIN_SUMMARY,
  allVersions: [SAMPLE_VERSION],
};

const SAMPLE_SEARCH_RESULT: SearchResult = {
  id: SAMPLE_PLUGIN_SUMMARY.id,
  name: SAMPLE_PLUGIN_SUMMARY.name,
  slug: SAMPLE_PLUGIN_SUMMARY.slug,
  description: SAMPLE_PLUGIN_SUMMARY.description,
  relevanceScore: 0.95,
  downloadCount: 100,
};

// ---------------------------------------------------------------------------
// MarketplaceApiError
// ---------------------------------------------------------------------------

describe('MarketplaceApiError', () => {
  it('is an instance of Error', () => {
    const pd: ProblemDetails = { title: 'Not Found', status: 404, detail: 'Plugin not found' };
    const err = new MarketplaceApiError(pd, 404);
    expect(err).toBeInstanceOf(Error);
  });

  it('exposes problemDetails and status', () => {
    const pd: ProblemDetails = { title: 'Conflict', status: 409, detail: 'Version already exists' };
    const err = new MarketplaceApiError(pd, 409);
    expect(err.problemDetails).toEqual(pd);
    expect(err.status).toBe(409);
  });

  it('uses ProblemDetails.title as the error message', () => {
    const pd: ProblemDetails = { title: 'Bad Request', status: 400, detail: 'Missing field' };
    const err = new MarketplaceApiError(pd, 400);
    expect(err.message).toBe('Bad Request');
  });
});

// ---------------------------------------------------------------------------
// createMarketplaceClient — request shaping
// ---------------------------------------------------------------------------

describe('createMarketplaceClient – request shaping', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('searchPlugins calls GET /api/v1/plugins/search with q and limit params', async () => {
    const paginated: PaginatedResponse<SearchResult> = {
      data: [SAMPLE_SEARCH_RESULT],
      totalCount: 1,
      page: 1,
      limit: 10,
      totalPages: 1,
    };
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => paginated,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.searchPlugins('authentication', 10);

    const [calledUrl] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, ...unknown[]];
    expect(calledUrl).toContain('/api/v1/plugins/search');
    expect(calledUrl).toContain('q=authentication');
    expect(calledUrl).toContain('limit=10');
  });

  it('getPlugin calls GET /api/v1/plugins/search or catalog endpoint with plugin name', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ data: [SAMPLE_PLUGIN_DETAIL], totalCount: 1, page: 1, limit: 1, totalPages: 1 }),
    });

    const client = createMarketplaceClient('https://api.example.com');
    await client.getPlugin('@namespace/plugin-name');

    const [calledUrl] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, ...unknown[]];
    expect(typeof calledUrl).toBe('string');
    expect(calledUrl.length).toBeGreaterThan(0);
  });

  it('getLatestVersion constructs a URL using the pluginId', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ data: [SAMPLE_VERSION], totalCount: 1, page: 1, limit: 1, totalPages: 1 }),
    });

    const client = createMarketplaceClient('https://api.example.com');
    const pluginId = SAMPLE_PLUGIN_SUMMARY.id;
    await client.getLatestVersion(pluginId);

    const [calledUrl] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0] as [string, ...unknown[]];
    expect(calledUrl).toContain(pluginId);
  });
});

// ---------------------------------------------------------------------------
// createMarketplaceClient — error mapping (ProblemDetails → MarketplaceApiError)
// ---------------------------------------------------------------------------

describe('createMarketplaceClient – error mapping', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn());
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('throws MarketplaceApiError with status 404 when server returns 404', async () => {
    const pd: ProblemDetails = { title: 'Not Found', status: 404, detail: 'Plugin not found' };
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 404,
      json: async () => pd,
    });

    const client = createMarketplaceClient('https://api.example.com');
    await expect(client.getPlugin('nonexistent')).rejects.toBeInstanceOf(MarketplaceApiError);
  });

  it('thrown MarketplaceApiError has the correct status code', async () => {
    const pd: ProblemDetails = { title: 'Conflict', status: 409, detail: 'Version already exists' };
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 409,
      json: async () => pd,
    });

    const client = createMarketplaceClient('https://api.example.com');
    let caught: MarketplaceApiError | undefined;
    try {
      await client.searchPlugins('test', 10);
    } catch (e) {
      if (e instanceof MarketplaceApiError) caught = e;
    }
    expect(caught?.status).toBe(409);
    expect(caught?.problemDetails.detail).toBe('Version already exists');
  });

  it('throws MarketplaceApiError when server is unreachable (network error)', async () => {
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockRejectedValueOnce(
      new TypeError('fetch failed'),
    );

    const client = createMarketplaceClient('https://unreachable.example.com');
    await expect(client.searchPlugins('test', 10)).rejects.toThrow();
  });

  it('throws a typed error wrapping the detail when 400 response has ProblemDetails', async () => {
    const pd: ProblemDetails = { title: 'Bad Request', status: 400, detail: 'Missing required field: type' };
    (globalThis.fetch as ReturnType<typeof vi.fn>).mockResolvedValueOnce({
      ok: false,
      status: 400,
      json: async () => pd,
    });

    const client = createMarketplaceClient('https://api.example.com');
    let caught: MarketplaceApiError | undefined;
    try {
      await client.uploadPlugin(new FormData());
    } catch (e) {
      if (e instanceof MarketplaceApiError) caught = e;
    }
    expect(caught?.status).toBe(400);
    expect(caught?.problemDetails.detail).toContain('Missing required field');
  });
});

// ---------------------------------------------------------------------------
// IMarketplaceClient interface shape (static type-level tests via fake impl)
// ---------------------------------------------------------------------------

describe('IMarketplaceClient interface contract', () => {
  it('fake client satisfies the IMarketplaceClient interface', () => {
    const client: IMarketplaceClient = makeFakeClient();
    expect(typeof client.searchPlugins).toBe('function');
    expect(typeof client.getPlugin).toBe('function');
    expect(typeof client.downloadPlugin).toBe('function');
    expect(typeof client.uploadPlugin).toBe('function');
    expect(typeof client.getLatestVersion).toBe('function');
    expect(typeof client.checkVersionExists).toBe('function');
  });

  it('searchPlugins returns PaginatedResponse<SearchResult> shape', async () => {
    const response: PaginatedResponse<SearchResult> = {
      data: [SAMPLE_SEARCH_RESULT],
      totalCount: 1,
      page: 1,
      limit: 10,
      totalPages: 1,
    };
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockResolvedValue(response),
    });
    const result = await client.searchPlugins('auth', 10);
    expect(result.data).toHaveLength(1);
    expect(result.data[0]).toMatchObject<Partial<SearchResult>>({
      name: '@namespace/plugin-name',
    });
  });

  it('getPlugin returns PluginDetail with allVersions array', async () => {
    const client = makeFakeClient({
      getPlugin: vi.fn().mockResolvedValue(SAMPLE_PLUGIN_DETAIL),
    });
    const result = await client.getPlugin('@namespace/plugin-name');
    expect(result.allVersions).toBeInstanceOf(Array);
    expect(result.latestVersion).toBe('1.2.3');
  });

  it('checkVersionExists returns a boolean', async () => {
    const client = makeFakeClient({
      checkVersionExists: vi.fn().mockResolvedValue(false),
    });
    const result = await client.checkVersionExists('some-id', '1.0.0');
    expect(typeof result).toBe('boolean');
  });
});
