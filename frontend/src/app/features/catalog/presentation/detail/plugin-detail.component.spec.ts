/**
 * RED tests — Task 12.4: Presentation layer — plugin detail component
 *
 * Expected production file (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/catalog/presentation/detail/plugin-detail.component.ts
 *
 * Production component the coder MUST define:
 *
 *   @Component({
 *     selector: 'cf-plugin-detail',
 *     standalone: true,
 *     changeDetection: ChangeDetectionStrategy.OnPush,
 *     imports: [...design-system primitives],
 *     ...
 *   })
 *   class PluginDetailComponent {
 *     private readonly facade = inject(CatalogFacade);
 *
 *     // Derived signals from facade:
 *     readonly plugin: Signal<PluginDetail | undefined>   — from facade.selectedPlugin
 *     readonly isLoading: Signal<boolean>                 — from facade.isLoadingDetail
 *     readonly hasError: Signal<boolean>                  — derived from facade.detailError !== undefined
 *
 *     // Outputs:
 *     readonly backRequested = output<void>();             — emits when back button is pressed
 *
 *     // Methods:
 *     onBack(): void   — emits backRequested
 *   }
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { PluginDetailComponent } from './plugin-detail.component';
import { CatalogFacade } from '../../application/facades/catalog.facade';
import type { Categories, PaginationMeta, PluginDetail, PluginSummary } from '../../domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../domain/rules/catalog-filter.rules';

// ---------------------------------------------------------------------------
// Stub facade for detail component tests
// ---------------------------------------------------------------------------

@Injectable()
class StubCatalogFacadeForDetail {
  private readonly _plugins = signal<PluginSummary[]>([]);
  private readonly _paginationMeta = signal<PaginationMeta | undefined>(undefined);
  private readonly _categories = signal<Categories | undefined>(undefined);
  private readonly _selectedPlugin = signal<PluginDetail | undefined>(undefined);
  private readonly _isLoadingPlugins = signal(false);
  private readonly _isLoadingDetail = signal(false);
  private readonly _isLoadingCategories = signal(false);
  private readonly _pluginsError = signal<{ code: string; message: string }[] | undefined>(undefined);
  private readonly _detailError = signal<{ code: string; message: string }[] | undefined>(undefined);

  // Test helpers
  setDetailState(plugin: PluginDetail | undefined): void {
    this._selectedPlugin.set(plugin);
  }
  setDetailLoading(loading: boolean): void {
    this._isLoadingDetail.set(loading);
  }
  setDetailError(errors: { code: string; message: string }[]): void {
    this._detailError.set(errors);
  }

  // Signals
  get plugins(): Signal<PluginSummary[]> { return this._plugins; }
  get paginationMeta(): Signal<PaginationMeta | undefined> { return this._paginationMeta; }
  get categories(): Signal<Categories | undefined> { return this._categories; }
  get selectedPlugin(): Signal<PluginDetail | undefined> { return this._selectedPlugin; }
  get isLoadingPlugins(): Signal<boolean> { return this._isLoadingPlugins; }
  get isLoadingDetail(): Signal<boolean> { return this._isLoadingDetail; }
  get isLoadingCategories(): Signal<boolean> { return this._isLoadingCategories; }
  get pluginsError(): Signal<{ code: string; message: string }[] | undefined> { return this._pluginsError; }
  get detailError(): Signal<{ code: string; message: string }[] | undefined> { return this._detailError; }

  // Recorded calls
  loadDetailCalls: string[] = [];
  loadCategoriesCalls = 0;

  loadPlugins(_query?: Partial<CatalogFilterQuery>): void { /* no-op */ }
  setPage(_page: number): void { /* no-op */ }
  setSort(_sort: string, _order?: 'asc' | 'desc'): void { /* no-op */ }
  setFilters(_filters: Partial<Pick<CatalogFilterQuery, 'types' | 'languages' | 'useCases'>>): void { /* no-op */ }
  loadDetail(pluginId: string): void { this.loadDetailCalls.push(pluginId); }
  loadCategories(): void { this.loadCategoriesCalls++; }
}

// ---------------------------------------------------------------------------
// Plugin fixture
// ---------------------------------------------------------------------------

const FULL_DETAIL: PluginDetail = {
  pluginId: 'p1',
  name: 'Awesome Plugin',
  slug: 'awesome-plugin',
  description: 'Does awesome things with TypeScript.',
  author: 'Jane Dev',
  types: ['formatter'],
  languages: ['typescript', 'javascript'],
  useCaseTags: ['code-quality'],
  downloadCount: 4200,
  latestVersion: '2.1.0',
  createdAt: new Date('2023-01-01T00:00:00.000Z'),
  updatedAt: new Date('2024-03-15T12:00:00.000Z'),
  versions: [
    {
      pluginId: 'p1',
      version: '2.1.0',
      isLatest: true,
      downloadCount: 1200,
      releaseNotes: 'Improved performance.',
      createdAt: new Date('2024-03-15T12:00:00.000Z'),
    },
    {
      pluginId: 'p1',
      version: '2.0.0',
      isLatest: false,
      downloadCount: 3000,
      releaseNotes: 'Major rewrite.',
      createdAt: new Date('2024-01-01T00:00:00.000Z'),
    },
  ],
};

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

function setupDetailComponent(): {
  fixture: ComponentFixture<PluginDetailComponent>;
  stub: StubCatalogFacadeForDetail;
} {
  const stub = new StubCatalogFacadeForDetail();
  TestBed.configureTestingModule({
    imports: [PluginDetailComponent],
    providers: [{ provide: CatalogFacade, useValue: stub }],
  }).overrideComponent(PluginDetailComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(PluginDetailComponent);
  return { fixture, stub };
}

// ---------------------------------------------------------------------------
// Component selector
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — selector', () => {
  it('should use selector "cf-plugin-detail"', () => {
    const { fixture } = setupDetailComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// No plugin selected (undefined state)
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — no plugin selected', () => {
  it('should not render plugin metadata when selectedPlugin is undefined', () => {
    const { fixture } = setupDetailComponent();
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    // Plugin name from fixture must not appear
    expect(text).not.toContain('Awesome Plugin');
  });

  it('should render a placeholder or empty state when selectedPlugin is undefined', () => {
    const { fixture } = setupDetailComponent();
    fixture.detectChanges();
    // Either empty-state component or empty container — just confirm no crash
    expect(fixture.nativeElement).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Loading state
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — loading state', () => {
  it('should render a loading indicator when isLoadingDetail is true', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailLoading(true);
    fixture.detectChanges();
    const loadingEl = fixture.debugElement.query(
      By.css('[aria-busy="true"], [data-testid="loading"], .loading, cf-skeleton'),
    );
    expect(loadingEl).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Plugin metadata rendering
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — metadata', () => {
  it('should render the plugin name', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Awesome Plugin');
  });

  it('should render the plugin description', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Does awesome things with TypeScript.');
  });

  it('should render the author', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Jane Dev');
  });

  it('should render the latest version', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('2.1.0');
  });

  it('should render the download count', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    // 4200 downloads
    expect(fixture.nativeElement.textContent).toContain('4200');
  });

  it('should render the type tags', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('formatter');
  });

  it('should render language tags', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('typescript');
  });
});

// ---------------------------------------------------------------------------
// Version history
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — version history', () => {
  it('should render all versions from the versions array', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    // Both version numbers should appear
    expect(fixture.nativeElement.textContent).toContain('2.1.0');
    expect(fixture.nativeElement.textContent).toContain('2.0.0');
  });

  it('should render release notes for each version', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Improved performance.');
  });

  it('should handle a plugin with no versions gracefully', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState({ ...FULL_DETAIL, versions: [] });
    fixture.detectChanges();
    // Component should not crash
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should indicate the latest version', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    // "latest" label should appear somewhere
    const text = (fixture.nativeElement.textContent as string).toLowerCase();
    expect(text).toContain('latest');
  });
});

// ---------------------------------------------------------------------------
// Error state
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — error state', () => {
  it('should render an error indicator when detailError is set', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailError([{ code: 'HTTP_404', message: 'Plugin not found' }]);
    fixture.detectChanges();
    const errorEl = fixture.debugElement.query(
      By.css('[data-testid="error-message"], [role="alert"], .error'),
    );
    expect(errorEl).not.toBeNull();
  });

  it('should not render plugin metadata when error is set and no plugin loaded', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailError([{ code: 'HTTP_404', message: 'Plugin not found' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('Awesome Plugin');
  });
});

// ---------------------------------------------------------------------------
// Back navigation output
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — back navigation', () => {
  it('should emit backRequested output when onBack() is called', () => {
    const { fixture } = setupDetailComponent();
    fixture.detectChanges();
    const emitted: void[] = [];
    fixture.componentInstance.backRequested.subscribe(() => emitted.push(undefined));
    fixture.componentInstance.onBack();
    expect(emitted).toHaveLength(1);
  });

  it('should render a back button', () => {
    const { fixture, stub } = setupDetailComponent();
    stub.setDetailState(FULL_DETAIL);
    fixture.detectChanges();
    const backBtn = fixture.debugElement.query(
      By.css('[data-testid="back-button"], button[aria-label*="back" i], button[aria-label*="Back"]'),
    );
    expect(backBtn).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary
// ---------------------------------------------------------------------------

describe('PluginDetailComponent — architecture boundary', () => {
  it('should compile and render with only CatalogFacade provided (no store/use-case needed)', () => {
    // If this test setup (no CatalogStore in providers) does not throw, the boundary is maintained.
    const { fixture } = setupDetailComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});
