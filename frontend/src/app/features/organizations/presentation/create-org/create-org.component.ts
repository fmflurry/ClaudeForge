/**
 * CreateOrgComponent — form to create a new organisation.
 * Uses OrganizationsFacade only (no direct store/port access).
 * Standalone component with @if (no CommonModule).
 * Gated by authentication via @if(authFacade.isAuthenticated()).
 */

import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-create-org',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (authFacade.isAuthenticated()) {
      <div class="cf-create-org">
        <h2 class="cf-create-org__title">{{ i18n.t('organizations.create-org-title') }}</h2>

        @if (orgsFacade.orgsError(); as errors) {
          <div class="cf-create-org__error" role="alert">
            @for (err of errors; track err.code) {
              <p>{{ err.message }}</p>
            }
          </div>
        }

        <form class="cf-create-org__form" (ngSubmit)="onSubmit()">
          <div class="cf-create-org__field">
            <label class="cf-create-org__label" for="org-name">{{ i18n.t('organizations.org-name-label') }}</label>
            <input
              id="org-name"
              class="cf-create-org__input"
              type="text"
              [placeholder]="i18n.t('organizations.org-name-placeholder')"
              [value]="name()"
              (input)="name.set(inputValue($event))"
              required
              aria-required="true"
            />
          </div>

          <div class="cf-create-org__field">
            <label class="cf-create-org__label" for="org-slug">{{ i18n.t('organizations.slug-label') }}</label>
            <input
              id="org-slug"
              class="cf-create-org__input"
              type="text"
              [placeholder]="i18n.t('organizations.slug-placeholder')"
              [value]="slug()"
              (input)="slug.set(inputValue($event))"
              required
              aria-required="true"
            />
          </div>

          <button
            type="submit"
            class="cf-create-org__submit"
            [disabled]="orgsFacade.isLoadingOrgs() || !name() || !slug()"
          >
            @if (orgsFacade.isLoadingOrgs()) {
              {{ i18n.t('organizations.creating') }}
            } @else {
              {{ i18n.t('organizations.create-org-btn') }}
            }
          </button>
        </form>
      </div>
    }
  `,
  styles: [
    `
      .cf-create-org {
        max-width: 28rem;
        margin: 2rem auto;
        padding: 1.5rem;
        border: 1px solid #e5e7eb;
        border-radius: 0.5rem;
        background: #fff;
      }

      .cf-create-org__title {
        font-size: 1.25rem;
        font-weight: 700;
        margin: 0 0 1rem;
      }

      .cf-create-org__error {
        background: #fef2f2;
        border: 1px solid #fecaca;
        border-radius: 0.25rem;
        padding: 0.75rem;
        margin-bottom: 1rem;
        color: #991b1b;
        font-size: 0.875rem;
      }

      .cf-create-org__form {
        display: flex;
        flex-direction: column;
        gap: 1rem;
      }

      .cf-create-org__field {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }

      .cf-create-org__label {
        font-size: 0.875rem;
        font-weight: 500;
        color: #374151;
      }

      .cf-create-org__input {
        border: 1px solid #d1d5db;
        border-radius: 0.25rem;
        padding: 0.5rem 0.75rem;
        font-size: 0.875rem;
      }

      .cf-create-org__input:focus {
        outline: none;
        border-color: #6366f1;
        box-shadow: 0 0 0 2px rgba(99, 102, 241, 0.25);
      }

      .cf-create-org__submit {
        background: #6366f1;
        color: #fff;
        border: none;
        border-radius: 0.25rem;
        padding: 0.625rem 1.25rem;
        font-size: 0.875rem;
        font-weight: 500;
        cursor: pointer;
        transition: background-color 0.2s ease;
      }

      .cf-create-org__submit:hover:not(:disabled) {
        background: #4f46e5;
      }

      .cf-create-org__submit:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
    `,
  ],
})
export class CreateOrgComponent {
  protected readonly orgsFacade = inject(OrganizationsFacade);
  protected readonly authFacade = inject(AuthFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly name = signal('');
  readonly slug = signal('');

  inputValue(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  onSubmit(): void {
    const n = this.name().trim();
    const s = this.slug().trim();
    if (!n || !s) return;
    this.orgsFacade.createOrg(n, s);
  }
}
