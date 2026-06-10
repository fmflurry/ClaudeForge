import { ChangeDetectionStrategy, Component, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OrganizationsFacade } from '../../../application/facades/organizations.facade';

@Component({
  selector: 'cf-org-detail',
  standalone: true,
  providers: [OrganizationsFacade],
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Organization Detail</h1>
    <p><a routerLink="/control-center/organizations">Back to Organizations</a></p>
    @if (facade.selectedOrg(); as org) {
      <div class="detail-card">
        <h2>{{ org.name }}</h2>
        <p><strong>Slug:</strong> {{ org.slug }}</p>
      </div>

      <h2>Members</h2>
      @if (facade.isLoadingMembers()) {
        <p>Loading members...</p>
      } @else {
        <table class="table">
          <thead><tr><th>User ID</th><th>Role</th></tr></thead>
          <tbody>
            @for (m of facade.orgMembers(); track m.userId) {
              <tr><td>{{ m.userId }}</td><td>{{ m.role }}</td></tr>
            } @empty {
              <tr><td colspan="2">No members</td></tr>
            }
          </tbody>
        </table>
      }

      <div class="invite-section">
        <h3>Invite Member</h3>
        <input placeholder="Email" [value]="inviteEmail()" (input)="inviteEmail.set($any($event.target).value)" />
        <input placeholder="Role" [value]="inviteRole()" (input)="inviteRole.set($any($event.target).value)" />
        <button (click)="invite(org.id)">Invite</button>
      </div>
    }
  `,
  styles: [`
    .detail-card { background: var(--card); border: 1px solid var(--border); border-radius: 0.5rem; padding: 1.25rem; margin-bottom: 1rem; }
    .table { width: 100%; border-collapse: collapse; }
    .table th, .table td { text-align: left; padding: 0.5rem; border-bottom: 1px solid var(--border); }
    .invite-section { margin-top: 2rem; display: flex; gap: 0.5rem; align-items: center; }
    input { padding: 0.5rem; border: 1px solid var(--border); border-radius: 0.25rem; }
    button { padding: 0.5rem 1rem; border: 1px solid var(--border); border-radius: 0.25rem; cursor: pointer; background: var(--primary); color: var(--primary-foreground); }
  `],
})
export class OrgDetailComponent implements OnInit {
  readonly facade = inject(OrganizationsFacade);
  readonly orgId = input.required<string>();
  readonly inviteEmail = signal('');
  readonly inviteRole = signal('member');

  ngOnInit(): void {
    this.facade.selectOrg(this.orgId());
  }

  invite(orgId: string): void {
    if (this.inviteEmail()) {
      this.facade.inviteMember(orgId, this.inviteEmail(), this.inviteRole());
      this.inviteEmail.set('');
    }
  }
}
