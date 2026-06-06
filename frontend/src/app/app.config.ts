import { ApplicationConfig, provideBrowserGlobalErrorListeners, inject } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';

import { routes } from './app.routes';
import { API_BASE_URL } from './core/config/api-config';

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
  ],
};

// Re-export inject for convenience so tests can override the token.
export { inject };
