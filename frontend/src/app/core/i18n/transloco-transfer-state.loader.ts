/**
 * Transloco loader with TransferState hydration support.
 *
 * BROWSER:
 *   - If TransferState has the key `transloco.<lang>`: returns cached value
 *     synchronously and removes the key (no HTTP request).
 *   - Otherwise: fetches /i18n/<lang>.json via HttpClient.
 *
 * SERVER (SSR):
 *   - Reads the JSON file directly from disk using the injected I18N_FILE_READER
 *     (avoids relative HTTP which has no host in SSR context).
 *   - Stores the result in TransferState so it serializes into the HTML for the
 *     client to reuse (eliminates a client-side HTTP round-trip).
 */

import { inject, Injectable, PLATFORM_ID, TransferState, makeStateKey } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { Observable, of } from 'rxjs';
import { TranslocoLoader } from '@jsverse/transloco';
import type { Translation } from '@jsverse/transloco';
import { I18N_DIST_PATH, I18N_FILE_READER } from './i18n-dist-path.token';
import type { FileReader } from './i18n-dist-path.token';

@Injectable({ providedIn: 'root' })
export class TranslocoTransferStateLoader implements TranslocoLoader {
  private readonly http = inject(HttpClient);
  private readonly transferState = inject(TransferState);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly distPath = inject(I18N_DIST_PATH, { optional: true });
  private readonly fileReader: FileReader | null = inject(I18N_FILE_READER, { optional: true });

  getTranslation(lang: string): Observable<Translation> {
    const key = makeStateKey<Translation>(`transloco.${lang}`);

    if (this.isBrowser) {
      if (this.transferState.hasKey(key)) {
        const cached = this.transferState.get<Translation>(key, {});
        this.transferState.remove(key);
        return of(cached);
      }
      return this.http.get<Translation>(`/i18n/${lang}.json`);
    }

    // Server: read from disk to avoid relative HTTP without a host
    if (this.fileReader && this.distPath) {
      const filePath = `${this.distPath}/i18n/${lang}.json`;
      const content = this.fileReader(filePath);
      const translation = JSON.parse(content) as Translation;
      this.transferState.set(key, translation);
      return of(translation);
    }

    // Fallback: if no disk reader configured, use HTTP (e.g., tests)
    return this.http.get<Translation>(`/i18n/${lang}.json`);
  }
}
