/**
 * Login page — presents Sign-in with Google and Microsoft buttons.
 * Calls AuthFacade.login(provider) which triggers a full-page redirect.
 * This is a standalone component with no CommonModule dependency.
 */

import { ChangeDetectionStrategy, Component, inject, Signal } from '@angular/core';
import { AuthFacade } from '../../application/facades/auth.facade';
import type { AuthProvider, AuthStatus } from '../../domain/models/auth.models';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-login-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-login">
      <div class="cf-login__card">
        <h1 class="cf-login__title">Sign in to ClaudeForge</h1>
        <p class="cf-login__subtitle">{{ i18n.t('auth.login.subtitle') }}</p>

        @if (authError()) {
          <div class="cf-login__error" role="alert">
            {{ authError() }}
          </div>
        }

        <div class="cf-login__actions">
          <button
            type="button"
            class="cf-login__btn cf-login__btn--google"
            [disabled]="isAuthenticating()"
            (click)="onLogin('google')"
            [attr.aria-label]="i18n.t('auth.login.sign-in-google')"
          >
            <span class="cf-login__btn-icon" aria-hidden="true">G</span>
            {{ i18n.t('auth.login.sign-in-google') }}
          </button>

          <button
            type="button"
            class="cf-login__btn cf-login__btn--microsoft"
            [disabled]="isAuthenticating()"
            (click)="onLogin('microsoft')"
            [attr.aria-label]="i18n.t('auth.login.sign-in-microsoft')"
          >
            <span class="cf-login__btn-icon" aria-hidden="true">M</span>
            {{ i18n.t('auth.login.sign-in-microsoft') }}
          </button>
        </div>

        @if (isAuthenticating()) {
          <p class="cf-login__loading" role="status" aria-live="polite">{{ i18n.t('auth.login.redirecting') }}</p>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .cf-login {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: #f9fafb;
        padding: 1.5rem;
      }

      .cf-login__card {
        background: #fff;
        border-radius: 0.75rem;
        box-shadow: 0 4px 24px rgba(0, 0, 0, 0.08);
        padding: 2.5rem;
        width: 100%;
        max-width: 24rem;
        display: flex;
        flex-direction: column;
        gap: 1.5rem;
      }

      .cf-login__title {
        font-size: 1.5rem;
        font-weight: 700;
        color: #111827;
        margin: 0;
        text-align: center;
      }

      .cf-login__subtitle {
        font-size: 0.9375rem;
        color: #6b7280;
        margin: 0;
        text-align: center;
      }

      .cf-login__error {
        padding: 0.75rem 1rem;
        background: #fef2f2;
        border: 1px solid #fecaca;
        border-radius: 0.5rem;
        color: #b91c1c;
        font-size: 0.875rem;
      }

      .cf-login__actions {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
      }

      .cf-login__btn {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.75rem;
        padding: 0.75rem 1.25rem;
        border-radius: 0.5rem;
        font-size: 0.9375rem;
        font-weight: 600;
        cursor: pointer;
        border: 2px solid transparent;
        transition:
          background-color 0.2s ease,
          border-color 0.2s ease;
      }

      .cf-login__btn:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }

      .cf-login__btn--google {
        background: #fff;
        border-color: #d1d5db;
        color: #374151;
      }

      .cf-login__btn--google:hover:not(:disabled) {
        background: #f9fafb;
      }

      .cf-login__btn--microsoft {
        background: #0078d4;
        color: #fff;
        border-color: #0078d4;
      }

      .cf-login__btn--microsoft:hover:not(:disabled) {
        background: #106ebe;
        border-color: #106ebe;
      }

      .cf-login__btn-icon {
        font-weight: 700;
        font-size: 1rem;
      }

      .cf-login__loading {
        text-align: center;
        font-size: 0.875rem;
        color: #6b7280;
        margin: 0;
      }
    `,
  ],
})
export class LoginPageComponent {
  private readonly authFacade = inject(AuthFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly isAuthenticating: Signal<boolean> = this.authFacade.isAuthenticating;
  readonly authStatus: Signal<AuthStatus> = this.authFacade.authStatus;
  readonly authError: Signal<string | undefined> = this.authFacade.authError;

  onLogin(provider: AuthProvider): void {
    this.authFacade.login(provider);
  }
}
