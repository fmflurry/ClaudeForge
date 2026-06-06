/**
 * Use case: Load a paginated, filtered list of plugins.
 */

import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import type { PaginationMeta, PluginSummary } from '../../domain/models/catalog.models';
import { CatalogPort } from '../../domain/ports/catalog.port';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';

@Injectable()
export class LoadPluginsUseCase {
  private readonly port = inject(CatalogPort);

  execute(query: CatalogFilterQuery): Observable<{ plugins: PluginSummary[]; meta: PaginationMeta }> {
    return this.port.loadPlugins(query);
  }
}
