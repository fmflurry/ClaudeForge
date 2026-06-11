/**
 * HTTP adapter implementing CatalogPort.
 * Maps API DTOs to domain models via the catalog mappers.
 */

import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import {
  mapAddOnDtoToAddOnDetail,
  mapAddOnDtoToAddOnSummary,
  mapCategoriesDtoToCategories,
  mapPaginatedEnvelopeToMeta,
} from '../../domain/mappers/catalog-mapper';
import type { AddOnDetail, AddOnSummary, Categories, PaginationMeta } from '../../domain/models/catalog.models';
import { CatalogPort } from '../../domain/ports/catalog.port';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';
import { toListAddOnsParams } from '../../domain/rules/catalog-filter.rules';

@Injectable()
export class CatalogHttpAdapter extends CatalogPort {
  private readonly apiClient = inject(ApiClient);

  loadAddOns(query: CatalogFilterQuery): Observable<{ addOns: AddOnSummary[]; meta: PaginationMeta }> {
    const params = toListAddOnsParams(query);
    return this.apiClient.listAddOns(params).pipe(
      map((envelope) => ({
        addOns: envelope.data.map(mapAddOnDtoToAddOnSummary),
        meta: mapPaginatedEnvelopeToMeta(envelope),
      })),
    );
  }

  getAddOn(pluginId: string): Observable<AddOnDetail> {
    return this.apiClient.getAddOnById(pluginId).pipe(map(mapAddOnDtoToAddOnDetail));
  }

  getCategories(): Observable<Categories> {
    return this.apiClient.listCategories().pipe(map(mapCategoriesDtoToCategories));
  }
}
