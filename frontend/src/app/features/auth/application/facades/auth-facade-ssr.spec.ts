/**
 * RED tests — Group 6: SSR enablement — AuthFacade.silentRefresh() SSR safety guards
 *
 * These tests pin the REQUIRED SSR-safe behavior that does NOT exist yet.
 * They MUST FAIL (RED) until the coder implements the GREEN contract below.
 *
 * GREEN contract for auth.facade.ts (silentRefresh) and app.config.ts (APP_INITIALIZER):
 *
 *   AuthFacade.silentRefresh():
 *     - Inject PLATFORM_ID.
 *     - Guard the body with isPlatformBrowser(platformId).
 *     - On server: silentRefresh() is a complete NO-OP — it MUST NOT call
 *       AuthPort.refreshToken(), MUST NOT call AuthPort.getCurrentUser(),
 *       and MUST NOT update the AuthStore.
 *     - On browser: behavior is identical to today (calls refreshToken, on
 *       success calls getCurrentUser and updates store; on failure clears store).
 *
 *   APP_INITIALIZER (silentRefreshInitializer factory in app.config.ts):
 *     - The factory function must also be platform-aware.
 *     - On server: the returned initializer function is a NO-OP (does not call
 *       facade.silentRefresh() at all, or silentRefresh() itself is already a NO-OP).
 *     - On browser: the initializer calls facade.silentRefresh() as today.
 *
 * Crash site (current behavior):
 *   On the server, APP_INITIALIZER fires silentRefresh() which calls
 *   AuthPort.refreshToken() → the HTTP request fires → the authInterceptor
 *   crashes on `window.location.origin` → SSR render throws before the
 *   first HTML byte is produced.
 *
 *   Even if the interceptor crash is fixed, the refresh attempt on the server
 *   is semantically meaningless (no HttpOnly cookie) and wastes an HTTP round-trip.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable, PLATFORM_ID } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { AuthStore, AuthStoreEnum } from '../store/auth.store';
import { AuthFacade } from './auth.facade';
import { AuthPort } from '../../domain/ports/auth.port';
import type { AuthProvider, AuthToken, CurrentUser } from '../../domain/models/auth.models';

// ---------------------------------------------------------------------------
// Test fixtures
// ---------------------------------------------------------------------------

const FAKE_TOKEN: AuthToken = {
  accessToken: 'eyJhbGciOiJSUzI1NiJ9.fake-access-token-for-ssr-guard',
};

const FAKE_USER: CurrentUser = {
  userId: 'user-ssr-1',
  email: 'alice@example.com',
  displayName: 'Alice',
  orgMemberships: [],
};

// ---------------------------------------------------------------------------
// Spy-based AuthPort — allows precise call tracking without real HTTP
// ---------------------------------------------------------------------------

@Injectable()
class SpyAuthPort extends AuthPort {
  refreshTokenCallCount = 0;
  getCurrentUserCallCount = 0;

  getAuthorizeUrl(_provider: AuthProvider): Observable<string> {
    return of('https://accounts.google.com/auth');
  }

  exchangeToken(_code: string, _state: string, _codeVerifier: string): Observable<AuthToken> {
    return of(FAKE_TOKEN);
  }

  refreshToken(): Observable<AuthToken> {
    this.refreshTokenCallCount++;
    return of(FAKE_TOKEN);
  }

  getCurrentUser(): Observable<CurrentUser> {
    this.getCurrentUserCallCount++;
    return of(FAKE_USER);
  }

  signOut(): Observable<void> {
    return of(undefined);
  }
}

@Injectable()
class SpyFailingAuthPort extends AuthPort {
  refreshTokenCallCount = 0;

  getAuthorizeUrl(_provider: AuthProvider): Observable<string> {
    return of('https://accounts.google.com/auth');
  }

  exchangeToken(_code: string, _state: string, _codeVerifier: string): Observable<AuthToken> {
    return throwError(() => new Error('no exchange on server'));
  }

  refreshToken(): Observable<AuthToken> {
    this.refreshTokenCallCount++;
    return throwError(() => new Error('no cookie on server'));
  }

  getCurrentUser(): Observable<CurrentUser> {
    return throwError(() => new Error('no user on server'));
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
// Setup helpers
// ---------------------------------------------------------------------------

interface ServerFacadeHarness {
  store: AuthStore;
  facade: AuthFacade;
  port: SpyAuthPort;
}

function setupServerFacadeHarness(): ServerFacadeHarness {
  TestBed.resetTestingModule();
  const port = new SpyAuthPort();
  TestBed.configureTestingModule({
    providers: [
      AuthStore,
      AuthFacade,
      { provide: AuthPort, useValue: port },
      { provide: Router, useValue: new FakeRouter() },
      { provide: PLATFORM_ID, useValue: 'server' },
    ],
  });
  return {
    store: TestBed.inject(AuthStore),
    facade: TestBed.inject(AuthFacade),
    port,
  };
}

interface BrowserFacadeHarness {
  store: AuthStore;
  facade: AuthFacade;
  port: SpyAuthPort;
}

function setupBrowserFacadeHarness(): BrowserFacadeHarness {
  TestBed.resetTestingModule();
  const port = new SpyAuthPort();
  TestBed.configureTestingModule({
    providers: [
      AuthStore,
      AuthFacade,
      { provide: AuthPort, useValue: port },
      { provide: Router, useValue: new FakeRouter() },
      { provide: PLATFORM_ID, useValue: 'browser' },
    ],
  });
  return {
    store: TestBed.inject(AuthStore),
    facade: TestBed.inject(AuthFacade),
    port,
  };
}

// ---------------------------------------------------------------------------
// AuthFacade.silentRefresh() — SERVER platform: MUST be a NO-OP
// ---------------------------------------------------------------------------

describe('AuthFacade.silentRefresh() — SSR safety (PLATFORM_ID=server): must be a NO-OP', () => {
  it('should NOT call AuthPort.refreshToken() when platform is server', () => {
    // CURRENTLY FAILS: silentRefresh() always calls port.refreshToken() regardless of platform.
    const { facade, port } = setupServerFacadeHarness();

    facade.silentRefresh();

    expect(port.refreshTokenCallCount).toBe(0);
  });

  it('should NOT call AuthPort.getCurrentUser() when platform is server', () => {
    // CURRENTLY FAILS: after a successful refreshToken() it also calls getCurrentUser().
    const { facade, port } = setupServerFacadeHarness();

    facade.silentRefresh();

    expect(port.getCurrentUserCallCount).toBe(0);
  });

  it('should NOT update the AuthStore when platform is server', () => {
    // CURRENTLY FAILS: the store gets updated to 'authenticated' after silentRefresh()
    // resolves on any platform.
    const { facade, store } = setupServerFacadeHarness();
    const stateBefore = store.get(AuthStoreEnum.AUTH)();

    facade.silentRefresh();

    const stateAfter = store.get(AuthStoreEnum.AUTH)();
    // On server the store must be untouched (same reference and same content)
    expect(stateAfter).toStrictEqual(stateBefore);
  });

  it('should NOT set isAuthenticated to true when platform is server', () => {
    // CURRENTLY FAILS: the facade becomes authenticated after silentRefresh() completes.
    const { facade } = setupServerFacadeHarness();

    facade.silentRefresh();

    expect(facade.isAuthenticated()).toBe(false);
  });

  it('should NOT throw when called on server platform', () => {
    // This is a safety guard: the NO-OP must be silent, not throw.
    const { facade } = setupServerFacadeHarness();
    expect(() => facade.silentRefresh()).not.toThrow();
  });

  it('should leave authStatus as "idle" when platform is server', () => {
    // CURRENTLY FAILS: status becomes 'authenticated' after the synchronous fake port resolves.
    const { facade } = setupServerFacadeHarness();

    facade.silentRefresh();

    expect(facade.authStatus()).toBe('idle');
  });

  it('should leave currentUser as undefined when platform is server', () => {
    // CURRENTLY FAILS: currentUser is populated after silentRefresh() on server.
    const { facade } = setupServerFacadeHarness();

    facade.silentRefresh();

    expect(facade.currentUser()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// AuthFacade.silentRefresh() — BROWSER platform: regression guard
// (behavior must be UNCHANGED after the SSR guard is introduced)
// ---------------------------------------------------------------------------

describe('AuthFacade.silentRefresh() — browser platform regression guard (PLATFORM_ID=browser)', () => {
  it('should call AuthPort.refreshToken() on browser platform', () => {
    const { facade, port } = setupBrowserFacadeHarness();

    facade.silentRefresh();

    expect(port.refreshTokenCallCount).toBe(1);
  });

  it('should call AuthPort.getCurrentUser() on browser platform after successful refresh', () => {
    const { facade, port } = setupBrowserFacadeHarness();

    facade.silentRefresh();

    expect(port.getCurrentUserCallCount).toBe(1);
  });

  it('should set isAuthenticated to true on browser platform after successful refresh', () => {
    const { facade } = setupBrowserFacadeHarness();

    facade.silentRefresh();

    expect(facade.isAuthenticated()).toBe(true);
  });

  it('should populate currentUser on browser platform after successful refresh', () => {
    const { facade } = setupBrowserFacadeHarness();

    facade.silentRefresh();

    expect(facade.currentUser()).toEqual(FAKE_USER);
  });

  it('should not throw on browser platform when refresh fails', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        AuthStore,
        AuthFacade,
        { provide: AuthPort, useClass: SpyFailingAuthPort },
        { provide: Router, useValue: new FakeRouter() },
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });
    const facade = TestBed.inject(AuthFacade);

    expect(() => facade.silentRefresh()).not.toThrow();
  });

  it('should leave isAuthenticated false on browser platform when refresh fails', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        AuthStore,
        AuthFacade,
        { provide: AuthPort, useClass: SpyFailingAuthPort },
        { provide: Router, useValue: new FakeRouter() },
        { provide: PLATFORM_ID, useValue: 'browser' },
      ],
    });
    const facade = TestBed.inject(AuthFacade);

    facade.silentRefresh();

    expect(facade.isAuthenticated()).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// APP_INITIALIZER factory (silentRefreshInitializer) — SERVER platform
// ---------------------------------------------------------------------------
// These tests import silentRefreshInitializer indirectly via the facade.
// The factory in app.config.ts: `(facade) => () => facade.silentRefresh()`
// After GREEN the factory MUST be wrapped with an isPlatformBrowser guard
// OR silentRefresh() itself is a NO-OP on server (the facade guard handles it).
//
// We test the observable outcome (no port calls) rather than the factory
// internals to stay behavior-focused.
// ---------------------------------------------------------------------------

describe('APP_INITIALIZER silentRefresh — server platform produces no side-effects', () => {
  it('calling the initializer factory result on server triggers zero port calls', () => {
    // CURRENTLY FAILS: the factory calls facade.silentRefresh() which calls port.refreshToken().
    const { facade, port } = setupServerFacadeHarness();

    // Simulate what APP_INITIALIZER does: call the returned thunk
    const initializer = () => facade.silentRefresh();
    initializer();

    expect(port.refreshTokenCallCount).toBe(0);
  });

  it('calling the initializer factory result on server leaves the store untouched', () => {
    // CURRENTLY FAILS: store is mutated to 'authenticated' after the synchronous fake resolves.
    const { facade, store } = setupServerFacadeHarness();
    const stateBefore = store.get(AuthStoreEnum.AUTH)();

    const initializer = () => facade.silentRefresh();
    initializer();

    const stateAfter = store.get(AuthStoreEnum.AUTH)();
    expect(stateAfter).toStrictEqual(stateBefore);
  });
});
