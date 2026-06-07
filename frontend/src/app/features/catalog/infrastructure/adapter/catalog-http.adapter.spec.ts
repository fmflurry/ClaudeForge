/**
 * Unit tests for CatalogHttpAdapter.
 * Uses a stub ApiClient to verify URL construction and mapping.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { CatalogHttpAdapter } from './catalog-http.adapter';
import { CatalogPort } from '../../domain/ports/catalog.port';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';
import type { Categories, PluginDetail, PluginSummary, PaginationMeta } from '../../domain/models/catalog.models';
import type {
  PaginatedEnvelope,
  PluginDto,
  CategoriesDto,
} from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

const PLUGIN_DTO: PluginDto = {
  pluginId: 'p-1',
  name: 'Awesome Plugin',
  slug: 'awesome-plugin',
  description: 'A great plugin.',
  author: 'Jane Dev',
  types: ['formatter'],
  languages: ['typescript'],
  useCaseTags: ['code-quality'],
  downloadCount: 1000,
  latestVersion: '1.0.0',
  versions: [
    {
      pluginId: 'p-1',
      version: '1.0.0',
      isLatest: true,
      downloadCount: 1000,
      releaseNotes: 'Initial release.',
      createdAt: '2024-01-01T00:00:00.000Z',
    },
  ],
  createdAt: '2024-01-01T00:00:00.000Z',
  updatedAt: '2024-06-01T00:00:00.000Z',
};

const PAGINATED_ENVELOPE: PaginatedEnvelope<PluginDto> = {
  data: [PLUGIN_DTO],
  totalCount: 1,
  page: 1,
  limit: 20,
  totalPages: 1,
};

const CATEGORIES_DTO: CategoriesDto = {
  types: [{ value: 'formatter', displayName: 'Formatter', description: 'Code formatters', count: 5 }],
  languages: [{ value: 'typescript', displayName: 'TypeScript', description: 'TS plugins', count: 3 }],
  useCases: [{ value: 'code-quality', displayName: 'Code Quality', description: 'Quality tools', count: 2 }],
};

// ---------------------------------------------------------------------------
// Stub ApiClient
// ---------------------------------------------------------------------------

@Injectable()
class StubApiClient {
  listPluginsCalls: Parameters<ApiClient['listPlugins']>[] = [];
  getPluginByIdCalls: string[] = [];
  listCategoriesCalls = 0;

  listPlugins(params: Parameters<ApiClient['listPlugins']>[0]): Observable<PaginatedEnvelope<PluginDto>> {
    this.listPluginsCalls.push([params ?? {}]);
    return of(PAGINATED_ENVELOPE);
  }

  getPluginById(pluginId: string): Observable<PluginDto> {
    this.getPluginByIdCalls.push(pluginId);
    return of(PLUGIN_DTO);
  }

  listCategories(): Observable<CategoriesDto> {
    this.listCategoriesCalls++;
    return of(CATEGORIES_DTO);
  }
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(): { adapter: CatalogHttpAdapter; stub: StubApiClient } {
  TestBed.resetTestingModule();
  const stub = new StubApiClient();
  TestBed.configureTestingModule({
    providers: [
      { provide: ApiClient, useValue: stub },
      { provide: CatalogPort, useClass: CatalogHttpAdapter },
      CatalogHttpAdapter,
    ],
  });
  return { adapter: TestBed.inject(CatalogHttpAdapter), stub };
}

// ---------------------------------------------------------------------------
// loadPlugins
// ---------------------------------------------------------------------------

describe('CatalogHttpAdapter — loadPlugins', () => {
  it('should call apiClient.listPlugins', () => {
    const { adapter, stub } = setup();
    const query: CatalogFilterQuery = { page: 1, limit: 20, sort: 'downloadCount', order: 'desc' };
    adapter.loadPlugins(query).subscribe();
    expect(stub.listPluginsCalls).toHaveLength(1);
  });

  it('should map plugin dto to PluginSummary with pluginId', () => {
    const { adapter } = setup();
    const query: CatalogFilterQuery = {};
    let result: { plugins: PluginSummary[]; meta: PaginationMeta } | undefined;
    adapter.loadPlugins(query).subscribe((r) => (result = r));
    expect(result!.plugins[0].pluginId).toBe('p-1');
  });

  it('should map plugin dto to PluginSummary with name', () => {
    const { adapter } = setup();
    let result: { plugins: PluginSummary[]; meta: PaginationMeta } | undefined;
    adapter.loadPlugins({}).subscribe((r) => (result = r));
    expect(result!.plugins[0].name).toBe('Awesome Plugin');
  });

  it('should include pagination meta in result', () => {
    const { adapter } = setup();
    let result: { plugins: PluginSummary[]; meta: PaginationMeta } | undefined;
    adapter.loadPlugins({}).subscribe((r) => (result = r));
    expect(result!.meta.totalCount).toBe(1);
    expect(result!.meta.totalPages).toBe(1);
  });

  it('should convert query to ListPluginsParams via toListPluginsParams', () => {
    const { adapter, stub } = setup();
    const query: CatalogFilterQuery = { page: 2, limit: 10 };
    adapter.loadPlugins(query).subscribe();
    const passedParams = stub.listPluginsCalls[0][0];
    expect(passedParams?.page).toBe(2);
    expect(passedParams?.limit).toBe(10);
  });
});

// ---------------------------------------------------------------------------
// getPlugin
// ---------------------------------------------------------------------------

describe('CatalogHttpAdapter — getPlugin', () => {
  it('should call apiClient.getPluginById with the given pluginId', () => {
    const { adapter, stub } = setup();
    adapter.getPlugin('p-1').subscribe();
    expect(stub.getPluginByIdCalls).toContain('p-1');
  });

  it('should map PluginDto to PluginDetail with versions', () => {
    const { adapter } = setup();
    let detail: PluginDetail | undefined;
    adapter.getPlugin('p-1').subscribe((d) => (detail = d));
    expect(detail!.pluginId).toBe('p-1');
    expect(detail!.versions).toHaveLength(1);
  });

  it('should map version dto to domain PluginVersion', () => {
    const { adapter } = setup();
    let detail: PluginDetail | undefined;
    adapter.getPlugin('p-1').subscribe((d) => (detail = d));
    expect(detail!.versions[0].version).toBe('1.0.0');
    expect(detail!.versions[0].isLatest).toBe(true);
  });

  it('should convert createdAt string to Date', () => {
    const { adapter } = setup();
    let detail: PluginDetail | undefined;
    adapter.getPlugin('p-1').subscribe((d) => (detail = d));
    expect(detail!.createdAt).toBeInstanceOf(Date);
  });
});

// ---------------------------------------------------------------------------
// getCategories
// ---------------------------------------------------------------------------

describe('CatalogHttpAdapter — getCategories', () => {
  it('should call apiClient.listCategories', () => {
    const { adapter, stub } = setup();
    adapter.getCategories().subscribe();
    expect(stub.listCategoriesCalls).toBe(1);
  });

  it('should map categories dto types', () => {
    const { adapter } = setup();
    let categories: Categories | undefined;
    adapter.getCategories().subscribe((c) => (categories = c));
    expect(categories!.types).toHaveLength(1);
    expect(categories!.types[0].value).toBe('formatter');
  });

  it('should map categories dto languages', () => {
    const { adapter } = setup();
    let categories: Categories | undefined;
    adapter.getCategories().subscribe((c) => (categories = c));
    expect(categories!.languages[0].value).toBe('typescript');
  });

  it('should map categories dto useCases', () => {
    const { adapter } = setup();
    let categories: Categories | undefined;
    adapter.getCategories().subscribe((c) => (categories = c));
    expect(categories!.useCases[0].value).toBe('code-quality');
  });
});

// ---------------------------------------------------------------------------
// Architecture — extends CatalogPort
// ---------------------------------------------------------------------------

describe('CatalogHttpAdapter — architecture', () => {
  it('should be an instance of CatalogPort', () => {
    const { adapter } = setup();
    expect(adapter).toBeInstanceOf(CatalogPort);
  });
});
