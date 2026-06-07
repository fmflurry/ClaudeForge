/**
 * RED tests — Task 17.2: DocsPort + DocsHttpAdapter
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/docs/domain/ports/docs.port.ts
 *   src/app/features/docs/infrastructure/adapter/docs-http.adapter.ts
 *
 * Production types/classes the coder MUST define:
 *
 *   // docs.port.ts
 *   abstract class DocsPort {
 *     abstract search(
 *       query: string,
 *       page?: number,
 *       limit?: number,
 *     ): Observable<{
 *       items: DocSearchResult[];
 *       totalCount: number;
 *       page: number;
 *       limit: number;
 *       totalPages: number;
 *     }>;
 *     abstract getPage(slug: string): Observable<DocPage>;
 *   }
 *
 *   // docs-http.adapter.ts
 *   @Injectable()
 *   class DocsHttpAdapter extends DocsPort {
 *     private readonly apiClient = inject(ApiClient);
 *     search(...): Observable<...>   — calls apiClient.searchDocs({ search: query, page, limit })
 *     getPage(slug): Observable<DocPage> — calls apiClient.getDocBySlug(slug)
 *                                          if slug starts with 'plugin:' uses convention to map to plugin doc slug
 *                                          if 404 → returns a not-found placeholder DocPage (never throws 404)
 *   }
 *
 * The not-found placeholder DocPage has:
 *   slug  = the requested slug
 *   title = 'No documentation available'
 *   category = ''
 *   contentMarkdown = ''
 *   lastUpdated = new Date(0)  (epoch sentinel)
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { DocsPort } from '../../domain/ports/docs.port';
import { DocsHttpAdapter } from './docs-http.adapter';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { DocSearchResult, DocPage } from '../../domain/models/docs.models';
import type { DocPageDto, PaginatedEnvelope } from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// Fake ApiClient
// ---------------------------------------------------------------------------

const FAKE_DOC_DTO: DocPageDto = {
  slug: 'getting-started',
  title: 'Getting Started',
  content: 'Welcome to ClaudeForge.',
  lastUpdated: '2024-03-01T10:00:00.000Z',
};

const FAKE_SEARCH_ENVELOPE: PaginatedEnvelope<DocPageDto> = {
  data: [FAKE_DOC_DTO],
  totalCount: 1,
  page: 1,
  limit: 20,
  totalPages: 1,
};

const FAKE_PLUGIN_DOC_DTO: DocPageDto = {
  slug: 'plugin:awesome-plugin',
  title: 'Awesome Plugin',
  content: 'README content for awesome-plugin.',
  lastUpdated: '2024-04-01T00:00:00.000Z',
};

@Injectable()
class FakeApiClient {
  searchDocsCalls: { search?: string; page?: number; limit?: number }[] = [];
  getDocBySlugCalls: string[] = [];

  searchDocs(params: { search?: string; page?: number; limit?: number }) {
    this.searchDocsCalls.push(params);
    return of(FAKE_SEARCH_ENVELOPE);
  }

  getDocBySlug(slug: string) {
    this.getDocBySlugCalls.push(slug);
    if (slug === 'not-found-slug') {
      return throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' }));
    }
    if (slug === 'plugin:awesome-plugin') {
      return of(FAKE_PLUGIN_DOC_DTO);
    }
    return of(FAKE_DOC_DTO);
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setupAdapter(): { adapter: DocsHttpAdapter; fakeApiClient: FakeApiClient } {
  const fakeApiClient = new FakeApiClient();
  TestBed.configureTestingModule({
    providers: [DocsHttpAdapter, { provide: ApiClient, useValue: fakeApiClient }],
  });
  return {
    adapter: TestBed.inject(DocsHttpAdapter) as DocsHttpAdapter,
    fakeApiClient,
  };
}

// ---------------------------------------------------------------------------
// DocsPort — abstract contract
// ---------------------------------------------------------------------------

describe('DocsPort — abstract contract', () => {
  it('should exist as an abstract class', () => {
    expect(DocsPort).toBeDefined();
  });

  it('DocsHttpAdapter should be an instance of DocsPort', () => {
    const { adapter } = setupAdapter();
    expect(adapter instanceof DocsPort).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// DocsHttpAdapter — search()
// ---------------------------------------------------------------------------

interface SearchResponse {
  items: DocSearchResult[];
  totalCount: number;
  page: number;
  limit: number;
  totalPages: number;
}

describe('DocsHttpAdapter — search()', () => {
  it('should return an observable emitting items array', () => {
    const { adapter } = setupAdapter();
    let result: SearchResponse | undefined;
    adapter.search('install').subscribe((r: SearchResponse) => (result = r));
    expect(result).toBeDefined();
    expect(Array.isArray(result!.items)).toBe(true);
  });

  it('should map the DTO array to DocSearchResult items', () => {
    const { adapter } = setupAdapter();
    let result: SearchResponse | undefined;
    adapter.search('welcome').subscribe((r: SearchResponse) => (result = r));
    expect(result!.items.length).toBe(1);
    expect(result!.items[0].slug).toBe('getting-started');
    expect(result!.items[0].title).toBe('Getting Started');
  });

  it('should forward pagination metadata', () => {
    const { adapter } = setupAdapter();
    let result: SearchResponse | undefined;
    adapter.search('install').subscribe((r: SearchResponse) => (result = r));
    expect(result!.totalCount).toBe(1);
    expect(result!.page).toBe(1);
    expect(result!.limit).toBe(20);
    expect(result!.totalPages).toBe(1);
  });

  it('should pass query to apiClient.searchDocs as search param', () => {
    const { adapter, fakeApiClient } = setupAdapter();
    adapter.search('typescript').subscribe();
    expect(fakeApiClient.searchDocsCalls[0].search).toBe('typescript');
  });

  it('should pass page and limit parameters', () => {
    const { adapter, fakeApiClient } = setupAdapter();
    adapter.search('install', 2, 10).subscribe();
    expect(fakeApiClient.searchDocsCalls[0].page).toBe(2);
    expect(fakeApiClient.searchDocsCalls[0].limit).toBe(10);
  });

  it('should default page to 1 when not provided', () => {
    const { adapter, fakeApiClient } = setupAdapter();
    adapter.search('install').subscribe();
    expect(fakeApiClient.searchDocsCalls[0].page === undefined || fakeApiClient.searchDocsCalls[0].page === 1).toBe(
      true,
    );
  });

  it('should handle empty search results gracefully', () => {
    TestBed.resetTestingModule();
    const emptyApiClient = {
      searchDocs: () => of({ data: [], totalCount: 0, page: 1, limit: 20, totalPages: 0 }),
      getDocBySlug: () => of(FAKE_DOC_DTO),
    };
    TestBed.configureTestingModule({
      providers: [DocsHttpAdapter, { provide: ApiClient, useValue: emptyApiClient }],
    });
    const adapter: DocsHttpAdapter = TestBed.inject(DocsHttpAdapter) as DocsHttpAdapter;
    let result: SearchResponse | undefined;
    adapter.search('nonexistent').subscribe((r: SearchResponse) => (result = r));
    expect(result!.items).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// DocsHttpAdapter — getPage()
// ---------------------------------------------------------------------------

describe('DocsHttpAdapter — getPage()', () => {
  it('should return an observable emitting a DocPage', () => {
    const { adapter } = setupAdapter();
    let result: DocPage | undefined;
    adapter.getPage('getting-started').subscribe((r: DocPage) => (result = r));
    expect(result).toBeDefined();
    expect(result!.slug).toBe('getting-started');
  });

  it('should map DTO to DocPage with all required fields', () => {
    const { adapter } = setupAdapter();
    let result: DocPage | undefined;
    adapter.getPage('getting-started').subscribe((r: DocPage) => (result = r));
    expect(result!.title).toBe('Getting Started');
    expect(result!.contentMarkdown).toBe('Welcome to ClaudeForge.');
    expect(result!.lastUpdated).toBeInstanceOf(Date);
  });

  it('should call apiClient.getDocBySlug with the slug', () => {
    const { adapter, fakeApiClient } = setupAdapter();
    adapter.getPage('getting-started').subscribe();
    expect(fakeApiClient.getDocBySlugCalls).toContain('getting-started');
  });

  it('should return a not-found placeholder DocPage when 404 occurs (never throw)', () => {
    const { adapter } = setupAdapter();
    let result: DocPage | undefined;
    let threw = false;
    adapter.getPage('not-found-slug').subscribe({
      next: (r: DocPage) => (result = r),
      error: () => (threw = true),
    });
    expect(threw).toBe(false);
    expect(result).toBeDefined();
    expect(result!.slug).toBe('not-found-slug');
  });

  it('should return a placeholder with empty contentMarkdown on 404', () => {
    const { adapter } = setupAdapter();
    let result: DocPage | undefined;
    adapter.getPage('not-found-slug').subscribe((r: DocPage) => (result = r));
    expect(result!.contentMarkdown).toBe('');
  });

  it('should return a placeholder with title "No documentation available" on 404', () => {
    const { adapter } = setupAdapter();
    let result: DocPage | undefined;
    adapter.getPage('not-found-slug').subscribe((r: DocPage) => (result = r));
    expect(result!.title).toBe('No documentation available');
  });

  it('should return a placeholder with a valid Date on 404', () => {
    const { adapter } = setupAdapter();
    let result: DocPage | undefined;
    adapter.getPage('not-found-slug').subscribe((r: DocPage) => (result = r));
    expect(result!.lastUpdated).toBeInstanceOf(Date);
  });
});

// ---------------------------------------------------------------------------
// DocsHttpAdapter — openPluginDoc() via slug convention
// ---------------------------------------------------------------------------

describe('DocsHttpAdapter — plugin doc convention (slug "plugin:{slug}")', () => {
  it('should fetch the doc page for slug "plugin:awesome-plugin"', () => {
    const { adapter } = setupAdapter();
    let result: DocPage | undefined;
    adapter.getPage('plugin:awesome-plugin').subscribe((r: DocPage) => (result = r));
    expect(result).toBeDefined();
    expect(result!.contentMarkdown).toBe('README content for awesome-plugin.');
  });

  it('should call apiClient.getDocBySlug with the exact slug', () => {
    const { adapter, fakeApiClient } = setupAdapter();
    adapter.getPage('plugin:awesome-plugin').subscribe();
    expect(fakeApiClient.getDocBySlugCalls).toContain('plugin:awesome-plugin');
  });

  it('should return a placeholder when plugin doc is not found', () => {
    const { adapter } = setupAdapter();
    let result: DocPage | undefined;
    let threw = false;
    adapter.getPage('not-found-slug').subscribe({
      next: (r: DocPage) => (result = r),
      error: () => (threw = true),
    });
    expect(threw).toBe(false);
    expect(result!.contentMarkdown).toBe('');
  });
});
