/**
 * InjectionToken for the active language on the server (SSR).
 * Provided per-request in app.config.server.ts from the request headers.
 */

import { InjectionToken } from '@angular/core';
import { DEFAULT_LANG } from './active-language';
import type { Lang } from './active-language';

export const SERVER_ACTIVE_LANG = new InjectionToken<Lang>('SERVER_ACTIVE_LANG', {
  providedIn: 'root',
  factory: () => DEFAULT_LANG,
});
