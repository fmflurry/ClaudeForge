/**
 * Use case: Load the full detail of a single add-on.
 */

import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import type { AddOnDetail } from '../../domain/models/catalog.models';
import { CatalogPort } from '../../domain/ports/catalog.port';

@Injectable()
export class LoadAddOnDetailUseCase {
  private readonly port = inject(CatalogPort);

  execute(pluginId: string): Observable<AddOnDetail> {
    return this.port.getAddOn(pluginId);
  }
}
