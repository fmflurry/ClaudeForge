/**
 * RED tests — Task 17.3: DocsStore + DocsFacade
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/docs/application/store/docs.store.ts
 *   src/app/features/docs/application/facades/docs.facade.ts
 *
 * Production types/classes the coder MUST define:
 *
 *   // docs.store.ts
 *   enum DocsStoreEnum {
 *     SEARCH_RESULTS = 'SEARCH_RESULTS',
 *     CURRENT_DOC    = 'CURRENT_DOC',
 *     CATEGORY_TREE  = 'CATEGORY_TREE',
 *   }
 *
 *   interface DocsState {
 *     [DocsStoreEnum.SEARCH_RESULTS]: ResourceState<DocSearchResult[]>;
 *     [DocsStoreEnum.CURRENT_DOC]:    ResourceState<DocPage>;
 *     [DocsStoreEnum.CATEGORY_TREE]:  ResourceState<DocCategoryNode[]>;
 *   }
 *
 *   @Injectable({ providedIn: 'root' })
 *   class DocsStore extends BaseStore<typeof DocsStoreEnum, DocsState>
 *
 *   // docs.facade.ts
 *   @Injectable()
 *   class DocsFacade {
 *     // Signals (readonly, derived from store):
 *     get searchResults(): Signal<DocSearchResult[]>
 *     get categoryTree(): Signal<readonly DocCategoryNode[]>
 *     get currentDoc(): Signal<DocPage | undefined>
 *     get isLoadingSearch(): Signal<boolean>
 *     get isLoadingDoc(): Signal<boolean>
 *     get searchError(): Signal<{ code: string; message: string }[] | undefined>
 *     get docError(): Signal<{ code: string; message: string }[] | undefined>
 *
 *     // Methods:
 *     search(query: string): void
 *     openDoc(slug: string): void
 *     openPluginDoc(pluginSlug: string): void   — fetches 'plugin:{pluginSlug}'
 *   }
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { ResourceState } from '../../../../shared/application/store/resource-state.model';
import { DocsStore, DocsStoreEnum } from './docs.store';
import type { DocsState } from './docs.store';
import { DocsFacade } from '../facades/docs.facade';
import { DocsPort } from '../../domain/ports/docs.port';
import type { DocSearchResult, DocPage, DocCategoryNode } from '../../domain/models/docs.models';

// ---------------------------------------------------------------------------
// Fake DocsPort
// ---------------------------------------------------------------------------

const FAKE_SEARCH_RESULTS: DocSearchResult[] = [
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
    relevanceScore: 0.7,
  },
  {
    slug: 'api-reference',
    title: 'API Reference',
    category: 'API Reference',
    snippet: 'The plugin API...',
    relevanceScore: 0.5,
  },
];

const FAKE_DOC_PAGE: DocPage = {
  slug: 'getting-started',
  title: 'Getting Started',
  category: 'Getting Started',
  contentMarkdown: '# Getting Started\n\nWelcome to ClaudeForge.',
  lastUpdated: new Date('2024-03-01T10:00:00.000Z'),
};

const FAKE_PLUGIN_DOC_PAGE: DocPage = {
  slug: 'plugin:awesome-plugin',
  title: 'Awesome Plugin Docs',
  category: '',
  contentMarkdown: '# Awesome Plugin\n\nInstall with: claude plugin install awesome-plugin',
  lastUpdated: new Date('2024-04-01T00:00:00.000Z'),
};

const NOT_FOUND_PLACEHOLDER: DocPage = {
  slug: 'plugin:missing-plugin',
  title: 'No documentation available',
  category: '',
  contentMarkdown: '',
  lastUpdated: new Date(0),
};

interface SearchResult {
  items: DocSearchResult[];
  totalCount: number;
  page: number;
  limit: number;
  totalPages: number;
}

@Injectable()
class FakeDocsPort extends DocsPort {
  search(_query: string, _page?: number, _limit?: number): Observable<SearchResult> {
    return of({
      items: FAKE_SEARCH_RESULTS,
      totalCount: 3,
      page: 1,
      limit: 20,
      totalPages: 1,
    });
  }

  getPage(slug: string): Observable<DocPage> {
    if (slug === 'plugin:awesome-plugin') {
      return of(FAKE_PLUGIN_DOC_PAGE);
    }
    if (slug === 'plugin:missing-plugin') {
      return of(NOT_FOUND_PLACEHOLDER);
    }
    return of(FAKE_DOC_PAGE);
  }
}

@Injectable()
class ErrorDocsPort extends DocsPort {
  search(_query: string): Observable<SearchResult> {
    return throwError(() => new Error('Search network error'));
  }

  getPage(_slug: string): Observable<DocPage> {
    return throwError(() => new Error('Doc network error'));
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setupWithFakePort(): { store: DocsStore; facade: DocsFacade } {
  TestBed.configureTestingModule({
    providers: [
      DocsStore,
      DocsFacade,
      { provide: DocsPort, useClass: FakeDocsPort },
    ],
  });
  return {
    store: TestBed.inject(DocsStore),
    facade: TestBed.inject(DocsFacade),
  };
}

function setupWithErrorPort(): { store: DocsStore; facade: DocsFacade } {
  TestBed.configureTestingModule({
    providers: [
      DocsStore,
      DocsFacade,
      { provide: DocsPort, useClass: ErrorDocsPort },
    ],
  });
  return {
    store: TestBed.inject(DocsStore),
    facade: TestBed.inject(DocsFacade),
  };
}

// ---------------------------------------------------------------------------
// DocsStore — enum keys
// ---------------------------------------------------------------------------

describe('DocsStore — enum keys', () => {
  it('should have SEARCH_RESULTS key', () => {
    expect(DocsStoreEnum.SEARCH_RESULTS).toBe('SEARCH_RESULTS');
  });

  it('should have CURRENT_DOC key', () => {
    expect(DocsStoreEnum.CURRENT_DOC).toBe('CURRENT_DOC');
  });

  it('should have CATEGORY_TREE key', () => {
    expect(DocsStoreEnum.CATEGORY_TREE).toBe('CATEGORY_TREE');
  });
});

// ---------------------------------------------------------------------------
// DocsStore — initial state
// ---------------------------------------------------------------------------

describe('DocsStore — initial state', () => {
  it('should initialise SEARCH_RESULTS with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [DocsStore] });
    const store: DocsStore = TestBed.inject(DocsStore);
    const state: ResourceState<DocSearchResult[]> = store.get(DocsStoreEnum.SEARCH_RESULTS)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
  });

  it('should initialise CURRENT_DOC with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [DocsStore] });
    const store: DocsStore = TestBed.inject(DocsStore);
    const state: ResourceState<DocPage> = store.get(DocsStoreEnum.CURRENT_DOC)();
    expect(state.isLoading).toBeFalsy();
  });

  it('should initialise CATEGORY_TREE with empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [DocsStore] });
    const store: DocsStore = TestBed.inject(DocsStore);
    const state: ResourceState<DocCategoryNode[]> = store.get(DocsStoreEnum.CATEGORY_TREE)();
    expect(state.isLoading).toBeFalsy();
  });

  it('should be an instance of DocsStore', () => {
    TestBed.configureTestingModule({ providers: [DocsStore] });
    const store: DocsStore = TestBed.inject(DocsStore);
    expect(store).toBeInstanceOf(DocsStore);
  });

  it('SEARCH_RESULTS state type should accept ResourceState<DocSearchResult[]>', () => {
    TestBed.configureTestingModule({ providers: [DocsStore] });
    const store: DocsStore = TestBed.inject(DocsStore);
    const partial: Partial<DocsState[typeof DocsStoreEnum.SEARCH_RESULTS]> = {
      data: FAKE_SEARCH_RESULTS,
      status: 'Success',
    };
    store.update(DocsStoreEnum.SEARCH_RESULTS, partial);
    expect(store.get(DocsStoreEnum.SEARCH_RESULTS)().status).toBe('Success');
  });
});

// ---------------------------------------------------------------------------
// DocsFacade — initial signal values
// ---------------------------------------------------------------------------

describe('DocsFacade — initial signal values', () => {
  it('searchResults should return empty array before any search', () => {
    const { facade } = setupWithFakePort();
    expect(facade.searchResults()).toEqual([]);
  });

  it('categoryTree should return empty array before any search', () => {
    const { facade } = setupWithFakePort();
    const tree = facade.categoryTree();
    expect(Array.isArray(tree)).toBe(true);
    expect(tree.length).toBe(0);
  });

  it('currentDoc should return undefined before any openDoc call', () => {
    const { facade } = setupWithFakePort();
    expect(facade.currentDoc()).toBeUndefined();
  });

  it('isLoadingSearch should return false initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.isLoadingSearch()).toBe(false);
  });

  it('isLoadingDoc should return false initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.isLoadingDoc()).toBe(false);
  });

  it('searchError should return undefined initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.searchError()).toBeUndefined();
  });

  it('docError should return undefined initially', () => {
    const { facade } = setupWithFakePort();
    expect(facade.docError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// DocsFacade — search() success
// ---------------------------------------------------------------------------

describe('DocsFacade — search() success', () => {
  it('should populate searchResults after search()', () => {
    const { facade } = setupWithFakePort();
    facade.search('install');
    expect(facade.searchResults()).toEqual(FAKE_SEARCH_RESULTS);
  });

  it('should populate categoryTree after search()', () => {
    const { facade } = setupWithFakePort();
    facade.search('install');
    const tree = facade.categoryTree();
    expect(tree.length).toBeGreaterThan(0);
  });

  it('should group search results into category nodes', () => {
    const { facade } = setupWithFakePort();
    facade.search('install');
    const tree: readonly DocCategoryNode[] = facade.categoryTree();
    const categories: string[] = tree.map((n: DocCategoryNode) => n.category);
    expect(categories).toContain('Getting Started');
    expect(categories).toContain('API Reference');
  });

  it('should set isLoadingSearch to false after successful search', () => {
    const { facade } = setupWithFakePort();
    facade.search('install');
    expect(facade.isLoadingSearch()).toBe(false);
  });

  it('should clear searchError after successful search', () => {
    const { facade } = setupWithFakePort();
    facade.search('install');
    expect(facade.searchError()).toBeUndefined();
  });

  it('should not throw when called with any string', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.search('any query string')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// DocsFacade — search() error
// ---------------------------------------------------------------------------

describe('DocsFacade — search() error', () => {
  it('should set searchError when port throws', () => {
    const { facade } = setupWithErrorPort();
    facade.search('install');
    expect(facade.searchError()).toBeDefined();
    expect(Array.isArray(facade.searchError())).toBe(true);
  });

  it('should not throw when port throws', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.search('install')).not.toThrow();
  });

  it('should set isLoadingSearch to false after error', () => {
    const { facade } = setupWithErrorPort();
    facade.search('install');
    expect(facade.isLoadingSearch()).toBe(false);
  });

  it('should leave searchResults as empty array after error', () => {
    const { facade } = setupWithErrorPort();
    facade.search('install');
    expect(Array.isArray(facade.searchResults())).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// DocsFacade — openDoc() success
// ---------------------------------------------------------------------------

describe('DocsFacade — openDoc() success', () => {
  it('should populate currentDoc after openDoc()', () => {
    const { facade } = setupWithFakePort();
    facade.openDoc('getting-started');
    expect(facade.currentDoc()).toBeDefined();
    expect(facade.currentDoc()!.slug).toBe('getting-started');
  });

  it('should set the contentMarkdown on currentDoc', () => {
    const { facade } = setupWithFakePort();
    facade.openDoc('getting-started');
    expect(typeof facade.currentDoc()!.contentMarkdown).toBe('string');
  });

  it('should set isLoadingDoc to false after successful openDoc', () => {
    const { facade } = setupWithFakePort();
    facade.openDoc('getting-started');
    expect(facade.isLoadingDoc()).toBe(false);
  });

  it('should clear docError after successful openDoc', () => {
    const { facade } = setupWithFakePort();
    facade.openDoc('getting-started');
    expect(facade.docError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// DocsFacade — openDoc() missing/placeholder
// ---------------------------------------------------------------------------

describe('DocsFacade — openDoc() missing doc placeholder', () => {
  it('should not throw when doc is not found (adapter returns placeholder)', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.openDoc('plugin:missing-plugin')).not.toThrow();
  });

  it('should set currentDoc to placeholder when doc is not found', () => {
    const { facade } = setupWithFakePort();
    facade.openDoc('plugin:missing-plugin');
    expect(facade.currentDoc()).toBeDefined();
    expect(facade.currentDoc()!.contentMarkdown).toBe('');
  });

  it('should have no docError when adapter handles 404 gracefully', () => {
    const { facade } = setupWithFakePort();
    facade.openDoc('plugin:missing-plugin');
    expect(facade.docError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// DocsFacade — openDoc() error (hard network error, not 404)
// ---------------------------------------------------------------------------

describe('DocsFacade — openDoc() error', () => {
  it('should set docError when port throws a hard error', () => {
    const { facade } = setupWithErrorPort();
    facade.openDoc('any-slug');
    expect(facade.docError()).toBeDefined();
  });

  it('should not throw when port throws on openDoc', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.openDoc('any-slug')).not.toThrow();
  });

  it('should set isLoadingDoc to false after error', () => {
    const { facade } = setupWithErrorPort();
    facade.openDoc('any-slug');
    expect(facade.isLoadingDoc()).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// DocsFacade — openPluginDoc()
// ---------------------------------------------------------------------------

describe('DocsFacade — openPluginDoc()', () => {
  it('should fetch the plugin doc using the "plugin:{slug}" convention', () => {
    const { facade } = setupWithFakePort();
    facade.openPluginDoc('awesome-plugin');
    expect(facade.currentDoc()).toBeDefined();
    expect(facade.currentDoc()!.contentMarkdown).toBe(
      '# Awesome Plugin\n\nInstall with: claude plugin install awesome-plugin',
    );
  });

  it('should set currentDoc with the plugin doc slug', () => {
    const { facade } = setupWithFakePort();
    facade.openPluginDoc('awesome-plugin');
    expect(facade.currentDoc()!.slug).toBe('plugin:awesome-plugin');
  });

  it('should not throw when called with any plugin slug', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.openPluginDoc('any-plugin')).not.toThrow();
  });

  it('should populate placeholder when plugin doc is not found', () => {
    const { facade } = setupWithFakePort();
    facade.openPluginDoc('missing-plugin');
    expect(facade.currentDoc()).toBeDefined();
    expect(facade.currentDoc()!.contentMarkdown).toBe('');
  });

  it('should not throw when plugin port throws a hard error', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.openPluginDoc('any-plugin')).not.toThrow();
  });

  it('should set docError when hard network error occurs on openPluginDoc', () => {
    const { facade } = setupWithErrorPort();
    facade.openPluginDoc('any-plugin');
    expect(facade.docError()).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// DocsFacade — architecture boundary
// ---------------------------------------------------------------------------

describe('DocsFacade — architecture boundary', () => {
  it('should expose only signals and named methods in public API', () => {
    const { facade } = setupWithFakePort();
    expect(typeof facade.searchResults).toBe('function');
    expect(typeof facade.categoryTree).toBe('function');
    expect(typeof facade.currentDoc).toBe('function');
    expect(typeof facade.isLoadingSearch).toBe('function');
    expect(typeof facade.isLoadingDoc).toBe('function');
    expect(typeof facade.searchError).toBe('function');
    expect(typeof facade.docError).toBe('function');
    expect(typeof facade.search).toBe('function');
    expect(typeof facade.openDoc).toBe('function');
    expect(typeof facade.openPluginDoc).toBe('function');
  });
});
