import type { Route } from '@angular/router';
import { routes } from './app.routes';
import { FunctionalAuthGuard } from './features/auth/infrastructure/guards/auth.guard';

function findShellChildRoute(path: string): Route | undefined {
  const shellRoute = routes.find((route) => route.path === '');
  return shellRoute?.children?.find((route) => route.path === path);
}

describe('app routes', () => {
  it('protects the dashboard route with the standard auth guard', () => {
    const dashboardRoute = findShellChildRoute('dashboard');
    expect(dashboardRoute?.canActivate).toContain(FunctionalAuthGuard);
  });
});
