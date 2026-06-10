import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../.././../core/config/api-config';
import {
  CategoriesDto,
  DiscoverPluginsParams,
  DiscoveryResultDto,
  DocPageDto,
  FeaturedPluginEnvelope,
  GetVersionHistoryParams,
  IngestTelemetryRequestDto,
  ListPluginsParams,
  MarketplaceStatsDto,
  PaginatedEnvelope,
  PluginDto,
  PluginVersionDto,
  SearchDocsParams,
  SearchPluginsParams,
  SearchResultDto,
  TelemetrySummaryDto,
  UploadPluginResponseDto,
} from './api-client.types';

/**
 * Typed HTTP client for the ClaudeForge API.
 * Generated hand-written from backend/openapi/ClaudeForge.Api.json.
 * No `any` used — all request/response shapes are fully typed.
 */
@Injectable({ providedIn: 'root' })
export class ApiClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  // ---------------------------------------------------------------------------
  // Plugin Catalog
  // ---------------------------------------------------------------------------

  listPlugins(params: ListPluginsParams = {}): Observable<PaginatedEnvelope<PluginDto>> {
    const httpParams = this.buildParams(params as Record<string, unknown>);
    return this.http.get<PaginatedEnvelope<PluginDto>>(`${this.baseUrl}/api/v1/plugins`, {
      params: httpParams,
    });
  }

  getPluginById(pluginId: string): Observable<PluginDto> {
    return this.http.get<PluginDto>(`${this.baseUrl}/api/v1/plugins/${pluginId}`);
  }

  listCategories(): Observable<CategoriesDto> {
    return this.http.get<CategoriesDto>(`${this.baseUrl}/api/v1/categories`);
  }

  // ---------------------------------------------------------------------------
  // Plugin Search / Discovery
  // ---------------------------------------------------------------------------

  searchPlugins(params: SearchPluginsParams = {}): Observable<PaginatedEnvelope<SearchResultDto>> {
    const httpParams = this.buildParams(params as Record<string, unknown>);
    return this.http.get<PaginatedEnvelope<SearchResultDto>>(`${this.baseUrl}/api/v1/plugins/search`, {
      params: httpParams,
    });
  }

  searchPluginsAlias(params: SearchPluginsParams = {}): Observable<PaginatedEnvelope<SearchResultDto>> {
    const httpParams = this.buildParams(params as Record<string, unknown>);
    return this.http.get<PaginatedEnvelope<SearchResultDto>>(`${this.baseUrl}/api/v1/search`, {
      params: httpParams,
    });
  }

  discoverPlugins(params: DiscoverPluginsParams = {}): Observable<PaginatedEnvelope<DiscoveryResultDto>> {
    const httpParams = this.buildParams(params as Record<string, unknown>);
    return this.http.get<PaginatedEnvelope<DiscoveryResultDto>>(`${this.baseUrl}/api/v1/discovery`, {
      params: httpParams,
    });
  }

  // ---------------------------------------------------------------------------
  // Plugin Publishing
  // ---------------------------------------------------------------------------

  uploadPlugin(formData: FormData): Observable<UploadPluginResponseDto> {
    return this.http.post<UploadPluginResponseDto>(`${this.baseUrl}/api/v1/plugins/upload`, formData);
  }

  publishPluginVersion(pluginId: string, formData: FormData): Observable<PluginVersionDto> {
    return this.http.post<PluginVersionDto>(`${this.baseUrl}/api/v1/plugins/${pluginId}/versions`, formData);
  }

  getVersionHistory(
    pluginId: string,
    params: GetVersionHistoryParams = {},
  ): Observable<PaginatedEnvelope<PluginVersionDto>> {
    const httpParams = this.buildParams(params as Record<string, unknown>);
    return this.http.get<PaginatedEnvelope<PluginVersionDto>>(`${this.baseUrl}/api/v1/plugins/${pluginId}/versions`, {
      params: httpParams,
    });
  }

  getVersion(pluginId: string, version: string): Observable<PluginVersionDto> {
    return this.http.get<PluginVersionDto>(`${this.baseUrl}/api/v1/plugins/${pluginId}/versions/${version}`);
  }

  // ---------------------------------------------------------------------------
  // Plugin Distribution
  // ---------------------------------------------------------------------------

  downloadPlugin(pluginId: string, version?: string): Observable<Blob> {
    const httpParams = version ? new HttpParams().set('version', version) : undefined;
    return this.http.get(`${this.baseUrl}/api/v1/plugins/${pluginId}/download`, {
      params: httpParams,
      responseType: 'blob',
    });
  }

  // ---------------------------------------------------------------------------
  // Telemetry
  // ---------------------------------------------------------------------------

  postTelemetryEvent(request: IngestTelemetryRequestDto): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/v1/telemetry/events`, request);
  }

  getTelemetrySummary(pluginId: string): Observable<TelemetrySummaryDto> {
    return this.http.get<TelemetrySummaryDto>(`${this.baseUrl}/api/v1/plugins/${pluginId}/telemetry/summary`);
  }

  // ---------------------------------------------------------------------------
  // Docs
  // ---------------------------------------------------------------------------

  searchDocs(params: SearchDocsParams = {}): Observable<PaginatedEnvelope<DocPageDto>> {
    const httpParams = this.buildParams(params as Record<string, unknown>);
    return this.http.get<PaginatedEnvelope<DocPageDto>>(`${this.baseUrl}/api/v1/docs`, {
      params: httpParams,
    });
  }

  getDocBySlug(slug: string): Observable<DocPageDto> {
    return this.http.get<DocPageDto>(`${this.baseUrl}/api/v1/docs/${slug}`);
  }

  // ---------------------------------------------------------------------------
  // Marketplace Stats
  // ---------------------------------------------------------------------------

  getMarketplaceStats(): Observable<MarketplaceStatsDto> {
    return this.http.get<MarketplaceStatsDto>(`${this.baseUrl}/api/v1/stats`);
  }

  // ---------------------------------------------------------------------------
  // Featured Plugin
  // ---------------------------------------------------------------------------

  getFeaturedPlugin(): Observable<FeaturedPluginEnvelope> {
    return this.http.get<FeaturedPluginEnvelope>(`${this.baseUrl}/api/v1/plugins/featured`);
  }

  // ---------------------------------------------------------------------------
  // Health
  // ---------------------------------------------------------------------------

  healthCheck(): Observable<unknown> {
    return this.http.get<unknown>(`${this.baseUrl}/health`);
  }

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  private buildParams(obj: Record<string, unknown>): HttpParams {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(obj)) {
      if (value === undefined || value === null) continue;
      if (Array.isArray(value)) {
        for (const item of value) {
          params = params.append(key, String(item));
        }
      } else {
        params = params.set(key, String(value));
      }
    }
    return params;
  }
}
