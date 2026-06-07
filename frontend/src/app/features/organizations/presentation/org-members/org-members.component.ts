/**
 * OrgMembersComponent — members table with role management (promote/remove).
 * Role-gated actions per org-role.rules + the caller's role.
 * Uses OrganizationsFacade + OrgContextFacade only.
 * Standalone component with @if/@for (no CommonModule).
 * Gated by authentication via @if(authFacade.isAuthenticated()).
 */

import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import { canChangeMemberRole, canRemoveMembers } from '../../domain/rules/org-role.rules';
import type { OrgRole } from '../../domain/models/organizations.models';

@Component({
  selector: 'cf-org-members',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (authFacade.isAuthenticated()) {
      <div class="cf-org-members">
        <h3 class="cf-org-members__title">Members</h3>

        @if (orgsFacade.isLoadingMembers()) {
          <p class="cf-org-members__loading" aria-live="polite">Loading members…</p>
        } @else if (orgsFacade.members().length === 0) {
          <p class="cf-org-members__empty">No members found.</p>
        } @else {
          <table class="cf-org-members__table" aria-label="Organisation members">
            <thead>
              <tr>
                <th scope="col">Name</th>
                <th scope="col">Email</th>
                <th scope="col">Role</th>
                <th scope="col">Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (member of orgsFacade.members(); track member.userId) {
                <tr>
                  <td>{{ member.displayName }}</td>
                  <td>{{ member.email }}</td>
                  <td>{{ member.role }}</td>
                  <td>
                    @if (canChangeRole(callerRole())) {
                      <button
                        type="button"
                        class="cf-org-members__action"
                        (click)="onChangeRole(member.userId, member.role)"
                        [attr.aria-label]="'Change role for ' + member.displayName"
                      >
                        Change Role
                      </button>
                    }
                    @if (canRemove(callerRole())) {
                      <button
                        type="button"
                        class="cf-org-members__action cf-org-members__action--danger"
                        (click)="onRemove(member.userId)"
                        [attr.aria-label]="'Remove ' + member.displayName"
                      >
                        Remove
                      </button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </div>
    }
  `,
  styles: [
    `
      .cf-org-members {
        margin-top: 1.5rem;
      }

      .cf-org-members__title {
        font-size: 1rem;
        font-weight: 600;
        margin: 0 0 0.75rem;
      }

      .cf-org-members__loading,
      .cf-org-members__empty {
        color: #6b7280;
        font-size: 0.875rem;
      }

      .cf-org-members__table {
        width: 100%;
        border-collapse: collapse;
        font-size: 0.875rem;
      }

      .cf-org-members__table th,
      .cf-org-members__table td {
        padding: 0.5rem 0.75rem;
        border-bottom: 1px solid #e5e7eb;
        text-align: left;
      }

      .cf-org-members__table th {
        font-weight: 600;
        color: #374151;
        background: #f9fafb;
      }

      .cf-org-members__action {
        background: #e5e7eb;
        border: none;
        border-radius: 0.25rem;
        padding: 0.25rem 0.5rem;
        font-size: 0.75rem;
        cursor: pointer;
        margin-right: 0.25rem;
        transition: background-color 0.2s ease;
      }

      .cf-org-members__action:hover {
        background: #d1d5db;
      }

      .cf-org-members__action--danger {
        background: #fef2f2;
        color: #991b1b;
      }

      .cf-org-members__action--danger:hover {
        background: #fee2e2;
      }
    `,
  ],
})
export class OrgMembersComponent {
  protected readonly orgsFacade = inject(OrganizationsFacade);
  protected readonly contextFacade = inject(OrgContextFacade);
  protected readonly authFacade = inject(AuthFacade);

  readonly orgId = input.required<string>();

  protected callerRole(): OrgRole {
    return this.contextFacade.activeOrg()?.role ?? 'member';
  }

  protected canChangeRole(role: OrgRole): boolean {
    return canChangeMemberRole(role);
  }

  protected canRemove(role: OrgRole): boolean {
    return canRemoveMembers(role);
  }

  onChangeRole(userId: string, currentRole: OrgRole): void {
    // Cycle through roles: member → admin → owner
    const nextRole: OrgRole = currentRole === 'member' ? 'admin' : 'member';
    this.orgsFacade.changeMemberRole(this.orgId(), userId, nextRole);
  }

  onRemove(userId: string): void {
    this.orgsFacade.removeMember(this.orgId(), userId);
  }
}
