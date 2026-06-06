/**
 * RED tests — Task 17.1 / 17.2: Docs domain models, mappers, and rules
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/docs/domain/models/docs.models.ts
 *   src/app/features/docs/domain/mappers/docs-mapper.ts
 *   src/app/features/docs/domain/rules/docs-category-tree.rules.ts
 *   src/app/features/docs/domain/rules/docs-highlight.rules.ts
 *
 * Production types the coder MUST define (all `type`, not `interface`, per project rules):
 *
 *   // docs.models.ts
 *   type DocSearchResult = {
 *     readonly slug: string;
 *     readonly title: string;
 *     readonly category: string;
 *     readonly snippet: string;
 *     readonly relevanceScore: number;
 *   }
 *
 *   type DocPage = {
 *     readonly slug: string;
 *     readonly title: string;
 *     readonly category: string;
 *     readonly contentMarkdown: string;
 *     readonly lastUpdated: Date;
 *   }
 *
 *   type DocCategoryNode = {
 *     readonly category: string;
 *     readonly docs: readonly DocSearchResult[];
 *   }
 *
 *   type DocHighlight = {
 *     readonly before: string;
 *     readonly match: string;
 *     readonly after: string;
 *   }
 *
 *   // docs-mapper.ts
 *   // NOTE: The API returns DocPageDto for BOTH /api/v1/docs?search= (search results)
 *   // AND /api/v1/docs/{slug} (full page). The search endpoint returns paginated
 *   // DocPageDto[] where snippet = first ~150 chars of content and relevanceScore
 *   // is not present in the current DTO. Per the task spec, the mapper must derive
 *   // a snippet from content when mapping search results and default relevanceScore=0.
 *   // The full DocPageDto from /api/v1/docs/{slug} maps to DocPage.
 *   function mapDocPageDtoToDocSearchResult(dto: DocPageDto): DocSearchResult
 *   function mapDocPageDtoToDocPage(dto: DocPageDto): DocPage
 *
 *   // docs-category-tree.rules.ts
 *   function buildCategoryTree(docs: readonly DocSearchResult[]): readonly DocCategoryNode[]
 *   function groupDocsByCategory(docs: readonly DocSearchResult[]): ReadonlyMap<string, readonly DocSearchResult[]>
 *
 *   // docs-highlight.rules.ts
 *   function highlightSnippet(content: string, query: string, windowSize?: number): DocHighlight
 *   function buildSnippetFromContent(content: string, maxLength?: number): string
 */

import {
  mapDocPageDtoToDocSearchResult,
  mapDocPageDtoToDocPage,
} from './docs-mapper';
import {
  buildCategoryTree,
  groupDocsByCategory,
} from '../rules/docs-category-tree.rules';
import {
  highlightSnippet,
  buildSnippetFromContent,
} from '../rules/docs-highlight.rules';
import type {
  DocSearchResult,
  DocPage,
  DocCategoryNode,
  DocHighlight,
} from '../models/docs.models';
import type { DocPageDto } from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// DTO fixtures
// ---------------------------------------------------------------------------

const gettingStartedDto: DocPageDto = {
  slug: 'getting-started',
  title: 'Getting Started',
  content: 'Welcome to ClaudeForge. This guide explains how to install plugins step by step.',
  lastUpdated: '2024-03-01T10:00:00.000Z',
};

const installGuideDto: DocPageDto = {
  slug: 'installation-guide',
  title: 'Installation Guide',
  content: 'To install a plugin, run the install command with the plugin name.',
  lastUpdated: '2024-03-02T10:00:00.000Z',
};

const apiReferenceDto: DocPageDto = {
  slug: 'api-reference',
  title: 'API Reference',
  content: 'The plugin API exposes hooks for lifecycle management.',
  lastUpdated: '2024-04-01T10:00:00.000Z',
};

// ---------------------------------------------------------------------------
// mapDocPageDtoToDocSearchResult
// ---------------------------------------------------------------------------

describe('mapDocPageDtoToDocSearchResult', () => {
  let result: DocSearchResult;

  beforeEach(() => {
    result = mapDocPageDtoToDocSearchResult(gettingStartedDto);
  });

  it('should map slug', () => {
    expect(result.slug).toBe('getting-started');
  });

  it('should map title', () => {
    expect(result.title).toBe('Getting Started');
  });

  it('should derive snippet from content', () => {
    expect(typeof result.snippet).toBe('string');
    expect(result.snippet.length).toBeGreaterThan(0);
  });

  it('should produce a snippet no longer than 200 characters', () => {
    const longDto: DocPageDto = {
      ...gettingStartedDto,
      content: 'A'.repeat(500),
    };
    const r = mapDocPageDtoToDocSearchResult(longDto);
    expect(r.snippet.length).toBeLessThanOrEqual(200);
  });

  it('should default relevanceScore to 0 when not present in DTO', () => {
    expect(result.relevanceScore).toBe(0);
  });

  it('should populate category field (defaulting to empty string when DTO lacks it)', () => {
    expect(typeof result.category).toBe('string');
  });

  it('should return a new object each call (immutability)', () => {
    const r1 = mapDocPageDtoToDocSearchResult(gettingStartedDto);
    const r2 = mapDocPageDtoToDocSearchResult(gettingStartedDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the source DTO', () => {
    const copy = { ...gettingStartedDto };
    mapDocPageDtoToDocSearchResult(gettingStartedDto);
    expect(gettingStartedDto).toEqual(copy);
  });

  it('should handle empty content gracefully', () => {
    const emptyDto: DocPageDto = { ...gettingStartedDto, content: '' };
    const r = mapDocPageDtoToDocSearchResult(emptyDto);
    expect(typeof r.snippet).toBe('string');
  });
});

// ---------------------------------------------------------------------------
// mapDocPageDtoToDocPage
// ---------------------------------------------------------------------------

describe('mapDocPageDtoToDocPage', () => {
  let result: DocPage;

  beforeEach(() => {
    result = mapDocPageDtoToDocPage(gettingStartedDto);
  });

  it('should map slug', () => {
    expect(result.slug).toBe('getting-started');
  });

  it('should map title', () => {
    expect(result.title).toBe('Getting Started');
  });

  it('should map contentMarkdown from content field', () => {
    expect(result.contentMarkdown).toBe(gettingStartedDto.content);
  });

  it('should convert lastUpdated string to a Date object', () => {
    expect(result.lastUpdated).toBeInstanceOf(Date);
    expect(result.lastUpdated.toISOString()).toBe('2024-03-01T10:00:00.000Z');
  });

  it('should populate category field', () => {
    expect(typeof result.category).toBe('string');
  });

  it('should return a new object each call', () => {
    const r1 = mapDocPageDtoToDocPage(gettingStartedDto);
    const r2 = mapDocPageDtoToDocPage(gettingStartedDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the source DTO', () => {
    const copy = { ...gettingStartedDto };
    mapDocPageDtoToDocPage(gettingStartedDto);
    expect(gettingStartedDto).toEqual(copy);
  });

  it('should handle a DTO with a Unix-epoch lastUpdated string gracefully', () => {
    const epochDto: DocPageDto = { ...gettingStartedDto, lastUpdated: '2023-01-01T00:00:00.000Z' };
    const r = mapDocPageDtoToDocPage(epochDto);
    expect(r.lastUpdated).toBeInstanceOf(Date);
    expect(r.lastUpdated.getFullYear()).toBe(2023);
  });
});

// ---------------------------------------------------------------------------
// buildCategoryTree
// ---------------------------------------------------------------------------

const SEARCH_RESULTS: DocSearchResult[] = [
  {
    slug: 'getting-started',
    title: 'Getting Started',
    category: 'Getting Started',
    snippet: 'Welcome to ClaudeForge...',
    relevanceScore: 0.9,
  },
  {
    slug: 'install-cli',
    title: 'Install via CLI',
    category: 'Getting Started',
    snippet: 'Run the install command...',
    relevanceScore: 0.8,
  },
  {
    slug: 'api-reference',
    title: 'API Reference',
    category: 'API Reference',
    snippet: 'The plugin API...',
    relevanceScore: 0.5,
  },
  {
    slug: 'troubleshooting-faq',
    title: 'FAQ',
    category: 'Troubleshooting',
    snippet: 'Common issues...',
    relevanceScore: 0.3,
  },
];

describe('buildCategoryTree', () => {
  it('should return an array of DocCategoryNode', () => {
    const tree: readonly DocCategoryNode[] = buildCategoryTree(SEARCH_RESULTS);
    expect(Array.isArray(tree)).toBe(true);
  });

  it('should group results by category', () => {
    const tree: readonly DocCategoryNode[] = buildCategoryTree(SEARCH_RESULTS);
    const categories: string[] = tree.map((node: DocCategoryNode) => node.category);
    expect(categories).toContain('Getting Started');
    expect(categories).toContain('API Reference');
    expect(categories).toContain('Troubleshooting');
  });

  it('should produce exactly one node per unique category', () => {
    const tree: readonly DocCategoryNode[] = buildCategoryTree(SEARCH_RESULTS);
    const uniqueCategories = new Set(SEARCH_RESULTS.map((r: DocSearchResult) => r.category));
    expect(tree.length).toBe(uniqueCategories.size);
  });

  it('should include all docs within each category node', () => {
    const tree: readonly DocCategoryNode[] = buildCategoryTree(SEARCH_RESULTS);
    const gettingStartedNode: DocCategoryNode | undefined = tree.find((n: DocCategoryNode) => n.category === 'Getting Started');
    expect(gettingStartedNode).toBeDefined();
    expect(gettingStartedNode!.docs.length).toBe(2);
  });

  it('should preserve all slugs within the grouped node', () => {
    const tree: readonly DocCategoryNode[] = buildCategoryTree(SEARCH_RESULTS);
    const gettingStartedNode: DocCategoryNode = tree.find((n: DocCategoryNode) => n.category === 'Getting Started')!;
    const slugs: string[] = gettingStartedNode.docs.map((d: DocSearchResult) => d.slug);
    expect(slugs).toContain('getting-started');
    expect(slugs).toContain('install-cli');
  });

  it('should return an empty array for empty input', () => {
    const tree = buildCategoryTree([]);
    expect(tree).toEqual([]);
  });

  it('should return a new array each call (immutability)', () => {
    const r1 = buildCategoryTree(SEARCH_RESULTS);
    const r2 = buildCategoryTree(SEARCH_RESULTS);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the input array', () => {
    const copy = [...SEARCH_RESULTS];
    buildCategoryTree(SEARCH_RESULTS);
    expect(SEARCH_RESULTS).toEqual(copy);
  });

  it('should handle a single result with one category', () => {
    const single: DocSearchResult[] = [SEARCH_RESULTS[0]];
    const tree = buildCategoryTree(single);
    expect(tree.length).toBe(1);
    expect(tree[0].docs.length).toBe(1);
  });

  it('should handle results where all docs share the same category', () => {
    const sameCat: DocSearchResult[] = [
      { ...SEARCH_RESULTS[0], category: 'General' },
      { ...SEARCH_RESULTS[1], category: 'General' },
    ];
    const tree = buildCategoryTree(sameCat);
    expect(tree.length).toBe(1);
    expect(tree[0].docs.length).toBe(2);
  });
});

// ---------------------------------------------------------------------------
// groupDocsByCategory
// ---------------------------------------------------------------------------

describe('groupDocsByCategory', () => {
  it('should return a ReadonlyMap', () => {
    const map = groupDocsByCategory(SEARCH_RESULTS);
    expect(map instanceof Map).toBe(true);
  });

  it('should have one entry per unique category', () => {
    const map = groupDocsByCategory(SEARCH_RESULTS);
    expect(map.size).toBe(3);
  });

  it('should contain the correct docs for a given category key', () => {
    const map = groupDocsByCategory(SEARCH_RESULTS);
    const gettingStartedDocs = map.get('Getting Started');
    expect(gettingStartedDocs).toBeDefined();
    expect(gettingStartedDocs!.length).toBe(2);
  });

  it('should return an empty Map for empty input', () => {
    const map = groupDocsByCategory([]);
    expect(map.size).toBe(0);
  });

  it('should not mutate the input array', () => {
    const copy = [...SEARCH_RESULTS];
    groupDocsByCategory(SEARCH_RESULTS);
    expect(SEARCH_RESULTS).toEqual(copy);
  });
});

// ---------------------------------------------------------------------------
// highlightSnippet
// ---------------------------------------------------------------------------

describe('highlightSnippet', () => {
  it('should return a DocHighlight object with before/match/after', () => {
    const result: DocHighlight = highlightSnippet(
      'Welcome to ClaudeForge. Install plugins easily.',
      'Install',
    );
    expect(typeof result.before).toBe('string');
    expect(typeof result.match).toBe('string');
    expect(typeof result.after).toBe('string');
  });

  it('should capture the matching term in the match field (case-insensitive)', () => {
    const result = highlightSnippet(
      'Welcome to ClaudeForge. Install plugins easily.',
      'install',
    );
    expect(result.match.toLowerCase()).toBe('install');
  });

  it('should include text before the match in the before field', () => {
    const result = highlightSnippet(
      'Welcome to ClaudeForge. Install plugins easily.',
      'install',
    );
    expect(result.before.toLowerCase()).toContain('welcome');
  });

  it('should include text after the match in the after field', () => {
    const result = highlightSnippet(
      'Welcome to ClaudeForge. Install plugins easily.',
      'install',
    );
    expect(result.after.toLowerCase()).toContain('plugins');
  });

  it('should return empty match when query is not found in content', () => {
    const result = highlightSnippet('Some content here.', 'nonexistent');
    expect(result.match).toBe('');
  });

  it('should handle empty query gracefully', () => {
    const result = highlightSnippet('Some content here.', '');
    expect(typeof result.before).toBe('string');
    expect(typeof result.match).toBe('string');
    expect(typeof result.after).toBe('string');
  });

  it('should handle empty content gracefully', () => {
    const result = highlightSnippet('', 'install');
    expect(result.match).toBe('');
  });

  it('should return a new object each call', () => {
    const r1 = highlightSnippet('Hello world', 'world');
    const r2 = highlightSnippet('Hello world', 'world');
    expect(r1).not.toBe(r2);
  });

  it('should find first occurrence of query in content', () => {
    const result = highlightSnippet('install once, install twice', 'install');
    expect(result.match.toLowerCase()).toBe('install');
    expect(result.before).toBe('');
  });

  it('should respect the windowSize parameter when trimming context', () => {
    const longContent = 'A'.repeat(100) + 'TARGET' + 'B'.repeat(100);
    const result = highlightSnippet(longContent, 'target', 10);
    expect(result.before.length).toBeLessThanOrEqual(10);
    expect(result.after.length).toBeLessThanOrEqual(10);
  });
});

// ---------------------------------------------------------------------------
// buildSnippetFromContent
// ---------------------------------------------------------------------------

describe('buildSnippetFromContent', () => {
  it('should return a string', () => {
    const snippet = buildSnippetFromContent('Hello world');
    expect(typeof snippet).toBe('string');
  });

  it('should truncate content longer than maxLength', () => {
    const snippet = buildSnippetFromContent('A'.repeat(300), 150);
    expect(snippet.length).toBeLessThanOrEqual(150);
  });

  it('should return content as-is when shorter than maxLength', () => {
    const short = 'Short text.';
    const snippet = buildSnippetFromContent(short, 200);
    expect(snippet).toBe(short);
  });

  it('should use a default maxLength of 150 when not provided', () => {
    const snippet = buildSnippetFromContent('A'.repeat(300));
    expect(snippet.length).toBeLessThanOrEqual(150);
  });

  it('should handle empty content', () => {
    const snippet = buildSnippetFromContent('');
    expect(snippet).toBe('');
  });

  it('should not mutate the original content string (strings are immutable in JS)', () => {
    const original = 'Some long content that might be truncated during snippet generation.';
    const before = original;
    buildSnippetFromContent(original, 10);
    expect(original).toBe(before);
  });
});

// ---------------------------------------------------------------------------
// Immutability: mapped models are independent of the DTO
// ---------------------------------------------------------------------------

describe('Docs mapper immutability', () => {
  it('mutating the mapped DocPage contentMarkdown does not affect the DTO', () => {
    const dto: DocPageDto = { ...gettingStartedDto };
    const page = mapDocPageDtoToDocPage(dto);
    // contentMarkdown is a string (primitive) — verify the object itself is new
    expect(page).not.toBe(dto);
  });

  it('mapDocPageDtoToDocSearchResult returns a fresh object on each call', () => {
    const r1 = mapDocPageDtoToDocSearchResult(installGuideDto);
    const r2 = mapDocPageDtoToDocSearchResult(installGuideDto);
    expect(r1).not.toBe(r2);
    expect(r1).toEqual(r2);
  });

  it('mapDocPageDtoToDocPage returns a fresh Date object on each call', () => {
    const r1 = mapDocPageDtoToDocPage(apiReferenceDto);
    const r2 = mapDocPageDtoToDocPage(apiReferenceDto);
    expect(r1.lastUpdated).not.toBe(r2.lastUpdated);
    expect(r1.lastUpdated.getTime()).toBe(r2.lastUpdated.getTime());
  });
});
