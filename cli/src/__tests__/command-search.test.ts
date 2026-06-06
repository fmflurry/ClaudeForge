/**
 * Tests for src/commands/search.ts
 *
 * Production module path: src/commands/search.ts
 * Exported functions:
 *   - runSearch(args: SearchArgs, deps: SearchDeps): Promise<CommandResult>
 *       args: { query: string; limit?: number }
 *       deps: { client: IMarketplaceClient; homeDir: string; env?: NodeJS.ProcessEnv }
 *   - CommandResult: { exitCode: number; output: string }
 *
 * VERBATIM spec strings:
 *   - no results: "No plugins found matching '<query>'"
 *   - table columns: Name, Version, Description, Downloads
 *   - more results hint: "--limit 20"
 *   - suggest browse: "claude plugin list-available"
 *   - default limit: 10
 */

import { describe, it, expect, vi } from 'vitest';

// These imports WILL FAIL until src/commands/search.ts is created (RED state).
import { runSearch } from '../commands/search.js';
import type { CommandResult } from '../commands/search.js';
import type { IMarketplaceClient, PaginatedResponse, SearchResult } from '../api/client.js';

// ---------------------------------------------------------------------------
// Helpers
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

function makeSearchResult(overrides?: Partial<SearchResult>): SearchResult {
  return {
    id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
    name: '@auth/plugin',
    slug: 'auth-plugin',
    description: 'Authentication helper plugin',
    relevanceScore: 0.9,
    downloadCount: 500,
    ...overrides,
  };
}

function makePagedResponse(results: SearchResult[]): PaginatedResponse<SearchResult> {
  return {
    data: results,
    totalCount: results.length,
    page: 1,
    limit: 10,
    totalPages: 1,
  };
}

// ---------------------------------------------------------------------------
// runSearch
// ---------------------------------------------------------------------------

describe('runSearch – happy path', () => {
  it('returns exitCode 0 on successful search with results', async () => {
    const result = makeSearchResult();
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockResolvedValue(makePagedResponse([result])),
    });
    const outcome: CommandResult = await runSearch(
      { query: 'authentication' },
      { client, homeDir: '/tmp/fake-home' },
    );
    expect(outcome.exitCode).toBe(0);
  });

  it('output contains table headers: Name, Version, Description, Downloads', async () => {
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockResolvedValue(makePagedResponse([makeSearchResult()])),
    });
    const outcome = await runSearch(
      { query: 'authentication' },
      { client, homeDir: '/tmp/fake-home' },
    );
    expect(outcome.output).toContain('Name');
    expect(outcome.output).toContain('Version');
    expect(outcome.output).toContain('Description');
    expect(outcome.output).toContain('Downloads');
  });

  it('output contains the result plugin name', async () => {
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockResolvedValue(
        makePagedResponse([makeSearchResult({ name: '@auth/plugin' })]),
      ),
    });
    const outcome = await runSearch(
      { query: 'authentication' },
      { client, homeDir: '/tmp/fake-home' },
    );
    expect(outcome.output).toContain('@auth/plugin');
  });

  it('output contains the download count', async () => {
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockResolvedValue(
        makePagedResponse([makeSearchResult({ downloadCount: 777 })]),
      ),
    });
    const outcome = await runSearch(
      { query: 'authentication' },
      { client, homeDir: '/tmp/fake-home' },
    );
    expect(outcome.output).toContain('777');
  });
});

describe('runSearch – default limit', () => {
  it('calls searchPlugins with limit 10 by default', async () => {
    const searchFn = vi.fn().mockResolvedValue(makePagedResponse([]));
    const client = makeFakeClient({ searchPlugins: searchFn });
    await runSearch({ query: 'authentication' }, { client, homeDir: '/tmp/fake-home' });
    expect(searchFn).toHaveBeenCalledWith('authentication', 10);
  });

  it('calls searchPlugins with custom limit when --limit is provided', async () => {
    const searchFn = vi.fn().mockResolvedValue(makePagedResponse([]));
    const client = makeFakeClient({ searchPlugins: searchFn });
    await runSearch({ query: 'authentication', limit: 20 }, { client, homeDir: '/tmp/fake-home' });
    expect(searchFn).toHaveBeenCalledWith('authentication', 20);
  });

  it('output suggests --limit 20 to fetch more results', async () => {
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockResolvedValue(makePagedResponse([makeSearchResult()])),
    });
    const outcome = await runSearch(
      { query: 'authentication' },
      { client, homeDir: '/tmp/fake-home' },
    );
    // Spec: "Suggest --limit 20 to fetch more results"
    expect(outcome.output).toContain('--limit 20');
  });
});

describe('runSearch – no results', () => {
  it('returns exitCode 0 (no results is not an error)', async () => {
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockResolvedValue(makePagedResponse([])),
    });
    const outcome = await runSearch(
      { query: 'xyz-nonexistent-plugin' },
      { client, homeDir: '/tmp/fake-home' },
    );
    expect(outcome.exitCode).toBe(0);
  });

  it('reports verbatim "No plugins found matching \'xyz-nonexistent-plugin\'"', async () => {
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockResolvedValue(makePagedResponse([])),
    });
    const outcome = await runSearch(
      { query: 'xyz-nonexistent-plugin' },
      { client, homeDir: '/tmp/fake-home' },
    );
    expect(outcome.output).toContain("No plugins found matching 'xyz-nonexistent-plugin'");
  });

  it('suggests browsing all plugins with claude plugin list-available', async () => {
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockResolvedValue(makePagedResponse([])),
    });
    const outcome = await runSearch(
      { query: 'xyz' },
      { client, homeDir: '/tmp/fake-home' },
    );
    expect(outcome.output).toContain('claude plugin list-available');
  });
});

describe('runSearch – network error', () => {
  it('returns non-zero exitCode when API call throws', async () => {
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockRejectedValue(new Error('Network failure')),
    });
    const outcome = await runSearch(
      { query: 'auth' },
      { client, homeDir: '/tmp/fake-home' },
    );
    expect(outcome.exitCode).toBeGreaterThan(0);
  });

  it('output mentions "Could not reach marketplace"', async () => {
    const client = makeFakeClient({
      searchPlugins: vi.fn().mockRejectedValue(new TypeError('fetch failed')),
    });
    const outcome = await runSearch(
      { query: 'auth' },
      { client, homeDir: '/tmp/fake-home' },
    );
    expect(outcome.output.toLowerCase()).toContain('could not reach marketplace');
  });
});
