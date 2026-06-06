/**
 * Abstract port for search and discovery data access.
 * Infrastructure adapters implement this; the facade depends on it.
 */

import { Observable } from 'rxjs';
import type { DiscoveryCriteria, DiscoveryResults, SearchResultsPage } from '../models/search.models';
import type { SearchFilterQuery } from '../rules/search-filter.rules';

export abstract class SearchPort {
  abstract search(query: SearchFilterQuery): Observable<SearchResultsPage>;
  abstract discover(criteria: DiscoveryCriteria): Observable<DiscoveryResults>;
}
