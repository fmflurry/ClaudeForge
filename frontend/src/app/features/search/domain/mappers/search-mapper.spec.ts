/**
 * RED tests — Task 13.1 / 13.2: Domain models + mappers
 *
 * Expected production files (DO NOT exist yet — tests MUST FAIL to compile):
 *   src/app/features/search/domain/models/search.models.ts
 *   src/app/features/search/domain/mappers/search-mapper.ts
 *
 * Production types the coder MUST define:
 *
 *   // search.models.ts
 *   type SearchResult = {
 *     readonly pluginId: string;
 *     readonly name: string;
 *     readonly slug: string;
 *     readonly description: string;
 *     readonly author: string;
 *     readonly types: readonly string[];
 *     readonly languages: readonly string[];
 *     readonly useCases: readonly string[];
 *     readonly downloadCount: number;
 *     readonly latestVersion: string | null;
 *     readonly relevanceScore: number;
 *   }
 *
 *   type DiscoveryResult = {
 *     readonly pluginId: string;
 *     readonly name: string;
 *     readonly slug: string;
 *     readonly description: string;
 *     readonly author: string;
 *     readonly types: readonly string[];
 *     readonly languages: readonly string[];
 *     readonly matchedLanguages: readonly string[];
 *     readonly maturityIndicator: string;
 *     readonly relevanceScore: number;
 *     readonly lastUpdated?: string;
 *   }
 *
 *   type SearchResultsPage = {
 *     readonly items: readonly SearchResult[];
 *     readonly totalCount: number;
 *     readonly page: number;
 *     readonly limit: number;
 *     readonly totalPages: number;
 *     readonly categorySuggestions: readonly string[];
 *   }
 *
 *   type DiscoveryResults = {
 *     readonly items: readonly DiscoveryResult[];
 *     readonly criteriaEchoed: DiscoveryCriteria;
 *   }
 *
 *   type DiscoveryCriteria = {
 *     readonly keyword?: string;
 *     readonly languages?: readonly string[];
 *     readonly useCases?: readonly string[];
 *     readonly types?: readonly string[];
 *   }
 *
 *   // search-mapper.ts
 *   function mapSearchResultDtoToSearchResult(dto: SearchResultDto): SearchResult
 *   function mapSearchEnvelopeToSearchResultsPage(
 *     envelope: PaginatedEnvelope<SearchResultDto>,
 *     categorySuggestions: readonly string[]
 *   ): SearchResultsPage
 *   function mapDiscoveryResultDtoToDiscoveryResult(dto: DiscoveryResultDto): DiscoveryResult
 *   function mapDiscoveryEnvelopeToDiscoveryResults(
 *     envelope: PaginatedEnvelope<DiscoveryResultDto>,
 *     criteriaEchoed: DiscoveryCriteria
 *   ): DiscoveryResults
 */

import {
  mapSearchResultDtoToSearchResult,
  mapSearchEnvelopeToSearchResultsPage,
  mapDiscoveryResultDtoToDiscoveryResult,
  mapDiscoveryEnvelopeToDiscoveryResults,
} from './search-mapper';
import type {
  DiscoveryCriteria,
  DiscoveryResult,
  DiscoveryResults,
  SearchResult,
  SearchResultsPage,
} from '../models/search.models';
import type {
  DiscoveryResultDto,
  PaginatedEnvelope,
  SearchResultDto,
} from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// DTO Fixtures
// ---------------------------------------------------------------------------

const searchResultDto: SearchResultDto = {
  pluginId: 'search-plugin-1',
  name: 'Search Plugin One',
  slug: 'search-plugin-one',
  description: 'A plugin found by search.',
  author: 'Dev One',
  types: ['formatter'],
  languages: ['typescript', 'javascript'],
  downloadCount: 2500,
  latestVersion: '3.0.0',
  relevanceScore: 0.92,
};

const searchResultDtoNullVersion: SearchResultDto = {
  ...searchResultDto,
  pluginId: 'search-plugin-null',
  latestVersion: null,
};

const discoveryResultDto: DiscoveryResultDto = {
  pluginId: 'discovery-plugin-1',
  name: 'Discovered Plugin',
  slug: 'discovered-plugin',
  description: 'A plugin found by discovery.',
  author: 'Dev Two',
  types: ['linter'],
  languages: ['python'],
  matchedLanguages: ['python'],
  maturityIndicator: 'stable',
  relevanceScore: 0.88,
};

const searchEnvelope: PaginatedEnvelope<SearchResultDto> = {
  data: [searchResultDto],
  totalCount: 1,
  page: 1,
  limit: 20,
  totalPages: 1,
};

const discoveryEnvelope: PaginatedEnvelope<DiscoveryResultDto> = {
  data: [discoveryResultDto],
  totalCount: 1,
  page: 1,
  limit: 20,
  totalPages: 1,
};

const fakeCriteria: DiscoveryCriteria = {
  keyword: 'lint',
  languages: ['python'],
  useCases: [],
  types: ['linter'],
};

// ---------------------------------------------------------------------------
// mapSearchResultDtoToSearchResult
// ---------------------------------------------------------------------------

describe('mapSearchResultDtoToSearchResult', () => {
  let result: SearchResult;

  beforeEach(() => {
    result = mapSearchResultDtoToSearchResult(searchResultDto);
  });

  it('should map pluginId', () => {
    expect(result.pluginId).toBe('search-plugin-1');
  });

  it('should map name', () => {
    expect(result.name).toBe('Search Plugin One');
  });

  it('should map slug', () => {
    expect(result.slug).toBe('search-plugin-one');
  });

  it('should map description', () => {
    expect(result.description).toBe('A plugin found by search.');
  });

  it('should map author', () => {
    expect(result.author).toBe('Dev One');
  });

  it('should map types array', () => {
    expect(result.types).toEqual(['formatter']);
  });

  it('should map languages array', () => {
    expect(result.languages).toEqual(['typescript', 'javascript']);
  });

  it('should map downloadCount', () => {
    expect(result.downloadCount).toBe(2500);
  });

  it('should map latestVersion (non-null)', () => {
    expect(result.latestVersion).toBe('3.0.0');
  });

  it('should map latestVersion when null', () => {
    const r = mapSearchResultDtoToSearchResult(searchResultDtoNullVersion);
    expect(r.latestVersion).toBeNull();
  });

  it('should map relevanceScore', () => {
    expect(result.relevanceScore).toBe(0.92);
  });

  it('should return a new object each call (immutability)', () => {
    const r1 = mapSearchResultDtoToSearchResult(searchResultDto);
    const r2 = mapSearchResultDtoToSearchResult(searchResultDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the source DTO', () => {
    const copy: SearchResultDto = { ...searchResultDto };
    mapSearchResultDtoToSearchResult(searchResultDto);
    expect(searchResultDto).toEqual(copy);
  });

  it('should produce independent types array (no shared reference)', () => {
    const r = mapSearchResultDtoToSearchResult(searchResultDto);
    const originalLength = searchResultDto.types.length;
    (r.types as string[]).push('injected');
    expect(searchResultDto.types).toHaveLength(originalLength);
  });

  it('should produce independent languages array (no shared reference)', () => {
    const r = mapSearchResultDtoToSearchResult(searchResultDto);
    const originalLength = searchResultDto.languages.length;
    (r.languages as string[]).push('injected');
    expect(searchResultDto.languages).toHaveLength(originalLength);
  });
});

// ---------------------------------------------------------------------------
// mapSearchEnvelopeToSearchResultsPage
// ---------------------------------------------------------------------------

describe('mapSearchEnvelopeToSearchResultsPage', () => {
  let result: SearchResultsPage;
  const suggestions = ['formatter', 'linter'];

  beforeEach(() => {
    result = mapSearchEnvelopeToSearchResultsPage(searchEnvelope, suggestions);
  });

  it('should map items from envelope data', () => {
    expect(result.items).toHaveLength(1);
    expect(result.items[0].pluginId).toBe('search-plugin-1');
  });

  it('should map totalCount', () => {
    expect(result.totalCount).toBe(1);
  });

  it('should map page', () => {
    expect(result.page).toBe(1);
  });

  it('should map limit', () => {
    expect(result.limit).toBe(20);
  });

  it('should map totalPages', () => {
    expect(result.totalPages).toBe(1);
  });

  it('should include the provided categorySuggestions', () => {
    expect(result.categorySuggestions).toEqual(['formatter', 'linter']);
  });

  it('should include empty categorySuggestions when provided empty array', () => {
    const r = mapSearchEnvelopeToSearchResultsPage(searchEnvelope, []);
    expect(r.categorySuggestions).toEqual([]);
  });

  it('should map empty data array to empty items', () => {
    const emptyEnvelope: PaginatedEnvelope<SearchResultDto> = {
      data: [],
      totalCount: 0,
      page: 1,
      limit: 20,
      totalPages: 0,
    };
    const r = mapSearchEnvelopeToSearchResultsPage(emptyEnvelope, suggestions);
    expect(r.items).toEqual([]);
    expect(r.totalCount).toBe(0);
  });

  it('should handle multi-page envelopes', () => {
    const multiPage: PaginatedEnvelope<SearchResultDto> = {
      data: [],
      totalCount: 200,
      page: 4,
      limit: 20,
      totalPages: 10,
    };
    const r = mapSearchEnvelopeToSearchResultsPage(multiPage, []);
    expect(r.totalCount).toBe(200);
    expect(r.page).toBe(4);
    expect(r.totalPages).toBe(10);
  });

  it('should return a new object each call', () => {
    const r1 = mapSearchEnvelopeToSearchResultsPage(searchEnvelope, suggestions);
    const r2 = mapSearchEnvelopeToSearchResultsPage(searchEnvelope, suggestions);
    expect(r1).not.toBe(r2);
  });

  it('should produce independent items array (no shared reference with envelope.data)', () => {
    const mutableEnvelope = {
      data: [{ ...searchResultDto }],
      totalCount: 1,
      page: 1,
      limit: 20,
      totalPages: 1,
    };
    const r = mapSearchEnvelopeToSearchResultsPage(mutableEnvelope, []);
    const originalLength = mutableEnvelope.data.length;
    (r.items as SearchResult[]).push({ ...r.items[0] });
    expect(mutableEnvelope.data).toHaveLength(originalLength);
  });
});

// ---------------------------------------------------------------------------
// mapDiscoveryResultDtoToDiscoveryResult
// ---------------------------------------------------------------------------

describe('mapDiscoveryResultDtoToDiscoveryResult', () => {
  let result: DiscoveryResult;

  beforeEach(() => {
    result = mapDiscoveryResultDtoToDiscoveryResult(discoveryResultDto);
  });

  it('should map pluginId', () => {
    expect(result.pluginId).toBe('discovery-plugin-1');
  });

  it('should map name', () => {
    expect(result.name).toBe('Discovered Plugin');
  });

  it('should map slug', () => {
    expect(result.slug).toBe('discovered-plugin');
  });

  it('should map description', () => {
    expect(result.description).toBe('A plugin found by discovery.');
  });

  it('should map author', () => {
    expect(result.author).toBe('Dev Two');
  });

  it('should map types array', () => {
    expect(result.types).toEqual(['linter']);
  });

  it('should map languages array', () => {
    expect(result.languages).toEqual(['python']);
  });

  it('should map matchedLanguages array', () => {
    expect(result.matchedLanguages).toEqual(['python']);
  });

  it('should map maturityIndicator', () => {
    expect(result.maturityIndicator).toBe('stable');
  });

  it('should map relevanceScore', () => {
    expect(result.relevanceScore).toBe(0.88);
  });

  it('should return a new object each call', () => {
    const r1 = mapDiscoveryResultDtoToDiscoveryResult(discoveryResultDto);
    const r2 = mapDiscoveryResultDtoToDiscoveryResult(discoveryResultDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate source DTO', () => {
    const copy: DiscoveryResultDto = { ...discoveryResultDto };
    mapDiscoveryResultDtoToDiscoveryResult(discoveryResultDto);
    expect(discoveryResultDto).toEqual(copy);
  });

  it('should produce independent languages array', () => {
    const r = mapDiscoveryResultDtoToDiscoveryResult(discoveryResultDto);
    const originalLength = discoveryResultDto.languages.length;
    (r.languages as string[]).push('injected');
    expect(discoveryResultDto.languages).toHaveLength(originalLength);
  });

  it('should produce independent matchedLanguages array', () => {
    const r = mapDiscoveryResultDtoToDiscoveryResult(discoveryResultDto);
    const originalLength = discoveryResultDto.matchedLanguages.length;
    (r.matchedLanguages as string[]).push('injected');
    expect(discoveryResultDto.matchedLanguages).toHaveLength(originalLength);
  });
});

// ---------------------------------------------------------------------------
// mapDiscoveryEnvelopeToDiscoveryResults
// ---------------------------------------------------------------------------

describe('mapDiscoveryEnvelopeToDiscoveryResults', () => {
  let result: DiscoveryResults;

  beforeEach(() => {
    result = mapDiscoveryEnvelopeToDiscoveryResults(discoveryEnvelope, fakeCriteria);
  });

  it('should map items from envelope data', () => {
    expect(result.items).toHaveLength(1);
    expect(result.items[0].pluginId).toBe('discovery-plugin-1');
  });

  it('should echo the provided criteria as criteriaEchoed', () => {
    expect(result.criteriaEchoed).toEqual(fakeCriteria);
  });

  it('should echo keyword from criteria', () => {
    expect(result.criteriaEchoed.keyword).toBe('lint');
  });

  it('should echo languages from criteria', () => {
    expect(result.criteriaEchoed.languages).toEqual(['python']);
  });

  it('should map empty data array to empty items', () => {
    const emptyEnvelope: PaginatedEnvelope<DiscoveryResultDto> = {
      data: [],
      totalCount: 0,
      page: 1,
      limit: 20,
      totalPages: 0,
    };
    const r = mapDiscoveryEnvelopeToDiscoveryResults(emptyEnvelope, fakeCriteria);
    expect(r.items).toEqual([]);
  });

  it('should return a new object each call', () => {
    const r1 = mapDiscoveryEnvelopeToDiscoveryResults(discoveryEnvelope, fakeCriteria);
    const r2 = mapDiscoveryEnvelopeToDiscoveryResults(discoveryEnvelope, fakeCriteria);
    expect(r1).not.toBe(r2);
  });

  it('should produce independent items array', () => {
    const r = mapDiscoveryEnvelopeToDiscoveryResults(discoveryEnvelope, fakeCriteria);
    const originalLength = discoveryEnvelope.data.length;
    (r.items as DiscoveryResult[]).push({ ...r.items[0] });
    expect(discoveryEnvelope.data).toHaveLength(originalLength);
  });

  it('should handle criteria with no keyword (undefined)', () => {
    const noKeyword: DiscoveryCriteria = { languages: ['typescript'] };
    const r = mapDiscoveryEnvelopeToDiscoveryResults(discoveryEnvelope, noKeyword);
    expect(r.criteriaEchoed.keyword).toBeUndefined();
    expect(r.criteriaEchoed.languages).toEqual(['typescript']);
  });
});
