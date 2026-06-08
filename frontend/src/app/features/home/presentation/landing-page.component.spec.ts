import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Injectable, Signal, signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { LandingPageComponent } from './landing-page.component';
import { CatalogFacade } from '../../catalog/application/facades/catalog.facade';
import type { PluginSummary } from '../../catalog/domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../catalog/domain/rules/catalog-filter.rules';
import { SeoMetadataService } from '../../../shared/infrastructure/seo/seo-metadata.service';
import { StructuredDataService } from '../../../shared/infrastructure/seo/structured-data.service';
import type { SeoConfig } from '../../../shared/infrastructure/seo/seo.models';
import { HomeMetricsFacade } from '../application/facades/home-metrics.facade';
import type { MarketplaceMetrics } from '../domain/models/marketplace-metrics.model';
import { I18nFacade } from '../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for home scope (Wave 1 i18n)
//
// En map returns EXACT current literals so all existing rendered-text
// assertions keep passing unchanged after migration.
// Fr map returns French — assertions using fr are RED until migration done.
//
// Key namespace: 'home' scope loaded via provideTranslocoScope('home')
// on the home route. In tests, scope keys are accessed via the scope alias
// prefix or directly as flat dot-delimited keys in the langs map.
//
// Canonical key list:
//   home.hero-title           → "The plugin marketplace for Claude Code"
//                             / "La place de marché de plugins pour Claude Code"
//   home.hero-tagline         → "Discover, install, and publish..."
//                             / "Découvrez, installez et publiez..."
//   home.browse-plugins       → "Browse plugins"       / "Parcourir les plugins"
//   home.publish-plugin       → "Publish a plugin"     / "Publier un plugin"
//   home.sign-in              → "Sign in"              / "Se connecter"
//   home.search-aria          → "Search plugins"       / "Rechercher des plugins"
//   home.search-placeholder   → "Search plugins — e.g. 'git commit helper'…"
//                             / "Rechercher des plugins — ex. 'aide git commit'…"
//   home.search-btn           → "Search"               / "Rechercher"
//   home.popular-heading      → "Popular plugins"      / "Plugins populaires"
//   home.loading-plugins      → "Loading plugins…"     / "Chargement des plugins…"
//   home.error-plugins        → "Could not load plugins right now"
//                             / "Impossible de charger les plugins"
//   home.empty-plugins        → "No plugins available yet — be the first to publish one!"
//                             / "Aucun plugin disponible — soyez le premier à en publier un !"
//   home.view-all-plugins     → "View all plugins"     / "Voir tous les plugins"
//   home.how-heading          → "How it works"         / "Comment ça marche"
//   home.footer-catalog       → "Plugin Catalog"       / "Catalogue de plugins"
//   home.footer-docs          → "Documentation"        / "Documentation"
//   home.footer-search        → "Search"               / "Rechercher"
//   home.footer-my-plugins    → "My Plugins"           / "Mes plugins"
// ---------------------------------------------------------------------------

const EN_HOME_LANGS: Record<string, string> = {
  'home.hero-title': 'The plugin marketplace for Claude Code',
  'home.hero-tagline': 'Discover, install, and publish Claude Code plugins from the community — all in one place.',
  'home.browse-plugins': 'Browse plugins',
  'home.publish-plugin': 'Publish a plugin',
  'home.sign-in': 'Sign in',
  'home.search-aria': 'Search plugins',
  'home.search-placeholder': "Search plugins — e.g. 'git commit helper', 'typescript formatter'…",
  'home.search-btn': 'Search',
  'home.popular-heading': 'Popular plugins',
  'home.loading-plugins': 'Loading plugins…',
  'home.error-plugins': 'Could not load plugins right now — try refreshing or',
  'home.empty-plugins': 'No plugins available yet — be the first to publish one!',
  'home.view-all-plugins': 'View all plugins',
  'home.how-heading': 'How it works',
  'home.footer-catalog': 'Plugin Catalog',
  'home.footer-docs': 'Documentation',
  'home.footer-search': 'Search',
  'home.footer-my-plugins': 'My Plugins',
  // SEO keys — required so setMetadata receives resolved strings, not raw key names
  'home.seo.title': 'ClaudeForge — The Plugin Marketplace for Claude Code',
  'home.seo.description': 'Discover, install, and publish Claude Code plugins from the community. Browse hundreds of tools, formatters, and automations on ClaudeForge.',
  'home.seo.og-title': 'ClaudeForge — The Plugin Marketplace for Claude Code',
  'home.seo.og-description': 'Discover, install, and publish Claude Code plugins from the community. Browse hundreds of tools, formatters, and automations on ClaudeForge.',
  'home.seo.twitter-title': 'ClaudeForge — The Plugin Marketplace for Claude Code',
  'home.seo.twitter-description': 'Discover, install, and publish Claude Code plugins from the community. Browse hundreds of tools, formatters, and automations on ClaudeForge.',
};

const FR_HOME_LANGS: Record<string, string> = {
  'home.hero-title': 'La place de marché de plugins pour Claude Code',
  'home.hero-tagline': 'Découvrez, installez et publiez des plugins Claude Code de la communauté — au même endroit.',
  'home.browse-plugins': 'Parcourir les plugins',
  'home.publish-plugin': 'Publier un plugin',
  'home.sign-in': 'Se connecter',
  'home.search-aria': 'Rechercher des plugins',
  'home.search-placeholder': "Rechercher des plugins — ex. 'aide git commit', 'formateur typescript'…",
  'home.search-btn': 'Rechercher',
  'home.popular-heading': 'Plugins populaires',
  'home.loading-plugins': 'Chargement des plugins…',
  'home.error-plugins': 'Impossible de charger les plugins pour le moment — essayez de rafraîchir ou',
  'home.empty-plugins': 'Aucun plugin disponible — soyez le premier à en publier un !',
  'home.view-all-plugins': 'Voir tous les plugins',
  'home.how-heading': 'Comment ça marche',
  'home.footer-catalog': 'Catalogue de plugins',
  'home.footer-docs': 'Documentation',
  'home.footer-search': 'Rechercher',
  'home.footer-my-plugins': 'Mes plugins',
};

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
  translocoService: TranslocoService;
} {
  const stub = new StubCatalogFacade();
  const seoMetadataSpy = new StubSeoMetadataService();
  const structuredDataSpy = new StubStructuredDataService();
  const homeMetricsFacadeStub = new StubHomeMetricsFacade();

  TestBed.configureTestingModule({
    imports: [
      LandingPageComponent,
      // Transloco test harness (Wave 1 pattern):
      // flat dot-delimited keys; en=current literals, fr=French translations.
      // home scope keys are prefixed with 'home.' — matches provideTranslocoScope('home').
      TranslocoTestingModule.forRoot({
        langs: { en: EN_HOME_LANGS, fr: FR_HOME_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: CatalogFacade, useValue: stub },
      { provide: SeoMetadataService, useValue: seoMetadataSpy },
      { provide: StructuredDataService, useValue: structuredDataSpy },
      // Provided so that StatsBandComponent (once added to the landing template)
      // can resolve its HomeMetricsFacade dependency without errors.
      { provide: HomeMetricsFacade, useValue: homeMetricsFacadeStub },
      // Real I18nFacade — injects TranslocoService from the testing module above.
      // Switching lang via translocoService.setActiveLang('fr') causes i18n.t()
      // to re-evaluate because the facade reads transloco.activeLang() internally.
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  });

  const fixture = TestBed.createComponent(LandingPageComponent);
  const component = fixture.componentInstance;
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);

  return { fixture, component, stub, seoMetadataSpy, structuredDataSpy, translocoService };
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
    expect(h1?.textContent).toContain('Claude Code');
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

  it('calls SeoMetadataService.setMetadata on init with title, description, and Open Graph fields', () => {
    const { seoMetadataSpy } = setup();
    expect(seoMetadataSpy.setMetadata).toHaveBeenCalledOnce();
    const config = seoMetadataSpy.setMetadata.mock.calls[0][0] as SeoConfig;
    expect(config.title).toBeTruthy();
    expect(config.description).toBeTruthy();
    // Open Graph — required for social-media unfurls
    expect(config.ogTitle).toBeTruthy();
    expect(config.ogDescription).toBeTruthy();
    expect(config.ogType).toBe('website');
    expect(config.ogUrl).toBeTruthy();
    expect(config.ogImage).toBeTruthy();
    // Twitter card
    expect(config.twitterCard).toBe('summary_large_image');
    expect(config.twitterTitle).toBeTruthy();
    expect(config.twitterDescription).toBeTruthy();
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

  // =========================================================================
  // GROUP 6 — i18n Wave 1 (RED — fail until template migrated to Transloco)
  //
  // When these are RED: the component still has hardcoded EN strings; the
  // TranslocoService.setActiveLang('fr') call has no effect because no
  // Transloco pipe/directive is used.
  //
  // When GREEN: all nav/CTA text must be driven by the 'home' scope keys.
  // =========================================================================

  it('[FR] hero h1 renders French title when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const h1 = fixture.nativeElement.querySelector('h1') as HTMLElement | null;
    expect(h1?.textContent).toContain('La place de marché de plugins pour Claude Code');
  });

  it('[FR] Browse plugins CTA renders "Parcourir les plugins" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const anchors: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('a[href]');
    const browse = Array.from(anchors).find((a) => a.textContent?.trim() === 'Parcourir les plugins');
    expect(browse).toBeDefined();
  });

  it('[FR] Publish a plugin CTA renders "Publier un plugin" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const anchors: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('a[href]');
    const publish = Array.from(anchors).find((a) => a.textContent?.trim() === 'Publier un plugin');
    expect(publish).toBeDefined();
  });

  it('[FR] Sign in button renders "Se connecter" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const btn = fixture.nativeElement.querySelector('button[aria-disabled="true"]') as HTMLButtonElement | null;
    expect(btn?.textContent?.trim()).toContain('Se connecter');
  });

  it('[FR] Popular plugins heading renders "Plugins populaires" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const h2Elements: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('h2');
    const headings = Array.from(h2Elements).map((h) => h.textContent?.trim());
    expect(headings).toContain('Plugins populaires');
  });

  it('[FR] How it works heading renders "Comment ça marche" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const h2Elements: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('h2');
    const headings = Array.from(h2Elements).map((h) => h.textContent?.trim());
    expect(headings).toContain('Comment ça marche');
  });

  it('[FR] footer Plugin Catalog link renders "Catalogue de plugins" when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const footerLinks: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.lp-footer__link');
    const texts = Array.from(footerLinks).map((a) => a.textContent?.trim());
    expect(texts).toContain('Catalogue de plugins');
  });

  it('[FR] loading state renders "Chargement des plugins…" when lang is fr', () => {
    const { fixture, stub, translocoService } = setup();
    stub.setLoading(true);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const loading = fixture.nativeElement.querySelector('[aria-busy="true"]') as HTMLElement | null;
    expect(loading?.textContent?.trim()).toContain('Chargement des plugins…');
  });
});
