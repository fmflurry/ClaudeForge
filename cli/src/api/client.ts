/**
 * Marketplace API client.
 * Wraps the marketplace REST API with fetch-based calls.
 * Maps HTTP errors (ProblemDetails) to typed MarketplaceApiError.
 */

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface ProblemDetails {
  type?: string;
  title: string;
  status: number;
  detail?: string;
}

export interface PluginSummary {
  id: string;
  name: string;
  slug: string;
  description: string;
  author: string;
  downloadCount: number;
  latestVersion: string | null;
}

export interface VersionSummary {
  version: string;
  releasedAt: string;
  downloadCount: number;
  isLatest: boolean;
  packageFormat: string;
  sizeBytes: number;
}

export interface PluginDetail extends PluginSummary {
  allVersions: VersionSummary[];
}

export interface SearchResult {
  id: string;
  name: string;
  slug: string;
  description: string;
  relevanceScore: number;
  downloadCount: number;
}

export interface PaginatedResponse<T> {
  data: T[];
  totalCount: number;
  page: number;
  limit: number;
  totalPages: number;
}

export interface UploadResponse {
  id: string;
  name: string;
  slug: string;
  version: string;
}

// ---------------------------------------------------------------------------
// Error
// ---------------------------------------------------------------------------

export class MarketplaceApiError extends Error {
  public readonly problemDetails: ProblemDetails;
  public readonly status: number;

  constructor(problemDetails: ProblemDetails, status: number) {
    super(problemDetails.title);
    this.name = 'MarketplaceApiError';
    this.problemDetails = problemDetails;
    this.status = status;
    Object.setPrototypeOf(this, MarketplaceApiError.prototype);
  }
}

// ---------------------------------------------------------------------------
// Interface
// ---------------------------------------------------------------------------

export interface IMarketplaceClient {
  searchPlugins(q: string, limit: number): Promise<PaginatedResponse<SearchResult>>;
  getPlugin(name: string): Promise<PluginDetail>;
  downloadPlugin(
    pluginId: string,
    version?: string,
    headers?: Record<string, string>,
  ): Promise<ReadableStream<Uint8Array>>;
  uploadPlugin(formData: FormData, headers?: Record<string, string>): Promise<UploadResponse>;
  getLatestVersion(pluginId: string): Promise<VersionSummary>;
  checkVersionExists(pluginId: string, version: string): Promise<boolean>;
}

// ---------------------------------------------------------------------------
// Implementation helpers
// ---------------------------------------------------------------------------

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let problemDetails: ProblemDetails;
    try {
      problemDetails = (await response.json()) as ProblemDetails;
    } catch {
      problemDetails = {
        title: `HTTP Error ${response.status}`,
        status: response.status,
      };
    }
    throw new MarketplaceApiError(problemDetails, response.status);
  }
  return response.json() as Promise<T>;
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------

export function createMarketplaceClient(apiUrl: string): IMarketplaceClient {
  const base = apiUrl.replace(/\/$/, '');

  return {
    async searchPlugins(q, limit) {
      const url = `${base}/api/v1/plugins/search?q=${encodeURIComponent(q)}&limit=${limit}`;
      const response = await fetch(url);
      return handleResponse<PaginatedResponse<SearchResult>>(response);
    },

    async getPlugin(name) {
      const url = `${base}/api/v1/plugins/search?q=${encodeURIComponent(name)}&limit=1`;
      const response = await fetch(url);
      const paged = await handleResponse<PaginatedResponse<PluginDetail>>(response);
      if (paged.data.length === 0) {
        throw new MarketplaceApiError(
          { title: 'Not Found', status: 404, detail: `Plugin ${name} not found` },
          404,
        );
      }
      return paged.data[0];
    },

    async downloadPlugin(pluginId, version, headers) {
      const versionSegment = version ? `/${encodeURIComponent(version)}` : '/latest';
      const url = `${base}/api/v1/plugins/${encodeURIComponent(pluginId)}/download${versionSegment}`;
      const response = await fetch(url, headers ? { headers } : undefined);
      if (!response.ok) {
        let problemDetails: ProblemDetails;
        try {
          problemDetails = (await response.json()) as ProblemDetails;
        } catch {
          problemDetails = { title: `HTTP Error ${response.status}`, status: response.status };
        }
        throw new MarketplaceApiError(problemDetails, response.status);
      }
      if (!response.body) {
        throw new MarketplaceApiError(
          { title: 'Empty Response', status: 500, detail: 'No response body' },
          500,
        );
      }
      return response.body as ReadableStream<Uint8Array>;
    },

    async uploadPlugin(formData, headers) {
      const url = `${base}/api/v1/plugins/upload`;
      const response = await fetch(url, {
        method: 'POST',
        body: formData,
        ...(headers ? { headers } : {}),
      });
      return handleResponse<UploadResponse>(response);
    },

    async getLatestVersion(pluginId) {
      const url = `${base}/api/v1/plugins/${encodeURIComponent(pluginId)}/versions?limit=1`;
      const response = await fetch(url);
      const paged = await handleResponse<PaginatedResponse<VersionSummary>>(response);
      if (paged.data.length === 0) {
        throw new MarketplaceApiError(
          { title: 'Not Found', status: 404, detail: `No versions found for ${pluginId}` },
          404,
        );
      }
      return paged.data[0];
    },

    async checkVersionExists(pluginId, version) {
      try {
        const url = `${base}/api/v1/plugins/${encodeURIComponent(pluginId)}/versions/${encodeURIComponent(version)}`;
        const response = await fetch(url);
        return response.ok;
      } catch {
        return false;
      }
    },
  };
}
