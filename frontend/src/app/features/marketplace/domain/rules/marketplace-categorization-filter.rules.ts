/**
 * Pure filter rules for the Marketplace Categorization domain.
 * Implements domain-first hierarchy: category AND structural OR keywords OR.
 * No Angular or infrastructure dependencies — zero side effects.
 *
 * @see tasks/3.1-3.5 Plugin Categorization Filter API
 */

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface PluginManifest {
  readonly name: string;
  readonly version: string;
  readonly description: string;
  readonly author: string;
  readonly category: string;
  readonly languages: readonly string[];
  readonly entrypoints: readonly string[];
  readonly keywords?: readonly string[];
  readonly dependencies?: Readonly<Record<string, string>>;
  readonly license?: string;
  readonly docsUrl?: string;
}

export interface MarketplaceFilters {
  category?: string;
  structural?: readonly string[];
  keywords?: string;
}

export interface FilterResult {
  readonly plugins: readonly PluginManifest[];
  readonly total: number;
  readonly filters: MarketplaceFilters;
}

export interface DeprecatedFilters {
  readonly types?: readonly string[];
  readonly useCaseTags?: readonly string[];
}

// ---------------------------------------------------------------------------
// Mapping tables
// ---------------------------------------------------------------------------

const TYPE_TO_STRUCTURAL: Record<string, string> = {
  skill: 'skill',
  hook: 'hook',
  agent: 'subagent',
  command: 'command',
  plugin: 'mcp-server',
};

const USE_CASE_TO_DOMAIN: Record<string, string> = {
  'dev-team': 'workflow-orchestration',
  'solo-dev': 'productivity-utilities',
  'code-review': 'code-intelligence',
  testing: 'testing-qa',
  deployment: 'devops-infrastructure',
  security: 'security',
  data: 'data-analytics',
  documentation: 'documentation',
  integration: 'external-service',
  language: 'language-framework',
  domain: 'domain-vertical',
};

// ---------------------------------------------------------------------------
// Individual filter functions
// ---------------------------------------------------------------------------

/**
 * Domain filter: exact match on `category`.
 * AND with other filter dimensions.
 */
export function filterByCategory(plugins: readonly PluginManifest[], category: string): PluginManifest[] {
  return plugins.filter((p) => p.category === category);
}

/**
 * Structural filter: OR within selection, AND with domain.
 * Matches if plugin has ANY of the specified structural keywords.
 */
export function filterByStructural(
  plugins: readonly PluginManifest[],
  structural: readonly string[],
): PluginManifest[] {
  if (!structural || structural.length === 0) return [...plugins];
  return plugins.filter((p) => p.keywords?.some((k) => structural.includes(k)) ?? false);
}

/**
 * Keyword filter: OR match, AND with domain + structural.
 * Splits search string on whitespace; plugin matches if ANY keyword
 * contains ANY search term (case-insensitive substring match).
 */
export function filterByKeywords(plugins: readonly PluginManifest[], keywords: string): PluginManifest[] {
  if (!keywords || keywords.trim() === '') return [...plugins];
  const searchTerms = keywords.toLowerCase().split(/\s+/);
  return plugins.filter(
    (p) => p.keywords?.some((k) => searchTerms.some((term) => k.toLowerCase().includes(term))) ?? false,
  );
}

// ---------------------------------------------------------------------------
// Composite filter (domain-first hierarchy)
// ---------------------------------------------------------------------------

/**
 * Applies all three filter dimensions with domain-first hierarchy:
 *   category AND (structural OR keywords OR).
 *
 * Logic:
 * 1. If category provided → exact match.
 * 2. If structural provided → OR match on structural keywords.
 * 3. If keywords provided → OR substring match on keywords.
 * 4. Structural and keywords are OR'd together.
 * 5. Category is AND'd with the structural+keywords group.
 *
 * Returns a NEW array — never mutates input.
 */
export function applyFilters(plugins: readonly PluginManifest[], filters: MarketplaceFilters): FilterResult {
  let result: PluginManifest[] = [...plugins];

  // Step 1: Domain filter (AND)
  if (filters.category) {
    result = filterByCategory(result, filters.category);
  }

  // Step 2+3: Structural OR keywords
  const hasStructural = filters.structural && filters.structural.length > 0;
  const hasKeywords = filters.keywords && filters.keywords.trim() !== '';

  if (hasStructural || hasKeywords) {
    result = result.filter((plugin) => {
      const matchesStructural = hasStructural ? filterByStructural([plugin], filters.structural!).length > 0 : false;
      const matchesKeywords = hasKeywords ? filterByKeywords([plugin], filters.keywords!).length > 0 : false;
      // OR within structural+keywords group
      return matchesStructural || matchesKeywords;
    });
  }

  return {
    plugins: result,
    total: result.length,
    filters,
  };
}

// ---------------------------------------------------------------------------
// Deprecated param mapping
// ---------------------------------------------------------------------------

/**
 * Maps deprecated `types` and `useCaseTags` params to new MarketplaceFilters.
 *
 * - `types` → `structural` (via TYPE_TO_STRUCTURAL mapping)
 * - `useCaseTags` → `category` (first tag mapped via USE_CASE_TO_DOMAIN)
 *
 * Returns a NEW object — never mutates input.
 */
export function mapDeprecatedFilters(deprecated: DeprecatedFilters): MarketplaceFilters {
  const filters: MarketplaceFilters = {};

  if (deprecated.types && deprecated.types.length > 0) {
    const mapped = deprecated.types.map((t) => TYPE_TO_STRUCTURAL[t]).filter(Boolean) as string[];
    if (mapped.length > 0) {
      filters.structural = mapped;
    }
  }

  if (deprecated.useCaseTags && deprecated.useCaseTags.length > 0) {
    const domain = USE_CASE_TO_DOMAIN[deprecated.useCaseTags[0]];
    if (domain) {
      filters.category = domain;
    }
  }

  return filters;
}

/**
 * Generates a deprecation warning header value.
 * Returns a string suitable for an HTTP Deprecation header.
 */
export function buildDeprecationHeader(deprecated: DeprecatedFilters): string {
  const warnings: string[] = [];

  if (deprecated.types) {
    warnings.push(`types=${deprecated.types.join(',')} is deprecated. Use structural instead.`);
  }
  if (deprecated.useCaseTags) {
    warnings.push(`useCaseTags=${deprecated.useCaseTags.join(',')} is deprecated. Use category instead.`);
  }

  return warnings.join('; ');
}
