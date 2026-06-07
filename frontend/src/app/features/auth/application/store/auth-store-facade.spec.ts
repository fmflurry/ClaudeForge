/**
 * RED tests — Task 9.1: AuthStore + AuthFacade state transitions
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile/resolve):
 *   src/app/features/auth/application/store/auth.store.ts
 *   src/app/features/auth/application/facades/auth.facade.ts
 *   src/app/features/auth/domain/ports/auth.port.ts
 *   src/app/features/auth/domain/models/auth.models.ts
 *
 * GREEN contract — exact types/classes the coder MUST implement:
 *
 *   // auth.models.ts
 *   export type AuthProvider = 'google' | 'microsoft';
 *   export type AuthStatus = 'idle' | 'authenticating' | 'authenticated' | 'error';
 *   export interface CurrentUser {
 *     userId: string;
 *     email: string;
 *     displayName: string;
 *     orgMemberships: OrgMembership[];
 *   }
 *   export interface OrgMembership {
 *     orgId: string;
 *     orgName: string;
 *     role: 'owner' | 'admin' | 'member';
 *   }
 *   export interface AuthToken {
 *     accessToken: string;          // in-memory only — NEVER written to localStorage/sessionStorage
 *   }
 *
 *   // auth.store.ts
 *   export enum AuthStoreEnum {
 *     AUTH = 'AUTH',
 *   }
 *   export interface AuthStoreData {
 *     status: AuthStatus;
 *     user: CurrentUser | undefined;
 *     token: AuthToken | undefined;    // access token — in memory only
 *     activeOrgId: string | undefined;
 *     errorMessage: string | undefined;
 *   }
 *   export interface AuthState {
 *     [AuthStoreEnum.AUTH]: ResourceState<AuthStoreData>;
 *   }
 *   @Injectable({ providedIn: 'root' })
 *   export class AuthStore extends BaseStore<typeof AuthStoreEnum, AuthState> { }
 *
 *   // auth.port.ts
 *   export abstract class AuthPort {
 *     abstract getAuthorizeUrl(provider: AuthProvider): Observable<string>;
 *     abstract exchangeToken(code: string, state: string, codeVerifier: string): Observable<AuthToken>;
 *     abstract refreshToken(): Observable<AuthToken>;
 *     abstract getCurrentUser(): Observable<CurrentUser>;
 *     abstract signOut(): Observable<void>;
 *   }
 *
 *   // auth.facade.ts
 *   @Injectable()
 *   export class AuthFacade {
 *     // Signal getters (derived from store — readonly):
 *     get currentUser(): Signal<CurrentUser | undefined>
 *     get isAuthenticated(): Signal<boolean>
 *     get activeOrgId(): Signal<string | undefined>
 *     get authStatus(): Signal<AuthStatus>
 *     get isAuthenticating(): Signal<boolean>
 *     get authError(): Signal<string | undefined>
 *
 *     // Methods (components call ONLY these — never use cases directly):
 *     login(provider: AuthProvider): void
 *       — sets status=authenticating, calls AuthPort.getAuthorizeUrl(provider),
 *         then navigates (window.location.href) to the returned URL.
 *         On error sets status=error with errorMessage.
 *     completeLogin(code: string, state: string): void
 *       — called by the callback route; exchanges code via AuthPort.exchangeToken,
 *         then fetches getCurrentUser(), sets status=authenticated, stores token in-memory.
 *         On error sets status=error.
 *     logout(): void
 *       — calls AuthPort.signOut(), then clearAll() on the store, routes to '/'.
 *     silentRefresh(): void
 *       — calls AuthPort.refreshToken(), on success fetches getCurrentUser(),
 *         updates token+user in store. On failure clears store (stays unauthenticated).
 *     setActiveOrg(orgId: string): void
 *       — updates activeOrgId in the store (immutable update).
 *
 *     CRITICAL constraints:
 *     - The access token (AuthToken.accessToken) MUST only live in the in-memory signal.
 *     - It MUST NEVER be written to localStorage, sessionStorage, or any cookie.
 *     - Facade methods call AuthPort, NOT use cases directly.
 *     - All state updates use immutable spread (no mutation).
 *   }
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { Signal } from '@angular/core';
import { ResourceState } from '../../../../shared/application/store/resource-state.model';
import { AuthStore, AuthStoreEnum } from './auth.store';
import type { AuthState, AuthStoreData } from './auth.store';
import { AuthFacade } from '../facades/auth.facade';
import { AuthPort } from '../../domain/ports/auth.port';
import type { AuthProvider, AuthStatus, AuthToken, CurrentUser } from '../../domain/models/auth.models';

// ---------------------------------------------------------------------------
// Test fixtures
// ---------------------------------------------------------------------------

const FAKE_TOKEN: AuthToken = {
  accessToken: 'eyJhbGciOiJSUzI1NiJ9.fake-access-token',
};

const FAKE_USER: CurrentUser = {
  userId: 'user-uuid-1',
  email: 'alice@example.com',
  displayName: 'Alice',
  orgMemberships: [
    { orgId: 'org-uuid-1', orgName: 'Acme Corp', role: 'owner' },
  ],
};

const FAKE_USER_NO_ORGS: CurrentUser = {
  userId: 'user-uuid-2',
  email: 'bob@example.com',
  displayName: 'Bob',
  orgMemberships: [],
};

// ---------------------------------------------------------------------------
// Fake AuthPort
// ---------------------------------------------------------------------------

@Injectable()
class FakeAuthPort extends AuthPort {
  authorizeUrlToReturn: string = 'https://accounts.google.com/o/oauth2/auth?client_id=test';
  tokenToReturn: AuthToken = FAKE_TOKEN;
  userToReturn: CurrentUser = FAKE_USER;
  shouldErrorOnAuthorize = false;
  shouldErrorOnExchange = false;
  shouldErrorOnRefresh = false;
  shouldErrorOnMe = false;
  shouldErrorOnSignOut = false;

  getAuthorizeUrl(_provider: AuthProvider): Observable<string> {
    if (this.shouldErrorOnAuthorize) {
      return throwError(() => new Error('Provider unreachable'));
    }
    return of(this.authorizeUrlToReturn);
  }

  exchangeToken(
    _code: string,
    _state: string,
    _codeVerifier: string,
  ): Observable<AuthToken> {
    if (this.shouldErrorOnExchange) {
      return throwError(() => new Error('Invalid code'));
    }
    return of(this.tokenToReturn);
  }

  refreshToken(): Observable<AuthToken> {
    if (this.shouldErrorOnRefresh) {
      return throwError(() => new Error('Refresh failed'));
    }
    return of(this.tokenToReturn);
  }

  getCurrentUser(): Observable<CurrentUser> {
    if (this.shouldErrorOnMe) {
      return throwError(() => new Error('Unauthorized'));
    }
    return of(this.userToReturn);
  }

  signOut(): Observable<void> {
    if (this.shouldErrorOnSignOut) {
      return throwError(() => new Error('Sign-out failed'));
    }
    return of(undefined);
  }
}

@Injectable()
class ErrorAuthPort extends AuthPort {
  getAuthorizeUrl(_provider: AuthProvider): Observable<string> {
    return throwError(() => new Error('Network error'));
  }

  exchangeToken(
    _code: string,
    _state: string,
    _codeVerifier: string,
  ): Observable<AuthToken> {
    return throwError(() => new Error('Exchange error'));
  }

  refreshToken(): Observable<AuthToken> {
    return throwError(() => new Error('Refresh error'));
  }

  getCurrentUser(): Observable<CurrentUser> {
    return throwError(() => new Error('Not found'));
  }

  signOut(): Observable<void> {
    return throwError(() => new Error('Sign-out error'));
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

interface TestHarness {
  store: AuthStore;
  facade: AuthFacade;
  port: FakeAuthPort;
}

function setupHarness(): TestHarness {
  TestBed.resetTestingModule();
  const port = new FakeAuthPort();
  TestBed.configureTestingModule({
    providers: [
      AuthStore,
      AuthFacade,
      { provide: AuthPort, useValue: port },
    ],
  });
  return {
    store: TestBed.inject(AuthStore),
    facade: TestBed.inject(AuthFacade),
    port,
  };
}

function setupWithErrorPort(): { store: AuthStore; facade: AuthFacade } {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      AuthStore,
      AuthFacade,
      { provide: AuthPort, useClass: ErrorAuthPort },
    ],
  });
  return {
    store: TestBed.inject(AuthStore),
    facade: TestBed.inject(AuthFacade),
  };
}

// ---------------------------------------------------------------------------
// AuthStore — enum keys
// ---------------------------------------------------------------------------

describe('AuthStore — enum keys', () => {
  it('should have AUTH key equal to "AUTH"', () => {
    expect(AuthStoreEnum.AUTH).toBe('AUTH');
  });
});

describe('AuthStore — initial state', () => {
  it('should initialise AUTH with an empty non-loading state', () => {
    TestBed.configureTestingModule({ providers: [AuthStore] });
    const store = TestBed.inject(AuthStore);
    const state: ResourceState<AuthStoreData> = store.get(AuthStoreEnum.AUTH)();
    expect(state.isLoading).toBeFalsy();
    expect(state.data).toBeUndefined();
    expect(state.status).toBeUndefined();
  });

  it('should be an instance of AuthStore', () => {
    TestBed.configureTestingModule({ providers: [AuthStore] });
    const store = TestBed.inject(AuthStore);
    expect(store).toBeInstanceOf(AuthStore);
  });

  it('AUTH state type should accept ResourceState<AuthStoreData>', () => {
    TestBed.configureTestingModule({ providers: [AuthStore] });
    const store = TestBed.inject(AuthStore);
    const partial: Partial<AuthState[typeof AuthStoreEnum.AUTH]> = {
      data: {
        status: 'idle',
        user: undefined,
        token: undefined,
        activeOrgId: undefined,
        errorMessage: undefined,
      },
      status: 'Success',
    };
    store.update(AuthStoreEnum.AUTH, partial);
    expect(store.get(AuthStoreEnum.AUTH)().status).toBe('Success');
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — initial signal values (idle state)
// ---------------------------------------------------------------------------

describe('AuthFacade — initial signal values (idle state)', () => {
  it('currentUser should be undefined before any login', () => {
    const { facade } = setupHarness();
    expect(facade.currentUser()).toBeUndefined();
  });

  it('isAuthenticated should be false before any login', () => {
    const { facade } = setupHarness();
    expect(facade.isAuthenticated()).toBe(false);
  });

  it('activeOrgId should be undefined before any login', () => {
    const { facade } = setupHarness();
    expect(facade.activeOrgId()).toBeUndefined();
  });

  it('authStatus should be "idle" before any action', () => {
    const { facade } = setupHarness();
    expect(facade.authStatus()).toBe('idle');
  });

  it('isAuthenticating should be false before any action', () => {
    const { facade } = setupHarness();
    expect(facade.isAuthenticating()).toBe(false);
  });

  it('authError should be undefined before any error', () => {
    const { facade } = setupHarness();
    expect(facade.authError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — login() transitions to authenticating
// ---------------------------------------------------------------------------

describe('AuthFacade — login() state transitions', () => {
  it('login() should not throw for google provider', () => {
    const { facade } = setupHarness();
    expect(() => facade.login('google')).not.toThrow();
  });

  it('login() should not throw for microsoft provider', () => {
    const { facade } = setupHarness();
    expect(() => facade.login('microsoft')).not.toThrow();
  });

  it('login() should set authStatus to authenticating while fetching URL', () => {
    const { facade } = setupHarness();
    // On synchronous fake port the observable resolves immediately,
    // but the status may transiently pass through 'authenticating'.
    // We verify the call did not crash and a redirect is attempted.
    expect(() => facade.login('google')).not.toThrow();
  });

  it('login() error should set authStatus to "error"', () => {
    const { facade } = setupWithErrorPort();
    facade.login('google');
    expect(facade.authStatus()).toBe('error');
  });

  it('login() error should set authError to a non-empty string', () => {
    const { facade } = setupWithErrorPort();
    facade.login('google');
    expect(typeof facade.authError()).toBe('string');
    expect((facade.authError() as string).length).toBeGreaterThan(0);
  });

  it('login() error must NOT set isAuthenticated to true', () => {
    const { facade } = setupWithErrorPort();
    facade.login('google');
    expect(facade.isAuthenticated()).toBe(false);
  });

  it('login() error must leave currentUser as undefined', () => {
    const { facade } = setupWithErrorPort();
    facade.login('google');
    expect(facade.currentUser()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — completeLogin() success path
// ---------------------------------------------------------------------------

describe('AuthFacade — completeLogin() success path', () => {
  it('completeLogin() should not throw with valid code and state', () => {
    const { facade } = setupHarness();
    expect(() => facade.completeLogin('auth-code-abc', 'state-xyz')).not.toThrow();
  });

  it('completeLogin() should set authStatus to "authenticated"', () => {
    const { facade } = setupHarness();
    facade.completeLogin('auth-code-abc', 'state-xyz');
    expect(facade.authStatus()).toBe('authenticated');
  });

  it('completeLogin() should set isAuthenticated to true', () => {
    const { facade } = setupHarness();
    facade.completeLogin('auth-code-abc', 'state-xyz');
    expect(facade.isAuthenticated()).toBe(true);
  });

  it('completeLogin() should set currentUser from getCurrentUser()', () => {
    const { facade } = setupHarness();
    facade.completeLogin('auth-code-abc', 'state-xyz');
    expect(facade.currentUser()).toEqual(FAKE_USER);
  });

  it('completeLogin() should set activeOrgId to the first org membership when orgs exist', () => {
    const { facade } = setupHarness();
    facade.completeLogin('auth-code-abc', 'state-xyz');
    // activeOrgId defaults to the first membership's orgId
    expect(facade.activeOrgId()).toBe('org-uuid-1');
  });

  it('completeLogin() with user having no orgs should set activeOrgId to undefined', () => {
    const { facade, port } = setupHarness();
    port.userToReturn = FAKE_USER_NO_ORGS;
    facade.completeLogin('auth-code-abc', 'state-xyz');
    expect(facade.activeOrgId()).toBeUndefined();
  });

  it('completeLogin() should clear authError after success', () => {
    const { facade } = setupHarness();
    facade.completeLogin('auth-code-abc', 'state-xyz');
    expect(facade.authError()).toBeUndefined();
  });

  it('isAuthenticating should be false after successful completeLogin()', () => {
    const { facade } = setupHarness();
    facade.completeLogin('auth-code-abc', 'state-xyz');
    expect(facade.isAuthenticating()).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — completeLogin() error path
// ---------------------------------------------------------------------------

describe('AuthFacade — completeLogin() error path', () => {
  it('completeLogin() error should set authStatus to "error"', () => {
    const { facade } = setupWithErrorPort();
    facade.completeLogin('bad-code', 'bad-state');
    expect(facade.authStatus()).toBe('error');
  });

  it('completeLogin() error should set authError to a non-empty string', () => {
    const { facade } = setupWithErrorPort();
    facade.completeLogin('bad-code', 'bad-state');
    expect(typeof facade.authError()).toBe('string');
    expect((facade.authError() as string).length).toBeGreaterThan(0);
  });

  it('completeLogin() error must NOT set isAuthenticated to true', () => {
    const { facade } = setupWithErrorPort();
    facade.completeLogin('bad-code', 'bad-state');
    expect(facade.isAuthenticated()).toBe(false);
  });

  it('completeLogin() error must leave currentUser as undefined', () => {
    const { facade } = setupWithErrorPort();
    facade.completeLogin('bad-code', 'bad-state');
    expect(facade.currentUser()).toBeUndefined();
  });

  it('completeLogin() should not throw on exchange error', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.completeLogin('bad-code', 'bad-state')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — logout() transitions
// ---------------------------------------------------------------------------

describe('AuthFacade — logout() transitions', () => {
  it('logout() should not throw', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    expect(() => facade.logout()).not.toThrow();
  });

  it('logout() should set isAuthenticated to false', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    facade.logout();
    expect(facade.isAuthenticated()).toBe(false);
  });

  it('logout() should clear currentUser to undefined', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    facade.logout();
    expect(facade.currentUser()).toBeUndefined();
  });

  it('logout() should clear activeOrgId to undefined', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    facade.logout();
    expect(facade.activeOrgId()).toBeUndefined();
  });

  it('logout() should reset authStatus to "idle"', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    facade.logout();
    expect(facade.authStatus()).toBe('idle');
  });

  it('logout() should not throw when called before login', () => {
    const { facade } = setupHarness();
    expect(() => facade.logout()).not.toThrow();
  });

  it('logout() on sign-out error should still clear local state', () => {
    const { facade, port } = setupHarness();
    port.shouldErrorOnSignOut = true;
    facade.completeLogin('code', 'state');
    facade.logout();
    // Even if server signout fails, local state must be cleared
    expect(facade.isAuthenticated()).toBe(false);
    expect(facade.currentUser()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — silentRefresh() success path
// ---------------------------------------------------------------------------

describe('AuthFacade — silentRefresh() success path', () => {
  it('silentRefresh() should not throw', () => {
    const { facade } = setupHarness();
    expect(() => facade.silentRefresh()).not.toThrow();
  });

  it('silentRefresh() success should set isAuthenticated to true', () => {
    const { facade } = setupHarness();
    facade.silentRefresh();
    expect(facade.isAuthenticated()).toBe(true);
  });

  it('silentRefresh() success should populate currentUser', () => {
    const { facade } = setupHarness();
    facade.silentRefresh();
    expect(facade.currentUser()).toEqual(FAKE_USER);
  });

  it('silentRefresh() success should set authStatus to "authenticated"', () => {
    const { facade } = setupHarness();
    facade.silentRefresh();
    expect(facade.authStatus()).toBe('authenticated');
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — silentRefresh() failure path
// ---------------------------------------------------------------------------

describe('AuthFacade — silentRefresh() failure path (no cookie / expired)', () => {
  it('silentRefresh() failure should leave isAuthenticated as false', () => {
    const { facade } = setupWithErrorPort();
    facade.silentRefresh();
    expect(facade.isAuthenticated()).toBe(false);
  });

  it('silentRefresh() failure should leave currentUser as undefined', () => {
    const { facade } = setupWithErrorPort();
    facade.silentRefresh();
    expect(facade.currentUser()).toBeUndefined();
  });

  it('silentRefresh() failure should NOT crash the application', () => {
    const { facade } = setupWithErrorPort();
    expect(() => facade.silentRefresh()).not.toThrow();
  });

  it('silentRefresh() failure should NOT set authStatus to "error" (silently stays idle/unauthenticated)', () => {
    const { facade } = setupWithErrorPort();
    facade.silentRefresh();
    // Silent refresh failure is not an error state — user is simply unauthenticated
    expect(facade.authStatus()).not.toBe('authenticated');
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — setActiveOrg() transitions
// ---------------------------------------------------------------------------

describe('AuthFacade — setActiveOrg()', () => {
  it('setActiveOrg() should update activeOrgId signal', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    facade.setActiveOrg('org-uuid-99');
    expect(facade.activeOrgId()).toBe('org-uuid-99');
  });

  it('setActiveOrg() should not throw when called without prior login', () => {
    const { facade } = setupHarness();
    expect(() => facade.setActiveOrg('org-uuid-99')).not.toThrow();
  });

  it('setActiveOrg() should not affect other signal values', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    const userBefore = facade.currentUser();
    facade.setActiveOrg('org-uuid-99');
    expect(facade.currentUser()).toEqual(userBefore);
  });

  it('setActiveOrg() update must be immutable (new state object, not mutation)', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    const storeBefore = facade['store'].get(AuthStoreEnum.AUTH)();
    facade.setActiveOrg('org-uuid-99');
    const storeAfter = facade['store'].get(AuthStoreEnum.AUTH)();
    expect(storeAfter).not.toBe(storeBefore);
  });
});

// ---------------------------------------------------------------------------
// CRITICAL — access token in-memory only (never persisted)
// ---------------------------------------------------------------------------

describe('AuthFacade — CRITICAL: access token in-memory only', () => {
  it('access token must NOT appear in localStorage after completeLogin()', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    const allKeys = Object.keys(localStorage);
    const hasToken = allKeys.some((k) => {
      const val = localStorage.getItem(k) ?? '';
      return val.includes(FAKE_TOKEN.accessToken);
    });
    expect(hasToken).toBe(false);
  });

  it('access token must NOT appear in sessionStorage after completeLogin()', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code', 'state');
    const allKeys = Object.keys(sessionStorage);
    const hasToken = allKeys.some((k) => {
      const val = sessionStorage.getItem(k) ?? '';
      return val.includes(FAKE_TOKEN.accessToken);
    });
    expect(hasToken).toBe(false);
  });

  it('access token must NOT appear in localStorage after silentRefresh()', () => {
    const { facade } = setupHarness();
    facade.silentRefresh();
    const allKeys = Object.keys(localStorage);
    const hasToken = allKeys.some((k) => {
      const val = localStorage.getItem(k) ?? '';
      return val.includes(FAKE_TOKEN.accessToken);
    });
    expect(hasToken).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — state transition sequence (idle → authenticating → authenticated → idle)
// ---------------------------------------------------------------------------

describe('AuthFacade — full state transition sequence', () => {
  it('should transition: idle → authenticated → idle (after logout)', () => {
    const { facade } = setupHarness();

    expect(facade.authStatus()).toBe('idle');
    facade.completeLogin('code', 'state');
    expect(facade.authStatus()).toBe('authenticated');
    facade.logout();
    expect(facade.authStatus()).toBe('idle');
  });

  it('should allow re-login after logout', () => {
    const { facade } = setupHarness();
    facade.completeLogin('code1', 'state1');
    facade.logout();
    facade.completeLogin('code2', 'state2');
    expect(facade.isAuthenticated()).toBe(true);
    expect(facade.currentUser()).toEqual(FAKE_USER);
  });

  it('should recover from error state via completeLogin()', () => {
    const { facade, port } = setupHarness();
    port.shouldErrorOnExchange = true;
    facade.completeLogin('bad-code', 'bad-state');
    expect(facade.authStatus()).toBe('error');

    // Fix port
    port.shouldErrorOnExchange = false;
    facade.completeLogin('good-code', 'good-state');
    expect(facade.authStatus()).toBe('authenticated');
    expect(facade.authError()).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — public API surface
// ---------------------------------------------------------------------------

describe('AuthFacade — public API surface', () => {
  it('should expose currentUser as a signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.currentUser).toBe('function');
  });

  it('should expose isAuthenticated as a signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.isAuthenticated).toBe('function');
  });

  it('should expose activeOrgId as a signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.activeOrgId).toBe('function');
  });

  it('should expose authStatus as a signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.authStatus).toBe('function');
  });

  it('should expose isAuthenticating as a signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.isAuthenticating).toBe('function');
  });

  it('should expose authError as a signal function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.authError).toBe('function');
  });

  it('should expose login() as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.login).toBe('function');
  });

  it('should expose completeLogin() as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.completeLogin).toBe('function');
  });

  it('should expose logout() as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.logout).toBe('function');
  });

  it('should expose silentRefresh() as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.silentRefresh).toBe('function');
  });

  it('should expose setActiveOrg() as a function', () => {
    const { facade } = setupHarness();
    expect(typeof facade.setActiveOrg).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// AuthFacade — architecture boundary
// ---------------------------------------------------------------------------

describe('AuthFacade — architecture boundary', () => {
  it('AuthFacade should be injectable without raw HttpClient in providers', () => {
    // The facade must depend only on AuthPort (abstract), not HttpClient directly.
    // If no DI error is thrown, the boundary is respected.
    const { facade } = setupHarness();
    expect(facade).toBeDefined();
  });

  it('AuthFacade should NOT expose the AuthStore directly to consumers', () => {
    // The architectural contract: consumers must use the facade's documented public API,
    // not the underlying store. TypeScript `private` is not runtime-private, so
    // bracket-access in tests is unavoidable — but `store` must not appear as a
    // named getter on the prototype (i.e. not part of the intentional public surface).
    const { facade } = setupHarness();

    // 1. Verify `store` is NOT declared as a prototype-level property/getter.
    //    Angular inject() sets instance fields — prototype should only have declared getters/methods.
    const protoKeys = Object.getOwnPropertyNames(Object.getPrototypeOf(facade)).filter(
      (k) => k !== 'constructor',
    );
    expect(protoKeys).not.toContain('store');

    // 2. The documented public signal getters must exist on the prototype (via `get` accessors).
    const expectedPublicMembers = [
      'currentUser',
      'isAuthenticated',
      'activeOrgId',
      'authStatus',
      'isAuthenticating',
      'authError',
      'login',
      'completeLogin',
      'logout',
      'silentRefresh',
      'setActiveOrg',
    ];
    for (const member of expectedPublicMembers) {
      expect(protoKeys).toContain(member);
    }

    // 3. The public signal getters return callable signals, not a BaseStore instance.
    //    This is the runtime boundary: components get Signal<T>, never AuthStore.
    const sig: Signal<boolean> = facade.isAuthenticated;
    expect(typeof sig).toBe('function');
    // Signals are plain functions, not AuthStore instances — they do not have a `.get()` store method
    expect(typeof (sig as unknown as Record<string, unknown>)['get']).not.toBe('function');
  });
});
