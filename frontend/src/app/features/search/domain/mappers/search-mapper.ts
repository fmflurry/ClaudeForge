/**
 * Pure mapping functions from API DTOs to Search domain models.
 * All functions are immutable — they return new objects and never mutate inputs.
 */

import type {
  DiscoveryResultDto,
  PaginatedEnvelope,
  SearchResultDto,
} from '../../../../shared/infrastructure/http/api-client.types';
import type {
  DiscoveryCriteria,
  DiscoveryResult,
  DiscoveryResults,
  SearchResult,
  SearchResultsPage,
} from '../models/search.models';

export function mapSearchResultDtoToSearchResult(dto: SearchResultDto): SearchResult {
  return {
    pluginId: dto.pluginId,
    name: dto.name,
    slug: dto.slug,
    description: dto.description,
    author: dto.author,
    types: [...dto.types],
    languages: [...dto.languages],
    useCases: [],
    downloadCount: dto.downloadCount,
    latestVersion: dto.latestVersion,
    relevanceScore: dto.relevanceScore,
  };
}

export function mapSearchEnvelopeToSearchResultsPage(
  envelope: PaginatedEnvelope<SearchResultDto>,
  categorySuggestions: readonly string[],
): SearchResultsPage {
  return {
    items: envelope.data.map(mapSearchResultDtoToSearchResult),
    totalCount: envelope.totalCount,
    page: envelope.page,
    limit: envelope.limit,
    totalPages: envelope.totalPages,
    categorySuggestions: [...categorySuggestions],
  };
}

export function mapDiscoveryResultDtoToDiscoveryResult(dto: DiscoveryResultDto): DiscoveryResult {
  return {
    pluginId: dto.pluginId,
    name: dto.name,
    slug: dto.slug,
    description: dto.description,
    author: dto.author,
    types: [...dto.types],
    languages: [...dto.languages],
    matchedLanguages: [...dto.matchedLanguages],
    maturityIndicator: dto.maturityIndicator,
    relevanceScore: dto.relevanceScore,
  };
}

export function mapDiscoveryEnvelopeToDiscoveryResults(
  envelope: PaginatedEnvelope<DiscoveryResultDto>,
  criteriaEchoed: DiscoveryCriteria,
): DiscoveryResults {
  return {
    items: envelope.data.map(mapDiscoveryResultDtoToDiscoveryResult),
    criteriaEchoed: { ...criteriaEchoed },
  };
}
