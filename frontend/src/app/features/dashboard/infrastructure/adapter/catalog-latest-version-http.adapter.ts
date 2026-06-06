/**
 * HTTP adapter for CatalogLatestVersionPort.
 * Looks up a plugin by name via the ApiClient and returns its latestVersion.
 * Returns null when the plugin is not found or the request fails.
 */

import { inject, Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import { CatalogLatestVersionPort } from '../../domain/ports/catalog-latest-version.port';

@Injectable()
export class CatalogLatestVersionHttpAdapter extends CatalogLatestVersionPort {
  private readonly apiClient = inject(ApiClient);

  getLatestVersion(pluginName: string): Observable<string | null> {
    return this.apiClient
      .searchPlugins({ q: pluginName, limit: 1 })
      .pipe(
        map((result) => {
          const match = result.data.find(
            (p) => p.name.toLowerCase() === pluginName.toLowerCase(),
          );
          return match?.latestVersion ?? null;
        }),
        catchError(() => of(null)),
      );
  }
}
