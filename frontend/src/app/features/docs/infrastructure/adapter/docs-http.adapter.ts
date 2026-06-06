/**
 * HTTP adapter implementing DocsPort.
 * Maps API DTOs to domain models via docs mappers.
 */

import { inject, Injectable } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, of, map, catchError } from 'rxjs';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import { mapDocPageDtoToDocPage, mapDocPageDtoToDocSearchResult } from '../../domain/mappers/docs-mapper';
import type { DocPage, DocSearchResult } from '../../domain/models/docs.models';
import { DocsPort, DocsSearchResponse } from '../../domain/ports/docs.port';

@Injectable()
export class DocsHttpAdapter extends DocsPort {
  private readonly apiClient = inject(ApiClient);

  search(query: string, page?: number, limit?: number): Observable<DocsSearchResponse> {
    return this.apiClient.searchDocs({ search: query, page, limit }).pipe(
      map((envelope) => ({
        items: envelope.data.map((dto): DocSearchResult => mapDocPageDtoToDocSearchResult(dto)),
        totalCount: envelope.totalCount,
        page: envelope.page,
        limit: envelope.limit,
        totalPages: envelope.totalPages,
      })),
    );
  }

  getPage(slug: string): Observable<DocPage> {
    return this.apiClient.getDocBySlug(slug).pipe(
      map((dto) => mapDocPageDtoToDocPage(dto)),
      catchError((err: unknown) => {
        if (err instanceof HttpErrorResponse && err.status === 404) {
          const placeholder: DocPage = {
            slug,
            title: 'No documentation available',
            category: '',
            contentMarkdown: '',
            lastUpdated: new Date(0),
          };
          return of(placeholder);
        }
        throw err;
      }),
    );
  }
}
