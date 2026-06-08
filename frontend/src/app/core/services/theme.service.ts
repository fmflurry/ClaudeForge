/**
 * ThemeService — singleton service for light/dark theme management.
 *
 * Design decisions (design.md D5):
 * - Theme state is held in a signals-native readonly signal (`theme`).
 * - Storage key `cf.theme` follows the project's `cf.*` localStorage convention.
 * - All browser APIs (localStorage, document, window.matchMedia) are guarded
 *   by `isPlatformBrowser(PLATFORM_ID)` — the service is safe to construct on
 *   the server; it is a no-op during SSR.
 * - On browser init the constructor reads the persisted preference, falls back
 *   to `prefers-color-scheme: dark`, then defaults to `'light'`, and immediately
 *   syncs the `.dark` class on `document.documentElement`.
 *
 * SSR / no-FOUC flow:
 * 1. Server renders theme-neutral HTML (no `.dark` class — service is a no-op).
 * 2. An inline render-blocking <script> in `index.html` (see src/index.html)
 *    reads the same `cf.theme` key BEFORE Angular bootstraps and applies the
 *    `.dark` class synchronously, so the first paint is already correct.
 * 3. After hydration this service reads the same key, sets its signal, and
 *    owns all subsequent toggles — no double-application occurs because the
 *    class is already present when `setTheme` is called with the same value.
 */

import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import type { Signal } from '@angular/core';

export type Theme = 'light' | 'dark';

/** localStorage key — must match the inline script in src/index.html. */
export const THEME_STORAGE_KEY = 'cf.theme' as const;

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly _theme = signal<Theme>(this.resolveInitialTheme());

  /** Read-only signal exposing the active theme. */
  readonly theme: Signal<Theme> = this._theme.asReadonly();

  constructor() {
    if (this.isBrowser) {
      this.applyClass(this._theme());
    }
  }

  /** Returns the currently active theme. */
  getTheme(): Theme {
    return this._theme();
  }

  /**
   * Persists `theme` to localStorage, updates the signal, and applies or
   * removes the `.dark` class on `document.documentElement`.
   * All operations are browser-guarded; calling this on the server is a no-op.
   */
  setTheme(theme: Theme): void {
    if (!this.isBrowser) {
      return;
    }

    localStorage.setItem(THEME_STORAGE_KEY, theme);
    this._theme.set(theme);
    this.applyClass(theme);
  }

  /** Toggles between `'light'` and `'dark'`. */
  toggle(): void {
    this.setTheme(this._theme() === 'dark' ? 'light' : 'dark');
  }

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  /**
   * Determines the initial theme (server: always 'light'; browser: persisted
   * value → system preference → 'light').
   * Called only once during construction.
   */
  private resolveInitialTheme(): Theme {
    if (!this.isBrowser) {
      return 'light';
    }

    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored === 'light' || stored === 'dark') {
      return stored;
    }

    const prefersDark =
      typeof window !== 'undefined' &&
      window.matchMedia != null &&
      window.matchMedia('(prefers-color-scheme: dark)').matches;

    return prefersDark ? 'dark' : 'light';
  }

  /** Adds or removes the `.dark` class on `document.documentElement`. */
  private applyClass(theme: Theme): void {
    if (theme === 'dark') {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  }
}
