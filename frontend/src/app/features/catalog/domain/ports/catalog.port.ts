/**
 * Abstract port for catalog data access.
 * Infrastructure adapters implement this; use-cases depend on it.
 */

import { Observable } from 'rxjs';
import type { AddOnDetail, AddOnSummary, Categories, PaginationMeta } from '../models/catalog.models';
import type { CatalogFilterQuery } from '../rules/catalog-filter.rules';

export abstract class CatalogPort {
  abstract loadAddOns(query: CatalogFilterQuery): Observable<{ addOns: AddOnSummary[]; meta: PaginationMeta }>;

  abstract getAddOn(pluginId: string): Observable<AddOnDetail>;

  abstract getCategories(): Observable<Categories>;
}
