/**
 * Hand-written TypeScript DTOs generated from
 * backend/openapi/ClaudeForge.Api.json (OpenAPI 3.1.1).
 *
 * Approach: hand-written (no codegen tool installed — avoids heavy/unknown deps).
 * All types are strict — no `any`.
 */

// ---------------------------------------------------------------------------
// Shared envelope
// ---------------------------------------------------------------------------

export interface PaginatedEnvelope<T> {
  data: T[];
  totalCount: number;
  page: number;
  limit: number;
  totalPages: number;
}

// ---------------------------------------------------------------------------
// ProblemDetails (RFC 7807)
// ---------------------------------------------------------------------------

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  /** Extension fields */
  errors?: Record<string, string[]>;
}

export interface ApiError {
  code: string;
  message: string;
}

// ---------------------------------------------------------------------------
// Plugin Catalog
// ---------------------------------------------------------------------------

export interface PluginVersionDto {
  pluginId: string;
  version: string;
  isLatest: boolean;
  downloadCount: number;
  releaseNotes: string;
  createdAt: string;
}

export interface PluginDto {
  pluginId: string;
  name: string;
  slug: string;
  description: string;
  author: string;
  types: string[];
  languages: string[];
  useCaseTags: string[];
  downloadCount: number;
  latestVersion: string | null;
  versions: PluginVersionDto[];
  createdAt: string;
  updatedAt: string;
}

export interface CategoryValueDto {
  value: string;
  displayName: string;
  description: string;
  count: number;
}

export interface CategoriesDto {
  types: CategoryValueDto[];
  languages: CategoryValueDto[];
  useCases: CategoryValueDto[];
}

// ---------------------------------------------------------------------------
// Plugin Search / Discovery
// ---------------------------------------------------------------------------

export interface SearchResultDto {
  pluginId: string;
  name: string;
  slug: string;
  description: string;
  author: string;
  types: string[];
  languages: string[];
  downloadCount: number;
  latestVersion: string | null;
  relevanceScore: number;
}

export interface DiscoveryResultDto {
  pluginId: string;
  name: string;
  slug: string;
  description: string;
  author: string;
  types: string[];
  languages: string[];
  matchedLanguages: string[];
  maturityIndicator: string;
  relevanceScore: number;
}

// ---------------------------------------------------------------------------
// Plugin Publishing
// ---------------------------------------------------------------------------

export interface UploadPluginResponseDto {
  pluginId: string;
  version: string;
}

// ---------------------------------------------------------------------------
// Telemetry
// ---------------------------------------------------------------------------

export interface IngestTelemetryRequestDto {
  eventType: string | null;
  pluginId: string;
  version: string | null;
  anonClientId: string | null;
  clientOs: string | null;
  clientArch: string | null;
}

export interface TelemetrySummaryDto {
  pluginId: string;
  totalDownloads: number;
  totalInstalls: number;
  last7dActivity: number;
}

// ---------------------------------------------------------------------------
// Docs
// ---------------------------------------------------------------------------

export interface DocPageDto {
  slug: string;
  title: string;
  content: string;
  lastUpdated: string;
}

// ---------------------------------------------------------------------------
// Query params
// ---------------------------------------------------------------------------

export interface ListPluginsParams {
  page?: number;
  limit?: number;
  sort?: string;
  order?: string;
  type?: string[];
  language?: string[];
  useCase?: string[];
}

export interface SearchPluginsParams {
  q?: string;
  type?: string[];
  language?: string[];
  useCase?: string[];
  page?: number;
  limit?: number;
}

export interface DiscoverPluginsParams {
  keyword?: string;
  language?: string[];
  useCase?: string[];
  type?: string[];
}

export interface SearchDocsParams {
  search?: string;
  page?: number;
  limit?: number;
}

export interface GetVersionHistoryParams {
  page?: number;
  limit?: number;
}

// ---------------------------------------------------------------------------
// Marketplace Stats
// ---------------------------------------------------------------------------

export interface MarketplaceStatsDto {
  totalPlugins: number;
  totalDownloads: number;
  publisherCount: number;
  categoryCount: number;
}
