/**
 * OrgsPageComponent — the main organisations page.
 * Shows the org list, allows creating orgs, and manages the active org members/invitations.
 * Standalone component with @if/@for (no CommonModule).
 */

import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import { CreateOrgComponent } from '../create-org/create-org.component';
import { OrgMembersComponent } from '../org-members/org-members.component';
import { OrgInvitationsComponent } from '../org-invitations/org-invitations.component';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-orgs-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, CreateOrgComponent, OrgMembersComponent, OrgInvitationsComponent],
  template: `
    @if (authFacade.isAuthenticated()) {
      <div class="cf-orgs-page">
        <h1 class="cf-orgs-page__title">{{ i18n.t('organizations.title') }}</h1>

        <cf-create-org />

        @if (orgsFacade.isLoadingOrgs()) {
          <p class="cf-orgs-page__loading" aria-live="polite">{{ i18n.t('organizations.loading-orgs') }}</p>
        } @else if (orgsFacade.organizations().length === 0) {
          <p class="cf-orgs-page__empty">{{ i18n.t('organizations.empty-orgs') }}</p>
        } @else {
          <ul class="cf-orgs-page__list" [attr.aria-label]="i18n.t('organizations.your-orgs-aria')">
            @for (org of orgsFacade.organizations(); track org.orgId) {
              <li class="cf-orgs-page__item">
                <a [routerLink]="['/orgs', org.orgId]" class="cf-orgs-page__link">
                  {{ org.name }}
                </a>
                <span class="cf-orgs-page__role">{{ org.role }}</span>
              </li>
            }
          </ul>
        }

        @if (contextFacade.activeOrgId(); as activeId) {
          <section class="cf-orgs-page__active" [attr.aria-label]="i18n.t('organizations.active-org-aria')">
            <cf-org-members [orgId]="activeId" />
            <cf-org-invitations [orgId]="activeId" />
          </section>
        }
      </div>
    } @else {
      <div class="cf-orgs-page__unauthenticated">
        <p>
          {{ i18n.t('organizations.sign-in-prompt') }}
          <a routerLink="/login">{{ i18n.t('organizations.sign-in-link') }}</a>
          {{ i18n.t('organizations.sign-in-suffix') }}
        </p>
      </div>
    }
  `,
  styles: [
    `
      .cf-orgs-page {
        max-width: 96rem;
        margin: 0 auto;
        padding: 2rem 1.5rem;
      }

      .cf-orgs-page__title {
        font-size: 1.5rem;
        font-weight: 700;
        margin: 0 0 1.5rem;
      }

      .cf-orgs-page__loading,
      .cf-orgs-page__empty {
        color: #6b7280;
        font-size: 0.875rem;
        margin-top: 1rem;
      }

      .cf-orgs-page__list {
        list-style: none;
        margin: 1.5rem 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }

      .cf-orgs-page__item {
        display: flex;
        align-items: center;
        gap: 1rem;
        padding: 0.75rem;
        border: 1px solid #e5e7eb;
        border-radius: 0.5rem;
        background: #fff;
      }

      .cf-orgs-page__link {
        font-weight: 500;
        color: #4f46e5;
        text-decoration: none;
        flex: 1;
      }

      .cf-orgs-page__link:hover {
        text-decoration: underline;
      }

      .cf-orgs-page__role {
        font-size: 0.75rem;
        color: #6b7280;
        background: #f3f4f6;
        padding: 0.125rem 0.5rem;
        border-radius: 9999px;
      }

      .cf-orgs-page__active {
        margin-top: 2rem;
        padding-top: 1.5rem;
        border-top: 1px solid #e5e7eb;
      }

      .cf-orgs-page__unauthenticated {
        text-align: center;
        margin-top: 4rem;
        color: #6b7280;
      }
    `,
  ],
})
export class OrgsPageComponent implements OnInit {
  protected readonly orgsFacade = inject(OrganizationsFacade);
  protected readonly contextFacade = inject(OrgContextFacade);
  protected readonly authFacade = inject(AuthFacade);
  protected readonly i18n = inject(I18nFacade);

  ngOnInit(): void {
    if (this.authFacade.isAuthenticated()) {
      this.contextFacade.loadOrganizations();
    }
  }
}
