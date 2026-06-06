/**
 * Rules for building a category tree from doc search results.
 */

import type { DocCategoryNode, DocSearchResult } from '../models/docs.models';

/**
 * Groups docs by category, returning a ReadonlyMap.
 */
export function groupDocsByCategory(
  docs: readonly DocSearchResult[],
): ReadonlyMap<string, readonly DocSearchResult[]> {
  const map = new Map<string, DocSearchResult[]>();

  for (const doc of docs) {
    const existing = map.get(doc.category) ?? [];
    map.set(doc.category, [...existing, doc]);
  }

  return map as ReadonlyMap<string, readonly DocSearchResult[]>;
}

/**
 * Builds a category tree (array of DocCategoryNode) from docs.
 * Each unique category produces exactly one node.
 */
export function buildCategoryTree(
  docs: readonly DocSearchResult[],
): readonly DocCategoryNode[] {
  const grouped = groupDocsByCategory(docs);

  return Array.from(grouped.entries()).map(([category, categoryDocs]) => ({
    category,
    docs: categoryDocs,
  }));
}
