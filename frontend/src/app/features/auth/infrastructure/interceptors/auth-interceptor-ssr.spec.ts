/**
 * RED tests — Group 6: SSR enablement — auth interceptor SSR safety guards
 *
 * These tests pin the REQUIRED SSR-safe behavior that does NOT exist yet.
 * They MUST FAIL (RED) until the coder implements the GREEN contract below.
 *
 * GREEN contract for auth.interceptor.ts:
 *   - Inject PLATFORM_ID and DOCUMENT (from @angular/common).
 *   - Remove the `window.location.origin` reference in isSkipped() entirely.
 *   - Instead, resolve the origin from the injected DOCUMENT.location.origin
 *     (DOCUMENT is always available — Angular provides it on both platforms).
 *   - Gate `window`-specific branches with isPlatformBrowser(platformId).
 *   - On the server platform (isPlatformBrowser = false):
 *       - MUST NOT access `window` directly (use DOCUMENT instead).
 *       - All skip-listed paths must still be correctly identified.
 *       - No Authorization header must be attached on skip-listed paths.
 *   - On the browser platform: existing behavior is unchanged (regression guard).
 *
 * Why jsdom-based 'server' tests alone are insufficient:
 *   The jsdom environment always provides `window`, so PLATFORM_ID='server' in
 *   TestBed does NOT cause a ReferenceError. The behavior contract is instead tested
 *   by asserting that the production code uses DOCUMENT (injected) rather than
 *   `window` for origin resolution — verified by providing a DOCUMENT stub whose
 *   location.origin differs from what window.location.origin would produce, and
 *   checking that origin comparison logic uses the stub value.
 *
 * Crash site (current code, line 46 of auth.interceptor.ts):
 *   const url = new URL(req.url, window.location.origin);
 *   → on Node/SSR: throws ReferenceError: window is not defined.
 *   → detectable in tests by providing a DOCUMENT stub with a known origin and
 *     asserting the interceptor uses it (not window.location.origin) to decide
 *     whether a request is external/same-origin.
 */

import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PLATFORM_ID } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { Observable, of } from 'rxjs';
import { Router } from '@angular/router';
import { AuthStore } from '../../application/store/auth.store';
import { AuthPort } from '../../domain/ports/auth.port';
import { authInterceptor } from './auth.interceptor';
import type { AuthProvider, AuthToken, CurrentUser } from '../../domain/models/auth.models';

// ---------------------------------------------------------------------------
// Test fixtures
// ---------------------------------------------------------------------------

const FAKE_TOKEN: AuthToken = { accessToken: 'eyJhbGciOiJSUzI1NiJ9.fake-access-token' };

const FAKE_USER: CurrentUser = {
  userId: 'user-1',
  email: 'alice@example.com',
  displayName: 'Alice',
  orgMemberships: [],
};

// ---------------------------------------------------------------------------
// Fake AuthPort
// ---------------------------------------------------------------------------

@Injectable()
class FakeAuthPort extends AuthPort {
  refreshCallCount = 0;

  getAuthorizeUrl(_provider: AuthProvider): Observable<string> {
    return of('https://accounts.google.com/auth');
  }

  exchangeToken(_code: string, _state: string, _codeVerifier: string): Observable<AuthToken> {
    return of(FAKE_TOKEN);
  }

  refreshToken(): Observable<AuthToken> {
    this.refreshCallCount++;
    return of(FAKE_TOKEN);
  }

  getCurrentUser(): Observable<CurrentUser> {
    return of(FAKE_USER);
  }

  signOut(): Observable<void> {
    return of(undefined);
  }
}

// ---------------------------------------------------------------------------
// Fake Router
// ---------------------------------------------------------------------------

@Injectable()
class FakeRouter {
  navigate(_commands: string[]): Promise<boolean> {
    return Promise.resolve(true);
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function seedTokenInStore(store: AuthStore, token: AuthToken): void {
  store.update('AUTH' as const, {
    data: {
      status: 'authenticated',
      user: FAKE_USER,
      token,
      activeOrgId: undefined,
      errorMessage: undefined,
    },
    status: 'Success',
    isLoading: false,
  });
}

/**
 * A DOCUMENT stub whose origin is deliberately set to a DIFFERENT value than
 * window.location.origin (which is 'http://localhost' in jsdom by default).
 *
 * After GREEN the interceptor must use the injected DOCUMENT to resolve origin,
 * NOT window.location.origin. By using a stub with origin='http://ssr-server:4000'
 * we can assert that:
 *   - Requests to an external origin (http://external.example.com/...) are skipped.
 *   - Requests to http://ssr-server:4000/... are treated as same-origin.
 * If the interceptor still uses window.location.origin it will use 'http://localhost'
 * instead of 'http://ssr-server:4000', which is the observable difference.
 */
function buildServerDocumentWithDistinctOrigin(): Partial<Document> {
  return {
    querySelector: () => null,
    location: {
      origin: 'http://ssr-server:4000',
      href: 'http://ssr-server:4000/',
      protocol: 'http:',
      host: 'ssr-server:4000',
      hostname: 'ssr-server',
      port: '4000',
      pathname: '/',
      search: '',
      hash: '',
      assign: () => undefined,
      replace: () => undefined,
      reload: () => undefined,
      ancestorOrigins: [] as unknown as DOMStringList,
      toString: () => 'http://ssr-server:4000/',
    } as Location,
  } as unknown as Document;
}

interface ServerHarness {
  httpClient: HttpClient;
  controller: HttpTestingController;
  store: AuthStore;
  port: FakeAuthPort;
}

/**
 * Server harness with DOCUMENT.location.origin = 'http://ssr-server:4000'
 * (deliberately different from jsdom's window.location.origin = 'http://localhost').
 */
function setupServerHarness(): ServerHarness {
  TestBed.resetTestingModule();
  const port = new FakeAuthPort();
  const router = new FakeRouter();

  TestBed.configureTestingModule({
    providers: [
      AuthStore,
      { provide: AuthPort, useValue: port },
      { provide: Router, useValue: router },
      { provide: PLATFORM_ID, useValue: 'server' },
      { provide: DOCUMENT, useValue: buildServerDocumentWithDistinctOrigin() },
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
    ],
  });

  return {
    httpClient: TestBed.inject(HttpClient),
    controller: TestBed.inject(HttpTestingController),
    store: TestBed.inject(AuthStore),
    port,
  };
}

interface BrowserHarness {
  httpClient: HttpClient;
  controller: HttpTestingController;
  store: AuthStore;
  port: FakeAuthPort;
}

function setupBrowserHarness(): BrowserHarness {
  TestBed.resetTestingModule();
  const port = new FakeAuthPort();
  const router = new FakeRouter();

  TestBed.configureTestingModule({
    providers: [
      AuthStore,
      { provide: AuthPort, useValue: port },
      { provide: Router, useValue: router },
      { provide: PLATFORM_ID, useValue: 'browser' },
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
    ],
  });

  return {
    httpClient: TestBed.inject(HttpClient),
    controller: TestBed.inject(HttpTestingController),
    store: TestBed.inject(AuthStore),
    port,
  };
}

// ---------------------------------------------------------------------------
// SSR platform guard — DOCUMENT injection contract
//
// The key observable behavior: when the interceptor correctly injects DOCUMENT
// (instead of calling window.location.origin), an absolute URL request to the
// DOCUMENT's origin is treated as same-origin, and an absolute URL to a
// different origin (e.g. jsdom's window.location = http://localhost) is treated
// as external/skipped.
//
// Current code: `new URL(req.url, window.location.origin)` uses window origin
// ('http://localhost' in jsdom). After GREEN it must use DOCUMENT.location.origin
// ('http://ssr-server:4000' in our stub).
//
// Test: send a request to 'http://ssr-server:4000/api/v1/dashboard'
// - With window origin: new URL('http://ssr-server:4000/...', 'http://localhost')
//   → origin is 'http://ssr-server:4000' ≠ window origin 'http://localhost'
//   → treated as EXTERNAL → SKIPPED → no Bearer header
// - With DOCUMENT origin: origin 'http://ssr-server:4000' = DOCUMENT origin → same-origin
//   → token attaches (not skipped) → Bearer header IS present
//
// So: if Bearer is present → interceptor used DOCUMENT (GREEN behavior).
//     If Bearer is absent  → interceptor used window (current RED behavior).
// ---------------------------------------------------------------------------

describe('authInterceptor — SSR: interceptor MUST use injected DOCUMENT for origin, not window', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('should attach Authorization header when absolute URL origin matches DOCUMENT.location.origin (not window.location.origin)', fakeAsync(() => {
    // CURRENTLY FAILS (RED):
    //   The interceptor uses window.location.origin ('http://localhost' in jsdom).
    //   The absolute URL 'http://ssr-server:4000/api/v1/dashboard' has origin
    //   'http://ssr-server:4000' which differs from window origin 'http://localhost',
    //   so the request is treated as external → skipped → NO Bearer header attached.
    //
    //   After GREEN (injecting DOCUMENT with origin 'http://ssr-server:4000'):
    //   The same URL origin matches DOCUMENT.location.origin → same-origin →
    //   Bearer header IS attached.
    const { httpClient, controller, store } = setupServerHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    // Absolute URL matching the DOCUMENT stub's origin (NOT window.location.origin)
    httpClient.get('http://ssr-server:4000/api/v1/dashboard').subscribe({ error: () => undefined });

    const req = controller.expectOne('http://ssr-server:4000/api/v1/dashboard');
    // GREEN: interceptor used DOCUMENT origin → same-origin → Bearer attached
    // RED (current): interceptor used window origin → external → no Bearer
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${FAKE_TOKEN.accessToken}`);
    req.flush({});
    tick();
  }));

  it('should skip (no Bearer) when absolute URL origin matches window.location.origin but NOT DOCUMENT.location.origin', fakeAsync(() => {
    // This is the mirror of the above: a URL to window's origin ('http://localhost')
    // is external from the perspective of DOCUMENT origin ('http://ssr-server:4000').
    // After GREEN the request to http://localhost/api/v1/dashboard is skipped (external).
    //
    // CURRENTLY FAILS (RED):
    //   Current code uses window.location.origin = 'http://localhost', so the URL
    //   http://localhost/api/v1/dashboard is SAME-origin → Bearer header IS attached.
    //   After GREEN it would be external → skipped → no Bearer.
    const { httpClient, controller, store } = setupServerHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    // URL matching window.location.origin but NOT DOCUMENT origin
    httpClient.get('http://localhost/api/v1/dashboard').subscribe({ error: () => undefined });

    const req = controller.expectOne('http://localhost/api/v1/dashboard');
    // GREEN: treated as external (origin ≠ DOCUMENT.location.origin) → no Bearer
    // RED (current): treated as same-origin (matches window) → Bearer attached
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
    tick();
  }));

  it('should NOT attach Authorization header on server for skip-listed relative path /api/v1/plugins', fakeAsync(() => {
    // Relative paths remain skip-listed on the server as on the browser.
    // This also verifies no crash occurs when PLATFORM_ID='server'.
    const { httpClient, controller, store } = setupServerHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/plugins').subscribe({ error: () => undefined });

    const req = controller.expectOne('/api/v1/plugins');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
    tick();
  }));

  it('should NOT attach Authorization header on server for skip-listed relative path /api/v1/search', fakeAsync(() => {
    const { httpClient, controller, store } = setupServerHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/search').subscribe({ error: () => undefined });

    const req = controller.expectOne('/api/v1/search');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
    tick();
  }));

  it('should NOT call AuthPort.refreshToken for skip-listed request on server platform', fakeAsync(() => {
    const { httpClient, controller, port } = setupServerHarness();

    httpClient.get('/api/v1/plugins').subscribe({ error: () => undefined });

    const req = controller.expectOne('/api/v1/plugins');
    req.flush([]);
    tick();

    expect(port.refreshCallCount).toBe(0);
  }));
});

// ---------------------------------------------------------------------------
// Browser platform regression — existing behavior preserved after GREEN change
// ---------------------------------------------------------------------------

describe('authInterceptor — browser platform regression guard (PLATFORM_ID=browser)', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('should attach Authorization header on browser for a non-skip-listed relative path', fakeAsync(() => {
    const { httpClient, controller, store } = setupBrowserHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/dashboard').subscribe({ error: () => undefined });

    const req = controller.expectOne('/api/v1/dashboard');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${FAKE_TOKEN.accessToken}`);
    req.flush({});
    tick();
  }));

  it('should NOT attach Authorization header on browser for skip-listed /api/v1/plugins', fakeAsync(() => {
    const { httpClient, controller, store } = setupBrowserHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/plugins').subscribe({ error: () => undefined });

    const req = controller.expectOne('/api/v1/plugins');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
    tick();
  }));

  it('should NOT attach Authorization header on browser for skip-listed /api/v1/search', fakeAsync(() => {
    const { httpClient, controller, store } = setupBrowserHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/search').subscribe({ error: () => undefined });

    const req = controller.expectOne('/api/v1/search');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush([]);
    tick();
  }));
});
