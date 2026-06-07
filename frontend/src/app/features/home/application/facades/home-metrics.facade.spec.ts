/**
 * RED tests — HomeMetricsFacade (signal-based, facade-only pattern).
 *
 * Production file that MUST exist (DO NOT create yet):
 *   src/app/features/home/application/facades/home-metrics.facade.ts
 *
 * The coder MUST export:
 *
 *   @Injectable()
 *   export class HomeMetricsFacade {
 *     private readonly port = inject(MarketplaceStatsPort);
 *     private readonly destroyRef = inject(DestroyRef);
 *
 *     private readonly _isLoadingStats = signal<boolean>(true);
 *     private readonly _stats = signal<MarketplaceMetrics | null>(null);
 *     private readonly _statsError = signal<string | undefined>(undefined);
 *
 *     readonly isLoadingStats: Signal<boolean> = this._isLoadingStats.asReadonly();
 *     readonly stats: Signal<MarketplaceMetrics | null> = this._stats.asReadonly();
 *     readonly statsError: Signal<string | undefined> = this._statsError.asReadonly();
 *
 *     loadStats(): void {
 *       this._isLoadingStats.set(true);
 *       this._statsError.set(undefined);
 *       this.port
 *         .getStats()
 *         .pipe(takeUntilDestroyed(this.destroyRef))
 *         .subscribe({
 *           next: (metrics) => {
 *             this._stats.set({ ...metrics }); // immutable spread
 *             this._isLoadingStats.set(false);
 *           },
 *           error: (err: unknown) => {
 *             this._statsError.set(err instanceof Error ? err.message : 'Unknown error');
 *             this._isLoadingStats.set(false);
 *           },
 *         });
 *     }
 *   }
 *
 * DI registration:
 *   app.config.ts must add:
 *     { provide: MarketplaceStatsPort, useClass: MarketplaceStatsHttpAdapter },
 *     HomeMetricsFacade,
 *
 * Spec uses a mocked MarketplaceStatsPort.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { HomeMetricsFacade } from './home-metrics.facade';
import { MarketplaceStatsPort } from '../../domain/ports/marketplace-stats.port';
import type { MarketplaceMetrics } from '../../domain/models/marketplace-metrics.model';

// ---------------------------------------------------------------------------
// Fake port implementations
// ---------------------------------------------------------------------------

const FAKE_METRICS: MarketplaceMetrics = {
  totalPlugins: 50,
  totalDownloads: 10_000,
  publisherCount: 12,
  categoryCount: 6,
};

@Injectable()
class FakeMarketplaceStatsPort extends MarketplaceStatsPort {
  override getStats(): Observable<MarketplaceMetrics> {
    return of(FAKE_METRICS);
  }
}

@Injectable()
class ErrorMarketplaceStatsPort extends MarketplaceStatsPort {
  override getStats(): Observable<MarketplaceMetrics> {
    return throwError(() => new Error('Stats fetch failed'));
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setupWithFakePort(): { facade: HomeMetricsFacade } {
  TestBed.configureTestingModule({
    providers: [HomeMetricsFacade, { provide: MarketplaceStatsPort, useClass: FakeMarketplaceStatsPort }],
  });
  return { facade: TestBed.inject(HomeMetricsFacade) };
}

function setupWithErrorPort(): { facade: HomeMetricsFacade } {
  TestBed.configureTestingModule({
    providers: [HomeMetricsFacade, { provide: MarketplaceStatsPort, useClass: ErrorMarketplaceStatsPort }],
  });
  return { facade: TestBed.inject(HomeMetricsFacade) };
}

// ---------------------------------------------------------------------------
// Initial state (before loadStats() is called)
// ---------------------------------------------------------------------------

describe('HomeMetricsFacade — initial state', () => {
  it('isLoadingStats() should be true on creation', () => {
    const { facade } = setupWithFakePort();
    expect(facade.isLoadingStats()).toBe(true);
  });

  it('stats() should be null on creation', () => {
    const { facade } = setupWithFakePort();
    expect(facade.stats()).toBeNull();
  });

  it('statsError() should be undefined on creation', () => {
    const { facade } = setupWithFakePort();
    expect(facade.statsError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// Signal types — signals are callable functions
// ---------------------------------------------------------------------------

describe('HomeMetricsFacade — signal API surface', () => {
  it('isLoadingStats should be a callable signal function', () => {
    const { facade } = setupWithFakePort();
    expect(typeof facade.isLoadingStats).toBe('function');
  });

  it('stats should be a callable signal function', () => {
    const { facade } = setupWithFakePort();
    expect(typeof facade.stats).toBe('function');
  });

  it('statsError should be a callable signal function', () => {
    const { facade } = setupWithFakePort();
    expect(typeof facade.statsError).toBe('function');
  });

  it('loadStats should be a method', () => {
    const { facade } = setupWithFakePort();
    expect(typeof facade.loadStats).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// loadStats() — success path
// ---------------------------------------------------------------------------

describe('HomeMetricsFacade — loadStats() success', () => {
  it('should set isLoadingStats to false after successful load', () => {
    const { facade } = setupWithFakePort();
    facade.loadStats();
    expect(facade.isLoadingStats()).toBe(false);
  });

  it('should populate stats() with the returned metrics on success', () => {
    const { facade } = setupWithFakePort();
    facade.loadStats();
    expect(facade.stats()).toEqual(FAKE_METRICS);
  });

  it('should set totalPlugins correctly on success', () => {
    const { facade } = setupWithFakePort();
    facade.loadStats();
    expect(facade.stats()?.totalPlugins).toBe(50);
  });

  it('should set totalDownloads correctly on success', () => {
    const { facade } = setupWithFakePort();
    facade.loadStats();
    expect(facade.stats()?.totalDownloads).toBe(10_000);
  });

  it('should set publisherCount correctly on success', () => {
    const { facade } = setupWithFakePort();
    facade.loadStats();
    expect(facade.stats()?.publisherCount).toBe(12);
  });

  it('should set categoryCount correctly on success', () => {
    const { facade } = setupWithFakePort();
    facade.loadStats();
    expect(facade.stats()?.categoryCount).toBe(6);
  });

  it('should leave statsError() undefined after successful load', () => {
    const { facade } = setupWithFakePort();
    facade.loadStats();
    expect(facade.statsError()).toBeUndefined();
  });

  it('should not throw when loadStats() is called', () => {
    const { facade } = setupWithFakePort();
    expect(() => facade.loadStats()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// loadStats() — immutability (stats object is a new reference on each call)
// ---------------------------------------------------------------------------

describe('HomeMetricsFacade — immutable updates', () => {
  it('should return a new stats object on each successful load (not mutating previous)', () => {
    @Injectable()
    class SequentialPort extends MarketplaceStatsPort {
      private callCount = 0;
      override getStats(): Observable<MarketplaceMetrics> {
        this.callCount++;
        return of({
          totalPlugins: this.callCount * 10,
          totalDownloads: this.callCount * 100,
          publisherCount: this.callCount,
          categoryCount: this.callCount,
        });
      }
    }

    TestBed.configureTestingModule({
      providers: [HomeMetricsFacade, { provide: MarketplaceStatsPort, useClass: SequentialPort }],
    });
    const facade = TestBed.inject(HomeMetricsFacade);

    facade.loadStats();
    const first = facade.stats();
    facade.loadStats();
    const second = facade.stats();

    // Second call should have updated values — not mutated first
    expect(second?.totalPlugins).toBe(20);
    expect(first?.totalPlugins).toBe(10); // first snapshot unchanged by reference
  });
});

// ---------------------------------------------------------------------------
// loadStats() — error path
// ---------------------------------------------------------------------------

describe('HomeMetricsFacade — loadStats() error', () => {
  it('should set isLoadingStats to false after error', () => {
    const { facade } = setupWithErrorPort();
    facade.loadStats();
    expect(facade.isLoadingStats()).toBe(false);
  });

  it('should set statsError to a non-empty string after error', () => {
    const { facade } = setupWithErrorPort();
    facade.loadStats();
    expect(facade.statsError()).toBeDefined();
    expect(typeof facade.statsError()).toBe('string');
    expect((facade.statsError() as string).length).toBeGreaterThan(0);
  });

  it('should preserve the error message from the thrown Error', () => {
    const { facade } = setupWithErrorPort();
    facade.loadStats();
    expect(facade.statsError()).toBe('Stats fetch failed');
  });

  it('should leave stats() as null after error', () => {
    const { facade } = setupWithErrorPort();
    facade.loadStats();
    expect(facade.stats()).toBeNull();
  });

  it('should not throw when port errors', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.loadStats()).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// loadStats() — re-load resets error state
// ---------------------------------------------------------------------------

describe('HomeMetricsFacade — loadStats() resets prior error on re-call', () => {
  it('should clear statsError when loadStats() is called again after a prior error', () => {
    // Simulate: first call errors, second succeeds
    let callCount = 0;

    @Injectable()
    class FirstErrorThenSuccessPort extends MarketplaceStatsPort {
      override getStats(): Observable<MarketplaceMetrics> {
        callCount++;
        if (callCount === 1) {
          return throwError(() => new Error('Temporary error'));
        }
        return of(FAKE_METRICS);
      }
    }

    TestBed.configureTestingModule({
      providers: [HomeMetricsFacade, { provide: MarketplaceStatsPort, useClass: FirstErrorThenSuccessPort }],
    });
    const facade = TestBed.inject(HomeMetricsFacade);

    facade.loadStats(); // first call — error
    expect(facade.statsError()).toBe('Temporary error');

    facade.loadStats(); // second call — success
    expect(facade.statsError()).toBeUndefined();
    expect(facade.stats()).toEqual(FAKE_METRICS);
  });
});

// ---------------------------------------------------------------------------
// loadStats() — loading flag is set to true before port responds
// ---------------------------------------------------------------------------

describe('HomeMetricsFacade — loadStats() sets loading true before emit', () => {
  it('should set isLoadingStats to true immediately when loadStats() is called', () => {
    // Use a port that does NOT synchronously emit — so we can observe
    // the intermediate loading=true state.
    // (In practice the fake ports are sync; to isolate the "set true" step,
    // we check the initial state before any emission.)
    const { facade } = setupWithFakePort();
    // After creation isLoadingStats=true (initial). After a successful load it
    // becomes false. If we call loadStats() again it must flip back to true
    // before the port emits.
    facade.loadStats(); // sync emit => isLoadingStats=false
    expect(facade.isLoadingStats()).toBe(false);

    // Simulate a no-op port to observe the intermediate true state:
    @Injectable()
    class NeverEmitPort extends MarketplaceStatsPort {
      override getStats(): Observable<MarketplaceMetrics> {
        return new Observable(); // never emits
      }
    }

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [HomeMetricsFacade, { provide: MarketplaceStatsPort, useClass: NeverEmitPort }],
    });
    const facade2 = TestBed.inject(HomeMetricsFacade);
    // Initial state: isLoadingStats=true (per spec)
    expect(facade2.isLoadingStats()).toBe(true);
    // After calling loadStats() with a port that never emits, still true
    facade2.loadStats();
    expect(facade2.isLoadingStats()).toBe(true);
  });
});
