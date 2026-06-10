import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateFn, Router, RouterStateSnapshot } from '@angular/router';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';

export const AdminGuard: CanActivateFn = (
  _route: ActivatedRouteSnapshot,
  _state: RouterStateSnapshot,
): boolean => {
  const authFacade = inject(AuthFacade);
  const router = inject(Router);

  const user = authFacade.currentUser();
  const isAdmin = user?.orgMemberships.some((membership) => membership.role === 'admin' || membership.role === 'owner');

  if (!authFacade.isAuthenticated()) {
    void router.navigate(['/login']);
    return false;
  }

  if (isAdmin) {
    return true;
  }

  void router.navigate(['/login']);
  return false;
};
