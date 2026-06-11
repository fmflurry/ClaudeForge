/**
 * Use case: Load a paginated, filtered list of add-ons.
 */

import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import type { AddOnSummary, PaginationMeta } from '../../domain/models/catalog.models';
import { CatalogPort } from '../../domain/ports/catalog.port';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';

@Injectable()
export class LoadAddOnsUseCase {
  private readonly port = inject(CatalogPort);

  execute(query: CatalogFilterQuery): Observable<{ addOns: AddOnSummary[]; meta: PaginationMeta }> {
    return this.port.loadAddOns(query);
  }
}
