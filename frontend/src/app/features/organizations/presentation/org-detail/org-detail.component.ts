/**
 * OrgDetailComponent — detail view for a specific organisation.
 * Route: /orgs/:orgId
 * Standalone component with @if (no CommonModule).
 */

import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import { OrgMembersComponent } from '../org-members/org-members.component';
import { OrgInvitationsComponent } from '../org-invitations/org-invitations.component';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-org-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, OrgMembersComponent, OrgInvitationsComponent],
  template: `
    @if (authFacade.isAuthenticated()) {
      <div class="cf-org-detail">
        <a routerLink="/orgs" class="cf-org-detail__back">{{ i18n.t('organizations.back-to-orgs') }}</a>

        @if (orgId) {
          <cf-org-members [orgId]="orgId" />
          <cf-org-invitations [orgId]="orgId" />
        }
      </div>
    }
  `,
  styles: [
    `
       .cf-org-detail {
         max-width: 96rem;
         margin: 0 auto;
         padding: 2rem 1.5rem;
       }

      .cf-org-detail__back {
        color: #4f46e5;
        text-decoration: none;
        font-size: 0.875rem;
        display: inline-block;
        margin-bottom: 1.5rem;
      }

      .cf-org-detail__back:hover {
        text-decoration: underline;
      }
    `,
  ],
})
export class OrgDetailComponent implements OnInit {
  protected readonly orgsFacade = inject(OrganizationsFacade);
  protected readonly contextFacade = inject(OrgContextFacade);
  protected readonly authFacade = inject(AuthFacade);
  protected readonly i18n = inject(I18nFacade);
  private readonly route = inject(ActivatedRoute);

  protected orgId: string | undefined;

  ngOnInit(): void {
    this.orgId = this.route.snapshot.paramMap.get('orgId') ?? undefined;
    if (this.orgId) {
      this.orgsFacade.listMembers(this.orgId);
    }
  }
}
