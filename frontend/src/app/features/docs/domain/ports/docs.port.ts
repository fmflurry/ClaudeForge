/**
 * Abstract port for docs data access.
 * Infrastructure adapters implement this; the facade depends on it.
 */

import { Observable } from 'rxjs';
import type { DocPage, DocSearchResult } from '../models/docs.models';

export interface DocsSearchResponse {
  items: DocSearchResult[];
  totalCount: number;
  page: number;
  limit: number;
  totalPages: number;
}

export abstract class DocsPort {
  abstract search(query: string, page?: number, limit?: number): Observable<DocsSearchResponse>;

  abstract getPage(slug: string): Observable<DocPage>;
}
