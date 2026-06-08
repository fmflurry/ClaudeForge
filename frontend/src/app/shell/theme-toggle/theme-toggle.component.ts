/**
 * Standalone theme toggle component.
 * Renders a single button that switches between light and dark modes.
 * Mirrors the language-switcher placement pattern — injected in the shell header.
 * Uses ThemeService (providedIn: 'root') for state; I18nFacade for aria-label.
 * Icons: lucideSun (light mode) and lucideMoon (dark mode) via @ng-icons/lucide.
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideSun, lucideMoon } from '@ng-icons/lucide';
import { ThemeService } from '../../core/services/theme.service';
import { I18nFacade } from '../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-theme-toggle',
  standalone: true,
  imports: [NgIcon],
  viewProviders: [provideIcons({ lucideSun, lucideMoon })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="cf-theme-toggle"
      (click)="themeService.toggle()"
      [attr.aria-label]="i18n.t('shell.aria.theme-toggle')"
      [attr.aria-pressed]="themeService.theme() === 'dark'"
    >
      @if (themeService.theme() === 'dark') {
        <ng-icon name="lucideSun" aria-hidden="true" />
      } @else {
        <ng-icon name="lucideMoon" aria-hidden="true" />
      }
    </button>
  `,
  styles: [
    `
      .cf-theme-toggle {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2rem;
        height: 2rem;
        padding: 0;
        background: transparent;
        border: 1px solid rgba(255, 255, 255, 0.35);
        border-radius: 0.25rem;
        color: rgba(255, 255, 255, 0.85);
        cursor: pointer;
        transition: background-color 0.2s ease;
        font-size: 1rem;
      }

      .cf-theme-toggle:hover {
        background: rgba(255, 255, 255, 0.1);
      }

      .cf-theme-toggle:focus-visible {
        outline: 2px solid var(--ring);
        outline-offset: 2px;
        border-radius: 0.25rem;
      }
    `,
  ],
})
export class ThemeToggleComponent {
  protected readonly themeService = inject(ThemeService);
  protected readonly i18n = inject(I18nFacade);
}
