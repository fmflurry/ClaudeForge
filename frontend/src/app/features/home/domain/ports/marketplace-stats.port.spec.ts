/**
 * RED tests — Home metrics port.
 *
 * Production file that MUST exist (DO NOT create yet):
 *   src/app/features/home/domain/ports/marketplace-stats.port.ts
 *
 * The coder MUST export:
 *
 *   import { Observable } from 'rxjs';
 *   import type { MarketplaceMetrics } from '../models/marketplace-metrics.model';
 *
 *   export abstract class MarketplaceStatsPort {
 *     abstract getStats(): Observable<MarketplaceMetrics>;
 *   }
 *
 * The port uses Observable (matching the catalog port convention), not Promise.
 * The spec overrides from the spec.md ("Promise") are superseded by the
 * project-wide Observable convention — all existing ports return Observable.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { MarketplaceStatsPort } from './marketplace-stats.port';
import type { MarketplaceMetrics } from '../models/marketplace-metrics.model';

// ---------------------------------------------------------------------------
// Concrete stub implementing the abstract port
// ---------------------------------------------------------------------------

const STUB_METRICS: MarketplaceMetrics = {
  totalPlugins: 42,
  totalDownloads: 1_000,
  publisherCount: 8,
  categoryCount: 4,
};

@Injectable()
class StubMarketplaceStatsPort extends MarketplaceStatsPort {
  override getStats(): Observable<MarketplaceMetrics> {
    return of(STUB_METRICS);
  }
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('MarketplaceStatsPort — contract', () => {
  it('should be an abstract class that can be used as an Angular DI token', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: MarketplaceStatsPort, useClass: StubMarketplaceStatsPort }],
    });
    const port = TestBed.inject(MarketplaceStatsPort);
    expect(port).toBeInstanceOf(StubMarketplaceStatsPort);
  });

  it('should declare getStats() returning an Observable', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: MarketplaceStatsPort, useClass: StubMarketplaceStatsPort }],
    });
    const port = TestBed.inject(MarketplaceStatsPort);
    const result = port.getStats();
    expect(result).toBeDefined();
    // Observable has a subscribe method
    expect(typeof result.subscribe).toBe('function');
  });

  it('getStats() Observable should emit a MarketplaceMetrics object', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: MarketplaceStatsPort, useClass: StubMarketplaceStatsPort }],
    });
    const port = TestBed.inject(MarketplaceStatsPort);
    let emitted: MarketplaceMetrics | undefined;
    port.getStats().subscribe((m) => (emitted = m));
    expect(emitted).toEqual(STUB_METRICS);
  });

  it('concrete subclass should be an instance of MarketplaceStatsPort', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: MarketplaceStatsPort, useClass: StubMarketplaceStatsPort }],
    });
    const port = TestBed.inject(MarketplaceStatsPort);
    expect(port).toBeInstanceOf(MarketplaceStatsPort);
  });

  it('should allow multiple implementations to be substituted via DI', () => {
    const DIFFERENT_METRICS: MarketplaceMetrics = {
      totalPlugins: 99,
      totalDownloads: 5_000,
      publisherCount: 20,
      categoryCount: 10,
    };

    @Injectable()
    class AltStubPort extends MarketplaceStatsPort {
      override getStats(): Observable<MarketplaceMetrics> {
        return of(DIFFERENT_METRICS);
      }
    }

    TestBed.configureTestingModule({
      providers: [{ provide: MarketplaceStatsPort, useClass: AltStubPort }],
    });
    const port = TestBed.inject(MarketplaceStatsPort);
    let emitted: MarketplaceMetrics | undefined;
    port.getStats().subscribe((m) => (emitted = m));
    expect(emitted?.totalPlugins).toBe(99);
  });
});
