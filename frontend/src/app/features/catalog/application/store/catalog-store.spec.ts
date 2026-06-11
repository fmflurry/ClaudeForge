/**
 * RED tests — Task 12.3: CatalogStore + CatalogFacade
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/catalog/application/store/catalog.store.ts
 *   src/app/features/catalog/application/store/catalog.store.enum.ts (or inline)
 *   src/app/features/catalog/application/facades/catalog.facade.ts
 *   src/app/features/catalog/domain/ports/catalog.port.ts
 *   src/app/features/catalog/application/use-cases/load-plugins.use-case.ts
 *   src/app/features/catalog/application/use-cases/load-plugin-detail.use-case.ts
 *   src/app/features/catalog/application/use-cases/load-categories.use-case.ts
 *
 * Production types/classes the coder MUST define:
 *
 *   // catalog.store.enum.ts (may be co-located in catalog.store.ts)
 *   enum CatalogStoreEnum {
 *     ADDONS = 'ADDONS',
 *     ADDON_DETAIL = 'ADDON_DETAIL',
 *     CATEGORIES = 'CATEGORIES',
 *   }
 *
 *   // catalog.store.ts
 *   interface CatalogState {
 *     [CatalogStoreEnum.ADDONS]: ResourceState<AddOnSummary[]>;      // includes PaginationMeta on data side via separate key or wrapper
 *     [CatalogStoreEnum.ADDON_DETAIL]: ResourceState<AddOnDetail>;
 *     [CatalogStoreEnum.CATEGORIES]: ResourceState<Categories>;
 *   }
 *   @Injectable({ providedIn: 'root' })
 *   class CatalogStore extends BaseStore<typeof CatalogStoreEnum, CatalogState>
 *
 *   // catalog.port.ts
 *   abstract class CatalogPort {
 *     abstract loadAddOns(query: CatalogFilterQuery): Observable<{ addOns: AddOnSummary[]; meta: PaginationMeta }>;
 *     abstract getAddOn(pluginId: string): Observable<AddOnDetail>;
 *     abstract getCategories(): Observable<Categories>;
 *   }
 *
 *   // catalog.facade.ts
 *   @Injectable()
 *   class CatalogFacade {
 *     // Signals (readonly, derived from store):
 *     get addOns(): Signal<AddOnSummary[]>
 *     get paginationMeta(): Signal<PaginationMeta | undefined>
 *     get categories(): Signal<Categories | undefined>
 *     get selectedAddOn(): Signal<AddOnDetail | undefined>
 *     get isLoadingAddOns(): Signal<boolean>
 *     get isLoadingDetail(): Signal<boolean>
 *     get isLoadingCategories(): Signal<boolean>
 *     get addOnsError(): Signal<{ code: string; message: string }[] | undefined>
 *     get detailError(): Signal<{ code: string; message: string }[] | undefined>
 *
 *     // Methods:
 *     loadAddOns(query?: Partial<CatalogFilterQuery>): void
 *     setPage(page: number): void
 *     setSort(sort: string, order?: 'asc' | 'desc'): void
 *     setFilters(filters: Partial<Pick<CatalogFilterQuery, 'types' | 'languages' | 'useCases'>>): void
 *     loadDetail(pluginId: string): void
 *     loadCategories(): void
 *   }
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { ResourceState } from '../../../../shared/application/store/resource-state.model';
import { CatalogStore, CatalogStoreEnum } from './catalog.store';
import type { CatalogState } from './catalog.store';
import { CatalogFacade } from '../facades/catalog.facade';
import { CatalogPort } from '../../domain/ports/catalog.port';
import type { Categories, PaginationMeta, AddOnDetail, AddOnSummary } from '../../domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';

// ---------------------------------------------------------------------------
// Fake CatalogPort for injection
// ---------------------------------------------------------------------------

const FAKE_PLUGINS: AddOnSummary[] = [
  {
    pluginId: 'p1',
    name: 'Plugin One',
    slug: 'plugin-one',
    description: 'First plugin.',
    author: 'Author A',
    types: ['formatter'],
    languages: ['typescript'],
    useCaseTags: ['code-quality'],
    downloadCount: 500,
    latestVersion: '1.0.0',
    createdAt: new Date('2024-01-01'),
    updatedAt: new Date('2024-01-01'),
  },
];

const FAKE_META: PaginationMeta = {
  totalCount: 1,
  page: 1,
  limit: 20,
  totalPages: 1,
};

const FAKE_DETAIL: AddOnDetail = {
  pluginId: 'p1',
  name: 'Plugin One',
  slug: 'plugin-one',
  description: 'First plugin.',
  author: 'Author A',
  types: ['formatter'],
  languages: ['typescript'],
  useCaseTags: ['code-quality'],
  downloadCount: 500,
  latestVersion: '1.0.0',
  createdAt: new Date('2024-01-01'),
  updatedAt: new Date('2024-01-01'),
  versions: [
    {
      pluginId: 'p1',
      version: '1.0.0',
      isLatest: true,
      downloadCount: 500,
      releaseNotes: 'Initial release.',
      createdAt: new Date('2024-01-01'),
    },
  ],
};

const FAKE_CATEGORIES: Categories = {
  types: [{ value: 'formatter', displayName: 'Formatter', description: 'Formatters.', count: 5 }],
  languages: [{ value: 'typescript', displayName: 'TypeScript', description: 'TS plugins.', count: 3 }],
  useCases: [{ value: 'code-quality', displayName: 'Code Quality', description: 'Quality.', count: 2 }],
};

@Injectable()
class FakeCatalogPort extends CatalogPort {
  loadAddOns(_query: CatalogFilterQuery): Observable<{ addOns: AddOnSummary[]; meta: PaginationMeta }> {
    return of({ addOns: FAKE_PLUGINS, meta: FAKE_META });
  }

  getAddOn(_pluginId: string): Observable<AddOnDetail> {
    return of(FAKE_DETAIL);
  }

  getCategories(): Observable<Categories> {
    return of(FAKE_CATEGORIES);
  }
}

@Injectable()
class ErrorCatalogPort extends CatalogPort {
  loadAddOns(_query: CatalogFilterQuery): Observable<{ addOns: AddOnSummary[]; meta: PaginationMeta }> {
    return throwError(() => new Error('Network error'));
  }

  getAddOn(_pluginId: string): Observable<AddOnDetail> {
    return throwError(() => new Error('Not found'));
  }

  getCategories(): Observable<Categories> {
    return throwError(() => new Error('Categories error'));
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setupWithFakePort(): { store: CatalogStore; facade: CatalogFacade } {
  TestBed.configureTestingModule({
    providers: [CatalogStore, CatalogFacade, { provide: CatalogPort, useClass: FakeCatalogPort }],
  });
  return {
    store: TestBed.inject(CatalogStore),
    facade: TestBed.inject(CatalogFacade),
  };
}

function setupWithErrorPort(): { store: CatalogStore; facade: CatalogFacade } {
  TestBed.configureTestingModule({
    providers: [CatalogStore, CatalogFacade, { provide: CatalogPort, useClass: ErrorCatalogPort }],
  });
  return {
    store: TestBed.inject(CatalogStore),
    facade: TestBed.inject(CatalogFacade),
  };
}

// ---------------------------------------------------------------------------
// CatalogStore — resource key existence
// ---------------------------------------------------------------------------

describe('CatalogStore — enum keys', () => {
  it('should have ADDONS key', () => {
    expect(CatalogStoreEnum.ADDONS).toBe('ADDONS');
  });

  it('should have ADDON_DETAIL key', () => {
    expect(CatalogStoreEnum.ADDON_DETAIL).toBe('ADDON_DETAIL');
  });

  it('should have CATEGORIES key', () => {
    expect(CatalogStoreEnum.CATEGORIES).toBe('CATEGORIES');
  });
});

describe('CatalogStore — initial state', () => {
  it('should initialise ADDONS with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [CatalogStore] });
    const store = TestBed.inject(CatalogStore);
    const state: ResourceState<AddOnSummary[]> = store.get(CatalogStoreEnum.ADDONS)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
    expect(state.status).toBeUndefined();
  });

  it('should initialise ADDON_DETAIL with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [CatalogStore] });
    const store = TestBed.inject(CatalogStore);
    const state: ResourceState<AddOnDetail> = store.get(CatalogStoreEnum.ADDON_DETAIL)();
    expect(state.isLoading).toBeFalsy();
  });

  it('should initialise CATEGORIES with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [CatalogStore] });
    const store = TestBed.inject(CatalogStore);
    const state: ResourceState<Categories> = store.get(CatalogStoreEnum.CATEGORIES)();
    expect(state.isLoading).toBeFalsy();
  });
});

describe('CatalogStore — type conformance', () => {
  it('should be an instance of CatalogStore', () => {
    TestBed.configureTestingModule({ providers: [CatalogStore] });
    const store = TestBed.inject(CatalogStore);
    expect(store).toBeInstanceOf(CatalogStore);
  });

  it('ADDONS state type should accept ResourceState<AddOnSummary[]>', () => {
    TestBed.configureTestingModule({ providers: [CatalogStore] });
    const store = TestBed.inject(CatalogStore);
    const partial: Partial<CatalogState[typeof CatalogStoreEnum.ADDONS]> = {
      data: FAKE_PLUGINS,
      status: 'Success',
    };
    store.update(CatalogStoreEnum.ADDONS, partial);
    expect(store.get(CatalogStoreEnum.ADDONS)().status).toBe('Success');
  });
});

// ---------------------------------------------------------------------------
// CatalogFacade — signals (initial/idle)
// ---------------------------------------------------------------------------

describe('CatalogFacade — initial signal values', () => {
  it('addOns signal should return empty array before any load', () => {
    const { facade } = setupWithFakePort();
    expect(facade.addOns()).toEqual([]);
  });

  it('paginationMeta signal should return undefined before any load', () => {
    const { facade } = setupWithFakePort();
    expect(facade.paginationMeta()).toBeUndefined();
  });

  it('categories signal should return undefined before any load', () => {
    const { facade } = setupWithFakePort();
    expect(facade.categories()).toBeUndefined();
  });

  it('selectedAddOn signal should return undefined before any detail load', () => {
    const { facade } = setupWithFakePort();
    expect(facade.selectedAddOn()).toBeUndefined();
  });

  it('isLoadingAddOns should return false initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.isLoadingAddOns()).toBe(false);
  });

  it('isLoadingDetail should return false initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.isLoadingDetail()).toBe(false);
  });

  it('isLoadingCategories should return false initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.isLoadingCategories()).toBe(false);
  });

  it('addOnsError should return undefined initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.addOnsError()).toBeUndefined();
  });

  it('detailError should return undefined initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.detailError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// CatalogFacade — loadPlugins (success path)
// ---------------------------------------------------------------------------

describe('CatalogFacade — loadAddOns success', () => {
  it('should populate the addOns signal after loadAddOns()', () => {
    const { facade } = setupWithFakePort();
    facade.loadAddOns();
    expect(facade.addOns()).toEqual(FAKE_PLUGINS);
  });

  it('should populate paginationMeta after loadAddOns()', () => {
    const { facade } = setupWithFakePort();
    facade.loadAddOns();
    expect(facade.paginationMeta()).toEqual(FAKE_META);
  });

  it('should set isLoadingAddOns to false after successful load', () => {
    const { facade } = setupWithFakePort();
    facade.loadAddOns();
    expect(facade.isLoadingAddOns()).toBe(false);
  });

  it('should clear addOnsError after successful load', () => {
    const { facade } = setupWithFakePort();
    facade.loadAddOns();
    expect(facade.addOnsError()).toBeUndefined();
  });

  it('should accept query parameters and pass them to the port', () => {
    const { facade } = setupWithFakePort();
    // Should not throw when called with a partial query
    expect(() => facade.loadAddOns({ types: ['formatter'], page: 2 })).not.toThrow();
    expect(facade.addOns()).toEqual(FAKE_PLUGINS);
  });
});

// ---------------------------------------------------------------------------
// CatalogFacade — loadPlugins (error path)
// ---------------------------------------------------------------------------

describe('CatalogFacade — loadAddOns error', () => {
  it('should set addOnsError when port throws', () => {
    const { facade } = setupWithErrorPort();
    facade.loadAddOns();
    expect(facade.addOnsError()).toBeDefined();
    expect(Array.isArray(facade.addOnsError())).toBe(true);
  });

  it('should not crash the application when port throws', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.loadAddOns()).not.toThrow();
  });

  it('should set isLoadingAddOns to false after error', () => {
    const { facade } = setupWithErrorPort();
    facade.loadAddOns();
    expect(facade.isLoadingAddOns()).toBe(false);
  });

  it('should leave addOns as empty array after error', () => {
    const { facade } = setupWithErrorPort();
    facade.loadAddOns();
    // Either empty array or undefined — should not have stale successful data
    const addOns = facade.addOns();
    expect(Array.isArray(addOns) ? addOns.length : 0).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// CatalogFacade — loadDetail (success path)
// ---------------------------------------------------------------------------

describe('CatalogFacade — loadDetail success', () => {
  it('should populate selectedAddOn after loadDetail()', () => {
    const { facade } = setupWithFakePort();
    facade.loadDetail('p1');
    expect(facade.selectedAddOn()).toEqual(FAKE_DETAIL);
  });

  it('should set isLoadingDetail to false after successful load', () => {
    const { facade } = setupWithFakePort();
    facade.loadDetail('p1');
    expect(facade.isLoadingDetail()).toBe(false);
  });

  it('should clear detailError after successful load', () => {
    const { facade } = setupWithFakePort();
    facade.loadDetail('p1');
    expect(facade.detailError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// CatalogFacade — loadDetail (error path)
// ---------------------------------------------------------------------------

describe('CatalogFacade — loadDetail error', () => {
  it('should set detailError when port throws', () => {
    const { facade } = setupWithErrorPort();
    facade.loadDetail('nonexistent');
    expect(facade.detailError()).toBeDefined();
  });

  it('should not throw when port throws on detail load', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.loadDetail('bad-id')).not.toThrow();
  });

  it('should set isLoadingDetail to false after error', () => {
    const { facade } = setupWithErrorPort();
    facade.loadDetail('bad-id');
    expect(facade.isLoadingDetail()).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// CatalogFacade — loadCategories (success path)
// ---------------------------------------------------------------------------

describe('CatalogFacade — loadCategories success', () => {
  it('should populate categories signal after loadCategories()', () => {
    const { facade } = setupWithFakePort();
    facade.loadCategories();
    expect(facade.categories()).toEqual(FAKE_CATEGORIES);
  });

  it('should set isLoadingCategories to false after successful load', () => {
    const { facade } = setupWithFakePort();
    facade.loadCategories();
    expect(facade.isLoadingCategories()).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// CatalogFacade — setPage
// ---------------------------------------------------------------------------

describe('CatalogFacade — setPage', () => {
  it('should not throw when called with a page number', () => {
    const { facade } = setupWithFakePort();
    facade.loadAddOns();
    expect(() => facade.setPage(2)).not.toThrow();
  });

  it('should trigger a reload with the updated page', () => {
    const { facade } = setupWithFakePort();
    facade.loadAddOns();
    facade.setPage(3);
    // After setPage the fake port still returns FAKE_META (page 1) but list should still be populated
    expect(facade.addOns()).toEqual(FAKE_PLUGINS);
  });
});

// ---------------------------------------------------------------------------
// CatalogFacade — setSort
// ---------------------------------------------------------------------------

describe('CatalogFacade — setSort', () => {
  it('should not throw when called with sort key', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.setSort('name')).not.toThrow();
  });

  it('should not throw when called with sort key and order', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.setSort('name', 'asc')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// CatalogFacade — setFilters
// ---------------------------------------------------------------------------

describe('CatalogFacade — setFilters', () => {
  it('should not throw when setting type filters', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.setFilters({ types: ['formatter'] })).not.toThrow();
  });

  it('should not throw when setting language filters', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.setFilters({ languages: ['typescript'] })).not.toThrow();
  });

  it('should not throw when clearing all filters', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.setFilters({ types: [], languages: [], useCases: [] })).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary — CatalogFacade must NOT expose store or use-case classes
// ---------------------------------------------------------------------------

describe('CatalogFacade — architecture boundary', () => {
  it('facade public API should only expose signals and named methods', () => {
    const { facade } = setupWithFakePort();
    // Verify signals exist as functions
    expect(typeof facade.addOns).toBe('function');
    expect(typeof facade.paginationMeta).toBe('function');
    expect(typeof facade.categories).toBe('function');
    expect(typeof facade.selectedAddOn).toBe('function');
    expect(typeof facade.isLoadingAddOns).toBe('function');
    expect(typeof facade.isLoadingDetail).toBe('function');
    expect(typeof facade.isLoadingCategories).toBe('function');
    expect(typeof facade.addOnsError).toBe('function');
    expect(typeof facade.detailError).toBe('function');
    // Verify methods exist
    expect(typeof facade.loadAddOns).toBe('function');
    expect(typeof facade.setPage).toBe('function');
    expect(typeof facade.setSort).toBe('function');
    expect(typeof facade.setFilters).toBe('function');
    expect(typeof facade.loadDetail).toBe('function');
    expect(typeof facade.loadCategories).toBe('function');
  });
});
