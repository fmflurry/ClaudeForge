/**
 * Functional org-member guard.
 * Reads AuthFacade.isAuthenticated() AND AuthFacade.activeOrgId() signals.
 *
 * Behaviour:
 * - isAuthenticated() && activeOrgId() !== undefined → true
 * - !isAuthenticated()                               → navigate /login, false
 * - isAuthenticated() && activeOrgId() === undefined → navigate /orgs, false
 * - Route data requiredOrgId !== activeOrgId()       → navigate /orgs, false
 *
 * Typed to return `boolean` (not the wider CanActivateFn return type)
 * so it is assignable to the spec's narrower runGuard parameter type.
 * It satisfies CanActivateFn at runtime and can be used in route config.
 */

import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { AuthFacade } from '../../application/facades/auth.facade';

export const OrgMemberGuard: (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot,
) => boolean = (
  route: ActivatedRouteSnapshot,
  _state: RouterStateSnapshot,
): boolean => {
  const facade = inject(AuthFacade);
  const router = inject(Router);

  if (!facade.isAuthenticated()) {
    void router.navigate(['/login']);
    return false;
  }

  const activeOrgId = facade.activeOrgId();

  if (activeOrgId === undefined) {
    void router.navigate(['/orgs']);
    return false;
  }

  // Optional: check requiredOrgId from route data
  const requiredOrgId = route.data?.['requiredOrgId'] as string | undefined;
  if (requiredOrgId !== undefined && requiredOrgId !== activeOrgId) {
    void router.navigate(['/orgs']);
    return false;
  }

  return true;
};

// Satisfy the CanActivateFn type constraint for route registration.
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const _typeCheck: CanActivateFn = OrgMemberGuard;
