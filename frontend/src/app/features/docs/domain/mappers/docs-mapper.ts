/**
 * Mappers between DocPageDto and domain models.
 * The API returns DocPageDto for both search results (/api/v1/docs?search=)
 * and full page fetches (/api/v1/docs/{slug}).
 */

import type { DocPageDto } from '../../../../shared/infrastructure/http/api-client.types';
import type { DocPage, DocSearchResult } from '../models/docs.models';
import { buildSnippetFromContent } from '../rules/docs-highlight.rules';

const SNIPPET_MAX_LENGTH = 150;

/**
 * Maps a DocPageDto (from search endpoint) to a DocSearchResult.
 * Derives a snippet from content (max 150 chars) and defaults relevanceScore to 0.
 */
export function mapDocPageDtoToDocSearchResult(dto: DocPageDto): DocSearchResult {
  return {
    slug: dto.slug,
    title: dto.title,
    category: '',
    snippet: buildSnippetFromContent(dto.content, SNIPPET_MAX_LENGTH),
    relevanceScore: 0,
  };
}

/**
 * Maps a DocPageDto (from full page endpoint) to a DocPage.
 * Maps content → contentMarkdown and parses lastUpdated string to Date.
 */
export function mapDocPageDtoToDocPage(dto: DocPageDto): DocPage {
  return {
    slug: dto.slug,
    title: dto.title,
    category: '',
    contentMarkdown: dto.content,
    lastUpdated: new Date(dto.lastUpdated),
  };
}
