/**
 * OrgSwitcherComponent — displays the current org and allows switching.
 * Uses OrgContextFacade only (no direct store/port access).
 * Standalone component with @if/@for (no CommonModule).
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { OrgContextFacade } from '../../application/facades/org-context.facade';
import { AuthFacade } from '../../../auth/application/facades/auth.facade';

@Component({
  selector: 'cf-org-switcher',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (authFacade.isAuthenticated()) {
      <div class="cf-org-switcher">
        @if (contextFacade.activeOrg(); as org) {
          <span class="cf-org-switcher__current" aria-label="Current organisation">
            {{ org.name }}
          </span>
          @if (contextFacade.organizations().length > 1) {
            <select
              class="cf-org-switcher__select"
              aria-label="Switch organisation"
              [value]="org.orgId"
              (change)="onOrgChange($event)"
            >
              @for (o of contextFacade.organizations(); track o.orgId) {
                <option [value]="o.orgId">{{ o.name }}</option>
              }
            </select>
          }
        } @else {
          <span class="cf-org-switcher__none">No organisation</span>
        }
      </div>
    }
  `,
  styles: [
    `
      .cf-org-switcher {
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }

      .cf-org-switcher__current {
        font-size: 0.875rem;
        font-weight: 600;
        color: rgba(255, 255, 255, 0.9);
      }

      .cf-org-switcher__none {
        font-size: 0.875rem;
        color: rgba(255, 255, 255, 0.5);
      }

      .cf-org-switcher__select {
        background: rgba(255, 255, 255, 0.1);
        border: 1px solid rgba(255, 255, 255, 0.25);
        color: #fff;
        border-radius: 0.25rem;
        padding: 0.2rem 0.5rem;
        font-size: 0.75rem;
        cursor: pointer;
      }
    `,
  ],
})
export class OrgSwitcherComponent {
  protected readonly contextFacade = inject(OrgContextFacade);
  protected readonly authFacade = inject(AuthFacade);

  onOrgChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.contextFacade.setActiveOrg(select.value);
  }
}
