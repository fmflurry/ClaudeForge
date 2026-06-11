import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { FeaturedAddOnDto } from '../../../../shared/infrastructure/http/api-client.types';
import { FeaturedAddOnPort } from '../../domain/ports/featured-plugin.port';
import type { FeaturedAddOn } from '../../domain/models/featured-plugin.model';

/** Maps the DTO from the backend to the immutable domain model. */
function mapDto(dto: FeaturedAddOnDto): FeaturedAddOn {
  return {
    pluginId: dto.pluginId,
    name: dto.name,
    slug: dto.slug,
    latestVersion: dto.latestVersion,
  };
}

@Injectable()
export class FeaturedAddOnHttpAdapter extends FeaturedAddOnPort {
  private readonly apiClient = inject(ApiClient);

  /**
   * Fetches the featured add-on from the backend.
   * Maps 404 and any other HTTP/network errors to null so the UI degrades
   * gracefully — no broken state when no add-on is featured.
   */
  override getFeaturedAddOn(): Observable<FeaturedAddOn | null> {
    return this.apiClient.getFeaturedAddOn().pipe(
      map((envelope) => mapDto(envelope.data)),
      catchError(() => of(null)),
    );
  }
}
