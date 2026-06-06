/**
 * Abstract port for catalog data access.
 * Infrastructure adapters implement this; use-cases depend on it.
 */

import { Observable } from 'rxjs';
import type { Categories, PaginationMeta, PluginDetail, PluginSummary } from '../models/catalog.models';
import type { CatalogFilterQuery } from '../rules/catalog-filter.rules';

export abstract class CatalogPort {
  abstract loadPlugins(
    query: CatalogFilterQuery,
  ): Observable<{ plugins: PluginSummary[]; meta: PaginationMeta }>;

  abstract getPlugin(pluginId: string): Observable<PluginDetail>;

  abstract getCategories(): Observable<Categories>;
}
