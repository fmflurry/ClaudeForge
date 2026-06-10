/**
 * Tests for FeaturedPluginHttpAdapter.
 *
 * Uses a StubApiClient to avoid HttpTestingController, matching the pattern
 * of marketplace-stats-http.adapter.spec.ts.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { FeaturedPluginHttpAdapter } from './featured-plugin-http.adapter';
import { FeaturedPluginPort } from '../../domain/ports/featured-plugin.port';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { FeaturedPlugin } from '../../domain/models/featured-plugin.model';
import type { FeaturedPluginEnvelope } from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// Stub ApiClient
// ---------------------------------------------------------------------------

const FAKE_ENVELOPE: FeaturedPluginEnvelope = {
  data: {
    pluginId: 'plugin-abc',
    name: 'Awesome Plugin',
    slug: 'awesome-plugin',
    latestVersion: '1.2.3',
  },
};

@Injectable()
class StubApiClient {
  private _envelope: FeaturedPluginEnvelope = FAKE_ENVELOPE;
  private _shouldError = false;
  private _errorStatus: number | undefined;

  getFeaturedPluginCalls = 0;

  setEnvelope(envelope: FeaturedPluginEnvelope): void {
    this._envelope = envelope;
    this._shouldError = false;
  }

  setError(status?: number): void {
    this._shouldError = true;
    this._errorStatus = status;
  }

  getFeaturedPlugin(): Observable<FeaturedPluginEnvelope> {
    this.getFeaturedPluginCalls++;
    if (this._shouldError) {
      const err = Object.assign(new Error('Http error'), { status: this._errorStatus ?? 500 });
      return throwError(() => err);
    }
    return of(this._envelope);
  }
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(): { adapter: FeaturedPluginHttpAdapter; stub: StubApiClient } {
  TestBed.resetTestingModule();
  const stub = new StubApiClient();
  TestBed.configureTestingModule({
    providers: [
      { provide: ApiClient, useValue: stub },
      { provide: FeaturedPluginPort, useClass: FeaturedPluginHttpAdapter },
      FeaturedPluginHttpAdapter,
    ],
  });
  return { adapter: TestBed.inject(FeaturedPluginHttpAdapter), stub };
}

// ---------------------------------------------------------------------------
// Architecture
// ---------------------------------------------------------------------------

describe('FeaturedPluginHttpAdapter — architecture', () => {
  it('should be an instance of FeaturedPluginPort', () => {
    const { adapter } = setup();
    expect(adapter).toBeInstanceOf(FeaturedPluginPort);
  });
});

// ---------------------------------------------------------------------------
// Success path
// ---------------------------------------------------------------------------

describe('FeaturedPluginHttpAdapter — getFeaturedPlugin() success', () => {
  it('should call apiClient.getFeaturedPlugin once', () => {
    const { adapter, stub } = setup();
    adapter.getFeaturedPlugin().subscribe();
    expect(stub.getFeaturedPluginCalls).toBe(1);
  });

  it('should map the DTO to the domain model', () => {
    const { adapter } = setup();
    let result: FeaturedPlugin | null | undefined;
    adapter.getFeaturedPlugin().subscribe((p) => (result = p));
    expect(result).toEqual({
      pluginId: 'plugin-abc',
      name: 'Awesome Plugin',
      slug: 'awesome-plugin',
      latestVersion: '1.2.3',
    });
  });

  it('should map pluginId from DTO to domain model', () => {
    const { adapter } = setup();
    let result: FeaturedPlugin | null | undefined;
    adapter.getFeaturedPlugin().subscribe((p) => (result = p));
    expect(result?.pluginId).toBe('plugin-abc');
  });

  it('should map slug from DTO to domain model', () => {
    const { adapter } = setup();
    let result: FeaturedPlugin | null | undefined;
    adapter.getFeaturedPlugin().subscribe((p) => (result = p));
    expect(result?.slug).toBe('awesome-plugin');
  });

  it('should map null latestVersion from DTO', () => {
    const { adapter, stub } = setup();
    stub.setEnvelope({
      data: { pluginId: 'p1', name: 'Plugin', slug: 'plugin', latestVersion: null },
    });
    let result: FeaturedPlugin | null | undefined;
    adapter.getFeaturedPlugin().subscribe((p) => (result = p));
    expect(result?.latestVersion).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// 404 / error path — must map to null
// ---------------------------------------------------------------------------

describe('FeaturedPluginHttpAdapter — getFeaturedPlugin() 404 / error → null', () => {
  it('should emit null when the backend returns a 404', () => {
    const { adapter, stub } = setup();
    stub.setError(404);
    let result: FeaturedPlugin | null | undefined;
    adapter.getFeaturedPlugin().subscribe((p) => (result = p));
    expect(result).toBeNull();
  });

  it('should emit null when the backend returns a 500', () => {
    const { adapter, stub } = setup();
    stub.setError(500);
    let result: FeaturedPlugin | null | undefined;
    adapter.getFeaturedPlugin().subscribe((p) => (result = p));
    expect(result).toBeNull();
  });

  it('should NOT propagate the error as an Observable error', () => {
    const { adapter, stub } = setup();
    stub.setError(404);
    let errorCaught: unknown;
    adapter.getFeaturedPlugin().subscribe({ error: (e: unknown) => (errorCaught = e) });
    expect(errorCaught).toBeUndefined();
  });

  it('should emit null on network error', () => {
    const { adapter, stub } = setup();
    stub.setError();
    let result: FeaturedPlugin | null | undefined;
    adapter.getFeaturedPlugin().subscribe((p) => (result = p));
    expect(result).toBeNull();
  });
});
