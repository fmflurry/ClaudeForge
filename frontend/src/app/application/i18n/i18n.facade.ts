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
  readonly availableLangs: readonly Lang[] = LANG_VALUES;

  setLanguage(lang: Lang): void {
    this.transloco.setActiveLang(lang);
    this.storage.write(lang);
    this._activeLang.set(lang);
  }
}
