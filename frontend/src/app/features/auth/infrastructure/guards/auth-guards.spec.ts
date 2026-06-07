/**
 * RED tests — Task 9.6: FunctionalAuthGuard + OrgMemberGuard
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile/resolve):
 *   src/app/features/auth/infrastructure/guards/auth.guard.ts
 *   src/app/features/auth/infrastructure/guards/org-member.guard.ts
 *   src/app/features/auth/application/facades/auth.facade.ts  (depended upon)
 *   src/app/features/auth/application/store/auth.store.ts    (depended upon)
 *
 * GREEN contract — exact types/functions the coder MUST implement:
 *
 *   // auth.guard.ts
 *   export const FunctionalAuthGuard: CanActivateFn
 *     — Reads AuthFacade.isAuthenticated() signal.
 *     — If true: returns true (allow navigation).
 *     — If false: calls Router.navigate(['/login']) and returns false.
 *     — MUST be a functional guard (CanActivateFn), not a class-based guard.
 *
 *   // org-member.guard.ts
 *   export const OrgMemberGuard: CanActivateFn
 *     — Reads AuthFacade.isAuthenticated() AND AuthFacade.activeOrgId() signals.
 *     — If isAuthenticated() === true AND activeOrgId() !== undefined: returns true.
 *     — If isAuthenticated() === false: calls Router.navigate(['/login']), returns false.
 *     — If isAuthenticated() === true AND activeOrgId() === undefined:
 *         calls Router.navigate(['/orgs']), returns false.
 *     — Route data: optionally reads ActivatedRouteSnapshot.data['requiredOrgId']:
 *         if provided AND activeOrgId() !== requiredOrgId: deny (navigate '/orgs').
 *     — MUST be a functional guard (CanActivateFn), not a class-based guard.
 *
 *   INJECTION:
 *     Both guards inject AuthFacade and Router using Angular's inject() function.
 *     Tests provide both via TestBed providers.
 *     AuthFacade itself requires AuthPort + AuthStore providers.
 */

import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Injectable, signal, WritableSignal } from '@angular/core';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot } from '@angular/router';
import { Observable } from 'rxjs';
import { AuthFacade } from '../../application/facades/auth.facade';
import { FunctionalAuthGuard } from './auth.guard';
import { OrgMemberGuard } from './org-member.guard';
import type { AuthProvider, CurrentUser } from '../../domain/models/auth.models';

// ---------------------------------------------------------------------------
// Fake AuthFacade — a minimal facade stub for guard tests
// ---------------------------------------------------------------------------

/**
 * A stub AuthFacade that exposes writable signals for test control.
 * Guards read signals via inject(AuthFacade), so we provide this stub.
 */
@Injectable()
class StubAuthFacade {
  private readonly _isAuthenticated: WritableSignal<boolean> = signal(false);
  private readonly _activeOrgId: WritableSignal<string | undefined> = signal(undefined);
  private readonly _currentUser: WritableSignal<CurrentUser | undefined> = signal(undefined);

  // Signal getters — same interface as the real AuthFacade
  get isAuthenticated() {
    return this._isAuthenticated.asReadonly();
  }
  get activeOrgId() {
    return this._activeOrgId.asReadonly();
  }
  get currentUser() {
    return this._currentUser.asReadonly();
  }
  get authStatus() {
    return signal<'idle' | 'authenticating' | 'authenticated' | 'error'>('idle').asReadonly();
  }
  get isAuthenticating() {
    return signal(false).asReadonly();
  }
  get authError() {
    return signal<string | undefined>(undefined).asReadonly();
  }

  // Methods (guards do not call these but they must exist on the type)
  login(_provider: AuthProvider): void {
    return undefined;
  }
  completeLogin(_code: string, _state: string): void {
    return undefined;
  }
  logout(): void {
    return undefined;
  }
  silentRefresh(): void {
    return undefined;
  }
  setActiveOrg(_orgId: string): void {
    return undefined;
  }

  // Test helpers to control signals
  simulateAuthenticated(orgId?: string, user?: CurrentUser): void {
    this._isAuthenticated.set(true);
    this._activeOrgId.set(orgId);
    this._currentUser.set(
      user ?? {
        userId: 'u1',
        email: 'alice@example.com',
        displayName: 'Alice',
        orgMemberships: orgId ? [{ orgId, orgName: 'Test Org', role: 'member' as const }] : [],
      },
    );
  }

  simulateUnauthenticated(): void {
    this._isAuthenticated.set(false);
    this._activeOrgId.set(undefined);
    this._currentUser.set(undefined);
  }
}

// ---------------------------------------------------------------------------
// Fake Router
// ---------------------------------------------------------------------------

@Injectable()
class FakeRouter {
  navigatedCommands: string[][] = [];
  navigate(commands: string[]): Promise<boolean> {
    this.navigatedCommands.push(commands);
    return Promise.resolve(false);
  }
  createUrlTree(_commands: string[]): unknown {
    return {};
  }
  serializeUrl(_tree: unknown): string {
    return '';
  }
}

// ---------------------------------------------------------------------------
// Route snapshot factory
// ---------------------------------------------------------------------------

function makeRouteSnapshot(data: Record<string, unknown> = {}): ActivatedRouteSnapshot {
  return { data } as unknown as ActivatedRouteSnapshot;
}

function makeStateSnapshot(url = '/protected'): RouterStateSnapshot {
  return { url } as unknown as RouterStateSnapshot;
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

interface GuardHarness {
  facade: StubAuthFacade;
  router: FakeRouter;
}

function setupGuardHarness(): GuardHarness {
  TestBed.resetTestingModule();
  const facade = new StubAuthFacade();
  const router = new FakeRouter();

  TestBed.configureTestingModule({
    providers: [
      { provide: AuthFacade, useValue: facade },
      { provide: Router, useValue: router },
    ],
  });

  return { facade, router };
}

// Helper: run a guard in TestBed injection context and get the sync result
function runGuard(
  guardFn: (
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot,
  ) => boolean | Observable<boolean> | Promise<boolean>,
  route = makeRouteSnapshot(),
  state = makeStateSnapshot(),
): boolean | Observable<boolean> | Promise<boolean> {
  return TestBed.runInInjectionContext(() => guardFn(route, state));
}

// ---------------------------------------------------------------------------
// FunctionalAuthGuard — authenticated users
// ---------------------------------------------------------------------------

describe('FunctionalAuthGuard — authenticated user', () => {
  it('should return true when user is authenticated', () => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated('org-1');

    const result = runGuard(FunctionalAuthGuard);
    expect(result).toBe(true);
  });

  it('should NOT call Router.navigate() when user is authenticated', () => {
    const { facade, router } = setupGuardHarness();
    facade.simulateAuthenticated('org-1');

    runGuard(FunctionalAuthGuard);
    expect(router.navigatedCommands).toHaveLength(0);
  });

  it('should allow navigation without an activeOrgId (auth guard only checks isAuthenticated)', () => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated(undefined);

    const result = runGuard(FunctionalAuthGuard);
    expect(result).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// FunctionalAuthGuard — unauthenticated users
// ---------------------------------------------------------------------------

describe('FunctionalAuthGuard — unauthenticated user', () => {
  it('should return false when user is NOT authenticated', () => {
    const { facade } = setupGuardHarness();
    facade.simulateUnauthenticated();

    const result = runGuard(FunctionalAuthGuard);
    expect(result).toBe(false);
  });

  it('should navigate to /login when user is NOT authenticated', fakeAsync(() => {
    const { facade, router } = setupGuardHarness();
    facade.simulateUnauthenticated();

    runGuard(FunctionalAuthGuard);
    tick();

    expect(router.navigatedCommands.some((cmds) => cmds.includes('/login') || cmds.includes('login'))).toBe(true);
  }));

  it('should NOT allow navigation through the guard when unauthenticated', () => {
    const { facade } = setupGuardHarness();
    facade.simulateUnauthenticated();

    const result = runGuard(FunctionalAuthGuard);
    // Regardless of Observable/boolean return, the guard must deny
    expect(result).not.toBe(true);
  });
});

// ---------------------------------------------------------------------------
// FunctionalAuthGuard — is a functional guard (not a class instance)
// ---------------------------------------------------------------------------

describe('FunctionalAuthGuard — is a functional guard', () => {
  it('FunctionalAuthGuard should be a function (CanActivateFn)', () => {
    expect(typeof FunctionalAuthGuard).toBe('function');
  });

  it('FunctionalAuthGuard should not be a class (no prototype.canActivate)', () => {
    const proto = (FunctionalAuthGuard as unknown as Record<string, unknown>)['prototype'];
    const canActivate = proto ? (proto as Record<string, unknown>)['canActivate'] : undefined;
    expect(typeof canActivate).not.toBe('function');
  });
});

// ---------------------------------------------------------------------------
// OrgMemberGuard — authenticated + org context
// ---------------------------------------------------------------------------

describe('OrgMemberGuard — authenticated user with active org', () => {
  it('should return true when user is authenticated and has an activeOrgId', () => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated('org-uuid-1');

    const result = runGuard(OrgMemberGuard);
    expect(result).toBe(true);
  });

  it('should NOT navigate away when user is authenticated and has an activeOrgId', () => {
    const { facade, router } = setupGuardHarness();
    facade.simulateAuthenticated('org-uuid-1');

    runGuard(OrgMemberGuard);
    expect(router.navigatedCommands).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// OrgMemberGuard — authenticated but no org context
// ---------------------------------------------------------------------------

describe('OrgMemberGuard — authenticated but no active org', () => {
  it('should return false when user is authenticated but activeOrgId is undefined', () => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated(undefined);

    const result = runGuard(OrgMemberGuard);
    expect(result).toBe(false);
  });

  it('should navigate to /orgs when authenticated but no activeOrgId', fakeAsync(() => {
    const { facade, router } = setupGuardHarness();
    facade.simulateAuthenticated(undefined);

    runGuard(OrgMemberGuard);
    tick();

    expect(router.navigatedCommands.some((cmds) => cmds.includes('/orgs') || cmds.includes('orgs'))).toBe(true);
  }));

  it('should not navigate to /login when user is authenticated but missing org', fakeAsync(() => {
    const { facade, router } = setupGuardHarness();
    facade.simulateAuthenticated(undefined);

    runGuard(OrgMemberGuard);
    tick();

    expect(router.navigatedCommands.some((cmds) => cmds.includes('/login') || cmds.includes('login'))).toBe(false);
  }));
});

// ---------------------------------------------------------------------------
// OrgMemberGuard — unauthenticated users
// ---------------------------------------------------------------------------

describe('OrgMemberGuard — unauthenticated user', () => {
  it('should return false when user is NOT authenticated', () => {
    const { facade } = setupGuardHarness();
    facade.simulateUnauthenticated();

    const result = runGuard(OrgMemberGuard);
    expect(result).toBe(false);
  });

  it('should navigate to /login when user is NOT authenticated', fakeAsync(() => {
    const { facade, router } = setupGuardHarness();
    facade.simulateUnauthenticated();

    runGuard(OrgMemberGuard);
    tick();

    expect(router.navigatedCommands.some((cmds) => cmds.includes('/login') || cmds.includes('login'))).toBe(true);
  }));
});

// ---------------------------------------------------------------------------
// OrgMemberGuard — requiredOrgId route data check
// ---------------------------------------------------------------------------

describe('OrgMemberGuard — requiredOrgId route data', () => {
  it('should return true when activeOrgId matches requiredOrgId in route data', () => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated('org-uuid-1');

    const route = makeRouteSnapshot({ requiredOrgId: 'org-uuid-1' });
    const result = runGuard(OrgMemberGuard, route);
    expect(result).toBe(true);
  });

  it('should return false when activeOrgId does NOT match requiredOrgId', fakeAsync(() => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated('org-uuid-1');

    const route = makeRouteSnapshot({ requiredOrgId: 'org-uuid-DIFFERENT' });
    const result = runGuard(OrgMemberGuard, route);
    tick();
    expect(result).toBe(false);
  }));

  it('should navigate to /orgs when activeOrgId does not match requiredOrgId', fakeAsync(() => {
    const { facade, router } = setupGuardHarness();
    facade.simulateAuthenticated('org-uuid-1');

    const route = makeRouteSnapshot({ requiredOrgId: 'org-uuid-DIFFERENT' });
    runGuard(OrgMemberGuard, route);
    tick();

    expect(router.navigatedCommands.some((cmds) => cmds.includes('/orgs') || cmds.includes('orgs'))).toBe(true);
  }));

  it('should return true when no requiredOrgId is specified and user has any activeOrgId', () => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated('any-org-id');

    const route = makeRouteSnapshot({}); // no requiredOrgId
    const result = runGuard(OrgMemberGuard, route);
    expect(result).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// OrgMemberGuard — is a functional guard
// ---------------------------------------------------------------------------

describe('OrgMemberGuard — is a functional guard', () => {
  it('OrgMemberGuard should be a function (CanActivateFn)', () => {
    expect(typeof OrgMemberGuard).toBe('function');
  });

  it('OrgMemberGuard should not be a class (no prototype.canActivate)', () => {
    const proto = (OrgMemberGuard as unknown as Record<string, unknown>)['prototype'];
    const canActivate = proto ? (proto as Record<string, unknown>)['canActivate'] : undefined;
    expect(typeof canActivate).not.toBe('function');
  });
});

// ---------------------------------------------------------------------------
// Guards — edge cases
// ---------------------------------------------------------------------------

describe('Guards — edge cases', () => {
  it('FunctionalAuthGuard should not throw on null/undefined route data', () => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated('org-1');

    const route = makeRouteSnapshot(undefined as unknown as Record<string, unknown>);
    expect(() => runGuard(FunctionalAuthGuard, route)).not.toThrow();
  });

  it('OrgMemberGuard should not throw on null/undefined route data', () => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated('org-1');

    const route = makeRouteSnapshot(undefined as unknown as Record<string, unknown>);
    expect(() => runGuard(OrgMemberGuard, route)).not.toThrow();
  });

  it('FunctionalAuthGuard should handle rapid authentication state changes', () => {
    const { facade } = setupGuardHarness();
    facade.simulateUnauthenticated();
    expect(runGuard(FunctionalAuthGuard)).toBe(false);

    facade.simulateAuthenticated('org-1');
    expect(runGuard(FunctionalAuthGuard)).toBe(true);

    facade.simulateUnauthenticated();
    expect(runGuard(FunctionalAuthGuard)).toBe(false);
  });

  it('OrgMemberGuard should handle rapid org context changes', () => {
    const { facade } = setupGuardHarness();

    facade.simulateAuthenticated('org-A');
    expect(runGuard(OrgMemberGuard)).toBe(true);

    facade.simulateAuthenticated(undefined);
    expect(runGuard(OrgMemberGuard)).toBe(false);

    facade.simulateAuthenticated('org-B');
    expect(runGuard(OrgMemberGuard)).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Guards — use AuthFacade (not use cases or store directly)
// ---------------------------------------------------------------------------

describe('Guards — architecture boundary: AuthFacade injection', () => {
  it('FunctionalAuthGuard should not require AuthStore directly in providers', () => {
    // The guard should depend on AuthFacade (abstract facade), not the store directly.
    // Providing only AuthFacade (as stub) and Router must be sufficient.
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated('org-1');

    // If this runs without DI error, the boundary is respected
    expect(() => runGuard(FunctionalAuthGuard)).not.toThrow();
  });

  it('OrgMemberGuard should not require AuthStore directly in providers', () => {
    const { facade } = setupGuardHarness();
    facade.simulateAuthenticated('org-1');

    expect(() => runGuard(OrgMemberGuard)).not.toThrow();
  });
});
