/**
 * Functional auth guard.
 * Reads AuthFacade.isAuthenticated() signal.
 * - true  → allows navigation
 * - false → navigates to /login and returns false
 *
 * Typed to return `boolean` (not the wider CanActivateFn return type)
 * so it is assignable to the spec's narrower runGuard parameter type.
 * It satisfies CanActivateFn at runtime and can be used in route config.
 */

import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { AuthFacade } from '../../application/facades/auth.facade';

export const FunctionalAuthGuard: (
  route: ActivatedRouteSnapshot,
  state: RouterStateSnapshot,
) => boolean = (
  _route: ActivatedRouteSnapshot,
  _state: RouterStateSnapshot,
): boolean => {
  const facade = inject(AuthFacade);
  const router = inject(Router);

  if (facade.isAuthenticated()) {
    return true;
  }

  void router.navigate(['/login']);
  return false;
};

// Satisfy the CanActivateFn type constraint for route registration.
// eslint-disable-next-line @typescript-eslint/no-unused-vars
const _typeCheck: CanActivateFn = FunctionalAuthGuard;
