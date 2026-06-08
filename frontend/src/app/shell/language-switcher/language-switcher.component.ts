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
  styles: [
    `
      /*
       * Language switcher — all colors use semantic CSS custom property tokens (D3 rule).
       * Lives in the shell header (sidebar surface).
       */
      .cf-lang-switcher {
        display: flex;
        align-items: center;
        gap: 0.25rem;
      }

      .cf-lang-switcher__btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        padding: 0.25rem 0.5rem;
        background: transparent;
        border: 1px solid transparent;
        border-radius: var(--radius-sm, 0.25rem);
        color: var(--sidebar-foreground);
        opacity: 0.7;
        font-size: 0.75rem;
        font-weight: 600;
        cursor: pointer;
        transition:
          background-color 0.2s ease,
          opacity 0.2s ease,
          border-color 0.2s ease;
      }

      .cf-lang-switcher__btn[aria-pressed='true'] {
        opacity: 1;
        background: var(--sidebar-accent);
        color: var(--sidebar-accent-foreground);
        border-color: var(--sidebar-border);
      }

      .cf-lang-switcher__btn:hover {
        opacity: 1;
        background: var(--sidebar-accent);
        color: var(--sidebar-accent-foreground);
      }

      .cf-lang-switcher__btn:focus-visible {
        outline: 2px solid var(--ring);
        outline-offset: 2px;
        opacity: 1;
      }
    `,
  ],
  template: `<div class="cf-lang-switcher" role="group" [attr.aria-label]="facade.t('language-switcher.aria')">
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
