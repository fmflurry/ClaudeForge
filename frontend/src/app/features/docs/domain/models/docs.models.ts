/**
 * Domain models for the Docs feature.
 * All fields are readonly to enforce immutability.
 */

// eslint-disable-next-line @typescript-eslint/consistent-type-definitions
export type DocSearchResult = {
  readonly slug: string;
  readonly title: string;
  readonly category: string;
  readonly snippet: string;
  readonly relevanceScore: number;
};

// eslint-disable-next-line @typescript-eslint/consistent-type-definitions
export type DocPage = {
  readonly slug: string;
  readonly title: string;
  readonly category: string;
  readonly contentMarkdown: string;
  readonly lastUpdated: Date;
};

// eslint-disable-next-line @typescript-eslint/consistent-type-definitions
export type DocCategoryNode = {
  readonly category: string;
  readonly docs: readonly DocSearchResult[];
};

// eslint-disable-next-line @typescript-eslint/consistent-type-definitions
export type DocHighlight = {
  readonly before: string;
  readonly match: string;
  readonly after: string;
};
