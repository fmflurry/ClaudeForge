/**
 * Tests for FeaturedAddOnHttpAdapter.
 *
 * Uses a StubApiClient to avoid HttpTestingController, matching the pattern
 * of marketplace-stats-http.adapter.spec.ts.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { FeaturedAddOnHttpAdapter } from './featured-plugin-http.adapter';
import { FeaturedAddOnPort } from '../../domain/ports/featured-plugin.port';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { FeaturedAddOn } from '../../domain/models/featured-plugin.model';
import type { FeaturedAddOnEnvelope } from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// Stub ApiClient
// ---------------------------------------------------------------------------

const FAKE_ENVELOPE: FeaturedAddOnEnvelope = {
  data: {
    pluginId: 'plugin-abc',
    name: 'Awesome AddOn',
    slug: 'awesome-addon',
    latestVersion: '1.2.3',
  },
};

@Injectable()
class StubApiClient {
  private _envelope: FeaturedAddOnEnvelope = FAKE_ENVELOPE;
  private _shouldError = false;
  private _errorStatus: number | undefined;

  getFeaturedAddOnCalls = 0;

  setEnvelope(envelope: FeaturedAddOnEnvelope): void {
    this._envelope = envelope;
    this._shouldError = false;
  }

  setError(status?: number): void {
    this._shouldError = true;
    this._errorStatus = status;
  }

  getFeaturedAddOn(): Observable<FeaturedAddOnEnvelope> {
    this.getFeaturedAddOnCalls++;
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

function setup(): { adapter: FeaturedAddOnHttpAdapter; stub: StubApiClient } {
  TestBed.resetTestingModule();
  const stub = new StubApiClient();
  TestBed.configureTestingModule({
    providers: [
      { provide: ApiClient, useValue: stub },
      { provide: FeaturedAddOnPort, useClass: FeaturedAddOnHttpAdapter },
      FeaturedAddOnHttpAdapter,
    ],
  });
  return { adapter: TestBed.inject(FeaturedAddOnHttpAdapter), stub };
}

// ---------------------------------------------------------------------------
// Architecture
// ---------------------------------------------------------------------------

describe('FeaturedAddOnHttpAdapter — architecture', () => {
  it('should be an instance of FeaturedAddOnPort', () => {
    const { adapter } = setup();
    expect(adapter).toBeInstanceOf(FeaturedAddOnPort);
  });
});

// ---------------------------------------------------------------------------
// Success path
// ---------------------------------------------------------------------------

describe('FeaturedAddOnHttpAdapter — getFeaturedAddOn() success', () => {
  it('should call apiClient.getFeaturedAddOn once', () => {
    const { adapter, stub } = setup();
    adapter.getFeaturedAddOn().subscribe();
    expect(stub.getFeaturedAddOnCalls).toBe(1);
  });

  it('should map the DTO to the domain model', () => {
    const { adapter } = setup();
    let result: FeaturedAddOn | null | undefined;
    adapter.getFeaturedAddOn().subscribe((p) => (result = p));
    expect(result).toEqual({
      pluginId: 'plugin-abc',
      name: 'Awesome AddOn',
      slug: 'awesome-addon',
      latestVersion: '1.2.3',
    });
  });

  it('should map pluginId from DTO to domain model', () => {
    const { adapter } = setup();
    let result: FeaturedAddOn | null | undefined;
    adapter.getFeaturedAddOn().subscribe((p) => (result = p));
    expect(result?.pluginId).toBe('plugin-abc');
  });

  it('should map slug from DTO to domain model', () => {
    const { adapter } = setup();
    let result: FeaturedAddOn | null | undefined;
    adapter.getFeaturedAddOn().subscribe((p) => (result = p));
    expect(result?.slug).toBe('awesome-addon');
  });

  it('should map null latestVersion from DTO', () => {
    const { adapter, stub } = setup();
    stub.setEnvelope({
      data: { pluginId: 'p1', name: 'AddOn', slug: 'addon', latestVersion: null },
    });
    let result: FeaturedAddOn | null | undefined;
    adapter.getFeaturedAddOn().subscribe((p) => (result = p));
    expect(result?.latestVersion).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// 404 / error path — must map to null
// ---------------------------------------------------------------------------

describe('FeaturedAddOnHttpAdapter — getFeaturedAddOn() 404 / error → null', () => {
  it('should emit null when the backend returns a 404', () => {
    const { adapter, stub } = setup();
    stub.setError(404);
    let result: FeaturedAddOn | null | undefined;
    adapter.getFeaturedAddOn().subscribe((p) => (result = p));
    expect(result).toBeNull();
  });

  it('should emit null when the backend returns a 500', () => {
    const { adapter, stub } = setup();
    stub.setError(500);
    let result: FeaturedAddOn | null | undefined;
    adapter.getFeaturedAddOn().subscribe((p) => (result = p));
    expect(result).toBeNull();
  });

  it('should NOT propagate the error as an Observable error', () => {
    const { adapter, stub } = setup();
    stub.setError(404);
    let errorCaught: unknown;
    adapter.getFeaturedAddOn().subscribe({ error: (e: unknown) => (errorCaught = e) });
    expect(errorCaught).toBeUndefined();
  });

  it('should emit null on network error', () => {
    const { adapter, stub } = setup();
    stub.setError();
    let result: FeaturedAddOn | null | undefined;
    adapter.getFeaturedAddOn().subscribe((p) => (result = p));
    expect(result).toBeNull();
  });
});
