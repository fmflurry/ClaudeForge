/**
 * Rules for highlighting search snippets within doc content.
 */

import type { DocHighlight } from '../models/docs.models';

const DEFAULT_MAX_LENGTH = 150;
const DEFAULT_WINDOW_SIZE = 50;

/**
 * Builds a snippet from content, truncated to maxLength characters.
 */
export function buildSnippetFromContent(content: string, maxLength: number = DEFAULT_MAX_LENGTH): string {
  if (content.length <= maxLength) {
    return content;
  }
  return content.slice(0, maxLength);
}

/**
 * Highlights a query term within content (case-insensitive).
 * Returns a DocHighlight with text before, the matched term, and text after.
 */
export function highlightSnippet(
  content: string,
  query: string,
  windowSize: number = DEFAULT_WINDOW_SIZE,
): DocHighlight {
  if (!content || !query) {
    return { before: content, match: '', after: '' };
  }

  const lowerContent = content.toLowerCase();
  const lowerQuery = query.toLowerCase();
  const idx = lowerContent.indexOf(lowerQuery);

  if (idx === -1) {
    return { before: content, match: '', after: '' };
  }

  const matchEnd = idx + query.length;
  const beforeStart = Math.max(0, idx - windowSize);
  const afterEnd = Math.min(content.length, matchEnd + windowSize);

  const before = content.slice(beforeStart, idx);
  const match = content.slice(idx, matchEnd);
  const after = content.slice(matchEnd, afterEnd);

  return { before, match, after };
}
