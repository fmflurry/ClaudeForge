/**
 * DocsPageComponent — render + wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { DocsPageComponent } from './docs-page.component';
import { DocsFacade } from '../application/facades/docs.facade';
import type { DocCategoryNode, DocPage, DocSearchResult } from '../domain/models/docs.models';

// ---------------------------------------------------------------------------
// Stub facade
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

  openDocCalls: string[] = [];

  get searchResults(): Signal<DocSearchResult[]> { return this._searchResults; }
  get categoryTree(): Signal<readonly DocCategoryNode[]> { return this._categoryTree; }
  get currentDoc(): Signal<DocPage | undefined> { return this._currentDoc; }
  get isLoadingSearch(): Signal<boolean> { return this._isLoadingSearch; }
  get isLoadingDoc(): Signal<boolean> { return this._isLoadingDoc; }
  get searchError(): Signal<{ code: string; message: string }[] | undefined> { return this._searchError; }
  get docError(): Signal<{ code: string; message: string }[] | undefined> { return this._docError; }

  search(_q: string): void { /* no-op */ }
  openDoc(slug: string): void { this.openDocCalls.push(slug); }
  openPluginDoc(_slug: string): void { /* no-op */ }
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(): { fixture: ComponentFixture<DocsPageComponent>; stub: StubDocsFacade } {
  const stub = new StubDocsFacade();
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [DocsPageComponent],
    providers: [{ provide: DocsFacade, useValue: stub }],
  }).overrideComponent(DocsPageComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(DocsPageComponent);
  return { fixture, stub };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('DocsPageComponent — render', () => {
  it('should instantiate', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should render the docs layout', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.docs-layout')).not.toBeNull();
  });

  it('should render the docs sidebar', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.docs-sidebar')).not.toBeNull();
  });

  it('should render cf-docs-search component', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-docs-search')).not.toBeNull();
  });

  it('should render cf-docs-tree component', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-docs-tree')).not.toBeNull();
  });

  it('should render cf-doc-viewer component', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('cf-doc-viewer')).not.toBeNull();
  });
});

describe('DocsPageComponent — onDocSelected', () => {
  it('should not throw when onDocSelected is called', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    expect(() => fixture.componentInstance.onDocSelected('some-slug')).not.toThrow();
  });
});
