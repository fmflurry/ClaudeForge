/**
 * HTTP adapter implementing CatalogPort.
 * Maps API DTOs to domain models via the catalog mappers.
 */

import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import {
  mapCategoriesDtoToCategories,
  mapPaginatedEnvelopeToMeta,
  mapPluginDtoToPluginDetail,
  mapPluginDtoToPluginSummary,
} from '../../domain/mappers/catalog-mapper';
import type { Categories, PaginationMeta, PluginDetail, PluginSummary } from '../../domain/models/catalog.models';
import { CatalogPort } from '../../domain/ports/catalog.port';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';
import { toListPluginsParams } from '../../domain/rules/catalog-filter.rules';

@Injectable()
export class CatalogHttpAdapter extends CatalogPort {
  private readonly apiClient = inject(ApiClient);

  loadPlugins(query: CatalogFilterQuery): Observable<{ plugins: PluginSummary[]; meta: PaginationMeta }> {
    const params = toListPluginsParams(query);
    return this.apiClient.listPlugins(params).pipe(
      map((envelope) => ({
        plugins: envelope.data.map(mapPluginDtoToPluginSummary),
        meta: mapPaginatedEnvelopeToMeta(envelope),
      })),
    );
  }

  getPlugin(pluginId: string): Observable<PluginDetail> {
    return this.apiClient.getPluginById(pluginId).pipe(map(mapPluginDtoToPluginDetail));
  }

  getCategories(): Observable<Categories> {
    return this.apiClient.listCategories().pipe(map(mapCategoriesDtoToCategories));
  }
}
