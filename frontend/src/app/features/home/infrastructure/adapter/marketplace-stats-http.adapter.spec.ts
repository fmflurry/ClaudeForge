/**
 * RED tests — Home metrics HTTP adapter.
 *
 * Production file that MUST exist (DO NOT create yet):
 *   src/app/features/home/infrastructure/adapter/marketplace-stats-http.adapter.ts
 *
 * The coder MUST export:
 *
 *   @Injectable()
 *   export class MarketplaceStatsHttpAdapter extends MarketplaceStatsPort {
 *     private readonly apiClient = inject(ApiClient);
 *
 *     override getStats(): Observable<MarketplaceMetrics> {
 *       return this.apiClient.getMarketplaceStats().pipe(
 *         map((dto) => marketplaceMetricsSchema.parse(dto)),
 *       );
 *     }
 *   }
 *
 * The coder MUST also add getMarketplaceStats() to ApiClient:
 *
 *   // In src/app/shared/infrastructure/http/api-client.ts:
 *   getMarketplaceStats(): Observable<MarketplaceStatsDto> {
 *     return this.http.get<MarketplaceStatsDto>(`${this.baseUrl}/api/v1/stats`);
 *   }
 *
 * And add MarketplaceStatsDto to api-client.types.ts:
 *
 *   export interface MarketplaceStatsDto {
 *     totalPlugins: number;
 *     totalDownloads: number;
 *     publisherCount: number;
 *     categoryCount: number;
 *   }
 *
 * The adapter validates via marketplaceMetricsSchema.parse() and throws on
 * malformed payloads (Zod SafeParseError propagated through the Observable).
 *
 * Tests use a StubApiClient (same pattern as catalog-http.adapter.spec.ts)
 * rather than HttpTestingController, because the adapter delegates HTTP to
 * the shared ApiClient.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { MarketplaceStatsHttpAdapter } from './marketplace-stats-http.adapter';
import { MarketplaceStatsPort } from '../../domain/ports/marketplace-stats.port';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { MarketplaceMetrics } from '../../domain/models/marketplace-metrics.model';

// ---------------------------------------------------------------------------
// Stub ApiClient that serves the stats endpoint
// ---------------------------------------------------------------------------

interface StubStatsDto {
  totalPlugins: number;
  totalDownloads: number;
  publisherCount: number;
  categoryCount: number;
}

@Injectable()
class StubApiClient {
  private _response: StubStatsDto | null = {
    totalPlugins: 42,
    totalDownloads: 1_234,
    publisherCount: 8,
    categoryCount: 4,
  };

  private _shouldError = false;

  getMarketplaceStatsCalls = 0;

  setResponse(dto: StubStatsDto): void {
    this._response = dto;
    this._shouldError = false;
  }

  setError(): void {
    this._shouldError = true;
  }

  getMarketplaceStats(): Observable<StubStatsDto> {
    this.getMarketplaceStatsCalls++;
    if (this._shouldError) {
      return throwError(() => new Error('Network error'));
    }
    return of(this._response as StubStatsDto);
  }
}

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

function setup(): { adapter: MarketplaceStatsHttpAdapter; stub: StubApiClient } {
  TestBed.resetTestingModule();
  const stub = new StubApiClient();
  TestBed.configureTestingModule({
    providers: [
      { provide: ApiClient, useValue: stub },
      { provide: MarketplaceStatsPort, useClass: MarketplaceStatsHttpAdapter },
      MarketplaceStatsHttpAdapter,
    ],
  });
  return { adapter: TestBed.inject(MarketplaceStatsHttpAdapter), stub };
}

// ---------------------------------------------------------------------------
// Architecture
// ---------------------------------------------------------------------------

describe('MarketplaceStatsHttpAdapter — architecture', () => {
  it('should be an instance of MarketplaceStatsPort', () => {
    const { adapter } = setup();
    expect(adapter).toBeInstanceOf(MarketplaceStatsPort);
  });
});

// ---------------------------------------------------------------------------
// Success path
// ---------------------------------------------------------------------------

describe('MarketplaceStatsHttpAdapter — getStats() success', () => {
  it('should call apiClient.getMarketplaceStats once', () => {
    const { adapter, stub } = setup();
    adapter.getStats().subscribe();
    expect(stub.getMarketplaceStatsCalls).toBe(1);
  });

  it('should map totalPlugins from DTO to domain model', () => {
    const { adapter, stub } = setup();
    stub.setResponse({ totalPlugins: 10, totalDownloads: 200, publisherCount: 3, categoryCount: 5 });
    let result: MarketplaceMetrics | undefined;
    adapter.getStats().subscribe((m) => (result = m));
    expect(result?.totalPlugins).toBe(10);
  });

  it('should map totalDownloads from DTO to domain model', () => {
    const { adapter, stub } = setup();
    stub.setResponse({ totalPlugins: 10, totalDownloads: 999, publisherCount: 3, categoryCount: 5 });
    let result: MarketplaceMetrics | undefined;
    adapter.getStats().subscribe((m) => (result = m));
    expect(result?.totalDownloads).toBe(999);
  });

  it('should map publisherCount from DTO to domain model', () => {
    const { adapter, stub } = setup();
    stub.setResponse({ totalPlugins: 10, totalDownloads: 200, publisherCount: 7, categoryCount: 5 });
    let result: MarketplaceMetrics | undefined;
    adapter.getStats().subscribe((m) => (result = m));
    expect(result?.publisherCount).toBe(7);
  });

  it('should map categoryCount from DTO to domain model', () => {
    const { adapter, stub } = setup();
    stub.setResponse({ totalPlugins: 10, totalDownloads: 200, publisherCount: 3, categoryCount: 12 });
    let result: MarketplaceMetrics | undefined;
    adapter.getStats().subscribe((m) => (result = m));
    expect(result?.categoryCount).toBe(12);
  });

  it('should return a complete MarketplaceMetrics with all four fields', () => {
    const { adapter, stub } = setup();
    stub.setResponse({ totalPlugins: 1, totalDownloads: 2, publisherCount: 3, categoryCount: 4 });
    let result: MarketplaceMetrics | undefined;
    adapter.getStats().subscribe((m) => (result = m));
    expect(result).toEqual({
      totalPlugins: 1,
      totalDownloads: 2,
      publisherCount: 3,
      categoryCount: 4,
    });
  });

  it('should handle zero values correctly', () => {
    const { adapter, stub } = setup();
    stub.setResponse({ totalPlugins: 0, totalDownloads: 0, publisherCount: 0, categoryCount: 0 });
    let result: MarketplaceMetrics | undefined;
    adapter.getStats().subscribe((m) => (result = m));
    expect(result?.totalPlugins).toBe(0);
    expect(result?.totalDownloads).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// Error path
// ---------------------------------------------------------------------------

describe('MarketplaceStatsHttpAdapter — getStats() error path', () => {
  it('should propagate HTTP errors as Observable errors', () => {
    const { adapter, stub } = setup();
    stub.setError();
    let errorCaught: unknown;
    adapter.getStats().subscribe({
      next: () => {
        /* should not emit */
      },
      error: (err: unknown) => (errorCaught = err),
    });
    expect(errorCaught).toBeDefined();
  });

  it('should not emit a value when the HTTP request fails', () => {
    const { adapter, stub } = setup();
    stub.setError();
    let didEmit = false;
    adapter.getStats().subscribe({
      next: () => {
        didEmit = true;
      },
      error: () => {
        /* expected */
      },
    });
    expect(didEmit).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Schema validation at adapter boundary
// ---------------------------------------------------------------------------

describe('MarketplaceStatsHttpAdapter — schema validation', () => {
  it('should throw/error when the backend returns a malformed payload', () => {
    // Simulate a backend that drops a required field
    @Injectable()
    class MalformedApiClient {
      getMarketplaceStats(): Observable<Partial<StubStatsDto>> {
        return of({ totalPlugins: 5 }); // missing totalDownloads, publisherCount, categoryCount
      }
    }

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: ApiClient, useClass: MalformedApiClient },
        { provide: MarketplaceStatsPort, useClass: MarketplaceStatsHttpAdapter },
        MarketplaceStatsHttpAdapter,
      ],
    });
    const adapter = TestBed.inject(MarketplaceStatsHttpAdapter);
    let errorCaught: unknown;
    let didEmit = false;
    adapter.getStats().subscribe({
      next: () => {
        didEmit = true;
      },
      error: (err: unknown) => (errorCaught = err),
    });
    expect(didEmit).toBe(false);
    expect(errorCaught).toBeDefined();
  });

  it('should error when backend returns a negative totalPlugins value', () => {
    @Injectable()
    class NegativeStatsApiClient {
      getMarketplaceStats(): Observable<StubStatsDto> {
        return of({ totalPlugins: -5, totalDownloads: 100, publisherCount: 3, categoryCount: 2 });
      }
    }

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: ApiClient, useClass: NegativeStatsApiClient },
        { provide: MarketplaceStatsPort, useClass: MarketplaceStatsHttpAdapter },
        MarketplaceStatsHttpAdapter,
      ],
    });
    const adapter = TestBed.inject(MarketplaceStatsHttpAdapter);
    let errorCaught: unknown;
    adapter.getStats().subscribe({
      next: () => {
        /* should not emit */
      },
      error: (err: unknown) => (errorCaught = err),
    });
    expect(errorCaught).toBeDefined();
  });
});
