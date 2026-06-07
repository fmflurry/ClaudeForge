/**
 * Unit tests for catalog use-cases:
 *   - LoadPluginsUseCase
 *   - LoadPluginDetailUseCase
 *   - LoadCategoriesUseCase
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { LoadPluginsUseCase } from './load-plugins.use-case';
import { LoadPluginDetailUseCase } from './load-plugin-detail.use-case';
import { LoadCategoriesUseCase } from './load-categories.use-case';
import { CatalogPort } from '../../domain/ports/catalog.port';
import type { Categories, PaginationMeta, PluginDetail, PluginSummary } from '../../domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const SUMMARY: PluginSummary = {
  pluginId: 'p-1',
  name: 'Awesome Plugin',
  slug: 'awesome',
  description: 'Desc',
  author: 'Alice',
  types: ['formatter'],
  languages: ['typescript'],
  useCaseTags: ['code-quality'],
  downloadCount: 100,
  latestVersion: '1.0.0',
  createdAt: new Date('2024-01-01'),
  updatedAt: new Date('2024-06-01'),
};

const META: PaginationMeta = { totalCount: 1, page: 1, limit: 20, totalPages: 1 };

const DETAIL: PluginDetail = { ...SUMMARY, versions: [] };

const CATEGORIES: Categories = {
  types: [{ value: 'formatter', displayName: 'Formatter', description: '', count: 1 }],
  languages: [],
  useCases: [],
};

// ---------------------------------------------------------------------------
// Fake port
// ---------------------------------------------------------------------------

@Injectable()
class FakeCatalogPort extends CatalogPort {
  loadPlugins(_query: CatalogFilterQuery): Observable<{ plugins: PluginSummary[]; meta: PaginationMeta }> {
    return of({ plugins: [SUMMARY], meta: META });
  }

  getPlugin(_pluginId: string): Observable<PluginDetail> {
    return of(DETAIL);
  }

  getCategories(): Observable<Categories> {
    return of(CATEGORIES);
  }
}

// ---------------------------------------------------------------------------
// LoadPluginsUseCase
// ---------------------------------------------------------------------------

describe('LoadPluginsUseCase', () => {
  function setup(): LoadPluginsUseCase {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LoadPluginsUseCase,
        { provide: CatalogPort, useClass: FakeCatalogPort },
      ],
    });
    return TestBed.inject(LoadPluginsUseCase);
  }

  it('should return plugins from the port', () => {
    const useCase = setup();
    let result: { plugins: PluginSummary[]; meta: PaginationMeta } | undefined;
    useCase.execute({ page: 1, limit: 20 }).subscribe((r) => (result = r));
    expect(result!.plugins).toHaveLength(1);
    expect(result!.plugins[0].pluginId).toBe('p-1');
  });

  it('should return pagination meta from the port', () => {
    const useCase = setup();
    let result: { plugins: PluginSummary[]; meta: PaginationMeta } | undefined;
    useCase.execute({}).subscribe((r) => (result = r));
    expect(result!.meta.totalCount).toBe(1);
  });

  it('should delegate query to port unchanged', () => {
    let captured: CatalogFilterQuery | undefined;

    @Injectable()
    class SpyPort extends CatalogPort {
      loadPlugins(q: CatalogFilterQuery): Observable<{ plugins: PluginSummary[]; meta: PaginationMeta }> {
        captured = q;
        return of({ plugins: [], meta: { totalCount: 0, page: 1, limit: 20, totalPages: 0 } });
      }
      getPlugin(_: string): Observable<PluginDetail> { return of(DETAIL); }
      getCategories(): Observable<Categories> { return of(CATEGORIES); }
    }

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LoadPluginsUseCase,
        { provide: CatalogPort, useClass: SpyPort },
      ],
    });
    const uc = TestBed.inject(LoadPluginsUseCase);
    const q: CatalogFilterQuery = { page: 3, limit: 5 };
    uc.execute(q).subscribe();
    expect(captured).toEqual(q);
  });

  it('should return an Observable', () => {
    const useCase = setup();
    const result = useCase.execute({});
    expect(typeof result.subscribe).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// LoadPluginDetailUseCase
// ---------------------------------------------------------------------------

describe('LoadPluginDetailUseCase', () => {
  function setup(): LoadPluginDetailUseCase {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LoadPluginDetailUseCase,
        { provide: CatalogPort, useClass: FakeCatalogPort },
      ],
    });
    return TestBed.inject(LoadPluginDetailUseCase);
  }

  it('should return plugin detail from port', () => {
    const useCase = setup();
    let result: PluginDetail | undefined;
    useCase.execute('p-1').subscribe((d) => (result = d));
    expect(result!.pluginId).toBe('p-1');
  });

  it('should delegate pluginId to port', () => {
    let capturedId: string | undefined;

    @Injectable()
    class SpyPort extends CatalogPort {
      loadPlugins(_q: CatalogFilterQuery): Observable<{ plugins: PluginSummary[]; meta: PaginationMeta }> {
        return of({ plugins: [], meta: { totalCount: 0, page: 1, limit: 20, totalPages: 0 } });
      }
      getPlugin(id: string): Observable<PluginDetail> {
        capturedId = id;
        return of(DETAIL);
      }
      getCategories(): Observable<Categories> { return of(CATEGORIES); }
    }

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LoadPluginDetailUseCase,
        { provide: CatalogPort, useClass: SpyPort },
      ],
    });
    const uc = TestBed.inject(LoadPluginDetailUseCase);
    uc.execute('plugin-xyz').subscribe();
    expect(capturedId).toBe('plugin-xyz');
  });

  it('should return an Observable', () => {
    const useCase = setup();
    const result = useCase.execute('p-1');
    expect(typeof result.subscribe).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// LoadCategoriesUseCase
// ---------------------------------------------------------------------------

describe('LoadCategoriesUseCase', () => {
  function setup(): LoadCategoriesUseCase {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LoadCategoriesUseCase,
        { provide: CatalogPort, useClass: FakeCatalogPort },
      ],
    });
    return TestBed.inject(LoadCategoriesUseCase);
  }

  it('should return categories from the port', () => {
    const useCase = setup();
    let result: Categories | undefined;
    useCase.execute().subscribe((c) => (result = c));
    expect(result!.types).toHaveLength(1);
    expect(result!.types[0].value).toBe('formatter');
  });

  it('should return an Observable', () => {
    const useCase = setup();
    const result = useCase.execute();
    expect(typeof result.subscribe).toBe('function');
  });
});
