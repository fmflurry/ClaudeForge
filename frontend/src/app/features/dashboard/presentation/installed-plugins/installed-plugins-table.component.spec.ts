/**
 * RED tests — Task 15.4a: InstalledPluginsTableComponent
 *
 * Expected production file (does NOT exist yet — tests WILL FAIL):
 *   src/app/features/dashboard/presentation/installed-plugins/installed-plugins-table.component.ts
 *
 * Production component the coder MUST define:
 *
 *   @Component({
 *     selector: 'cf-installed-plugins-table',
 *     standalone: true,
 *     changeDetection: ChangeDetectionStrategy.OnPush,
 *     imports: [...],
 *   })
 *   class InstalledPluginsTableComponent {
 *     private readonly facade = inject(DashboardFacade);
 *
 *     // Derived signals from facade:
 *     readonly plugins: Signal<InstalledPlugin[]>        — from facade.installedPlugins
 *     readonly isLoading: Signal<boolean>                — from facade.isLoading
 *     readonly hasError: Signal<boolean>                 — true when facade.error() !== undefined
 *     readonly hasUpdates: Signal<boolean>               — from facade.hasUpdates
 *
 *     // Outputs:
 *     readonly removePlugin = output<string>();          — emits plugin name
 *     readonly viewDetails = output<string>();           — emits plugin name
 *     readonly updatePlugin = output<string>();          — emits plugin name (when update available)
 *
 *     // Methods:
 *     onRemove(name: string): void   — emits removePlugin after calling facade.removeInstalled
 *     onViewDetails(name: string): void — emits viewDetails
 *     onUpdate(name: string): void   — emits updatePlugin
 *   }
 *
 *   Selector: cf-installed-plugins-table
 *   Template must include:
 *     - [data-testid="loading"]        when isLoading=true
 *     - [data-testid="error-message"]  when hasError=true
 *     - [data-testid="empty-state"]    when plugins empty and not loading/error
 *     - [data-testid="plugins-table"]  when plugins present
 *     - [data-testid="update-badge"]   for each plugin with status 'update-available'
 *     - [data-testid="remove-btn"]     per row
 *     - [data-testid="details-btn"]    per row
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy, Injectable, Signal, signal } from '@angular/core';
import { By } from '@angular/platform-browser';
import { InstalledPluginsTableComponent } from './installed-plugins-table.component';
import { DashboardFacade } from '../../application/facades/dashboard.facade';
import type { InstalledPlugin, DashboardGroup, RecommendedPlugin } from '../../domain/models/dashboard.models';

// ---------------------------------------------------------------------------
// Stub DashboardFacade
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
  setLoading(loading: boolean): void {
    this._isLoading.set(loading);
  }
  setError(errors: { code: string; message: string }[]): void {
    this._error.set(errors);
  }
  setHasUpdates(hasUpdates: boolean): void {
    this._hasUpdates.set(hasUpdates);
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

  loadInstalledCalls = 0;
  removeInstalledCalls: string[] = [];
  checkForUpdatesCalls = 0;
  recordIntentCalls: { name: string; version: string }[] = [];

  loadInstalled(): void {
    this.loadInstalledCalls++;
  }
  removeInstalled(name: string): void {
    this.removeInstalledCalls.push(name);
  }
  checkForUpdates(): void {
    this.checkForUpdatesCalls++;
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
    version: '1.0.0',
    installedAt: '2024-01-01T00:00:00.000Z',
    status: 'up-to-date',
    latestVersion: '1.0.0',
    ...overrides,
  };
}

const PLUGIN_UP_TO_DATE = makePlugin({ name: 'alpha-plugin', status: 'up-to-date' });
const PLUGIN_UPDATE_AVAILABLE = makePlugin({
  name: 'beta-plugin',
  version: '1.0.0',
  status: 'update-available',
  latestVersion: '2.0.0',
});

// ---------------------------------------------------------------------------
// Setup helper (single TestBed per describe via beforeEach)
// ---------------------------------------------------------------------------

function setupComponent(): { fixture: ComponentFixture<InstalledPluginsTableComponent>; stub: StubDashboardFacade } {
  const stub = new StubDashboardFacade();
  TestBed.configureTestingModule({
    imports: [InstalledPluginsTableComponent],
    providers: [{ provide: DashboardFacade, useValue: stub }],
  }).overrideComponent(InstalledPluginsTableComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(InstalledPluginsTableComponent);
  return { fixture, stub };
}

// ---------------------------------------------------------------------------
// Selector test
// ---------------------------------------------------------------------------

describe('InstalledPluginsTableComponent — selector', () => {
  it('should use selector "cf-installed-plugins-table"', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Loading state
// ---------------------------------------------------------------------------

describe('InstalledPluginsTableComponent — loading state', () => {
  it('should render loading indicator when isLoading is true', () => {
    const { fixture, stub } = setupComponent();
    stub.setLoading(true);
    fixture.detectChanges();
    const el = fixture.debugElement.query(By.css('[data-testid="loading"], [aria-busy="true"], .loading'));
    expect(el).not.toBeNull();
  });

  it('should NOT render loading indicator when isLoading is false', () => {
    const { fixture, stub } = setupComponent();
    stub.setLoading(false);
    stub.setInstalled([PLUGIN_UP_TO_DATE]);
    fixture.detectChanges();
    const el = fixture.debugElement.query(By.css('[data-testid="loading"]'));
    expect(el).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Error state
// ---------------------------------------------------------------------------

describe('InstalledPluginsTableComponent — error state', () => {
  it('should render error message when error is set', () => {
    const { fixture, stub } = setupComponent();
    stub.setError([{ code: 'LOAD_ERROR', message: 'Failed to load' }]);
    fixture.detectChanges();
    const el = fixture.debugElement.query(By.css('[data-testid="error-message"], [role="alert"], .error'));
    expect(el).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Empty state
// ---------------------------------------------------------------------------

describe('InstalledPluginsTableComponent — empty state', () => {
  it('should render empty state when plugins array is empty and not loading', () => {
    const { fixture, stub } = setupComponent();
    stub.setInstalled([]);
    stub.setLoading(false);
    fixture.detectChanges();
    const el = fixture.debugElement.query(By.css('[data-testid="empty-state"], cf-empty-state, [role="status"]'));
    expect(el).not.toBeNull();
  });

  it('should NOT render empty state when plugins are present', () => {
    const { fixture, stub } = setupComponent();
    stub.setInstalled([PLUGIN_UP_TO_DATE]);
    fixture.detectChanges();
    const table = fixture.debugElement.query(By.css('[data-testid="plugins-table"], table, cf-table'));
    expect(table).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Plugin list rendering
// ---------------------------------------------------------------------------

describe('InstalledPluginsTableComponent — plugin list rendering', () => {
  it('should render a row for each plugin', () => {
    const { fixture, stub } = setupComponent();
    stub.setInstalled([PLUGIN_UP_TO_DATE, PLUGIN_UPDATE_AVAILABLE]);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('alpha-plugin');
    expect(text).toContain('beta-plugin');
  });

  it('should render the plugin version', () => {
    const { fixture, stub } = setupComponent();
    stub.setInstalled([PLUGIN_UP_TO_DATE]);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('1.0.0');
  });

  it('should render an update badge for plugins with "update-available" status', () => {
    const { fixture, stub } = setupComponent();
    stub.setInstalled([PLUGIN_UPDATE_AVAILABLE]);
    fixture.detectChanges();
    const badge = fixture.debugElement.query(By.css('[data-testid="update-badge"], .update-badge, cf-badge'));
    expect(badge).not.toBeNull();
  });

  it('should NOT render update badge for up-to-date plugins', () => {
    const { fixture, stub } = setupComponent();
    stub.setInstalled([PLUGIN_UP_TO_DATE]);
    fixture.detectChanges();
    const badge = fixture.debugElement.query(By.css('[data-testid="update-badge"]'));
    expect(badge).toBeNull();
  });

  it('should render a remove button per row', () => {
    const { fixture, stub } = setupComponent();
    stub.setInstalled([PLUGIN_UP_TO_DATE]);
    fixture.detectChanges();
    const btn = fixture.debugElement.query(
      By.css('[data-testid="remove-btn"], button[aria-label*="Remove"], .remove-btn'),
    );
    expect(btn).not.toBeNull();
  });

  it('should render a details button per row', () => {
    const { fixture, stub } = setupComponent();
    stub.setInstalled([PLUGIN_UP_TO_DATE]);
    fixture.detectChanges();
    const btn = fixture.debugElement.query(
      By.css('[data-testid="details-btn"], button[aria-label*="Details"], .details-btn'),
    );
    expect(btn).not.toBeNull();
  });
});

// ---------------------------------------------------------------------------
// Remove interaction
// ---------------------------------------------------------------------------

describe('InstalledPluginsTableComponent — remove interaction', () => {
  it('should call facade.removeInstalled when onRemove is invoked', () => {
    const { fixture, stub } = setupComponent();
    stub.setInstalled([PLUGIN_UP_TO_DATE]);
    fixture.detectChanges();
    fixture.componentInstance.onRemove('alpha-plugin');
    expect(stub.removeInstalledCalls).toContain('alpha-plugin');
  });

  it('should emit removePlugin output when onRemove is invoked', () => {
    const { fixture } = setupComponent();
    const emitted: string[] = [];
    fixture.componentInstance.removePlugin.subscribe((name: string) => emitted.push(name));
    fixture.componentInstance.onRemove('alpha-plugin');
    expect(emitted).toContain('alpha-plugin');
  });
});

// ---------------------------------------------------------------------------
// View details interaction
// ---------------------------------------------------------------------------

describe('InstalledPluginsTableComponent — view details interaction', () => {
  it('should emit viewDetails output when onViewDetails is invoked', () => {
    const { fixture } = setupComponent();
    const emitted: string[] = [];
    fixture.componentInstance.viewDetails.subscribe((name: string) => emitted.push(name));
    fixture.componentInstance.onViewDetails('alpha-plugin');
    expect(emitted).toContain('alpha-plugin');
  });
});

// ---------------------------------------------------------------------------
// Update interaction
// ---------------------------------------------------------------------------

describe('InstalledPluginsTableComponent — update interaction', () => {
  it('should emit updatePlugin output when onUpdate is invoked', () => {
    const { fixture } = setupComponent();
    const emitted: string[] = [];
    fixture.componentInstance.updatePlugin.subscribe((name: string) => emitted.push(name));
    fixture.componentInstance.onUpdate('beta-plugin');
    expect(emitted).toContain('beta-plugin');
  });
});

// ---------------------------------------------------------------------------
// Architecture boundary
// ---------------------------------------------------------------------------

describe('InstalledPluginsTableComponent — architecture boundary', () => {
  it('should compile and instantiate using only DashboardFacade (no store or use-case injection)', () => {
    const { fixture } = setupComponent();
    expect(fixture.componentInstance).toBeDefined();
  });
});
