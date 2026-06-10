import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { FeaturedPluginDto } from '../../../../shared/infrastructure/http/api-client.types';
import { FeaturedPluginPort } from '../../domain/ports/featured-plugin.port';
import type { FeaturedPlugin } from '../../domain/models/featured-plugin.model';

/** Maps the DTO from the backend to the immutable domain model. */
function mapDto(dto: FeaturedPluginDto): FeaturedPlugin {
  return {
    pluginId: dto.pluginId,
    name: dto.name,
    slug: dto.slug,
    latestVersion: dto.latestVersion,
  };
}

@Injectable()
export class FeaturedPluginHttpAdapter extends FeaturedPluginPort {
  private readonly apiClient = inject(ApiClient);

  /**
   * Fetches the featured plugin from the backend.
   * Maps 404 and any other HTTP/network errors to null so the UI degrades
   * gracefully — no broken state when no plugin is featured.
   */
  override getFeaturedPlugin(): Observable<FeaturedPlugin | null> {
    return this.apiClient.getFeaturedPlugin().pipe(
      map((envelope) => mapDto(envelope.data)),
      catchError(() => of(null)),
    );
  }
}
