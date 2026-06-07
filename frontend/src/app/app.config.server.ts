import { ApplicationConfig, mergeApplicationConfig } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { API_BASE_URL } from './core/config/api-config';
import { SERVER_ACTIVE_LANG } from './core/i18n/server-language.token';
import { I18N_DIST_PATH, I18N_FILE_READER } from './core/i18n/i18n-dist-path.token';
import { DEFAULT_LANG } from './core/i18n/active-language';

/**
 * On the server there is no DOM meta tag, so resolve API_BASE_URL from the
 * SSR_API_BASE_URL environment variable (must be an absolute URL).
 * If not set, falls back to '' (relative) which typically means the Angular
 * dev-server proxies requests — gracefully degrades.
 */
function resolveServerApiBaseUrl(): string {
  return process.env['SSR_API_BASE_URL'] ?? '';
}

/**
 * Browser dist folder: server bundle lives at dist/frontend/server/,
 * browser bundle at dist/frontend/browser/. import.meta.dirname is the server dir.
 */
const browserDistFolder = join(import.meta.dirname, '../browser');

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    {
      provide: API_BASE_URL,
      useFactory: resolveServerApiBaseUrl,
    },
    // SSR language: defaulted here; per-request lang is overridden in server.ts
    {
      provide: SERVER_ACTIVE_LANG,
      useValue: DEFAULT_LANG,
    },
    // Disk path for reading i18n JSON files during SSR
    {
      provide: I18N_DIST_PATH,
      useValue: browserDistFolder,
    },
    // Node.js filesystem reader (stubbed in tests)
    {
      provide: I18N_FILE_READER,
      useValue: (path: string) => readFileSync(path, 'utf-8'),
    },
  ],
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
