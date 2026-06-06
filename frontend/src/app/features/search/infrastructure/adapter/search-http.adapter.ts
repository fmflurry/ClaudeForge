/**
 * HTTP adapter implementing SearchPort.
 * Maps API DTOs to domain models via the search mappers.
 */

import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import {
  mapDiscoveryEnvelopeToDiscoveryResults,
  mapSearchEnvelopeToSearchResultsPage,
} from '../../domain/mappers/search-mapper';
import type { DiscoveryCriteria, DiscoveryResults, SearchResultsPage } from '../../domain/models/search.models';
import { SearchPort } from '../../domain/ports/search.port';
import {
  buildDiscoveryQueryParams,
  buildSearchQueryParams,
} from '../../domain/rules/search-filter.rules';
import type { SearchFilterQuery } from '../../domain/rules/search-filter.rules';

@Injectable()
export class SearchHttpAdapter extends SearchPort {
  private readonly apiClient = inject(ApiClient);

  search(query: SearchFilterQuery): Observable<SearchResultsPage> {
    const params = buildSearchQueryParams(query);
    return this.apiClient.searchPlugins(params).pipe(
      map((envelope) => {
        // The API envelope does not yet carry categorySuggestions at the DTO level.
        // Suggestions are an empty-result hint — surface an empty array by default.
        const categorySuggestions: readonly string[] = [];
        return mapSearchEnvelopeToSearchResultsPage(envelope, categorySuggestions);
      }),
    );
  }

  discover(criteria: DiscoveryCriteria): Observable<DiscoveryResults> {
    const params = buildDiscoveryQueryParams(criteria);
    return this.apiClient.discoverPlugins(params).pipe(
      map((envelope) => mapDiscoveryEnvelopeToDiscoveryResults(envelope, criteria)),
    );
  }
}
