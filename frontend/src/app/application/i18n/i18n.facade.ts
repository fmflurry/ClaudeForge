/**
 * Application facade for i18n — components never inject TranslocoService directly.
 * Manages active language signal, persists selection, and delegates to TranslocoService.
 */

import { inject, Injectable, Signal, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { LanguageStoragePort } from '../../core/i18n/language-storage.port';
import { LANG_VALUES, DEFAULT_LANG } from '../../core/i18n/active-language';
import type { Lang } from '../../core/i18n/active-language';

@Injectable()
export class I18nFacade {
  private readonly transloco = inject(TranslocoService);
  private readonly storage = inject(LanguageStoragePort);

  private readonly _activeLang = signal<Lang>(DEFAULT_LANG);

  readonly activeLang: Signal<Lang> = this._activeLang.asReadonly();

  /**
   * Available languages, exposed as a getter so LANG_VALUES is read lazily at
   * access time rather than captured as a class-field initialiser at construction
   * time. This avoids an esbuild circular-dependency / module-loading-order edge
   * case where LANG_VALUES could still be `undefined` when the constructor runs
   * (manifests only when many spec files are bundled together with isolate:false).
   */
  get availableLangs(): readonly Lang[] {
    return LANG_VALUES;
  }

  /**
   * Translate a key using the current active language.
   *
   * Reads `this.transloco.activeLang()` as a reactive signal dependency so
   * that any template expression calling `i18n.t(key)` is automatically
   * tracked by Angular's signal graph and re-evaluated on every language
   * change — whether triggered via `facade.setLanguage()` or directly via
   * `TranslocoService.setActiveLang()` (e.g. in tests).
   */
  t(key: string, params?: Record<string, unknown>): string {
    return this.transloco.translate(key, params, this.transloco.activeLang());
  }

  setLanguage(lang: Lang): void {
    this.transloco.setActiveLang(lang);
    this.storage.write(lang);
    this._activeLang.set(lang);
  }
}
