/**
 * localStorage-backed adapter for LanguageStoragePort.
 * SSR-guarded via PLATFORM_ID injection — reads/writes only on browser platform.
 * On server: read returns null, write is a no-op.
 */

import { inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { LanguageStoragePort } from './language-storage.port';
import { LANG_VALUES } from './active-language';
import type { Lang } from './active-language';

@Injectable()
export class LocalStorageLanguageAdapter extends LanguageStoragePort {
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  override read(): Lang | null {
    if (!this.isBrowser) {
      return null;
    }

    const stored = localStorage.getItem(LanguageStoragePort.STORAGE_KEY);
    if (stored === null) {
      return null;
    }

    return (LANG_VALUES as readonly string[]).includes(stored) ? (stored as Lang) : null;
  }

  override write(lang: Lang): void {
    if (!this.isBrowser) {
      return;
    }

    localStorage.setItem(LanguageStoragePort.STORAGE_KEY, lang);
  }
}
