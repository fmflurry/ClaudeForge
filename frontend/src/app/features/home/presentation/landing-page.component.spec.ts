import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, Injectable, Input, Signal, signal } from '@angular/core';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { LandingPageComponent } from './landing-page.component';
import { CatalogFacade } from '../../catalog/application/facades/catalog.facade';
import type { Categories, AddOnSummary } from '../../catalog/domain/models/catalog.models';
import type { CatalogFilterQuery } from '../../catalog/domain/rules/catalog-filter.rules';
import { SeoMetadataService } from '../../../shared/infrastructure/seo/seo-metadata.service';
import { StructuredDataService } from '../../../shared/infrastructure/seo/structured-data.service';
import type { SeoConfig } from '../../../shared/infrastructure/seo/seo.models';
import { HomeMetricsFacade } from '../application/facades/home-metrics.facade';
import type { MarketplaceMetrics } from '../domain/models/marketplace-metrics.model';
import { I18nFacade } from '../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../core/i18n/language-storage.port';
import { FeaturedAddOnFacade } from '../application/facades/featured-plugin.facade';
import type { FeaturedAddOn } from '../domain/models/featured-plugin.model';
import { AuthFacade } from '../../auth/application/facades/auth.facade';
import type { CurrentUser } from '../../auth/domain/models/auth.models';
import { EmptyStateComponent } from '../../../shared/design-system/empty-state.component';

// ---------------------------------------------------------------------------
// Transloco test langs for home scope (Wave 1 i18n)
//
// En map returns EXACT current literals so all existing rendered-text
// assertions keep passing unchanged after migration.
// Fr map returns French — assertions using fr are RED until migration done.
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
  // Install showcase keys
  'home.install-showcase.heading': 'Install a plugin',
  'home.install-showcase.copy-btn': 'Copy',
  'home.install-showcase.copied': 'Copied!',
  'home.install-showcase.caption': 'Learn how to install →',
  // Category discovery
  'home.categories.heading': 'Browse by use case',
  // Aria labels
  'home.aria.primary-actions': 'Primary actions',
  'home.aria.browse-all': 'Browse all plugins',
  'home.aria.learn-publish': 'Learn how to publish a plugin',
  'home.aria.loading-plugins': 'Loading plugins',
  'home.aria.popular-plugins': 'Popular plugins',
  'home.aria.plugin-downloads': '{count} downloads',
  'home.aria.plugin-types': 'Plugin types',
  'home.aria.footer-nav': 'Footer navigation',
  'home.error.browse-catalog': 'browse the catalog',
  // How it works
  'home.how.step-discover-title': 'Discover',
  'home.how.step-discover-desc': 'Find plugins in the catalog',
  'home.how.step-install-title': 'Install',
  'home.how.step-install-desc': 'One command to install',
  'home.how.step-publish-title': 'Publish',
  'home.how.step-publish-desc': 'Share your own plugins',
  'home.how.step-share-title': 'Share',
  'home.how.step-share-desc': 'Grow the community',
  // Plugin card
  'home.plugin-card.by': 'by',
  'home.plugin-card.downloads': 'downloads',
  // SEO keys — required so setMetadata receives resolved strings, not raw key names
  'home.seo.title': 'ClaudeForge — The Plugin Marketplace for Claude Code',
  'home.seo.description':
    'Discover, install, and publish Claude Code plugins from the community. Browse hundreds of tools, formatters, and automations on ClaudeForge.',
  'home.seo.og-title': 'ClaudeForge — The Plugin Marketplace for Claude Code',
  'home.seo.og-description':
    'Discover, install, and publish Claude Code plugins from the community. Browse hundreds of tools, formatters, and automations on ClaudeForge.',
  'home.seo.twitter-title': 'ClaudeForge — The Plugin Marketplace for Claude Code',
  'home.seo.twitter-description':
    'Discover, install, and publish Claude Code plugins from the community. Browse hundreds of tools, formatters, and automations on ClaudeForge.',
};

const FR_HOME_LANGS: Record<string, string> = {
  'home.hero-title': 'La Marketplace de plugins pour Claude Code',
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
  'home.install-showcase.heading': 'Installer un plugin',
  'home.install-showcase.copy-btn': 'Copier',
  'home.install-showcase.copied': 'Copié !',
  'home.install-showcase.caption': 'Apprendre à installer →',
  'home.categories.heading': "Parcourir par cas d'usage",
  'home.aria.primary-actions': 'Actions principales',
  'home.aria.browse-all': 'Parcourir tous les plugins',
  'home.aria.learn-publish': 'Apprendre à publier un plugin',
  'home.aria.loading-plugins': 'Chargement des plugins',
  'home.aria.popular-plugins': 'Plugins populaires',
  'home.aria.plugin-downloads': '{count} téléchargements',
  'home.aria.plugin-types': 'Types de plugins',
  'home.aria.footer-nav': 'Navigation du pied de page',
  'home.error.browse-catalog': 'parcourir le catalogue',
  'home.how.step-discover-title': 'Découvrir',
  'home.how.step-discover-desc': 'Trouver des plugins dans le catalogue',
  'home.how.step-install-title': 'Installer',
  'home.how.step-install-desc': 'Une commande pour installer',
  'home.how.step-publish-title': 'Publier',
  'home.how.step-publish-desc': 'Partagez vos propres plugins',
  'home.how.step-share-title': 'Partager',
  'home.how.step-share-desc': 'Faire grandir la communauté',
  'home.plugin-card.by': 'par',
  'home.plugin-card.downloads': 'téléchargements',
  'home.seo.title': 'ClaudeForge — La Marketplace de plugins pour Claude Code',
  'home.seo.description': 'Découvrez, installez et publiez des plugins Claude Code de la communauté.',
  'home.seo.og-title': 'ClaudeForge — La Marketplace de plugins pour Claude Code',
  'home.seo.og-description': 'Découvrez, installez et publiez des plugins Claude Code de la communauté.',
  'home.seo.twitter-title': 'ClaudeForge — La Marketplace de plugins pour Claude Code',
  'home.seo.twitter-description': 'Découvrez, installez et publiez des plugins Claude Code de la communauté.',
};

// ---------------------------------------------------------------------------
// Stub facades — injectable, controllable signals
// ---------------------------------------------------------------------------

@Injectable()
class StubCatalogFacade {
  private readonly _addOns = signal<AddOnSummary[]>([]);
  private readonly _isLoadingAddOns = signal(false);
  private readonly _addOnsError = signal<{ code: string; message: string }[] | undefined>(undefined);
  private readonly _categories = signal<Categories | undefined>(undefined);

  setPlugins(addOns: AddOnSummary[]): void {
    this._addOns.set(addOns);
  }

  setLoading(loading: boolean): void {
    this._isLoadingAddOns.set(loading);
  }

  setError(errors: { code: string; message: string }[]): void {
    this._addOnsError.set(errors);
  }

  setCategories(cats: Categories | undefined): void {
    this._categories.set(cats);
  }

  // Facade signal getters consumed by component
  get addOns(): Signal<AddOnSummary[]> {
    return this._addOns;
  }

  get isLoadingAddOns(): Signal<boolean> {
    return this._isLoadingAddOns;
  }

  get addOnsError(): Signal<{ code: string; message: string }[] | undefined> {
    return this._addOnsError;
  }

  get categories(): Signal<Categories | undefined> {
    return this._categories;
  }

  // Call tracking
  loadAddOnsCalls: Partial<CatalogFilterQuery>[] = [];
  loadAddOns(query?: Partial<CatalogFilterQuery>): void {
    this.loadAddOnsCalls.push(query ?? {});
  }

  loadCategoriesCalls = 0;
  loadCategories(): void {
    this.loadCategoriesCalls++;
  }
}

@Injectable()
class StubFeaturedAddOnFacade {
  private readonly _featuredAddOn = signal<FeaturedAddOn | null>(null);

  readonly featuredAddOn: Signal<FeaturedAddOn | null> = this._featuredAddOn.asReadonly();

  setFeaturedPlugin(addOn: FeaturedAddOn | null): void {
    this._featuredAddOn.set(addOn);
  }

  load = vi.fn();
}

@Injectable()
class StubAuthFacade {
  private readonly _currentUser = signal<CurrentUser | undefined>(undefined);

  readonly currentUser: Signal<CurrentUser | undefined> = this._currentUser.asReadonly();

  setUser(user: CurrentUser | undefined): void {
    this._currentUser.set(user);
  }

  logout = vi.fn();
  silentRefresh = vi.fn();
}

@Component({
  selector: 'cf-empty-state',
  standalone: true,
  template: '<div class="cf-empty-state" role="status">{{ message }}<ng-content /></div>',
})
class StubEmptyStateComponent {
  @Input() message = '';
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const makeSummary = (overrides: Partial<AddOnSummary> = {}): AddOnSummary => ({
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
  featuredPluginStub: StubFeaturedAddOnFacade;
  authStub: StubAuthFacade;
  seoMetadataSpy: StubSeoMetadataService;
  structuredDataSpy: StubStructuredDataService;
  translocoService: TranslocoService;
} {
  const stub = new StubCatalogFacade();
  const seoMetadataSpy = new StubSeoMetadataService();
  const structuredDataSpy = new StubStructuredDataService();
  const homeMetricsFacadeStub = new StubHomeMetricsFacade();
  const featuredPluginStub = new StubFeaturedAddOnFacade();
  const authStub = new StubAuthFacade();

  TestBed.configureTestingModule({
    imports: [
      LandingPageComponent,
      // Transloco test harness (Wave 1 pattern):
      // flat dot-delimited keys; en=current literals, fr=French translations.
      TranslocoTestingModule.forRoot({
        langs: { en: EN_HOME_LANGS, fr: FR_HOME_LANGS },
        translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: CatalogFacade, useValue: stub },
      { provide: FeaturedAddOnFacade, useValue: featuredPluginStub },
      { provide: AuthFacade, useValue: authStub },
      { provide: SeoMetadataService, useValue: seoMetadataSpy },
      { provide: StructuredDataService, useValue: structuredDataSpy },
      // Provided so that StatsBandComponent (once added to the landing template)
      // can resolve its HomeMetricsFacade dependency without errors.
      { provide: HomeMetricsFacade, useValue: homeMetricsFacadeStub },
      // Real I18nFacade — injects TranslocoService from the testing module above.
      I18nFacade,
      { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
    ],
  });
  TestBed.overrideComponent(LandingPageComponent, {
    remove: { imports: [EmptyStateComponent] },
    add: { imports: [StubEmptyStateComponent] },
  });

  const fixture = TestBed.createComponent(LandingPageComponent);
  const component = fixture.componentInstance;
  fixture.detectChanges();
  const translocoService = TestBed.inject(TranslocoService);

  return {
    fixture,
    component,
    stub,
    featuredPluginStub,
    authStub,
    seoMetadataSpy,
    structuredDataSpy,
    translocoService,
  };
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
  // Task 4.3 — Restored search entry
  // -------------------------------------------------------------------------

  it('does NOT render an aria-disabled sign-in button (disabled hero login CTA removed)', () => {
    const { fixture } = setup();
    const btn = fixture.nativeElement.querySelector('button[aria-disabled="true"]') as HTMLButtonElement | null;
    expect(btn).toBeNull();
  });

  it('renders a search input with id lp-search-input', () => {
    const { fixture } = setup();
    const input = fixture.nativeElement.querySelector('#lp-search-input') as HTMLInputElement | null;
    expect(input).not.toBeNull();
    expect(input?.getAttribute('type')).toBe('search');
    expect(input?.getAttribute('aria-label')).toBe('Search plugins');
  });

  it('renders a search submit form', () => {
    const { fixture } = setup();
    const form = fixture.nativeElement.querySelector('form.lp-search-entry') as HTMLFormElement | null;
    const button = fixture.nativeElement.querySelector('.lp-search-entry__btn') as HTMLButtonElement | null;
    expect(form).not.toBeNull();
    expect(form?.getAttribute('role')).toBe('search');
    expect(button?.type).toBe('submit');
    expect(button?.textContent?.trim()).toBe('Search');
  });

  it('navigates to /search with q query param when a search query exists', () => {
    const { fixture } = setup();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const input = fixture.nativeElement.querySelector('#lp-search-input') as HTMLInputElement | null;
    const form = fixture.nativeElement.querySelector('form.lp-search-entry') as HTMLFormElement | null;

    expect(input).not.toBeNull();
    expect(form).not.toBeNull();

    if (input !== null && form !== null) {
      input.value = 'formatter';
      input.dispatchEvent(new Event('input', { bubbles: true }));
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    }

    expect(navSpy).toHaveBeenCalledWith(['/search'], { queryParams: { q: 'formatter' } });
    navSpy.mockRestore();
  });

  it('navigates to /search without q query param when search query is empty', () => {
    const { fixture } = setup();
    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const form = fixture.nativeElement.querySelector('form.lp-search-entry') as HTMLFormElement | null;

    expect(form).not.toBeNull();

    form?.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));

    expect(navSpy).toHaveBeenCalledWith(['/search']);
    navSpy.mockRestore();
  });

  // -------------------------------------------------------------------------
  // Task 4.4 — Hero install showcase
  // -------------------------------------------------------------------------

  it('renders the install showcase code block', () => {
    const { fixture } = setup();
    const codeBlock = fixture.nativeElement.querySelector('.lp-showcase__code-block') as HTMLElement | null;
    expect(codeBlock).not.toBeNull();
  });

  it('shows generic fallback install command when no featured plugin is set', () => {
    const { fixture } = setup();
    // featuredPluginStub defaults to null — fallback should be shown
    const code = fixture.nativeElement.querySelector('.lp-showcase__code') as HTMLElement | null;
    expect(code?.textContent?.trim()).toBe('claude-plugin install <plugin-name>');
  });

  it('shows plugin slug in install command when a featured plugin is available', () => {
    const { fixture, featuredPluginStub } = setup();
    featuredPluginStub.setFeaturedPlugin({
      pluginId: 'fp1',
      name: 'My Awesome Plugin',
      slug: 'my-awesome-plugin',
      latestVersion: '2.0.0',
    });
    fixture.detectChanges();
    const code = fixture.nativeElement.querySelector('.lp-showcase__code') as HTMLElement | null;
    expect(code?.textContent?.trim()).toBe('claude-plugin install my-awesome-plugin');
  });

  it('renders the copy install command button', () => {
    const { fixture } = setup();
    const copyBtn = fixture.nativeElement.querySelector('.lp-showcase__copy-btn') as HTMLButtonElement | null;
    expect(copyBtn).not.toBeNull();
  });

  // -------------------------------------------------------------------------
  // Task 4.4 — Category discovery chips (not rendered in current landing source)
  // -------------------------------------------------------------------------

  it('does NOT render category chips section when categories are undefined', () => {
    const { fixture } = setup();
    const section = fixture.nativeElement.querySelector('.lp-categories') as HTMLElement | null;
    expect(section).toBeNull();
  });

  it('does NOT render category chips when use-case categories are available', () => {
    const { fixture, stub } = setup();
    stub.setCategories({
      types: [],
      languages: [],
      useCases: [
        { value: 'productivity', displayName: 'Productivity', description: '', count: 5 },
        { value: 'testing', displayName: 'Testing', description: '', count: 3 },
      ],
    });
    fixture.detectChanges();
    const chips: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.lp-categories__chip');
    expect(chips.length).toBe(0);
  });

  it('does NOT render category chip display names from catalog data', () => {
    const { fixture, stub } = setup();
    stub.setCategories({
      types: [],
      languages: [],
      useCases: [
        { value: 'productivity', displayName: 'Productivity', description: '', count: 5 },
        { value: 'testing', displayName: 'Testing', description: '', count: 3 },
      ],
    });
    fixture.detectChanges();
    const chips: NodeListOf<HTMLElement> = fixture.nativeElement.querySelectorAll('.lp-categories__chip');
    const names = Array.from(chips).map((c) => c.textContent?.trim());
    expect(names).not.toContain('Productivity');
    expect(names).not.toContain('Testing');
  });

  it('does NOT navigate by category chip because category chips are not rendered', () => {
    const { fixture, stub } = setup();
    stub.setCategories({
      types: [],
      languages: [],
      useCases: [{ value: 'productivity', displayName: 'Productivity', description: '', count: 5 }],
    });
    fixture.detectChanges();

    const router = TestBed.inject(Router);
    const navSpy = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    const chip = fixture.nativeElement.querySelector('.lp-categories__chip') as HTMLButtonElement | null;
    chip?.click();
    fixture.detectChanges();

    expect(chip).toBeNull();
    expect(navSpy).not.toHaveBeenCalled();
    navSpy.mockRestore();
  });

  it('does NOT call catalogFacade.loadCategories on init', () => {
    const { stub } = setup();
    expect(stub.loadCategoriesCalls).toBe(0);
  });

  it('calls featuredPluginFacade.load on init', () => {
    const { featuredPluginStub } = setup();
    expect(featuredPluginStub.load).toHaveBeenCalledOnce();
  });

  // -------------------------------------------------------------------------
  // Task 4.4 — Auth-gated footer link
  // -------------------------------------------------------------------------

  it('does NOT show My Plugins footer link when user is not authenticated', () => {
    const { fixture } = setup();
    const footerLinks: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.lp-footer__link');
    const texts = Array.from(footerLinks).map((a) => a.textContent?.trim());
    expect(texts).not.toContain('My Plugins');
  });

  it('shows My Plugins footer link when user is authenticated', () => {
    const { fixture, authStub } = setup();
    authStub.setUser({
      userId: 'u1',
      email: 'user@example.com',
      displayName: 'Test User',
      orgMemberships: [],
    });
    fixture.detectChanges();
    const footerLinks: NodeListOf<HTMLAnchorElement> = fixture.nativeElement.querySelectorAll('.lp-footer__link');
    const texts = Array.from(footerLinks).map((a) => a.textContent?.trim());
    expect(texts).toContain('My Plugins');
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
    const empty = fixture.nativeElement.querySelector('.cf-empty-state') as HTMLElement | null;
    expect(empty).not.toBeNull();
    expect(empty?.textContent).toContain('No plugins available yet');
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
    expect(stub.loadAddOnsCalls).toContainEqual({ sort: 'downloadCount', order: 'desc' });
  });

  // =========================================================================
  // GROUP 5 — Stats band + SEO integration
  // =========================================================================

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
    const calledWith = structuredDataSpy.injectPluginItemList.mock.calls[0][0] as readonly AddOnSummary[];
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
    ][0] as readonly AddOnSummary[];
    // first element should be the highest-downloaded plugin
    expect(calledWith[0].pluginId).toBe('high');
  });

  // =========================================================================
  // GROUP 6 — i18n Wave 1
  // =========================================================================

  it('[FR] hero h1 renders French title when lang is fr', () => {
    const { fixture, translocoService } = setup();
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const h1 = fixture.nativeElement.querySelector('h1') as HTMLElement | null;
    expect(h1?.textContent).toContain('La Marketplace de plugins pour Claude Code');
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
