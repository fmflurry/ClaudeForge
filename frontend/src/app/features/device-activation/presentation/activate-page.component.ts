/**
 * ActivatePageComponent — Device activation / approval page.
 *
 * Reads ?user_code from the query string on init and prefills the input.
 * Calls DeviceActivationFacade.approve() on submit.
 * All state (status, errorReason) is exposed via facade signals only.
 *
 * Route: /activate (auth-guarded — see app.routes.ts)
 */

import { ChangeDetectionStrategy, Component, inject, OnInit, Signal } from '@angular/core';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { provideTranslocoScope } from '@jsverse/transloco';
import { DeviceActivationFacade } from '../application/facades/device-activation.facade';
import type { DeviceActivationErrorReason, DeviceActivationStatus } from '../domain/ports/device-activation.port';
import { I18nFacade } from '../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-activate-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  providers: [provideTranslocoScope('device-activation')],
  template: `
    <div class="cf-activate">
      <div class="cf-activate__card">
        <h1 class="cf-activate__title">{{ i18n.t('device-activation.title') }}</h1>
        <p class="cf-activate__subtitle">{{ i18n.t('device-activation.subtitle') }}</p>

        @if (status() === 'approved') {
          <div class="cf-activate__approved" role="status">
            <p>{{ i18n.t('device-activation.approved') }}</p>
          </div>
        }

        @if (status() === 'error') {
          <div class="cf-activate__error" role="alert">
            {{ errorMessage() }}
          </div>
        }

        @if (status() !== 'approved') {
          <form class="cf-activate__form" (ngSubmit)="onSubmit()">
            <label class="cf-activate__label" for="userCode">{{ i18n.t('device-activation.label-user-code') }}</label>
            <input
              id="userCode"
              class="cf-activate__input"
              type="text"
              [formControl]="userCodeControl"
              [placeholder]="i18n.t('device-activation.placeholder')"
              autocomplete="off"
            />

            <button type="submit" class="cf-activate__btn" [disabled]="status() === 'submitting'">
              @if (status() === 'submitting') {
                {{ i18n.t('device-activation.btn-approving') }}
              } @else {
                {{ i18n.t('device-activation.btn-approve') }}
              }
            </button>
          </form>
        }
      </div>
    </div>
  `,
  styles: [
    `
      .cf-activate {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: #f9fafb;
        padding: 1.5rem;
      }

      .cf-activate__card {
        background: #fff;
        border-radius: 0.75rem;
        box-shadow: 0 4px 24px rgba(0, 0, 0, 0.08);
        padding: 2.5rem;
        width: 100%;
        max-width: 26rem;
        display: flex;
        flex-direction: column;
        gap: 1.25rem;
      }

      .cf-activate__title {
        font-size: 1.5rem;
        font-weight: 700;
        color: #111827;
        margin: 0;
        text-align: center;
      }

      .cf-activate__subtitle {
        font-size: 0.9375rem;
        color: #6b7280;
        margin: 0;
        text-align: center;
      }

      .cf-activate__approved {
        padding: 0.75rem 1rem;
        background: #f0fdf4;
        border: 1px solid #bbf7d0;
        border-radius: 0.5rem;
        color: #166534;
        font-size: 0.875rem;
        text-align: center;
      }

      .cf-activate__error {
        padding: 0.75rem 1rem;
        background: #fef2f2;
        border: 1px solid #fecaca;
        border-radius: 0.5rem;
        color: #b91c1c;
        font-size: 0.875rem;
      }

      .cf-activate__form {
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
      }

      .cf-activate__label {
        font-size: 0.875rem;
        font-weight: 600;
        color: #374151;
      }

      .cf-activate__input {
        width: 100%;
        padding: 0.625rem 0.875rem;
        font-size: 1rem;
        border: 1px solid #d1d5db;
        border-radius: 0.5rem;
        outline: none;
        box-sizing: border-box;
      }

      .cf-activate__input:focus {
        border-color: #6366f1;
        box-shadow: 0 0 0 2px rgba(99, 102, 241, 0.2);
      }

      .cf-activate__btn {
        padding: 0.75rem 1.25rem;
        background: #6366f1;
        color: #fff;
        border: none;
        border-radius: 0.5rem;
        font-size: 0.9375rem;
        font-weight: 600;
        cursor: pointer;
        transition: background-color 0.2s ease;
      }

      .cf-activate__btn:hover:not(:disabled) {
        background: #4f46e5;
      }

      .cf-activate__btn:disabled {
        opacity: 0.6;
        cursor: not-allowed;
      }
    `,
  ],
})
export class ActivatePageComponent implements OnInit {
  private readonly facade = inject(DeviceActivationFacade);
  private readonly route = inject(ActivatedRoute);
  protected readonly i18n = inject(I18nFacade);

  // Expose facade signals to template (no any/$any)
  readonly status: Signal<DeviceActivationStatus> = this.facade.status;
  readonly errorReason: Signal<DeviceActivationErrorReason | undefined> = this.facade.errorReason;

  // Reactive form control for the user code input
  readonly userCodeControl = new FormControl<string>('', { nonNullable: true });

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('user_code');
    if (code) {
      this.userCodeControl.setValue(code);
    }
  }

  onSubmit(): void {
    const trimmed = this.userCodeControl.value.trim();
    if (!trimmed) {
      return;
    }
    this.facade.approve(trimmed);
  }

  errorMessage(): string {
    const reason = this.errorReason();
    switch (reason) {
      case 'invalid':
        return this.i18n.t('device-activation.error.invalid');
      case 'not-found':
        return this.i18n.t('device-activation.error.not-found');
      case 'already-approved':
        return this.i18n.t('device-activation.error.already-approved');
      case 'expired':
        return this.i18n.t('device-activation.error.expired');
      case 'unauthorized':
        return this.i18n.t('device-activation.error.unauthorized');
      case 'unknown':
        return this.i18n.t('device-activation.error.unknown');
      default:
        return this.i18n.t('device-activation.error.unknown');
    }
  }
}
