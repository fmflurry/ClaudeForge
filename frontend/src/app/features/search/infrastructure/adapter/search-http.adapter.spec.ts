/**
 * Unit tests for SearchHttpAdapter.
 * Uses a stub ApiClient to verify params mapping and response mapping.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { SearchHttpAdapter } from './search-http.adapter';
import { SearchPort } from '../../domain/ports/search.port';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { SearchFilterQuery } from '../../domain/rules/search-filter.rules';
import type { DiscoveryCriteria, DiscoveryResults, SearchResultsPage } from '../../domain/models/search.models';
import type {
  PaginatedEnvelope,
  SearchResultDto,
  DiscoveryResultDto,
} from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

const SEARCH_DTO: SearchResultDto = {
  pluginId: 's-1',
  name: 'SearchPlugin',
  slug: 'search-plugin',
  description: 'A search result.',
  author: 'Bob',
  types: ['linter'],
  languages: ['javascript'],
  downloadCount: 500,
  latestVersion: '2.0.0',
  relevanceScore: 0.95,
};

const SEARCH_ENVELOPE: PaginatedEnvelope<SearchResultDto> = {
  data: [SEARCH_DTO],
  totalCount: 1,
  page: 1,
  limit: 20,
  totalPages: 1,
};

const DISCOVERY_DTO: DiscoveryResultDto = {
  pluginId: 'd-1',
  name: 'DiscoverPlugin',
  slug: 'discover-plugin',
  description: 'A discovery result.',
  author: 'Carol',
  types: ['debugger'],
  languages: ['python'],
  matchedLanguages: ['python'],
  maturityIndicator: 'stable',
  relevanceScore: 0.88,
};

const DISCOVERY_ENVELOPE: PaginatedEnvelope<DiscoveryResultDto> = {
  data: [DISCOVERY_DTO],
  totalCount: 1,
  page: 1,
  limit: 20,
  totalPages: 1,
};

// ---------------------------------------------------------------------------
// Stub ApiClient
// ---------------------------------------------------------------------------

@Injectable()
class StubApiClient {
  searchPluginsCalls: Parameters<ApiClient['searchPlugins']>[] = [];
  discoverPluginsCalls: Parameters<ApiClient['discoverPlugins']>[] = [];

  searchPlugins(params: Parameters<ApiClient['searchPlugins']>[0]): Observable<PaginatedEnvelope<SearchResultDto>> {
    this.searchPluginsCalls.push([params ?? {}]);
    return of(SEARCH_ENVELOPE);
  }

  discoverPlugins(params: Parameters<ApiClient['discoverPlugins']>[0]): Observable<PaginatedEnvelope<DiscoveryResultDto>> {
    this.discoverPluginsCalls.push([params ?? {}]);
    return of(DISCOVERY_ENVELOPE);
  }
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(): { adapter: SearchHttpAdapter; stub: StubApiClient } {
  TestBed.resetTestingModule();
  const stub = new StubApiClient();
  TestBed.configureTestingModule({
    providers: [
      { provide: ApiClient, useValue: stub },
      { provide: SearchPort, useClass: SearchHttpAdapter },
      SearchHttpAdapter,
    ],
  });
  return { adapter: TestBed.inject(SearchHttpAdapter), stub };
}

// ---------------------------------------------------------------------------
// search
// ---------------------------------------------------------------------------

describe('SearchHttpAdapter — search', () => {
  it('should call apiClient.searchPlugins', () => {
    const { adapter, stub } = setup();
    const query: SearchFilterQuery = { keyword: 'typescript', page: 1, limit: 20 };
    adapter.search(query).subscribe();
    expect(stub.searchPluginsCalls).toHaveLength(1);
  });

  it('should map SearchResultDto to SearchResult with pluginId', () => {
    const { adapter } = setup();
    let page: SearchResultsPage | undefined;
    adapter.search({ keyword: 'test' }).subscribe((p) => (page = p));
    expect(page!.items[0].pluginId).toBe('s-1');
  });

  it('should map name', () => {
    const { adapter } = setup();
    let page: SearchResultsPage | undefined;
    adapter.search({}).subscribe((p) => (page = p));
    expect(page!.items[0].name).toBe('SearchPlugin');
  });

  it('should include totalCount in the page', () => {
    const { adapter } = setup();
    let page: SearchResultsPage | undefined;
    adapter.search({}).subscribe((p) => (page = p));
    expect(page!.totalCount).toBe(1);
  });

  it('should default categorySuggestions to an empty array', () => {
    const { adapter } = setup();
    let page: SearchResultsPage | undefined;
    adapter.search({}).subscribe((p) => (page = p));
    expect(page!.categorySuggestions).toEqual([]);
  });

  it('should pass keyword to searchPlugins params', () => {
    const { adapter, stub } = setup();
    adapter.search({ keyword: 'myKeyword' }).subscribe();
    const params = stub.searchPluginsCalls[0][0];
    expect(params?.q).toBe('myKeyword');
  });
});

// ---------------------------------------------------------------------------
// discover
// ---------------------------------------------------------------------------

describe('SearchHttpAdapter — discover', () => {
  it('should call apiClient.discoverPlugins', () => {
    const { adapter, stub } = setup();
    const criteria: DiscoveryCriteria = { languages: ['python'] };
    adapter.discover(criteria).subscribe();
    expect(stub.discoverPluginsCalls).toHaveLength(1);
  });

  it('should map DiscoveryResultDto to DiscoveryResult', () => {
    const { adapter } = setup();
    let results: DiscoveryResults | undefined;
    adapter.discover({}).subscribe((r) => (results = r));
    expect(results!.items[0].pluginId).toBe('d-1');
    expect(results!.items[0].maturityIndicator).toBe('stable');
  });

  it('should echo the criteria in the result', () => {
    const { adapter } = setup();
    const criteria: DiscoveryCriteria = { languages: ['go'], keyword: 'linter' };
    let results: DiscoveryResults | undefined;
    adapter.discover(criteria).subscribe((r) => (results = r));
    expect(results!.criteriaEchoed.languages).toEqual(['go']);
    expect(results!.criteriaEchoed.keyword).toBe('linter');
  });

  it('should map matchedLanguages', () => {
    const { adapter } = setup();
    let results: DiscoveryResults | undefined;
    adapter.discover({}).subscribe((r) => (results = r));
    expect(results!.items[0].matchedLanguages).toEqual(['python']);
  });

  it('should pass language criteria to discoverPlugins params', () => {
    const { adapter, stub } = setup();
    adapter.discover({ languages: ['rust', 'go'] }).subscribe();
    const params = stub.discoverPluginsCalls[0][0];
    expect(params?.language).toEqual(['rust', 'go']);
  });
});

// ---------------------------------------------------------------------------
// Architecture — extends SearchPort
// ---------------------------------------------------------------------------

describe('SearchHttpAdapter — architecture', () => {
  it('should be an instance of SearchPort', () => {
    const { adapter } = setup();
    expect(adapter).toBeInstanceOf(SearchPort);
  });
});
