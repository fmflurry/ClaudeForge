import { ApplicationConfig, mergeApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { API_BASE_URL } from './core/config/api-config';

/**
 * On the server there is no DOM meta tag, so resolve API_BASE_URL from the
 * SSR_API_BASE_URL environment variable (must be an absolute URL).
 * If not set, falls back to '' (relative) which typically means the Angular
 * dev-server proxies requests — gracefully degrades.
 */
function resolveServerApiBaseUrl(): string {
  return process.env['SSR_API_BASE_URL'] ?? '';
}

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    {
      provide: API_BASE_URL,
      useFactory: resolveServerApiBaseUrl,
    },
  ],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
