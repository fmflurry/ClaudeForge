/**
 * Unit spec for StatsBandComponent — RED phase.
 *
 * All assertions are intentionally written against the full GREEN implementation
 * contract. The stub renders nothing, so all tests fail until the coder
 * delivers the real component.
 *
 * Naming conventions follow landing-page.component.spec.ts (same repo).
 * Test framework: Vitest + Angular Testing Library via @angular/core/testing.
 * Change detection: zoneless (provideZonelessChangeDetection from test-providers.ts).
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Injectable, signal, Signal } from '@angular/core';
import { StatsBandComponent } from './stats-band.component';
import { HomeMetricsFacade } from '../../application/facades/home-metrics.facade';
import type { MarketplaceMetrics } from '../../domain/models/marketplace-metrics.model';

// ---------------------------------------------------------------------------
// Stub facade — injectable, controllable signals
// ---------------------------------------------------------------------------

@Injectable()
class StubHomeMetricsFacade {
  private readonly _isLoadingStats = signal<boolean>(false);
  private readonly _stats = signal<MarketplaceMetrics | null>(null);
  private readonly _statsError = signal<string | undefined>(undefined);

  // Controllable setters for test scenarios
  setLoading(value: boolean): void {
    this._isLoadingStats.set(value);
  }

  setStats(metrics: MarketplaceMetrics | null): void {
    this._stats.set(metrics);
  }

  setError(message: string | undefined): void {
    this._statsError.set(message);
  }

  // Public signal surface matching HomeMetricsFacade
  readonly isLoadingStats: Signal<boolean> = this._isLoadingStats.asReadonly();
  readonly stats: Signal<MarketplaceMetrics | null> = this._stats.asReadonly();
  readonly statsError: Signal<string | undefined> = this._statsError.asReadonly();

  // Call tracking
  loadStatsCalls = 0;
  loadStats(): void {
    this.loadStatsCalls += 1;
  }
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const SAMPLE_METRICS: MarketplaceMetrics = {
  totalPlugins: 2547,
  totalDownloads: 1_800_000,
  publisherCount: 89,
  categoryCount: 12,
};

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

function setup(initialise?: (stub: StubHomeMetricsFacade) => void): {
  fixture: ComponentFixture<StatsBandComponent>;
  component: StatsBandComponent;
  stub: StubHomeMetricsFacade;
  el: HTMLElement;
} {
  const stub = new StubHomeMetricsFacade();
  initialise?.(stub);

  TestBed.configureTestingModule({
    imports: [StatsBandComponent],
    providers: [{ provide: HomeMetricsFacade, useValue: stub }],
  });

  const fixture = TestBed.createComponent(StatsBandComponent);
  const component = fixture.componentInstance;
  fixture.detectChanges();

  return { fixture, component, stub, el: fixture.nativeElement as HTMLElement };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('StatsBandComponent', () => {
  // -------------------------------------------------------------------------
  // Facade wiring
  // -------------------------------------------------------------------------

  describe('facade wiring', () => {
    it('injects HomeMetricsFacade', () => {
      const { component } = setup();
      // The real component must expose the facade via inject(HomeMetricsFacade).
      // We verify the facade instance is the stub (not a real one with a port dep).
      expect(component.facade).toBeInstanceOf(StubHomeMetricsFacade);
    });

    it('calls facade.loadStats() exactly once on init', () => {
      const { stub } = setup();
      expect(stub.loadStatsCalls).toBe(1);
    });

    it('does NOT call loadStats() more than once on a single init', () => {
      const { stub } = setup();
      expect(stub.loadStatsCalls).toBeLessThanOrEqual(1);
    });
  });

  // -------------------------------------------------------------------------
  // LOADING state
  // -------------------------------------------------------------------------

  describe('LOADING state', () => {
    it('renders a region with role="status" aria-live="polite" while loading', () => {
      const { el, fixture, stub } = setup();
      stub.setLoading(true);
      fixture.detectChanges();

      const statusEl = el.querySelector('[role="status"][aria-live="polite"]') as HTMLElement | null;
      expect(statusEl).not.toBeNull();
    });

    it('loading region contains descriptive text about loading state', () => {
      const { el, fixture, stub } = setup();
      stub.setLoading(true);
      fixture.detectChanges();

      const statusEl = el.querySelector('[role="status"]') as HTMLElement | null;
      expect(statusEl?.textContent?.toLowerCase()).toContain('loading');
    });

    it('does NOT render metric cards while loading', () => {
      const { el, fixture, stub } = setup();
      stub.setLoading(true);
      fixture.detectChanges();

      // There should be no metric card articles/items while loading
      const cards = el.querySelectorAll('[data-testid="stat-card"], .sb-stat-card, article');
      // We expect zero fully-rendered metric cards in loading state
      // (stub renders nothing, confirming RED; GREEN must also honour this)
      expect(cards.length).toBe(0);
    });
  });

  // -------------------------------------------------------------------------
  // ERROR state
  // -------------------------------------------------------------------------

  describe('ERROR state', () => {
    it('renders an element with role="alert" on stats error', () => {
      const { el, fixture, stub } = setup();
      stub.setError('Network error');
      fixture.detectChanges();

      const alertEl = el.querySelector('[role="alert"]') as HTMLElement | null;
      expect(alertEl).not.toBeNull();
    });

    it('error message element contains the error text or a generic message', () => {
      const { el, fixture, stub } = setup();
      stub.setError('Network error');
      fixture.detectChanges();

      const alertEl = el.querySelector('[role="alert"]') as HTMLElement | null;
      // Must contain either the error message or a fallback generic message
      const text = alertEl?.textContent?.toLowerCase() ?? '';
      const hasContent =
        text.includes('network error') ||
        text.includes('could not load') ||
        text.includes('error') ||
        text.includes('try');
      expect(hasContent).toBe(true);
    });

    it('renders a retry button in the error state', () => {
      const { el, fixture, stub } = setup();
      stub.setError('Network error');
      fixture.detectChanges();

      // The spec requires a retry affordance (button or link)
      const retryBtn =
        (el.querySelector('[data-testid="retry-btn"]') as HTMLElement | null) ??
        (el.querySelector('button') as HTMLElement | null);
      expect(retryBtn).not.toBeNull();
    });

    it('clicking the retry button calls facade.loadStats() again', () => {
      const { el, fixture, stub } = setup();
      stub.setError('Network error');
      fixture.detectChanges();

      const initialCalls = stub.loadStatsCalls;

      const retryBtn =
        (el.querySelector('[data-testid="retry-btn"]') as HTMLButtonElement | null) ??
        (el.querySelector('button') as HTMLButtonElement | null);

      retryBtn?.click();
      fixture.detectChanges();

      expect(stub.loadStatsCalls).toBe(initialCalls + 1);
    });

    it('does NOT render metric cards in error state', () => {
      const { el, fixture, stub } = setup();
      stub.setError('fail');
      fixture.detectChanges();

      const cards = el.querySelectorAll('[data-testid="stat-card"], .sb-stat-card, article');
      expect(cards.length).toBe(0);
    });
  });

  // -------------------------------------------------------------------------
  // NULL / EMPTY state (stats === null, not loading, no error)
  // -------------------------------------------------------------------------

  describe('NULL / EMPTY state', () => {
    it('does not crash when stats is null and not loading', () => {
      expect(() => {
        const { fixture } = setup((stub) => {
          stub.setStats(null);
          stub.setLoading(false);
          stub.setError(undefined);
        });
        fixture.detectChanges();
      }).not.toThrow();
    });

    it('renders no metric cards when stats is null', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats(null);
        stub.setLoading(false);
        stub.setError(undefined);
      });
      fixture.detectChanges();

      // Either hidden or a neutral placeholder — must not render 4 metric cards
      const cards = el.querySelectorAll('[data-testid="stat-card"]');
      expect(cards.length).toBe(0);
    });
  });

  // -------------------------------------------------------------------------
  // SUCCESS state — four metric cards rendered
  // -------------------------------------------------------------------------

  describe('SUCCESS state', () => {
    it('renders exactly four metric cards', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats(SAMPLE_METRICS);
      });
      fixture.detectChanges();

      const cards = el.querySelectorAll('[data-testid="stat-card"]');
      expect(cards.length).toBe(4);
    });

    it('renders the total plugins count', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, totalPlugins: 2547 });
      });
      fixture.detectChanges();

      // 2547 → "2.5k" (formatDownloads / formatMetricCount style)
      expect(el.textContent).toContain('2.5k');
    });

    it('renders the total downloads count formatted as millions for >= 1M', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, totalDownloads: 1_800_000 });
      });
      fixture.detectChanges();

      // 1_800_000 → "1.8M"
      expect(el.textContent).toContain('1.8M');
    });

    it('renders the publisher count as plain number when < 1k', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, publisherCount: 89 });
      });
      fixture.detectChanges();

      expect(el.textContent).toContain('89');
    });

    it('renders the category count as plain number when < 1k', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, categoryCount: 12 });
      });
      fixture.detectChanges();

      expect(el.textContent).toContain('12');
    });

    it('formats exactly 1000 downloads as "1.0k"', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, totalDownloads: 1000 });
      });
      fixture.detectChanges();

      expect(el.textContent).toContain('1.0k');
    });

    it('formats exactly 1500 plugins as "1.5k"', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, totalPlugins: 1500 });
      });
      fixture.detectChanges();

      expect(el.textContent).toContain('1.5k');
    });

    it('formats exactly 2_000_000 downloads as "2.0M"', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, totalDownloads: 2_000_000 });
      });
      fixture.detectChanges();

      expect(el.textContent).toContain('2.0M');
    });

    it('formats 999 plugins as plain "999"', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, totalPlugins: 999 });
      });
      fixture.detectChanges();

      expect(el.textContent).toContain('999');
    });

    it('does NOT render role="alert" in success state', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats(SAMPLE_METRICS);
      });
      fixture.detectChanges();

      const alertEl = el.querySelector('[role="alert"]');
      expect(alertEl).toBeNull();
    });

    it('does NOT render loading status region in success state', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats(SAMPLE_METRICS);
      });
      fixture.detectChanges();

      const statusEl = el.querySelector('[role="status"][aria-live="polite"]');
      expect(statusEl).toBeNull();
    });
  });

  // -------------------------------------------------------------------------
  // Accessibility
  // -------------------------------------------------------------------------

  describe('accessibility', () => {
    it('each metric card has an accessible label (aria-label or visible text label)', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats(SAMPLE_METRICS);
      });
      fixture.detectChanges();

      const cards = el.querySelectorAll('[data-testid="stat-card"]');
      expect(cards.length).toBe(4);

      cards.forEach((card) => {
        // Either aria-label on the card or a child element acting as label
        const hasAriaLabel = card.hasAttribute('aria-label');
        // Or there's a visible text child (dt, caption, span, p, h3 etc) that labels the metric
        const hasLabelChild = card.querySelector('[aria-label], dt, h3, h4, p, caption, .sb-stat-card__label') !== null;
        expect(hasAriaLabel || hasLabelChild).toBe(true);
      });
    });

    it('metric cards use semantic HTML (article, section, or role="region")', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats(SAMPLE_METRICS);
      });
      fixture.detectChanges();

      const cards = el.querySelectorAll('[data-testid="stat-card"]');
      cards.forEach((card) => {
        const tagName = card.tagName.toLowerCase();
        const roleAttr = card.getAttribute('role');
        const isSemantic = tagName === 'article' || tagName === 'section' || roleAttr === 'region';
        expect(isSemantic).toBe(true);
      });
    });

    it('the stats band section is labelled for screen readers', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats(SAMPLE_METRICS);
      });
      fixture.detectChanges();

      // The outermost section element must have aria-label or aria-labelledby
      const section =
        (el.querySelector('section') as HTMLElement | null) ??
        (el.querySelector('[role="region"]') as HTMLElement | null);
      expect(section).not.toBeNull();
      const hasLabel = section?.hasAttribute('aria-label') || section?.hasAttribute('aria-labelledby');
      expect(hasLabel).toBe(true);
    });
  });

  // -------------------------------------------------------------------------
  // Reactive updates
  // -------------------------------------------------------------------------

  describe('reactive signal updates', () => {
    it('transitions from LOADING to SUCCESS without a crash', () => {
      const { fixture, stub, el } = setup((s) => {
        s.setLoading(true);
      });
      fixture.detectChanges();

      // Transition to success
      stub.setLoading(false);
      stub.setStats(SAMPLE_METRICS);
      fixture.detectChanges();

      const cards = el.querySelectorAll('[data-testid="stat-card"]');
      expect(cards.length).toBe(4);
    });

    it('transitions from LOADING to ERROR without a crash', () => {
      const { fixture, stub, el } = setup((s) => {
        s.setLoading(true);
      });
      fixture.detectChanges();

      stub.setLoading(false);
      stub.setError('timeout');
      fixture.detectChanges();

      const alertEl = el.querySelector('[role="alert"]');
      expect(alertEl).not.toBeNull();
    });

    it('re-renders with updated metric values when stats signal changes', () => {
      const { fixture, stub, el } = setup((s) => {
        s.setStats({ ...SAMPLE_METRICS, totalPlugins: 100 });
      });
      fixture.detectChanges();
      expect(el.textContent).toContain('100');

      stub.setStats({ ...SAMPLE_METRICS, totalPlugins: 5000 });
      fixture.detectChanges();
      expect(el.textContent).toContain('5.0k');
    });
  });

  // -------------------------------------------------------------------------
  // Edge cases
  // -------------------------------------------------------------------------

  describe('edge cases', () => {
    it('handles totalDownloads = 0 without crash (renders "0")', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, totalDownloads: 0 });
      });
      fixture.detectChanges();

      expect(el.textContent).toContain('0');
    });

    it('handles all zero metrics without crash', () => {
      expect(() => {
        const { fixture } = setup((stub) => {
          stub.setStats({ totalPlugins: 0, totalDownloads: 0, publisherCount: 0, categoryCount: 0 });
        });
        fixture.detectChanges();
      }).not.toThrow();
    });

    it('handles very large totalDownloads (999_999_999) formatted as M', () => {
      const { el, fixture } = setup((stub) => {
        stub.setStats({ ...SAMPLE_METRICS, totalDownloads: 999_999_999 });
      });
      fixture.detectChanges();

      expect(el.textContent).toContain('M');
    });
  });
});
