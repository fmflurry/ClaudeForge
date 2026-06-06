/**
 * Use case: Load the available plugin categories.
 */

import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import type { Categories } from '../../domain/models/catalog.models';
import { CatalogPort } from '../../domain/ports/catalog.port';

@Injectable()
export class LoadCategoriesUseCase {
  private readonly port = inject(CatalogPort);

  execute(): Observable<Categories> {
    return this.port.getCategories();
  }
}
