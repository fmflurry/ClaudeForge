import { Routes } from '@angular/router';
import { FunctionalAuthGuard } from './features/auth/infrastructure/guards/auth.guard';
import { OrgMemberGuard } from './features/auth/infrastructure/guards/org-member.guard';

export const routes: Routes = [
  // ---------------------------------------------------------------------------
  // Auth routes (outside shell — full-page, no navigation chrome)
  // ---------------------------------------------------------------------------
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/presentation/login/login-page.component').then((m) => m.LoginPageComponent),
  },
  {
    path: 'auth/callback',
    loadComponent: () =>
      import('./features/auth/presentation/callback/auth-callback.component').then((m) => m.AuthCallbackComponent),
  },
  {
    path: 'activate',
    canActivate: [FunctionalAuthGuard],
    loadComponent: () =>
      import('./features/device-activation/presentation/activate-page.component').then((m) => m.ActivatePageComponent),
  },
  // ---------------------------------------------------------------------------
  // Shell routes (with navigation header)
  // ---------------------------------------------------------------------------
  {
    path: '',
    loadComponent: () => import('./shell/shell-layout.component').then((m) => m.ShellLayoutComponent),
    children: [
      {
        path: 'catalog',
        loadComponent: () =>
          import('./features/catalog/presentation/catalog-page.component').then((m) => m.CatalogPageComponent),
      },
      {
        path: 'search',
        loadComponent: () =>
          import('./features/search/presentation/search-results/search-results.component').then(
            (m) => m.SearchResultsComponent,
          ),
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/presentation/dashboard-page.component').then((m) => m.DashboardPageComponent),
      },
      {
        path: 'docs',
        loadComponent: () =>
          import('./features/docs/presentation/docs-page.component').then((m) => m.DocsPageComponent),
      },
      {
        path: '',
        loadComponent: () =>
          import('./features/home/presentation/landing-page.component').then((m) => m.LandingPageComponent),
        pathMatch: 'full',
      },
      // -----------------------------------------------------------------------
      // Organization routes
      // -----------------------------------------------------------------------
      {
        path: 'orgs',
        canActivate: [FunctionalAuthGuard],
        loadComponent: () =>
          import('./features/organizations/presentation/orgs-page/orgs-page.component').then(
            (m) => m.OrgsPageComponent,
          ),
      },
      {
        path: 'orgs/:orgId',
        canActivate: [FunctionalAuthGuard, OrgMemberGuard],
        loadComponent: () =>
          import('./features/organizations/presentation/org-detail/org-detail.component').then(
            (m) => m.OrgDetailComponent,
          ),
      },
    ],
  },
];
