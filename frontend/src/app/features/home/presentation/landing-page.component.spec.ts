import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Injectable, Signal, signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { LandingPageComponent } from './landing-page.component';
import { CatalogFacade } from '../../catalog/application/facades/catalog.facade';
import type { PluginSummary } from '../../catalog/domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../catalog/domain/rules/catalog-filter.rules';
import { SeoMetadataService } from '../../../shared/infrastructure/seo/seo-metadata.service';
import { StructuredDataService } from '../../../shared/infrastructure/seo/structured-data.service';
import type { SeoConfig } from '../../../shared/infrastructure/seo/seo.models';
import { HomeMetricsFacade } from '../application/facades/home-metrics.facade';
import type { MarketplaceMetrics } from '../domain/models/marketplace-metrics.model';

// ---------------------------------------------------------------------------
// Stub facade — injectable, controllable signals
// ---------------------------------------------------------------------------

@Injectable()
class StubCatalogFacade {
  private readonly _plugins = signal<PluginSummary[]>([]);
  private readonly _isLoadingPlugins = signal(false);
  private readonly _pluginsError = signal<{ code: string; message: string }[] | undefined>(undefined);

  setPlugins(plugins: PluginSummary[]): void {
    this._plugins.set(plugins);
  }

  setLoading(loading: boolean): void {
    this._isLoadingPlugins.set(loading);
  }

  setError(errors: { code: string; message: string }[]): void {
    this._pluginsError.set(errors);
  }

  // Facade signal getters consumed by component
  get plugins(): Signal<PluginSummary[]> {
    return this._plugins;
  }

  get isLoadingPlugins(): Signal<boolean> {
    return this._isLoadingPlugins;
  }

  get pluginsError(): Signal<{ code: string; message: string }[] | undefined> {
    return this._pluginsError;
  }

  // Call tracking
  loadPluginsCalls: Partial<CatalogFilterQuery>[] = [];
  loadPlugins(query?: Partial<CatalogFilterQuery>): void {
    this.loadPluginsCalls.push(query ?? {});
  }

  // Unused by landing page but part of CatalogFacade interface
  loadCategories(): void {
    // no-op
  }
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const makeSummary = (overrides: Partial<PluginSummary> = {}): PluginSummary => ({
  pluginId: 'plugin-1',
  name: 'Test Plugin',
  slug: 'test-plugin',
  description: 'A test plugin',
  author: 'tester',
  types: ['tool'],
  languages: ['typescript'],
  useCaseTags: ['testing'],
  downloadCount: 100,
  latestVersion: '1.0.0',
  createdAt: new Date('2024-01-01'),
  updatedAt: new Date('2024-01-01'),
  ...overrides,
});

// ---------------------------------------------------------------------------
// HomeMetricsFacade stub (needed when StatsBandComponent is rendered in the template)
// ---------------------------------------------------------------------------

@Injectable()
class StubHomeMetricsFacade {
  private readonly _isLoadingStats = signal(false);
  private readonly _stats = signal<MarketplaceMetrics | null>(null);
  private readonly _statsError = signal<string | undefined>(undefined);

  readonly isLoadingStats: Signal<boolean> = this._isLoadingStats;
  readonly stats: Signal<MarketplaceMetrics | null> = this._stats;
  readonly statsError: Signal<string | undefined> = this._statsError;

  loadStats = vi.fn();
}

// ---------------------------------------------------------------------------
// SEO service stubs
// ---------------------------------------------------------------------------

class StubSeoMetadataService {
  setMetadata = vi.fn();
  clearMetadata = vi.fn();
}

class StubStructuredDataService {
  injectOrganizationAndWebSite = vi.fn();
  injectPluginItemList = vi.fn();
  removeAll = vi.fn();
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(): {
  fixture: ComponentFixture<LandingPageComponent>;
  component: LandingPageComponent;
  stub: StubCatalogFacade;
  seoMetadataSpy: StubSeoMetadataService;
  structuredDataSpy: StubStructuredDataService;
} {
  const stub = new StubCatalogFacade();
  const seoMetadataSpy = new StubSeoMetadataService();
  const structuredDataSpy = new StubStructuredDataService();
  const homeMetricsFacadeStub = new StubHomeMetricsFacade();

  TestBed.configureTestingModule({
    imports: [LandingPageComponent],
    providers: [
      provideRouter([]),
      { provide: CatalogFacade, useValue: stub },
      { provide: SeoMetadataService, useValue: seoMetadataSpy },
      { provide: StructuredDataService, useValue: structuredDataSpy },
      // Provided so that StatsBandComponent (once added to the landing template)
      // can resolve its HomeMetricsFacade dependency without errors.
      { provide: HomeMetricsFacade, useValue: homeMetricsFacadeStub },
    ],
  });

  const fixture = TestBed.createComponent(LandingPageComponent);
  const component = fixture.componentInstance;
  fixture.detectChanges();

  return { fixture, component, stub, seoMetadataSpy, structuredDataSpy };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('LandingPageComponent', () => {
  // -------------------------------------------------------------------------
  // Render structure
  // -------------------------------------------------------------------------

  it('renders the hero section with product name', () => {
    const { fixture } = setup();
    const h1 = fixture.nativeElement.querySelector('h1') as HTMLElement | null;
    expect(h1?.textContent).toContain('Claude Code');
  });

  it('renders Browse plugins CTA linking to /catalog', () => {
    const { fixture } = setup();
    const anchors: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('a[href]');
    const browse = Array.from(anchors).find((a) => a.textContent?.trim() === 'Browse plugins');
    expect(browse).toBeDefined();
    expect(browse?.getAttribute('href')).toBe('/catalog');
  });

  it('renders Publish a plugin CTA linking to /docs', () => {
    const { fixture } = setup();
    const anchors: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('a[href]');
    const publish = Array.from(anchors).find((a) => a.textContent?.trim() === 'Publish a plugin');
    expect(publish).toBeDefined();
    expect(publish?.getAttribute('href')).toBe('/docs');
  });

  it('renders the sign-in placeholder button as aria-disabled', () => {
    const { fixture } = setup();
    const btn = fixture.nativeElement.querySelector('button[aria-disabled="true"]') as HTMLButtonElement | null;
    expect(btn).not.toBeNull();
    expect(btn?.textContent).toContain('Sign in');
  });

  it('renders 4 How-it-works steps', () => {
    const { fixture } = setup();
    const steps: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.lp-how__step');
    expect(steps.length).toBe(4);
  });

  it('renders the footer with catalog and docs links', () => {
    const { fixture } = setup();
    const footerLinks: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.lp-footer__link');
    const hrefs = Array.from(footerLinks).map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('/catalog');
    expect(hrefs).toContain('/docs');
  });

  // -------------------------------------------------------------------------
  // Loading state
  // -------------------------------------------------------------------------

  it('shows a loading indicator while plugins load', () => {
    const { fixture, stub } = setup();
    stub.setLoading(true);
    fixture.detectChanges();
    const loading = fixture.nativeElement.querySelector('[aria-busy="true"]') as HTMLElement | null;
    expect(loading).not.toBeNull();
    expect(loading?.textContent).toContain('Loading plugins');
  });

  // -------------------------------------------------------------------------
  // Error state
  // -------------------------------------------------------------------------

  it('shows an error message when plugin load fails', () => {
    const { fixture, stub } = setup();
    stub.setError([{ code: 'ERR', message: 'fail' }]);
    fixture.detectChanges();
    const err = fixture.nativeElement.querySelector('.lp-featured__error') as HTMLElement | null;
    expect(err).not.toBeNull();
  });

  // -------------------------------------------------------------------------
  // Empty state
  // -------------------------------------------------------------------------

  it('shows empty state when no plugins are available', () => {
    const { fixture } = setup();
    const empty = fixture.nativeElement.querySelector('cf-empty-state') as HTMLElement | null;
    expect(empty).not.toBeNull();
  });

  // -------------------------------------------------------------------------
  // Populated state
  // -------------------------------------------------------------------------

  it('renders plugin cards when plugins are available', () => {
    const { fixture, stub } = setup();
    stub.setPlugins([
      makeSummary({ pluginId: 'p1', name: 'Plugin Alpha', downloadCount: 500 }),
      makeSummary({ pluginId: 'p2', name: 'Plugin Beta', downloadCount: 200 }),
    ]);
    fixture.detectChanges();
    const cards: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.lp-plugin-card');
    expect(cards.length).toBe(2);
  });

  it('caps featured plugins at 6', () => {
    const { fixture, stub } = setup();
    stub.setPlugins(
      Array.from({ length: 10 }, (_, i) =>
        makeSummary({ pluginId: `p${i}`, name: `Plugin ${i}`, downloadCount: i * 10 }),
      ),
    );
    fixture.detectChanges();
    const cards: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.lp-plugin-card');
    expect(cards.length).toBe(6);
  });

  it('sorts featured plugins by download count descending', () => {
    const { fixture, stub } = setup();
    stub.setPlugins([
      makeSummary({ pluginId: 'low', name: 'Low Downloads', downloadCount: 10 }),
      makeSummary({ pluginId: 'high', name: 'High Downloads', downloadCount: 9999 }),
      makeSummary({ pluginId: 'mid', name: 'Mid Downloads', downloadCount: 500 }),
    ]);
    fixture.detectChanges();
    const cardNames: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.lp-plugin-card__name');
    expect(cardNames[0].textContent?.trim()).toBe('High Downloads');
    expect(cardNames[1].textContent?.trim()).toBe('Mid Downloads');
    expect(cardNames[2].textContent?.trim()).toBe('Low Downloads');
  });

  // -------------------------------------------------------------------------
  // Search input
  // -------------------------------------------------------------------------

  it('updates searchQuery signal on input', () => {
    const { fixture, component } = setup();
    const input = fixture.nativeElement.querySelector('#lp-search-input') as HTMLInputElement | null;
    expect(input).not.toBeNull();
    if (input) {
      input.value = 'git helper';
      input.dispatchEvent(new Event('input'));
      fixture.detectChanges();
      expect(component.searchQuery()).toBe('git helper');
    }
  });

  it('navigates to /search with query params on form submit with non-empty query', () => {
    const { fixture, component } = setup();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    component.searchQuery.set('formatter');
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement | null;
    form?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(navSpy).toHaveBeenCalledWith(['/search'], { queryParams: { q: 'formatter' } });
    navSpy.mockRestore();
  });

  it('navigates to /search without query params when query is empty', () => {
    const { fixture } = setup();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement | null;
    form?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(navSpy).toHaveBeenCalledWith(['/search']);
    navSpy.mockRestore();
  });

  // -------------------------------------------------------------------------
  // formatDownloads helper
  // -------------------------------------------------------------------------

  it('formats download counts below 1k as plain numbers', () => {
    const { component } = setup();
    expect(component.formatDownloads(999)).toBe('999');
  });

  it('formats download counts >= 1k with k suffix', () => {
    const { component } = setup();
    expect(component.formatDownloads(1_500)).toBe('1.5k');
  });

  it('formats download counts >= 1M with M suffix', () => {
    const { component } = setup();
    expect(component.formatDownloads(2_000_000)).toBe('2.0M');
  });

  // -------------------------------------------------------------------------
  // ngOnInit calls facade
  // -------------------------------------------------------------------------

  it('calls catalogFacade.loadPlugins on init with sort/order params', () => {
    const { stub } = setup();
    expect(stub.loadPluginsCalls).toContainEqual({ sort: 'downloadCount', order: 'desc' });
  });

  // =========================================================================
  // GROUP 5 — Stats band + SEO integration (RED — not yet implemented)
  // =========================================================================

  // -------------------------------------------------------------------------
  // Stats band template integration
  // -------------------------------------------------------------------------

  it('renders <cf-stats-band> element in the template', () => {
    const { fixture } = setup();
    const statsBand = fixture.nativeElement.querySelector('cf-stats-band') as HTMLElement | null;
    expect(statsBand).not.toBeNull();
  });

  it('renders <cf-stats-band> above the Popular plugins section', () => {
    const { fixture } = setup();
    const statsBand = fixture.nativeElement.querySelector('cf-stats-band') as HTMLElement | null;
    const featured = fixture.nativeElement.querySelector('.lp-featured') as HTMLElement | null;
    expect(statsBand).not.toBeNull();
    expect(featured).not.toBeNull();
    if (statsBand !== null && featured !== null) {
      // Node.DOCUMENT_POSITION_FOLLOWING means statsBand comes before featured
      const position = statsBand.compareDocumentPosition(featured);
      expect(position & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  // -------------------------------------------------------------------------
  // SEO metadata on init
  // -------------------------------------------------------------------------

  it('calls SeoMetadataService.setMetadata on init with title and description', () => {
    const { seoMetadataSpy } = setup();
    expect(seoMetadataSpy.setMetadata).toHaveBeenCalledOnce();
    const config = seoMetadataSpy.setMetadata.mock.calls[0][0] as SeoConfig;
    expect(config.title).toBeTruthy();
    expect(config.description).toBeTruthy();
  });

  it('passes a landing-page title containing "ClaudeForge" to SeoMetadataService.setMetadata', () => {
    const { seoMetadataSpy } = setup();
    const config = seoMetadataSpy.setMetadata.mock.calls[0][0] as SeoConfig;
    expect(config.title).toContain('ClaudeForge');
  });

  it('passes a description about the plugin marketplace to SeoMetadataService.setMetadata', () => {
    const { seoMetadataSpy } = setup();
    const config = seoMetadataSpy.setMetadata.mock.calls[0][0] as SeoConfig;
    // description must mention the marketplace purpose
    expect(config.description.length).toBeGreaterThan(20);
  });

  // -------------------------------------------------------------------------
  // Structured data on init
  // -------------------------------------------------------------------------

  it('calls StructuredDataService.injectOrganizationAndWebSite on init', () => {
    const { structuredDataSpy } = setup();
    expect(structuredDataSpy.injectOrganizationAndWebSite).toHaveBeenCalledOnce();
  });

  it('calls StructuredDataService.injectPluginItemList when featured plugins are loaded', () => {
    const { fixture, stub, structuredDataSpy } = setup();
    const plugins = [
      makeSummary({ pluginId: 'p1', name: 'Plugin Alpha', downloadCount: 500 }),
      makeSummary({ pluginId: 'p2', name: 'Plugin Beta', downloadCount: 200 }),
    ];
    stub.setPlugins(plugins);
    fixture.detectChanges();

    expect(structuredDataSpy.injectPluginItemList).toHaveBeenCalled();
    const calledWith = structuredDataSpy.injectPluginItemList.mock.calls[0][0] as readonly PluginSummary[];
    expect(calledWith.length).toBeGreaterThan(0);
  });

  it('passes the featured plugins (sorted by download count) to injectPluginItemList', () => {
    const { fixture, stub, structuredDataSpy } = setup();
    const plugins = [
      makeSummary({ pluginId: 'low', name: 'Low Downloads', downloadCount: 10 }),
      makeSummary({ pluginId: 'high', name: 'High Downloads', downloadCount: 9999 }),
    ];
    stub.setPlugins(plugins);
    fixture.detectChanges();

    const calledWith = structuredDataSpy.injectPluginItemList.mock.calls[
      structuredDataSpy.injectPluginItemList.mock.calls.length - 1
    ][0] as readonly PluginSummary[];
    // first element should be the highest-downloaded plugin
    expect(calledWith[0].pluginId).toBe('high');
  });
});
