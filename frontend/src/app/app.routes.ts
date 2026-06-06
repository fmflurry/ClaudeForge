import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./shell/shell-layout.component').then((m) => m.ShellLayoutComponent),
    children: [
      {
        path: 'catalog',
        loadComponent: () =>
          import('./features/catalog/presentation/catalog-page.component').then(
            (m) => m.CatalogPageComponent,
          ),
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
          import('./features/dashboard/presentation/dashboard-page.component').then(
            (m) => m.DashboardPageComponent,
          ),
      },
      {
        path: 'docs',
        loadComponent: () =>
          import('./features/docs/presentation/docs-page.component').then(
            (m) => m.DocsPageComponent,
          ),
      },
      {
        path: '',
        redirectTo: 'catalog',
        pathMatch: 'full',
      },
    ],
  },
];
