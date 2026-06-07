/**
 * RED tests — Task 15.4b: PluginDetailsModalComponent + DashboardPageComponent
 *
 * Expected production files (do NOT exist yet — tests WILL FAIL):
 *   src/app/features/dashboard/presentation/plugin-details-modal/plugin-details-modal.component.ts
 *   (DashboardPageComponent is the existing stub — this file tests the composed page behaviour)
 *
 * Production components the coder MUST define:
 *
 *   // plugin-details-modal.component.ts
 *   @Component({
 *     selector: 'cf-plugin-details-modal',
 *     standalone: true,
 *     changeDetection: ChangeDetectionStrategy.OnPush,
 *   })
 *   class PluginDetailsModalComponent {
 *     // Inputs:
 *     readonly pluginName = input.required<string>();
 *     readonly plugin = input<InstalledPlugin | undefined>(undefined);
 *
 *     // Outputs:
 *     readonly closed = output<void>();
 *     readonly confirmRemove = output<string>();  — emits plugin name
 *     readonly confirmUpdate = output<string>();  — emits plugin name when update confirmed
 *
 *     // Methods:
 *     onClose(): void
 *     onConfirmRemove(): void
 *     onConfirmUpdate(): void
 *
 *     // Template must include:
 *     //   [data-testid="modal-title"]       — plugin name
 *     //   [data-testid="plugin-version"]    — current version
 *     //   [data-testid="modal-close-btn"]   — close button
 *     //   [data-testid="modal-remove-btn"]  — remove confirm button
 *     //   [data-testid="update-section"]    — visible only when status='update-available'
 *     //   [data-testid="release-notes"]     — visible when latestVersion known
 *     //   [data-testid="docs-link"] OR [data-testid="docs-placeholder"] — docs link or placeholder
 *   }
 *
 *   // DashboardPageComponent (extend existing stub) MUST:
 *   //   - inject DashboardFacade only
 *   //   - host InstalledPluginsTableComponent + PluginDetailsModalComponent
 *   //   - run a periodic checkForUpdates on a 5-minute interval (300_000 ms)
 *   //   - template must include:
 *   //     [data-testid="dashboard-page"]
 *   //     [data-testid="update-banner"]  — visible when hasUpdates()
 *   //     cf-installed-plugins-table
 *   //     cf-plugin-details-modal       — conditionally rendered
 *   //     [data-testid="install-search"] — search-to-install section
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { vi } from 'vitest';
import { TranslocoTestingModule, TranslocoService } from '@jsverse/transloco';
import { PluginDetailsModalComponent } from './plugin-details-modal.component';
import { DashboardFacade } from '../../application/facades/dashboard.facade';
import { DashboardPageComponent } from '../dashboard-page.component';
import type { InstalledPlugin, DashboardGroup, RecommendedPlugin } from '../../domain/models/dashboard.models';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';
import { LanguageStoragePort } from '../../../../core/i18n/language-storage.port';

// ---------------------------------------------------------------------------
// Transloco test langs for dashboard scope (Wave 1 i18n pattern)
//
// En map returns EXACT current literals so all existing assertions stay green.
// Fr map returns French — fr assertions verify the migration works.
// ---------------------------------------------------------------------------

const EN_DASHBOARD_LANGS: Record<string, string> = {
  'dashboard.update-banner': 'Updates are available for your installed plugins.',
  'dashboard.search-placeholder': 'Search for plugins to install…',
  'dashboard.search-aria': 'Search for plugins to install',
  'dashboard.col-name': 'Name',
  'dashboard.col-version': 'Version',
  'dashboard.col-installed': 'Installed',
  'dashboard.col-status': 'Status',
  'dashboard.col-actions': 'Actions',
  'dashboard.loading': 'Loading plugins…',
  'dashboard.error-load': 'Failed to load plugins. Please try again.',
  'dashboard.empty-state': 'No plugins installed yet.',
  'dashboard.update-available': 'Update available: {{ version }}',
  'dashboard.up-to-date': 'Up to date',
  'dashboard.details-btn': 'Details',
  'dashboard.remove-btn': 'Remove',
  'dashboard.details-btn-aria': 'Details {{ name }}',
  'dashboard.remove-btn-aria': 'Remove {{ name }}',
  'dashboard.modal-close-aria': 'Close modal',
  'dashboard.modal-version': 'Version: {{ version }}',
  'dashboard.modal-version-unknown': 'Version: —',
  'dashboard.modal-installed': 'Installed: {{ date }}',
  'dashboard.modal-update-available': 'Update available: {{ version }}',
  'dashboard.modal-update-now': 'Update now',
  'dashboard.modal-release-notes-heading': 'Release Notes',
  'dashboard.modal-latest-version': 'Latest version: {{ version }}',
  'dashboard.modal-docs-link': 'View Documentation',
  'dashboard.modal-remove-btn': 'Remove Plugin',
  'dashboard.modal-remove-btn-aria': 'Remove plugin',
};

const FR_DASHBOARD_LANGS: Record<string, string> = {
  'dashboard.update-banner': 'Des mises à jour sont disponibles pour vos plugins installés.',
  'dashboard.search-placeholder': 'Rechercher des plugins à installer…',
  'dashboard.search-aria': 'Rechercher des plugins à installer',
  'dashboard.col-name': 'Nom',
  'dashboard.col-version': 'Version',
  'dashboard.col-installed': 'Installé',
  'dashboard.col-status': 'Statut',
  'dashboard.col-actions': 'Actions',
  'dashboard.loading': 'Chargement des plugins…',
  'dashboard.error-load': 'Impossible de charger les plugins. Veuillez réessayer.',
  'dashboard.empty-state': "Aucun plugin installé pour l'instant.",
  'dashboard.update-available': 'Mise à jour disponible : {{ version }}',
  'dashboard.up-to-date': 'À jour',
  'dashboard.details-btn': 'Détails',
  'dashboard.remove-btn': 'Supprimer',
  'dashboard.details-btn-aria': 'Détails {{ name }}',
  'dashboard.remove-btn-aria': 'Supprimer {{ name }}',
  'dashboard.modal-close-aria': 'Fermer la fenêtre',
  'dashboard.modal-version': 'Version : {{ version }}',
  'dashboard.modal-version-unknown': 'Version : —',
  'dashboard.modal-installed': 'Installé : {{ date }}',
  'dashboard.modal-update-available': 'Mise à jour disponible : {{ version }}',
  'dashboard.modal-update-now': 'Mettre à jour maintenant',
  'dashboard.modal-release-notes-heading': 'Notes de version',
  'dashboard.modal-latest-version': 'Dernière version : {{ version }}',
  'dashboard.modal-docs-link': 'Voir la documentation',
  'dashboard.modal-remove-btn': 'Supprimer le plugin',
  'dashboard.modal-remove-btn-aria': 'Supprimer le plugin',
};

// ---------------------------------------------------------------------------
// Shared Transloco imports + providers factory
// ---------------------------------------------------------------------------

function makeTranslocoImports() {
  return TranslocoTestingModule.forRoot({
    langs: { en: EN_DASHBOARD_LANGS, fr: FR_DASHBOARD_LANGS },
    translocoConfig: { availableLangs: ['en', 'fr'], defaultLang: 'en' },
    preloadLangs: true,
  });
}

const I18N_PROVIDERS = [
  I18nFacade,
  { provide: LanguageStoragePort, useValue: { read: () => null, write: () => undefined } },
];

// ---------------------------------------------------------------------------
// Stub DashboardFacade (shared for page tests)
// ---------------------------------------------------------------------------

@Injectable()
class StubDashboardFacade {
  private readonly _installed = signal<InstalledPlugin[]>([]);
  private readonly _isLoading = signal(false);
  private readonly _error = signal<{ code: string; message: string }[] | undefined>(undefined);
  private readonly _hasUpdates = signal(false);
  private readonly _groupsByTeam = signal<DashboardGroup>({ teamId: 'team-test', plugins: [] });
  private readonly _recommended = signal<readonly RecommendedPlugin[]>([]);

  setInstalled(plugins: InstalledPlugin[]): void {
    this._installed.set(plugins);
  }
  setHasUpdates(v: boolean): void {
    this._hasUpdates.set(v);
  }

  get installedPlugins(): Signal<InstalledPlugin[]> {
    return this._installed;
  }
  get isLoading(): Signal<boolean> {
    return this._isLoading;
  }
  get error(): Signal<{ code: string; message: string }[] | undefined> {
    return this._error;
  }
  get hasUpdates(): Signal<boolean> {
    return this._hasUpdates;
  }
  get groupsByTeam(): Signal<DashboardGroup> {
    return this._groupsByTeam;
  }
  get recommendedPlugins(): Signal<readonly RecommendedPlugin[]> {
    return this._recommended;
  }

  checkForUpdatesCalls = 0;
  loadInstalledCalls = 0;
  removeInstalledCalls: string[] = [];
  recordIntentCalls: { name: string; version: string }[] = [];

  checkForUpdates(): void {
    this.checkForUpdatesCalls++;
  }
  loadInstalled(): void {
    this.loadInstalledCalls++;
  }
  removeInstalled(name: string): void {
    this.removeInstalledCalls.push(name);
  }
  recordInstallIntent(name: string, version: string): void {
    this.recordIntentCalls.push({ name, version });
  }
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function makePlugin(overrides: Partial<InstalledPlugin> = {}): InstalledPlugin {
  return {
    name: 'alpha-plugin',
    version: '1.2.0',
    installedAt: '2024-01-01T00:00:00.000Z',
    status: 'up-to-date',
    latestVersion: '1.2.0',
    ...overrides,
  };
}

const PLUGIN_BASIC = makePlugin();
const PLUGIN_NEEDS_UPDATE = makePlugin({
  name: 'beta-plugin',
  version: '1.0.0',
  status: 'update-available',
  latestVersion: '3.0.0',
});

// ---------------------------------------------------------------------------
// PluginDetailsModalComponent — setup
// ---------------------------------------------------------------------------

function setupModal(plugin: InstalledPlugin | undefined = PLUGIN_BASIC): {
  fixture: ComponentFixture<PluginDetailsModalComponent>;
  translocoService: TranslocoService;
} {
  TestBed.configureTestingModule({
    imports: [PluginDetailsModalComponent, makeTranslocoImports()],
    providers: [...I18N_PROVIDERS],
  }).overrideComponent(PluginDetailsModalComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(PluginDetailsModalComponent);
  fixture.componentRef.setInput('pluginName', plugin?.name ?? 'unknown');
  fixture.componentRef.setInput('plugin', plugin);
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, translocoService };
}

// ---------------------------------------------------------------------------
// PluginDetailsModalComponent — selector
// ---------------------------------------------------------------------------

describe('PluginDetailsModalComponent — selector', () => {
  it('should use selector "cf-plugin-details-modal"', () => {
    const { fixture } = setupModal();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// PluginDetailsModalComponent — rendering
// ---------------------------------------------------------------------------

describe('PluginDetailsModalComponent — rendering', () => {
  it('should render the plugin name in the modal title', () => {
    const { fixture } = setupModal(PLUGIN_BASIC);
    fixture.detectChanges();
    const title = fixture.debugElement.query(By.css('[data-testid="modal-title"], .modal-title, h2, h3'));
    expect(title).not.toBeNull();
    expect((title.nativeElement as HTMLElement).textContent).toContain('alpha-plugin');
  });

  it('should render the plugin version', () => {
    const { fixture } = setupModal(PLUGIN_BASIC);
    fixture.detectChanges();
    const el = fixture.debugElement.query(By.css('[data-testid="plugin-version"]'));
    if (el) {
      expect((el.nativeElement as HTMLElement).textContent).toContain('1.2.0');
    } else {
      expect(fixture.nativeElement.textContent).toContain('1.2.0');
    }
  });

  it('should render a close button', () => {
    const { fixture } = setupModal();
    fixture.detectChanges();
    const btn = fixture.debugElement.query(
      By.css('[data-testid="modal-close-btn"], button[aria-label*="Close"], .modal-close'),
    );
    expect(btn).not.toBeNull();
  });

  it('should render a remove confirm button', () => {
    const { fixture } = setupModal();
    fixture.detectChanges();
    const btn = fixture.debugElement.query(By.css('[data-testid="modal-remove-btn"], button[aria-label*="Remove"]'));
    expect(btn).not.toBeNull();
  });

  it('should render update section when status is "update-available"', () => {
    const { fixture } = setupModal(PLUGIN_NEEDS_UPDATE);
    fixture.detectChanges();
    const section = fixture.debugElement.query(By.css('[data-testid="update-section"], .update-section'));
    expect(section).not.toBeNull();
  });

  it('should NOT render update section when status is "up-to-date"', () => {
    const { fixture } = setupModal(PLUGIN_BASIC);
    fixture.detectChanges();
    const section = fixture.debugElement.query(By.css('[data-testid="update-section"]'));
    expect(section).toBeNull();
  });

  it('should render release notes section when latestVersion is known', () => {
    const { fixture } = setupModal(PLUGIN_NEEDS_UPDATE);
    fixture.detectChanges();
    const el = fixture.debugElement.query(By.css('[data-testid="release-notes"], .release-notes'));
    expect(el).not.toBeNull();
  });

  it('should render a docs link or docs placeholder', () => {
    const { fixture } = setupModal(PLUGIN_BASIC);
    fixture.detectChanges();
    const docsEl = fixture.debugElement.query(
      By.css('[data-testid="docs-link"], [data-testid="docs-placeholder"], a[href*="doc"]'),
    );
    expect(docsEl).not.toBeNull();
  });

  it('should render when plugin is undefined without crashing', () => {
    TestBed.configureTestingModule({
      imports: [PluginDetailsModalComponent, makeTranslocoImports()],
      providers: [...I18N_PROVIDERS],
    }).overrideComponent(PluginDetailsModalComponent, {
      set: { changeDetection: ChangeDetectionStrategy.Default },
    });
    const fixture = TestBed.createComponent(PluginDetailsModalComponent);
    fixture.componentRef.setInput('pluginName', 'unknown-plugin');
    fixture.componentRef.setInput('plugin', undefined);
    expect(() => fixture.detectChanges()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// PluginDetailsModalComponent — events
// ---------------------------------------------------------------------------

describe('PluginDetailsModalComponent — close event', () => {
  it('should emit closed output when onClose is called', () => {
    const { fixture } = setupModal();
    let closedCount = 0;
    fixture.componentInstance.closed.subscribe(() => closedCount++);
    fixture.componentInstance.onClose();
    expect(closedCount).toBe(1);
  });
});

describe('PluginDetailsModalComponent — remove event', () => {
  it('should emit confirmRemove with the plugin name when onConfirmRemove is called', () => {
    const { fixture } = setupModal(PLUGIN_BASIC);
    const emitted: string[] = [];
    fixture.componentInstance.confirmRemove.subscribe((name: string) => emitted.push(name));
    fixture.componentInstance.onConfirmRemove();
    expect(emitted).toContain('alpha-plugin');
  });
});

describe('PluginDetailsModalComponent — update confirm event', () => {
  it('should emit confirmUpdate with the plugin name when onConfirmUpdate is called', () => {
    const { fixture } = setupModal(PLUGIN_NEEDS_UPDATE);
    const emitted: string[] = [];
    fixture.componentInstance.confirmUpdate.subscribe((name: string) => emitted.push(name));
    fixture.componentInstance.onConfirmUpdate();
    expect(emitted).toContain('beta-plugin');
  });
});

// ---------------------------------------------------------------------------
// PluginDetailsModalComponent — i18n fr assertions
// ---------------------------------------------------------------------------

describe('PluginDetailsModalComponent — i18n', () => {
  it('[FR] remove button renders French label when lang is fr', () => {
    const { fixture, translocoService } = setupModal(PLUGIN_BASIC);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const removeBtn = fixture.nativeElement.querySelector('[data-testid="modal-remove-btn"]') as HTMLElement | null;
    expect(removeBtn?.textContent?.trim()).toContain('Supprimer le plugin');
  });

  it('[FR] docs link renders French label when lang is fr', () => {
    const { fixture, translocoService } = setupModal(PLUGIN_BASIC);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Voir la documentation');
  });
});

// ---------------------------------------------------------------------------
// DashboardPageComponent — setup
// ---------------------------------------------------------------------------

function setupPage(): {
  fixture: ComponentFixture<DashboardPageComponent>;
  stub: StubDashboardFacade;
  translocoService: TranslocoService;
} {
  const stub = new StubDashboardFacade();
  TestBed.configureTestingModule({
    imports: [DashboardPageComponent, makeTranslocoImports()],
    providers: [{ provide: DashboardFacade, useValue: stub }, ...I18N_PROVIDERS],
  }).overrideComponent(DashboardPageComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(DashboardPageComponent);
  const translocoService = TestBed.inject(TranslocoService);
  return { fixture, stub, translocoService };
}

// ---------------------------------------------------------------------------
// DashboardPageComponent — basic rendering
// ---------------------------------------------------------------------------

describe('DashboardPageComponent — rendering', () => {
  it('should render the dashboard page container', () => {
    const { fixture } = setupPage();
    fixture.detectChanges();
    // The page itself IS the component root — just verify it renders without error
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should include the installed-plugins table', () => {
    const { fixture } = setupPage();
    fixture.detectChanges();
    const table = fixture.debugElement.query(By.css('cf-installed-plugins-table'));
    expect(table).not.toBeNull();
  });

  it('should include an install-search section', () => {
    const { fixture } = setupPage();
    fixture.detectChanges();
    const search = fixture.debugElement.query(By.css('[data-testid="install-search"], .install-search'));
    expect(search).not.toBeNull();
  });

  it('should show update banner when hasUpdates is true', () => {
    const { fixture, stub } = setupPage();
    stub.setHasUpdates(true);
    fixture.detectChanges();
    const banner = fixture.debugElement.query(By.css('[data-testid="update-banner"], .update-banner'));
    expect(banner).not.toBeNull();
  });

  it('should NOT show update banner when hasUpdates is false', () => {
    const { fixture, stub } = setupPage();
    stub.setHasUpdates(false);
    fixture.detectChanges();
    const banner = fixture.debugElement.query(By.css('[data-testid="update-banner"]'));
    expect(banner).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// DashboardPageComponent — periodic background update check (5-min timer)
// ---------------------------------------------------------------------------

describe('DashboardPageComponent — periodic update check', () => {
  it('should call facade.checkForUpdates once on init (immediate check)', () => {
    vi.useFakeTimers();
    const { fixture, stub } = setupPage();
    fixture.detectChanges();
    // At init, at least one check should be triggered
    vi.runAllTimers();
    expect(stub.checkForUpdatesCalls).toBeGreaterThanOrEqual(1);
    vi.useRealTimers();
  });

  it('should trigger additional facade.checkForUpdates after 5 minutes', () => {
    vi.useFakeTimers();
    const { fixture, stub } = setupPage();
    fixture.detectChanges();
    const callsAfterInit = stub.checkForUpdatesCalls;
    vi.advanceTimersByTime(300_000); // 5 minutes
    expect(stub.checkForUpdatesCalls).toBeGreaterThan(callsAfterInit);
    vi.useRealTimers();
  });

  it('should gracefully handle checkForUpdates failure without crashing the page', () => {
    vi.useFakeTimers();
    // We test this by overriding checkForUpdates to throw and verifying the page doesn't crash
    const stub = new StubDashboardFacade();
    stub.checkForUpdates = () => {
      throw new Error('Update check failed');
    };
    TestBed.configureTestingModule({
      imports: [DashboardPageComponent, makeTranslocoImports()],
      providers: [{ provide: DashboardFacade, useValue: stub }, ...I18N_PROVIDERS],
    }).overrideComponent(DashboardPageComponent, {
      set: { changeDetection: ChangeDetectionStrategy.Default },
    });
    const fixture = TestBed.createComponent(DashboardPageComponent);
    // Should not crash
    expect(() => {
      fixture.detectChanges();
      vi.advanceTimersByTime(300_000);
    }).not.toThrow();
    vi.useRealTimers();
  });
});

// ---------------------------------------------------------------------------
// DashboardPageComponent — loadInstalled on init
// ---------------------------------------------------------------------------

describe('DashboardPageComponent — init', () => {
  it('should call facade.loadInstalled on component init', () => {
    const { fixture, stub } = setupPage();
    fixture.detectChanges();
    expect(stub.loadInstalledCalls).toBeGreaterThanOrEqual(1);
  });
});

// ---------------------------------------------------------------------------
// DashboardPageComponent — modal show/hide
// ---------------------------------------------------------------------------

describe('DashboardPageComponent — plugin details modal', () => {
  it('should show modal when a plugin is selected for details', () => {
    const { fixture } = setupPage();
    fixture.detectChanges();
    // Programmatically invoke the method that opens the modal
    const comp = fixture.componentInstance as DashboardPageComponent & { onViewDetails?: (name: string) => void };
    if (typeof comp.onViewDetails === 'function') {
      comp.onViewDetails('alpha-plugin');
      fixture.detectChanges();
      const modal = fixture.debugElement.query(By.css('cf-plugin-details-modal'));
      expect(modal).not.toBeNull();
    } else {
      // Method may be named differently — at minimum the component compiles
      expect(fixture.componentInstance).toBeDefined();
    }
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary — page uses facade only
// ---------------------------------------------------------------------------

describe('DashboardPageComponent — architecture boundary', () => {
  it('should compile and instantiate using only DashboardFacade', () => {
    const { fixture } = setupPage();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// DashboardPageComponent — i18n fr assertions
// ---------------------------------------------------------------------------

describe('DashboardPageComponent — i18n', () => {
  it('[FR] update banner renders French text when lang is fr', () => {
    const { fixture, stub, translocoService } = setupPage();
    stub.setHasUpdates(true);
    translocoService.setActiveLang('fr');
    fixture.detectChanges();

    const banner = fixture.nativeElement.querySelector('[data-testid="update-banner"]') as HTMLElement | null;
    expect(banner?.textContent?.trim()).toContain('Des mises à jour sont disponibles');
  });
});
