/**
 * RED tests — Task 17.4: Docs presentation layer components
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/docs/presentation/tree/docs-tree.component.ts
 *   src/app/features/docs/presentation/viewer/doc-viewer.component.ts
 *   src/app/features/docs/presentation/search/docs-search.component.ts
 *   src/app/features/docs/presentation/plugin-docs-tab/plugin-docs-tab.component.ts
 *
 * Selectors (coder MUST match exactly):
 *   cf-docs-tree            — sidebar category tree
 *   cf-doc-viewer           — doc viewer (renders markdown as preformatted/escaped text, NOT a heavy lib)
 *   cf-docs-search          — search box + ranked result list
 *   cf-plugin-docs-tab      — plugin "Docs" tab content
 *
 * Markdown rendering approach (MVP):
 *   Render contentMarkdown as plain preformatted text inside a <pre> element OR escaped HTML in a <div>.
 *   Do NOT add a heavy markdown parsing library (e.g. marked, showdown, ngx-markdown).
 *   Raw/preformatted rendering is acceptable for MVP.
 *   Components may use Angular's `innerHTML` with escaped content or simply render in a <pre> tag.
 *
 * All components:
 *   - standalone: true
 *   - ChangeDetectionStrategy.OnPush
 *   - inject DocsFacade only (no store, no use-case injection)
 *   - use inject() (no constructor params)
 *
 * Component inputs/outputs (coder MUST match):
 *
 *   DocsTreeComponent:
 *     // Reads facade.categoryTree() and facade.searchResults()
 *     // Emits when user selects a doc
 *     readonly docSelected = output<string>();   // emits slug
 *     selectDoc(slug: string): void              // calls facade.openDoc(slug) + emits docSelected
 *
 *   DocViewerComponent:
 *     // Reads facade.currentDoc(), facade.isLoadingDoc(), facade.docError()
 *     // Shows missing-doc placeholder when currentDoc is undefined or contentMarkdown is empty
 *     // No outputs — navigation/selection is done via tree/search
 *
 *   DocsSearchComponent:
 *     // Reads facade.searchResults(), facade.isLoadingSearch(), facade.searchError()
 *     // Emits when user selects a result
 *     readonly docSelected = output<string>();   // emits slug
 *     onSearch(query: string): void              // calls facade.search(query)
 *     selectResult(slug: string): void           // calls facade.openDoc(slug) + emits docSelected
 *
 *   PluginDocsTabComponent:
 *     // Reads facade.currentDoc(), facade.isLoadingDoc(), facade.docError()
 *     // input: pluginSlug = input<string>()     — triggers openPluginDoc on change
 *     // Shows missing-doc placeholder when no docs available
 *     readonly pluginSlug = input<string>();     // when set, calls facade.openPluginDoc(pluginSlug)
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { DocsTreeComponent } from './tree/docs-tree.component';
import { DocViewerComponent } from './viewer/doc-viewer.component';
import { DocsSearchComponent } from './search/docs-search.component';
import { PluginDocsTabComponent } from './plugin-docs-tab/plugin-docs-tab.component';
import { DocsFacade } from '../application/facades/docs.facade';
import type { DocSearchResult, DocPage, DocCategoryNode } from '../domain/models/docs.models';

// ---------------------------------------------------------------------------
// Stub DocsFacade
// ---------------------------------------------------------------------------

@Injectable()
class StubDocsFacade {
  private readonly _searchResults = signal<DocSearchResult[]>([]);
  private readonly _categoryTree = signal<readonly DocCategoryNode[]>([]);
  private readonly _currentDoc = signal<DocPage | undefined>(undefined);
  private readonly _isLoadingSearch = signal(false);
  private readonly _isLoadingDoc = signal(false);
  private readonly _searchError = signal<{ code: string; message: string }[] | undefined>(undefined);
  private readonly _docError = signal<{ code: string; message: string }[] | undefined>(undefined);

  // Test helpers
  setSearchResults(results: DocSearchResult[]): void {
    this._searchResults.set(results);
    this._categoryTree.set(this.buildTree(results));
  }
  setCurrentDoc(doc: DocPage | undefined): void {
    this._currentDoc.set(doc);
  }
  setSearchLoading(loading: boolean): void {
    this._isLoadingSearch.set(loading);
  }
  setDocLoading(loading: boolean): void {
    this._isLoadingDoc.set(loading);
  }
  setSearchError(errors: { code: string; message: string }[]): void {
    this._searchError.set(errors);
  }
  setDocError(errors: { code: string; message: string }[]): void {
    this._docError.set(errors);
  }

  private buildTree(results: DocSearchResult[]): readonly DocCategoryNode[] {
    const map = new Map<string, DocSearchResult[]>();
    for (const r of results) {
      const existing = map.get(r.category) ?? [];
      map.set(r.category, [...existing, r]);
    }
    return Array.from(map.entries()).map(([category, docs]) => ({ category, docs }));
  }

  // Facade signal getters
  get searchResults(): Signal<DocSearchResult[]> { return this._searchResults; }
  get categoryTree(): Signal<readonly DocCategoryNode[]> { return this._categoryTree; }
  get currentDoc(): Signal<DocPage | undefined> { return this._currentDoc; }
  get isLoadingSearch(): Signal<boolean> { return this._isLoadingSearch; }
  get isLoadingDoc(): Signal<boolean> { return this._isLoadingDoc; }
  get searchError(): Signal<{ code: string; message: string }[] | undefined> { return this._searchError; }
  get docError(): Signal<{ code: string; message: string }[] | undefined> { return this._docError; }

  // Recorded calls
  searchCalls: string[] = [];
  openDocCalls: string[] = [];
  openPluginDocCalls: string[] = [];

  search(query: string): void { this.searchCalls.push(query); }
  openDoc(slug: string): void { this.openDocCalls.push(slug); }
  openPluginDoc(pluginSlug: string): void { this.openPluginDocCalls.push(pluginSlug); }
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const GETTING_STARTED_RESULT: DocSearchResult = {
  slug: 'getting-started',
  title: 'Getting Started',
  category: 'Getting Started',
  snippet: 'Welcome to ClaudeForge.',
  relevanceScore: 0.9,
};

const INSTALL_CLI_RESULT: DocSearchResult = {
  slug: 'install-cli',
  title: 'Install via CLI',
  category: 'Getting Started',
  snippet: 'Run the install command with the plugin name.',
  relevanceScore: 0.7,
};

const API_REF_RESULT: DocSearchResult = {
  slug: 'api-reference',
  title: 'API Reference',
  category: 'API Reference',
  snippet: 'The plugin API exposes hooks.',
  relevanceScore: 0.5,
};

const FULL_DOC_PAGE: DocPage = {
  slug: 'getting-started',
  title: 'Getting Started',
  category: 'Getting Started',
  contentMarkdown: '# Getting Started\n\nWelcome to ClaudeForge. Install plugins easily.',
  lastUpdated: new Date('2024-03-01T10:00:00.000Z'),
};

const MISSING_DOC_PLACEHOLDER: DocPage = {
  slug: 'plugin:no-readme',
  title: 'No documentation available',
  category: '',
  contentMarkdown: '',
  lastUpdated: new Date(0),
};

const PLUGIN_DOC_PAGE: DocPage = {
  slug: 'plugin:awesome-plugin',
  title: 'Awesome Plugin',
  category: '',
  contentMarkdown: '# Awesome Plugin\n\nInstall with: claude plugin install awesome-plugin',
  lastUpdated: new Date('2024-04-01T00:00:00.000Z'),
};

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setupDocsTree(): { fixture: ComponentFixture<DocsTreeComponent>; stub: StubDocsFacade } {
  const stub = new StubDocsFacade();
  TestBed.configureTestingModule({
    imports: [DocsTreeComponent],
    providers: [{ provide: DocsFacade, useValue: stub }],
  }).overrideComponent(DocsTreeComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(DocsTreeComponent);
  return { fixture, stub };
}

function setupDocViewer(): { fixture: ComponentFixture<DocViewerComponent>; stub: StubDocsFacade } {
  const stub = new StubDocsFacade();
  TestBed.configureTestingModule({
    imports: [DocViewerComponent],
    providers: [{ provide: DocsFacade, useValue: stub }],
  }).overrideComponent(DocViewerComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(DocViewerComponent);
  return { fixture, stub };
}

function setupDocsSearch(): { fixture: ComponentFixture<DocsSearchComponent>; stub: StubDocsFacade } {
  const stub = new StubDocsFacade();
  TestBed.configureTestingModule({
    imports: [DocsSearchComponent],
    providers: [{ provide: DocsFacade, useValue: stub }],
  }).overrideComponent(DocsSearchComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(DocsSearchComponent);
  return { fixture, stub };
}

function setupPluginDocsTab(): {
  fixture: ComponentFixture<PluginDocsTabComponent>;
  stub: StubDocsFacade;
} {
  const stub = new StubDocsFacade();
  TestBed.configureTestingModule({
    imports: [PluginDocsTabComponent],
    providers: [{ provide: DocsFacade, useValue: stub }],
  }).overrideComponent(PluginDocsTabComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(PluginDocsTabComponent);
  return { fixture, stub };
}

// ===========================================================================
// DocsTreeComponent (cf-docs-tree)
// ===========================================================================

describe('DocsTreeComponent — selector', () => {
  it('should instantiate cf-docs-tree', () => {
    const { fixture } = setupDocsTree();
    expect(fixture.componentInstance).toBeDefined();
  });
});

describe('DocsTreeComponent — empty state', () => {
  it('should render without error when categoryTree is empty', () => {
    const { fixture } = setupDocsTree();
    fixture.detectChanges();
    expect(fixture.nativeElement).toBeDefined();
  });

  it('should not render any category heading when tree is empty', () => {
    const { fixture } = setupDocsTree();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Getting Started');
  });
});

describe('DocsTreeComponent — category rendering', () => {
  it('should render each category from the tree', () => {
    const { fixture, stub } = setupDocsTree();
    stub.setSearchResults([GETTING_STARTED_RESULT, INSTALL_CLI_RESULT, API_REF_RESULT]);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Getting Started');
    expect(text).toContain('API Reference');
  });

  it('should render doc titles within each category', () => {
    const { fixture, stub } = setupDocsTree();
    stub.setSearchResults([GETTING_STARTED_RESULT, INSTALL_CLI_RESULT]);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Getting Started');
    expect(text).toContain('Install via CLI');
  });

  it('should render a clickable element for each doc in the tree', () => {
    const { fixture, stub } = setupDocsTree();
    stub.setSearchResults([GETTING_STARTED_RESULT, INSTALL_CLI_RESULT, API_REF_RESULT]);
    fixture.detectChanges();
    const links = fixture.debugElement.queryAll(
      By.css('a, button, [role="link"], [data-testid^="doc-link"]'),
    );
    expect(links.length).toBeGreaterThan(0);
  });
});

describe('DocsTreeComponent — doc selection', () => {
  it('should call facade.openDoc when selectDoc is called', () => {
    const { fixture, stub } = setupDocsTree();
    stub.setSearchResults([GETTING_STARTED_RESULT]);
    fixture.detectChanges();
    fixture.componentInstance.selectDoc('getting-started');
    expect(stub.openDocCalls).toContain('getting-started');
  });

  it('should emit docSelected output when selectDoc is called', () => {
    const { fixture } = setupDocsTree();
    fixture.detectChanges();
    const emitted: string[] = [];
    fixture.componentInstance.docSelected.subscribe((slug: string) => emitted.push(slug));
    fixture.componentInstance.selectDoc('getting-started');
    expect(emitted).toContain('getting-started');
  });

  it('should emit the slug of the selected doc', () => {
    const { fixture } = setupDocsTree();
    fixture.detectChanges();
    const emitted: string[] = [];
    fixture.componentInstance.docSelected.subscribe((slug: string) => emitted.push(slug));
    fixture.componentInstance.selectDoc('api-reference');
    expect(emitted[0]).toBe('api-reference');
  });
});

describe('DocsTreeComponent — architecture boundary', () => {
  it('should compile with only DocsFacade provided (no store injection needed)', () => {
    const { fixture } = setupDocsTree();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ===========================================================================
// DocViewerComponent (cf-doc-viewer)
// ===========================================================================

describe('DocViewerComponent — selector', () => {
  it('should instantiate cf-doc-viewer', () => {
    const { fixture } = setupDocViewer();
    expect(fixture.componentInstance).toBeDefined();
  });
});

describe('DocViewerComponent — no doc loaded', () => {
  it('should show a missing-doc placeholder when currentDoc is undefined', () => {
    const { fixture } = setupDocViewer();
    fixture.detectChanges();
    // Either a placeholder element or the text "No documentation available"
    const text = fixture.nativeElement.textContent as string;
    const hasPlaceholder =
      text.toLowerCase().includes('no documentation') ||
      text.toLowerCase().includes('no doc') ||
      fixture.debugElement.query(By.css('[data-testid="missing-doc"], cf-empty-state')) !== null;
    expect(hasPlaceholder).toBe(true);
  });

  it('should NOT render markdown content area when currentDoc is undefined', () => {
    const { fixture } = setupDocViewer();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Getting Started');
  });
});

describe('DocViewerComponent — loading state', () => {
  it('should render a loading indicator when isLoadingDoc is true', () => {
    const { fixture, stub } = setupDocViewer();
    stub.setDocLoading(true);
    fixture.detectChanges();
    const loadingEl = fixture.debugElement.query(
      By.css('[aria-busy="true"], [data-testid="loading"], .loading, cf-skeleton'),
    );
    expect(loadingEl).not.toBeNull();
  });
});

describe('DocViewerComponent — doc content rendering', () => {
  it('should render the doc title', () => {
    const { fixture, stub } = setupDocViewer();
    stub.setCurrentDoc(FULL_DOC_PAGE);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Getting Started');
  });

  it('should render the contentMarkdown in some container', () => {
    const { fixture, stub } = setupDocViewer();
    stub.setCurrentDoc(FULL_DOC_PAGE);
    fixture.detectChanges();
    // The raw markdown text should appear (preformatted or escaped)
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Welcome to ClaudeForge');
  });

  it('should render lastUpdated date', () => {
    const { fixture, stub } = setupDocViewer();
    stub.setCurrentDoc(FULL_DOC_PAGE);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    // Date should appear in some human-readable format
    expect(text).toMatch(/2024/);
  });

  it('should use a <pre> or similar element for markdown rendering (MVP approach)', () => {
    const { fixture, stub } = setupDocViewer();
    stub.setCurrentDoc(FULL_DOC_PAGE);
    fixture.detectChanges();
    // pre, code, or a data-testid="markdown-content" container
    const contentEl = fixture.debugElement.query(
      By.css('pre, code, [data-testid="markdown-content"], [data-testid="doc-content"]'),
    );
    expect(contentEl).not.toBeNull();
  });
});

describe('DocViewerComponent — missing-doc placeholder', () => {
  it('should show missing-doc placeholder when contentMarkdown is empty', () => {
    const { fixture, stub } = setupDocViewer();
    stub.setCurrentDoc(MISSING_DOC_PLACEHOLDER);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    const hasPlaceholder =
      text.toLowerCase().includes('no documentation') ||
      fixture.debugElement.query(By.css('[data-testid="missing-doc"], cf-empty-state')) !== null;
    expect(hasPlaceholder).toBe(true);
  });

  it('should not crash when currentDoc has empty contentMarkdown', () => {
    const { fixture, stub } = setupDocViewer();
    stub.setCurrentDoc(MISSING_DOC_PLACEHOLDER);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeDefined();
  });
});

describe('DocViewerComponent — error state', () => {
  it('should render an error indicator when docError is set', () => {
    const { fixture, stub } = setupDocViewer();
    stub.setDocError([{ code: 'HTTP_500', message: 'Server error' }]);
    fixture.detectChanges();
    const errorEl = fixture.debugElement.query(
      By.css('[data-testid="error-message"], [role="alert"], .error'),
    );
    expect(errorEl).not.toBeNull();
  });
});

describe('DocViewerComponent — architecture boundary', () => {
  it('should compile with only DocsFacade provided', () => {
    const { fixture } = setupDocViewer();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ===========================================================================
// DocsSearchComponent (cf-docs-search)
// ===========================================================================

describe('DocsSearchComponent — selector', () => {
  it('should instantiate cf-docs-search', () => {
    const { fixture } = setupDocsSearch();
    expect(fixture.componentInstance).toBeDefined();
  });
});

describe('DocsSearchComponent — search input', () => {
  it('should render a search input element', () => {
    const { fixture } = setupDocsSearch();
    fixture.detectChanges();
    const inputEl = fixture.debugElement.query(
      By.css('input[type="search"], input[type="text"], input[placeholder], [data-testid="search-input"]'),
    );
    expect(inputEl).not.toBeNull();
  });

  it('should call facade.search when onSearch is invoked', () => {
    const { fixture, stub } = setupDocsSearch();
    fixture.detectChanges();
    fixture.componentInstance.onSearch('install');
    expect(stub.searchCalls).toContain('install');
  });

  it('should not throw when onSearch is called with an empty string', () => {
    const { fixture } = setupDocsSearch();
    fixture.detectChanges();
    expect(() => fixture.componentInstance.onSearch('')).not.toThrow();
  });
});

describe('DocsSearchComponent — empty results', () => {
  it('should render without error when searchResults is empty', () => {
    const { fixture } = setupDocsSearch();
    fixture.detectChanges();
    expect(fixture.nativeElement).toBeDefined();
  });
});

describe('DocsSearchComponent — loading state', () => {
  it('should render a loading indicator when isLoadingSearch is true', () => {
    const { fixture, stub } = setupDocsSearch();
    stub.setSearchLoading(true);
    fixture.detectChanges();
    const loadingEl = fixture.debugElement.query(
      By.css('[aria-busy="true"], [data-testid="loading"], .loading, cf-skeleton'),
    );
    expect(loadingEl).not.toBeNull();
  });
});

describe('DocsSearchComponent — results rendering', () => {
  it('should render each search result title', () => {
    const { fixture, stub } = setupDocsSearch();
    stub.setSearchResults([GETTING_STARTED_RESULT, INSTALL_CLI_RESULT, API_REF_RESULT]);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Getting Started');
    expect(text).toContain('Install via CLI');
    expect(text).toContain('API Reference');
  });

  it('should render snippets for each result', () => {
    const { fixture, stub } = setupDocsSearch();
    stub.setSearchResults([GETTING_STARTED_RESULT]);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Welcome to ClaudeForge');
  });

  it('should render relevanceScore or a visual indicator for each result', () => {
    const { fixture, stub } = setupDocsSearch();
    stub.setSearchResults([GETTING_STARTED_RESULT]);
    fixture.detectChanges();
    // Either numeric score OR a visual badge/indicator element
    const text = fixture.nativeElement.textContent as string;
    const hasScore = text.includes('0.9') ||
      fixture.debugElement.query(By.css('[data-testid="relevance-score"], .relevance, cf-badge')) !== null;
    expect(hasScore).toBe(true);
  });
});

describe('DocsSearchComponent — result selection', () => {
  it('should call facade.openDoc when selectResult is called', () => {
    const { fixture, stub } = setupDocsSearch();
    stub.setSearchResults([GETTING_STARTED_RESULT]);
    fixture.detectChanges();
    fixture.componentInstance.selectResult('getting-started');
    expect(stub.openDocCalls).toContain('getting-started');
  });

  it('should emit docSelected output when selectResult is called', () => {
    const { fixture } = setupDocsSearch();
    fixture.detectChanges();
    const emitted: string[] = [];
    fixture.componentInstance.docSelected.subscribe((slug: string) => emitted.push(slug));
    fixture.componentInstance.selectResult('getting-started');
    expect(emitted).toContain('getting-started');
  });
});

describe('DocsSearchComponent — error state', () => {
  it('should render an error indicator when searchError is set', () => {
    const { fixture, stub } = setupDocsSearch();
    stub.setSearchError([{ code: 'SEARCH_ERROR', message: 'Search failed' }]);
    fixture.detectChanges();
    const errorEl = fixture.debugElement.query(
      By.css('[data-testid="error-message"], [role="alert"], .error'),
    );
    expect(errorEl).not.toBeNull();
  });
});

describe('DocsSearchComponent — architecture boundary', () => {
  it('should compile with only DocsFacade provided', () => {
    const { fixture } = setupDocsSearch();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ===========================================================================
// PluginDocsTabComponent (cf-plugin-docs-tab)
// ===========================================================================

describe('PluginDocsTabComponent — selector', () => {
  it('should instantiate cf-plugin-docs-tab', () => {
    const { fixture } = setupPluginDocsTab();
    expect(fixture.componentInstance).toBeDefined();
  });
});

describe('PluginDocsTabComponent — loading state', () => {
  it('should render a loading indicator when isLoadingDoc is true', () => {
    const { fixture, stub } = setupPluginDocsTab();
    stub.setDocLoading(true);
    fixture.detectChanges();
    const loadingEl = fixture.debugElement.query(
      By.css('[aria-busy="true"], [data-testid="loading"], .loading, cf-skeleton'),
    );
    expect(loadingEl).not.toBeNull();
  });
});

describe('PluginDocsTabComponent — missing doc placeholder', () => {
  it('should show a missing-doc placeholder when no doc is loaded', () => {
    const { fixture } = setupPluginDocsTab();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    const hasPlaceholder =
      text.toLowerCase().includes('no documentation') ||
      fixture.debugElement.query(By.css('[data-testid="missing-doc"], cf-empty-state')) !== null;
    expect(hasPlaceholder).toBe(true);
  });

  it('should show placeholder when contentMarkdown is empty', () => {
    const { fixture, stub } = setupPluginDocsTab();
    stub.setCurrentDoc(MISSING_DOC_PLACEHOLDER);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    const hasPlaceholder =
      text.toLowerCase().includes('no documentation') ||
      fixture.debugElement.query(By.css('[data-testid="missing-doc"], cf-empty-state')) !== null;
    expect(hasPlaceholder).toBe(true);
  });

  it('should not crash when doc has no content', () => {
    const { fixture, stub } = setupPluginDocsTab();
    stub.setCurrentDoc(MISSING_DOC_PLACEHOLDER);
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeDefined();
  });
});

describe('PluginDocsTabComponent — plugin doc rendering', () => {
  it('should render the plugin doc title', () => {
    const { fixture, stub } = setupPluginDocsTab();
    stub.setCurrentDoc(PLUGIN_DOC_PAGE);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Awesome Plugin');
  });

  it('should render the plugin contentMarkdown', () => {
    const { fixture, stub } = setupPluginDocsTab();
    stub.setCurrentDoc(PLUGIN_DOC_PAGE);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Awesome Plugin');
  });

  it('should render lastUpdated date when doc is available', () => {
    const { fixture, stub } = setupPluginDocsTab();
    stub.setCurrentDoc(PLUGIN_DOC_PAGE);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toMatch(/2024/);
  });
});

describe('PluginDocsTabComponent — pluginSlug input', () => {
  it('should have a pluginSlug input property', () => {
    const { fixture } = setupPluginDocsTab();
    expect('pluginSlug' in fixture.componentInstance).toBe(true);
  });
});

describe('PluginDocsTabComponent — error state', () => {
  it('should render an error indicator when docError is set', () => {
    const { fixture, stub } = setupPluginDocsTab();
    stub.setDocError([{ code: 'HTTP_500', message: 'Failed to load plugin docs' }]);
    fixture.detectChanges();
    const errorEl = fixture.debugElement.query(
      By.css('[data-testid="error-message"], [role="alert"], .error'),
    );
    expect(errorEl).not.toBeNull();
  });

  it('should not render plugin content when error is set and no doc loaded', () => {
    const { fixture, stub } = setupPluginDocsTab();
    stub.setDocError([{ code: 'HTTP_500', message: 'Error' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Awesome Plugin');
  });
});

describe('PluginDocsTabComponent — architecture boundary', () => {
  it('should compile with only DocsFacade provided (no store/use-case injection)', () => {
    const { fixture } = setupPluginDocsTab();
    expect(fixture.componentInstance).toBeDefined();
  });
});
