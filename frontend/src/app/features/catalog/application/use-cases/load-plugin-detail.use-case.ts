/**
 * Use case: Load the full detail of a single plugin.
 */

import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import type { PluginDetail } from '../../domain/models/catalog.models';
import { CatalogPort } from '../../domain/ports/catalog.port';

@Injectable()
export class LoadPluginDetailUseCase {
  private readonly port = inject(CatalogPort);

  execute(pluginId: string): Observable<PluginDetail> {
    return this.port.getPlugin(pluginId);
  }
}
