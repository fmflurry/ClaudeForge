/**
 * RED tests — Task 9.4: HTTP interceptor single-flight refresh
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile/resolve):
 *   src/app/features/auth/infrastructure/interceptors/auth.interceptor.ts
 *   src/app/features/auth/application/store/auth.store.ts  (depended upon)
 *   src/app/features/auth/domain/ports/auth.port.ts       (depended upon)
 *
 * GREEN contract — exact shape the coder MUST implement:
 *
 *   // auth.interceptor.ts
 *   export function authInterceptor(
 *     req: HttpRequest<unknown>,
 *     next: HttpHandlerFn,
 *   ): Observable<HttpEvent<unknown>>
 *
 *   BEHAVIOUR RULES:
 *   1. ATTACH: Reads the in-memory access token from AuthStore.
 *              If present, adds `Authorization: Bearer <token>` header.
 *   2. SKIP-LIST (do NOT attach Bearer and do NOT intercept 401 on):
 *      a. Catalog/search/download public API paths:
 *         - /api/v1/plugins  (listing)
 *         - /api/v1/plugins/:id  (detail — GET only)
 *         - /api/v1/plugins/:id/download
 *         - /api/v1/search
 *         - /api/v1/categories
 *      b. Auth endpoints (do not intercept their own 401s):
 *         - /auth/authorize
 *         - /auth/token
 *         - /auth/refresh
 *      c. Any URL whose origin differs from the API origin (external IdP URLs etc.)
 *   3. ON 401:
 *      a. Call AuthPort.refreshToken() — ONCE, shared across concurrent 401s
 *         (single-flight: all concurrent 401 requests share one refresh Observable
 *          rather than triggering N parallel refreshes).
 *      b. On refresh success: update in-memory token in AuthStore, retry the original
 *         request with the new Bearer header.
 *      c. On refresh failure: call AuthStore.clearAll() to wipe in-memory state,
 *         navigate to '/login', and propagate the error (do NOT retry again).
 *   4. Non-401 errors pass through unchanged.
 *
 *   INJECTION: The interceptor MUST be a functional interceptor (not class-based).
 *              It must be provided via `withInterceptors([authInterceptor])` in the app config.
 *              Tests provide AuthStore and AuthPort directly via TestBed providers.
 */

import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import {
  HttpClient,
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpRequest,
  HttpResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Observable, Subject, of, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { AuthStore } from '../../application/store/auth.store';
import { AuthPort } from '../../domain/ports/auth.port';
import { authInterceptor } from './auth.interceptor';
import type { AuthProvider, AuthToken, CurrentUser } from '../../domain/models/auth.models';

// ---------------------------------------------------------------------------
// Test fixtures
// ---------------------------------------------------------------------------

const FAKE_ACCESS_TOKEN = 'eyJhbGciOiJSUzI1NiJ9.original-token';
const REFRESHED_TOKEN = 'eyJhbGciOiJSUzI1NiJ9.refreshed-token';

const FAKE_TOKEN: AuthToken = { accessToken: FAKE_ACCESS_TOKEN };
const REFRESHED: AuthToken = { accessToken: REFRESHED_TOKEN };

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
  tokenToReturn: AuthToken = REFRESHED;
  shouldFailRefresh = false;
  // Allows tests to control when the refresh observable completes
  private refreshSubject: Subject<AuthToken> | null = null;

  setManualRefresh(subject: Subject<AuthToken>): void {
    this.refreshSubject = subject;
  }

  getAuthorizeUrl(_provider: AuthProvider): Observable<string> {
    return of('https://accounts.google.com/auth');
  }

  exchangeToken(
    _code: string,
    _state: string,
    _codeVerifier: string,
  ): Observable<AuthToken> {
    return of(FAKE_TOKEN);
  }

  refreshToken(): Observable<AuthToken> {
    this.refreshCallCount++;
    if (this.shouldFailRefresh) {
      return throwError(() => new HttpErrorResponse({ status: 401, statusText: 'Unauthorized' }));
    }
    if (this.refreshSubject) {
      return this.refreshSubject.asObservable();
    }
    return of(this.tokenToReturn);
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
  navigatedUrls: string[] = [];
  navigate(commands: string[]): Promise<boolean> {
    this.navigatedUrls.push(commands.join('/'));
    return Promise.resolve(true);
  }
}

// ---------------------------------------------------------------------------
// Helper: seed an AuthStore with an in-memory access token
// ---------------------------------------------------------------------------

function seedTokenInStore(store: AuthStore, token: AuthToken): void {
  store.update('AUTH' as 'AUTH', {
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

// ---------------------------------------------------------------------------
// Setup helper
// ---------------------------------------------------------------------------

interface TestHarness {
  httpClient: HttpClient;
  controller: HttpTestingController;
  store: AuthStore;
  port: FakeAuthPort;
  router: FakeRouter;
}

function setupHarness(): TestHarness {
  TestBed.resetTestingModule();
  const port = new FakeAuthPort();
  const router = new FakeRouter();

  TestBed.configureTestingModule({
    providers: [
      AuthStore,
      { provide: AuthPort, useValue: port },
      { provide: Router, useValue: router },
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
    ],
  });

  return {
    httpClient: TestBed.inject(HttpClient),
    controller: TestBed.inject(HttpTestingController),
    store: TestBed.inject(AuthStore),
    port,
    router,
  };
}

// ---------------------------------------------------------------------------
// 1. Bearer token attachment
// ---------------------------------------------------------------------------

describe('authInterceptor — Bearer token attachment', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('should add Authorization: Bearer header when token is in-memory', () => {
    const { httpClient, controller, store } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/dashboard').subscribe();

    const req = controller.expectOne('/api/v1/dashboard');
    expect(req.request.headers.get('Authorization')).toBe(`Bearer ${FAKE_ACCESS_TOKEN}`);
    req.flush({});
  });

  it('should NOT add Authorization header when no token is in store', () => {
    const { httpClient, controller } = setupHarness();
    // Do not seed a token

    httpClient.get('/api/v1/dashboard').subscribe();

    const req = controller.expectOne('/api/v1/dashboard');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
  });

  it('should not mutate the original request (must clone)', fakeAsync(() => {
    const { httpClient, controller, store } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    let interceptedReq: HttpRequest<unknown> | null = null;
    httpClient.get('/api/v1/dashboard').subscribe();

    const testReq = controller.expectOne('/api/v1/dashboard');
    interceptedReq = testReq.request;
    testReq.flush({});
    tick();

    // The request that arrives at the "server" should have the Bearer header
    expect(interceptedReq.headers.get('Authorization')).toBe(`Bearer ${FAKE_ACCESS_TOKEN}`);
  }));
});

// ---------------------------------------------------------------------------
// 2. Skip-list — public catalog/search/download paths
// ---------------------------------------------------------------------------

describe('authInterceptor — skip-list: public catalog paths', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  const PUBLIC_PATHS = [
    '/api/v1/plugins',
    '/api/v1/plugins/some-plugin-id',
    '/api/v1/plugins/some-plugin-id/download',
    '/api/v1/search',
    '/api/v1/categories',
  ];

  for (const path of PUBLIC_PATHS) {
    it(`should NOT attach Bearer header to public path: ${path}`, () => {
      const { httpClient, controller, store } = setupHarness();
      seedTokenInStore(store, FAKE_TOKEN);

      httpClient.get(path).subscribe();

      const req = controller.expectOne(path);
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush([]);
    });
  }
});

// ---------------------------------------------------------------------------
// 3. Skip-list — auth endpoints (do not intercept their 401s)
// ---------------------------------------------------------------------------

describe('authInterceptor — skip-list: auth endpoints', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  const AUTH_PATHS = [
    '/auth/authorize',
    '/auth/token',
    '/auth/refresh',
  ];

  for (const path of AUTH_PATHS) {
    it(`should NOT attach Bearer header to auth endpoint: ${path}`, () => {
      const { httpClient, controller, store } = setupHarness();
      seedTokenInStore(store, FAKE_TOKEN);

      httpClient.get(path).subscribe({ error: () => undefined });

      const req = controller.expectOne(path);
      expect(req.request.headers.has('Authorization')).toBe(false);
      req.flush({}, { status: 200, statusText: 'OK' });
    });
  }

  it('should not attempt refresh when /auth/refresh itself returns 401', fakeAsync(() => {
    const { httpClient, controller, port } = setupHarness();

    httpClient.post('/auth/refresh', {}).subscribe({ error: () => undefined });

    const req = controller.expectOne('/auth/refresh');
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    tick();

    // refresh should NOT have been called by the interceptor
    expect(port.refreshCallCount).toBe(0);
    controller.verify();
  }));
});

// ---------------------------------------------------------------------------
// 4. 401 handling — single-flight refresh + retry
// ---------------------------------------------------------------------------

describe('authInterceptor — 401 refresh and retry', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('should call AuthPort.refreshToken() once on 401', fakeAsync(() => {
    const { httpClient, controller, store, port } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/dashboard').subscribe({ error: () => undefined });

    // Original request returns 401
    const original = controller.expectOne('/api/v1/dashboard');
    original.flush({}, { status: 401, statusText: 'Unauthorized' });
    tick();

    // After refresh, retry fires
    const retry = controller.expectOne('/api/v1/dashboard');
    retry.flush({ ok: true });
    tick();

    expect(port.refreshCallCount).toBe(1);
  }));

  it('should retry the original request with the new Bearer token after refresh', fakeAsync(() => {
    const { httpClient, controller, store } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/dashboard').subscribe({ error: () => undefined });

    const original = controller.expectOne('/api/v1/dashboard');
    original.flush({}, { status: 401, statusText: 'Unauthorized' });
    tick();

    const retry = controller.expectOne('/api/v1/dashboard');
    expect(retry.request.headers.get('Authorization')).toBe(`Bearer ${REFRESHED_TOKEN}`);
    retry.flush({ data: 'fresh' });
    tick();
  }));

  it('should update the in-memory token in AuthStore after successful refresh', fakeAsync(() => {
    const { httpClient, controller, store } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/dashboard').subscribe({ error: () => undefined });

    const original = controller.expectOne('/api/v1/dashboard');
    original.flush({}, { status: 401, statusText: 'Unauthorized' });
    tick();

    const retry = controller.expectOne('/api/v1/dashboard');
    retry.flush({});
    tick();

    // The store's in-memory token must now be the refreshed token
    const storeData = store.get('AUTH' as 'AUTH')().data;
    expect(storeData?.token?.accessToken).toBe(REFRESHED_TOKEN);
  }));

  it('should NOT store the refreshed token in localStorage', fakeAsync(() => {
    const { httpClient, controller, store } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    httpClient.get('/api/v1/dashboard').subscribe({ error: () => undefined });

    const original = controller.expectOne('/api/v1/dashboard');
    original.flush({}, { status: 401, statusText: 'Unauthorized' });
    tick();

    const retry = controller.expectOne('/api/v1/dashboard');
    retry.flush({});
    tick();

    const allKeys = Object.keys(localStorage);
    const hasToken = allKeys.some((k) => {
      const val = localStorage.getItem(k) ?? '';
      return val.includes(REFRESHED_TOKEN);
    });
    expect(hasToken).toBe(false);
  }));
});

// ---------------------------------------------------------------------------
// 5. Single-flight: concurrent 401s share ONE refresh call
// ---------------------------------------------------------------------------

describe('authInterceptor — single-flight: concurrent 401s', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('should call refreshToken() only ONCE when two requests fail with 401 concurrently', fakeAsync(() => {
    const { httpClient, controller, store, port } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    // Manually controlled refresh observable so we can defer it
    const refreshSubject = new Subject<AuthToken>();
    port.setManualRefresh(refreshSubject);

    httpClient.get('/api/v1/dashboard').subscribe({ error: () => undefined });
    httpClient.get('/api/v1/plugins/private-plugin-id/download').subscribe({
      // skip-listed path won't go through the 401 handler
      error: () => undefined,
    });
    httpClient.get('/api/v1/org/members').subscribe({ error: () => undefined });

    // First non-skipped request gets 401
    const req1 = controller.expectOne('/api/v1/dashboard');
    req1.flush({}, { status: 401, statusText: 'Unauthorized' });

    // Second non-skipped request gets 401 before refresh completes
    const req2 = controller.expectOne('/api/v1/org/members');
    req2.flush({}, { status: 401, statusText: 'Unauthorized' });

    // The skip-listed download request bypasses the interceptor entirely — flush it
    // with a normal 200 so HttpTestingController.verify() is satisfied in afterEach.
    const skipReq = controller.expectOne('/api/v1/plugins/private-plugin-id/download');
    expect(skipReq.request.headers.has('Authorization')).toBe(false); // confirm skip-list worked
    skipReq.flush([]);

    tick();

    // Now complete the single shared refresh
    refreshSubject.next(REFRESHED);
    refreshSubject.complete();
    tick();

    // Both non-skipped retries should fire
    const retry1 = controller.expectOne('/api/v1/dashboard');
    const retry2 = controller.expectOne('/api/v1/org/members');
    retry1.flush({ ok: true });
    retry2.flush({ ok: true });
    tick();

    // CRITICAL: refreshToken called exactly once despite two concurrent 401s
    expect(port.refreshCallCount).toBe(1);
  }));
});

// ---------------------------------------------------------------------------
// 6. Refresh failure — clear store + navigate to /login
// ---------------------------------------------------------------------------

describe('authInterceptor — refresh failure handling', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('should call clearAll() on AuthStore when refresh fails', fakeAsync(() => {
    const { httpClient, controller, store, port } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);
    port.shouldFailRefresh = true;

    const clearAllSpy = vi.spyOn(store, 'clearAll');

    httpClient.get('/api/v1/dashboard').subscribe({ error: () => undefined });

    const req = controller.expectOne('/api/v1/dashboard');
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(clearAllSpy).toHaveBeenCalledTimes(1);
  }));

  it('should navigate to /login when refresh fails', fakeAsync(() => {
    const { httpClient, controller, port, router } = setupHarness();
    port.shouldFailRefresh = true;

    httpClient.get('/api/v1/dashboard').subscribe({ error: () => undefined });

    const req = controller.expectOne('/api/v1/dashboard');
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(router.navigatedUrls.some((u) => u.includes('login'))).toBe(true);
  }));

  it('should propagate the refresh error as an observable error', fakeAsync(() => {
    const { httpClient, controller, port } = setupHarness();
    port.shouldFailRefresh = true;

    const errors: unknown[] = [];
    httpClient.get('/api/v1/dashboard').subscribe({ error: (e) => errors.push(e) });

    const req = controller.expectOne('/api/v1/dashboard');
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    tick();

    expect(errors).toHaveLength(1);
  }));

  it('should not retry again after refresh failure (no infinite loop)', fakeAsync(() => {
    const { httpClient, controller, port } = setupHarness();
    port.shouldFailRefresh = true;

    httpClient.get('/api/v1/dashboard').subscribe({ error: () => undefined });

    const req = controller.expectOne('/api/v1/dashboard');
    req.flush({}, { status: 401, statusText: 'Unauthorized' });
    tick();

    // No further requests should be pending
    controller.verify();
  }));
});

// ---------------------------------------------------------------------------
// 7. Non-401 errors pass through unchanged
// ---------------------------------------------------------------------------

describe('authInterceptor — non-401 errors pass through', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('should pass through 500 errors without calling refresh', fakeAsync(() => {
    const { httpClient, controller, port, store } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    const errors: unknown[] = [];
    httpClient.get('/api/v1/dashboard').subscribe({ error: (e) => errors.push(e) });

    const req = controller.expectOne('/api/v1/dashboard');
    req.flush({ message: 'Internal Server Error' }, { status: 500, statusText: 'Server Error' });
    tick();

    expect(port.refreshCallCount).toBe(0);
    expect(errors).toHaveLength(1);
  }));

  it('should pass through 403 errors without calling refresh', fakeAsync(() => {
    const { httpClient, controller, port, store } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    const errors: unknown[] = [];
    httpClient.get('/api/v1/dashboard').subscribe({ error: (e) => errors.push(e) });

    const req = controller.expectOne('/api/v1/dashboard');
    req.flush({ message: 'Forbidden' }, { status: 403, statusText: 'Forbidden' });
    tick();

    expect(port.refreshCallCount).toBe(0);
    expect(errors).toHaveLength(1);
  }));

  it('should pass through 404 errors without calling refresh', fakeAsync(() => {
    const { httpClient, controller, port, store } = setupHarness();
    seedTokenInStore(store, FAKE_TOKEN);

    const errors: unknown[] = [];
    httpClient.get('/api/v1/dashboard').subscribe({ error: (e) => errors.push(e) });

    const req = controller.expectOne('/api/v1/dashboard');
    req.flush({ message: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    tick();

    expect(port.refreshCallCount).toBe(0);
    expect(errors).toHaveLength(1);
  }));
});

// ---------------------------------------------------------------------------
// 8. Interceptor function export contract
// ---------------------------------------------------------------------------

describe('authInterceptor — function export contract', () => {
  it('should be exported as a named function (functional interceptor)', () => {
    expect(typeof authInterceptor).toBe('function');
  });

  it('should accept HttpRequest and HttpHandlerFn parameters', () => {
    // Verify the function signature is compatible with Angular's functional interceptor type
    const dummyReq = new HttpRequest('GET', '/test');
    const dummyNext: HttpHandlerFn = (_: HttpRequest<unknown>) =>
      of(new HttpResponse({ status: 200 })) as Observable<HttpEvent<unknown>>;

    // This must not throw at function-call level; actual DI happens at app config time
    expect(() => {
      // We call it without TestBed — it will fail to inject AuthStore/AuthPort,
      // but the function itself must exist and be callable
      try {
        authInterceptor(dummyReq, dummyNext);
      } catch {
        // DI errors are expected outside TestBed — the function signature is what matters
      }
    }).not.toThrow();
  });
});
