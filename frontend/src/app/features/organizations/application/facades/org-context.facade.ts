/**
 * OrgContextFacade — manages the currently active organisation.
 *
 * Cross-domain communication: setActiveOrg() publishes 'org:active-org-switched'
 * via the module-level contextRegistry singleton — NO direct cross-domain DI.
 *
 * Allowed injections: OrganizationsStore, OrgPort, DestroyRef only.
 */

import { computed, DestroyRef, inject, Injectable, Signal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { contextRegistry } from '../../../../core/context/context-registry';
import { OrganizationsStore, OrganizationsStoreEnum } from '../store/organizations.store';
import { OrgPort } from '../../domain/ports/org.port';
import type { OrgSummary } from '../../domain/models/organizations.models';

// ---------------------------------------------------------------------------
// Event constant & payload type
// ---------------------------------------------------------------------------

export const ORG_ACTIVE_ORG_SWITCHED = 'org:active-org-switched';

export interface ActiveOrgSwitchedPayload {
  readonly orgId: string;
}

// ---------------------------------------------------------------------------
// OrgContextFacade
// ---------------------------------------------------------------------------

@Injectable()
export class OrgContextFacade {
  private readonly store = inject(OrganizationsStore);
  private readonly port = inject(OrgPort);
  private readonly destroyRef = inject(DestroyRef);

  /** Internal writable signal for the active org id. */
  private readonly _activeOrgId = signal<string | undefined>(undefined);

  // ---------------------------------------------------------------------------
  // Signal getters
  // ---------------------------------------------------------------------------

  get organizations(): Signal<OrgSummary[]> {
    return computed(
      () => this.store.get(OrganizationsStoreEnum.ORGANIZATIONS)().data?.organizations ?? [],
    );
  }

  get activeOrg(): Signal<OrgSummary | undefined> {
    return computed(() => {
      const id = this._activeOrgId();
      if (id === undefined) return undefined;
      return this.organizations().find((o) => o.orgId === id);
    });
  }

  get activeOrgId(): Signal<string | undefined> {
    return this._activeOrgId.asReadonly();
  }

  // ---------------------------------------------------------------------------
  // Methods
  // ---------------------------------------------------------------------------

  /**
   * Updates the active org and publishes 'org:active-org-switched' via contextRegistry.
   * Does NOT publish when called internally from loadOrganizations().
   */
  setActiveOrg(orgId: string): void {
    this._activeOrgId.set(orgId);
    contextRegistry.publish<ActiveOrgSwitchedPayload>(ORG_ACTIVE_ORG_SWITCHED, { orgId });
  }

  /**
   * Calls OrgPort.listOrganizations(), stores the result, and sets the default
   * active org to the first org if none is currently active.
   * Does NOT publish the org-switched event (internal default selection is not
   * a user-initiated switch).
   */
  loadOrganizations(): void {
    this.store.startLoading(OrganizationsStoreEnum.ORGANIZATIONS);

    this.port
      .listOrganizations()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (orgs) => {
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            data: { organizations: orgs },
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });

          // Set default active org to first if none is currently active
          const currentId = this._activeOrgId();
          const stillPresent =
            currentId !== undefined && orgs.some((o) => o.orgId === currentId);

          if (!stillPresent && orgs.length > 0) {
            // Internal default selection — do NOT publish switch event
            this._activeOrgId.set(orgs[0].orgId);
          }
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }
}
