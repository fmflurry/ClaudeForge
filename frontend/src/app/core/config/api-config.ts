import { InjectionToken } from '@angular/core';

/**
 * Runtime-injectable base URL for the ClaudeForge API.
 * Provide via environment or a runtime config loader.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => '',
});
