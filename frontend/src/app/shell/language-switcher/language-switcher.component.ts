/**
 * Standalone language switcher component.
 * Renders one button per available language; injects I18nFacade only.
 * Uses transloco pipe for translated aria-labels.
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoModule } from '@jsverse/transloco';
import { I18nFacade } from '../../application/i18n/i18n.facade';
import { LANG_VALUES } from '../../core/i18n/active-language';
import type { Lang } from '../../core/i18n/active-language';

@Component({
  selector: 'cf-language-switcher',
  standalone: true,
  imports: [TranslocoModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cf-lang-switcher" role="group" aria-label="Language switcher">
    @for (lang of langs; track lang) {
      <button
        type="button"
        [attr.aria-pressed]="lang === facade.activeLang()"
        [attr.aria-current]="lang === facade.activeLang() ? 'true' : null"
        [attr.aria-label]="'language-switcher.' + lang | transloco"
        (click)="facade.setLanguage(lang)"
        class="cf-lang-switcher__btn"
      >
        {{ lang.toUpperCase() }}
      </button>
    }
  </div>`,
})
export class LanguageSwitcherComponent {
  protected readonly facade = inject(I18nFacade);
  protected readonly langs: readonly Lang[] = LANG_VALUES;
}
