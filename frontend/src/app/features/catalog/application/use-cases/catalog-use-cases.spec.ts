/**
 * Unit tests for catalog use-cases:
 *   - LoadAddOnsUseCase
 *   - LoadAddOnDetailUseCase
 *   - LoadCategoriesUseCase
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { LoadAddOnsUseCase } from './load-plugins.use-case';
import { LoadAddOnDetailUseCase } from './load-plugin-detail.use-case';
import { LoadCategoriesUseCase } from './load-categories.use-case';
import { CatalogPort } from '../../domain/ports/catalog.port';
import type { Categories, PaginationMeta, AddOnDetail, AddOnSummary } from '../../domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const SUMMARY: AddOnSummary = {
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

const DETAIL: AddOnDetail = { ...SUMMARY, versions: [] };

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
  loadAddOns(_query: CatalogFilterQuery): Observable<{ addOns: AddOnSummary[]; meta: PaginationMeta }> {
    return of({ addOns: [SUMMARY], meta: META });
  }

  getAddOn(_pluginId: string): Observable<AddOnDetail> {
    return of(DETAIL);
  }

  getCategories(): Observable<Categories> {
    return of(CATEGORIES);
  }
}

// ---------------------------------------------------------------------------
// LoadAddOnsUseCase
// ---------------------------------------------------------------------------

describe('LoadAddOnsUseCase', () => {
  function setup(): LoadAddOnsUseCase {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [LoadAddOnsUseCase, { provide: CatalogPort, useClass: FakeCatalogPort }],
    });
    return TestBed.inject(LoadAddOnsUseCase);
  }

  it('should return addOns from the port', () => {
    const useCase = setup();
    let result: { addOns: AddOnSummary[]; meta: PaginationMeta } | undefined;
    useCase.execute({ page: 1, limit: 20 }).subscribe((r) => (result = r));
    expect(result!.addOns).toHaveLength(1);
    expect(result!.addOns[0].pluginId).toBe('p-1');
  });

  it('should return pagination meta from the port', () => {
    const useCase = setup();
    let result: { addOns: AddOnSummary[]; meta: PaginationMeta } | undefined;
    useCase.execute({}).subscribe((r) => (result = r));
    expect(result!.meta.totalCount).toBe(1);
  });

  it('should delegate query to port unchanged', () => {
    let captured: CatalogFilterQuery | undefined;

    @Injectable()
    class SpyPort extends CatalogPort {
      loadAddOns(q: CatalogFilterQuery): Observable<{ addOns: AddOnSummary[]; meta: PaginationMeta }> {
        captured = q;
        return of({ addOns: [], meta: { totalCount: 0, page: 1, limit: 20, totalPages: 0 } });
      }
      getAddOn(_: string): Observable<AddOnDetail> {
        return of(DETAIL);
      }
      getCategories(): Observable<Categories> {
        return of(CATEGORIES);
      }
    }

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [LoadAddOnsUseCase, { provide: CatalogPort, useClass: SpyPort }],
    });
    const uc = TestBed.inject(LoadAddOnsUseCase);
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
// LoadAddOnDetailUseCase
// ---------------------------------------------------------------------------

describe('LoadAddOnDetailUseCase', () => {
  function setup(): LoadAddOnDetailUseCase {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [LoadAddOnDetailUseCase, { provide: CatalogPort, useClass: FakeCatalogPort }],
    });
    return TestBed.inject(LoadAddOnDetailUseCase);
  }

  it('should return addon detail from port', () => {
    const useCase = setup();
    let result: AddOnDetail | undefined;
    useCase.execute('p-1').subscribe((d) => (result = d));
    expect(result!.pluginId).toBe('p-1');
  });

  it('should delegate pluginId to port', () => {
    let capturedId: string | undefined;

    @Injectable()
    class SpyPort extends CatalogPort {
      loadAddOns(_q: CatalogFilterQuery): Observable<{ addOns: AddOnSummary[]; meta: PaginationMeta }> {
        return of({ addOns: [], meta: { totalCount: 0, page: 1, limit: 20, totalPages: 0 } });
      }
      getAddOn(id: string): Observable<AddOnDetail> {
        capturedId = id;
        return of(DETAIL);
      }
      getCategories(): Observable<Categories> {
        return of(CATEGORIES);
      }
    }

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [LoadAddOnDetailUseCase, { provide: CatalogPort, useClass: SpyPort }],
    });
    const uc = TestBed.inject(LoadAddOnDetailUseCase);
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
      providers: [LoadCategoriesUseCase, { provide: CatalogPort, useClass: FakeCatalogPort }],
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
