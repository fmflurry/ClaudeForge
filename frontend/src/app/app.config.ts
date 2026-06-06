import { ApplicationConfig, provideBrowserGlobalErrorListeners, inject } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';

import { routes } from './app.routes';
import { API_BASE_URL } from './core/config/api-config';
import { TeamContextStoragePort } from './shared/domain/ports/team-context-storage.port';
import { LocalStorageTeamContextAdapter } from './shared/infrastructure/storage/local-storage-team-context.adapter';
import { InstalledPluginsStoragePort } from './shared/domain/ports/installed-plugins-storage.port';
import { LocalStorageInstalledPluginsAdapter } from './shared/infrastructure/storage/local-storage-installed-plugins.adapter';
import { CatalogLatestVersionPort } from './features/dashboard/domain/ports/catalog-latest-version.port';
import { CatalogLatestVersionHttpAdapter } from './features/dashboard/infrastructure/adapter/catalog-latest-version-http.adapter';
import { CatalogPort } from './features/catalog/domain/ports/catalog.port';
import { CatalogHttpAdapter } from './features/catalog/infrastructure/adapter/catalog-http.adapter';
import { SearchPort } from './features/search/domain/ports/search.port';
import { SearchHttpAdapter } from './features/search/infrastructure/adapter/search-http.adapter';
import { DashboardFacade } from './features/dashboard/application/facades/dashboard.facade';
import { CatalogFacade } from './features/catalog/application/facades/catalog.facade';
import { SearchFacade } from './features/search/application/facades/search.facade';

/**
 * Reads the runtime API base URL from:
 * 1. A <meta name="api-base-url"> tag injected by the server/nginx at deploy time.
 * 2. Falls back to empty string (relative URLs) for local dev.
 */
function resolveApiBaseUrl(): string {
  if (typeof document !== 'undefined') {
    const meta = document.querySelector<HTMLMetaElement>('meta[name="api-base-url"]');
    if (meta?.content) return meta.content;
  }
  return '';
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withFetch()),
    {
      provide: API_BASE_URL,
      useFactory: resolveApiBaseUrl,
    },
    {
      provide: TeamContextStoragePort,
      useClass: LocalStorageTeamContextAdapter,
    },
    {
      provide: InstalledPluginsStoragePort,
      useClass: LocalStorageInstalledPluginsAdapter,
    },
    {
      provide: CatalogLatestVersionPort,
      useClass: CatalogLatestVersionHttpAdapter,
    },
    {
      provide: CatalogPort,
      useClass: CatalogHttpAdapter,
    },
    {
      provide: SearchPort,
      useClass: SearchHttpAdapter,
    },
    DashboardFacade,
    CatalogFacade,
    SearchFacade,
  ],
};

// Re-export inject for convenience so tests can override the token.
export { inject };
