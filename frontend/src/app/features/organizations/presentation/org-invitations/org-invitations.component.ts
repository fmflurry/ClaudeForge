/**
 * OrgInvitationsComponent — manage pending invitations (send/accept/revoke).
 * Role-gated actions per org-role.rules + the caller's role.
 * Uses OrganizationsFacade + OrgContextFacade + AuthFacade only.
 * Standalone component with @if/@for (no CommonModule).
 */

import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';
import { canInviteMembers, canRevokeInvitation } from '../../domain/rules/org-role.rules';
import type { OrgRole } from '../../domain/models/organizations.models';

@Component({
  selector: 'cf-org-invitations',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (authFacade.isAuthenticated()) {
      <div class="cf-org-inv">
        <h3 class="cf-org-inv__title">Invitations</h3>

        @if (canInvite(callerRole())) {
          <form class="cf-org-inv__send-form" (ngSubmit)="onSendInvite()">
            <input
              class="cf-org-inv__input"
              type="email"
              placeholder="email@example.com"
              [value]="inviteEmail()"
              (input)="inviteEmail.set(inputValue($event))"
              aria-label="Invite email address"
              required
            />
            <select
              class="cf-org-inv__select"
              aria-label="Invite role"
              [value]="inviteRole()"
              (change)="inviteRole.set(selectValue($event))"
            >
              <option value="member">Member</option>
              <option value="admin">Admin</option>
            </select>
            <button
              type="submit"
              class="cf-org-inv__send-btn"
              [disabled]="!inviteEmail()"
            >
              Send Invite
            </button>
          </form>
        }

        @if (orgsFacade.invitations().length === 0) {
          <p class="cf-org-inv__empty">No pending invitations.</p>
        } @else {
          <ul class="cf-org-inv__list" aria-label="Pending invitations">
            @for (inv of orgsFacade.invitations(); track inv.invitationId) {
              <li class="cf-org-inv__item">
                <span class="cf-org-inv__email">{{ inv.email }}</span>
                <span class="cf-org-inv__role">{{ inv.role }}</span>
                <span class="cf-org-inv__status">{{ inv.status }}</span>
                @if (inv.status === 'pending') {
                  <button
                    type="button"
                    class="cf-org-inv__action"
                    (click)="onAccept(inv.invitationId)"
                    [attr.aria-label]="'Accept invitation from ' + inv.email"
                  >
                    Accept
                  </button>
                  @if (canRevoke(callerRole())) {
                    <button
                      type="button"
                      class="cf-org-inv__action cf-org-inv__action--danger"
                      (click)="onRevoke(inv.invitationId)"
                      [attr.aria-label]="'Revoke invitation for ' + inv.email"
                    >
                      Revoke
                    </button>
                  }
                }
              </li>
            }
          </ul>
        }
      </div>
    }
  `,
  styles: [
    `
      .cf-org-inv {
        margin-top: 1.5rem;
      }

      .cf-org-inv__title {
        font-size: 1rem;
        font-weight: 600;
        margin: 0 0 0.75rem;
      }

      .cf-org-inv__send-form {
        display: flex;
        gap: 0.5rem;
        margin-bottom: 1rem;
        flex-wrap: wrap;
      }

      .cf-org-inv__input,
      .cf-org-inv__select {
        border: 1px solid #d1d5db;
        border-radius: 0.25rem;
        padding: 0.5rem 0.75rem;
        font-size: 0.875rem;
      }

      .cf-org-inv__input {
        flex: 1;
        min-width: 12rem;
      }

      .cf-org-inv__send-btn {
        background: #6366f1;
        color: #fff;
        border: none;
        border-radius: 0.25rem;
        padding: 0.5rem 1rem;
        font-size: 0.875rem;
        font-weight: 500;
        cursor: pointer;
        transition: background-color 0.2s ease;
      }

      .cf-org-inv__send-btn:hover:not(:disabled) {
        background: #4f46e5;
      }

      .cf-org-inv__send-btn:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }

      .cf-org-inv__empty {
        color: #6b7280;
        font-size: 0.875rem;
      }

      .cf-org-inv__list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
      }

      .cf-org-inv__item {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.5rem 0;
        border-bottom: 1px solid #e5e7eb;
        font-size: 0.875rem;
      }

      .cf-org-inv__email {
        flex: 1;
      }

      .cf-org-inv__role,
      .cf-org-inv__status {
        color: #6b7280;
        font-size: 0.75rem;
      }

      .cf-org-inv__action {
        background: #e5e7eb;
        border: none;
        border-radius: 0.25rem;
        padding: 0.25rem 0.5rem;
        font-size: 0.75rem;
        cursor: pointer;
        transition: background-color 0.2s ease;
      }

      .cf-org-inv__action:hover {
        background: #d1d5db;
      }

      .cf-org-inv__action--danger {
        background: #fef2f2;
        color: #991b1b;
      }

      .cf-org-inv__action--danger:hover {
        background: #fee2e2;
      }
    `,
  ],
})
export class OrgInvitationsComponent {
  protected readonly orgsFacade = inject(OrganizationsFacade);
  protected readonly contextFacade = inject(OrgContextFacade);
  protected readonly authFacade = inject(AuthFacade);

  readonly orgId = input.required<string>();

  readonly inviteEmail = signal('');
  readonly inviteRole = signal<OrgRole>('member');

  protected callerRole(): OrgRole {
    return this.contextFacade.activeOrg()?.role ?? 'member';
  }

  protected canInvite(role: OrgRole): boolean {
    return canInviteMembers(role);
  }

  protected canRevoke(role: OrgRole): boolean {
    return canRevokeInvitation(role);
  }

  inputValue(event: Event): string {
    return (event.target as HTMLInputElement).value;
  }

  selectValue(event: Event): OrgRole {
    return (event.target as HTMLSelectElement).value as OrgRole;
  }

  onSendInvite(): void {
    const email = this.inviteEmail().trim();
    if (!email) return;
    this.orgsFacade.invite(this.orgId(), email, this.inviteRole());
    this.inviteEmail.set('');
  }

  onAccept(invitationId: string): void {
    this.orgsFacade.acceptInvitation(this.orgId(), invitationId);
  }

  onRevoke(invitationId: string): void {
    this.orgsFacade.revokeInvitation(this.orgId(), invitationId);
  }
}
