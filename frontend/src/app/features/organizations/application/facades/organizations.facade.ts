/**
 * OrganizationsFacade — the ONLY entry point for org management in components.
 *
 * Components call ONLY these methods — no direct store/port access.
 * All state updates use immutable spread (no mutation).
 */

import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { OrgPort } from '../../domain/ports/org.port';
import type { OrgInvitation, OrgMember, OrgRole, OrgSummary } from '../../domain/models/organizations.models';
import { OrganizationsStore, OrganizationsStoreEnum } from '../store/organizations.store';

@Injectable()
export class OrganizationsFacade {
  private readonly store = inject(OrganizationsStore);
  private readonly orgPort = inject(OrgPort);
  private readonly destroyRef = inject(DestroyRef);

  // ---------------------------------------------------------------------------
  // Signal getters (derived from store)
  // ---------------------------------------------------------------------------

  get organizations(): Signal<OrgSummary[]> {
    return computed(
      () => this.store.get(OrganizationsStoreEnum.ORGANIZATIONS)().data?.organizations ?? [],
    );
  }

  get members(): Signal<OrgMember[]> {
    return computed(
      () => this.store.get(OrganizationsStoreEnum.MEMBERS)().data?.members ?? [],
    );
  }

  get invitations(): Signal<OrgInvitation[]> {
    return computed(
      () => this.store.get(OrganizationsStoreEnum.INVITATIONS)().data?.invitations ?? [],
    );
  }

  get isLoadingOrgs(): Signal<boolean> {
    return computed(
      () => this.store.get(OrganizationsStoreEnum.ORGANIZATIONS)().isLoading ?? false,
    );
  }

  get isLoadingMembers(): Signal<boolean> {
    return computed(
      () => this.store.get(OrganizationsStoreEnum.MEMBERS)().isLoading ?? false,
    );
  }

  get orgsError(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(OrganizationsStoreEnum.ORGANIZATIONS)().errors);
  }

  // ---------------------------------------------------------------------------
  // Methods
  // ---------------------------------------------------------------------------

  createOrg(name: string, slug: string): void {
    this.store.startLoading(OrganizationsStoreEnum.ORGANIZATIONS);

    this.orgPort
      .createOrganization(name, slug)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'CREATE_ERROR', message }],
          });
        },
      });
  }

  listMembers(orgId: string): void {
    this.store.startLoading(OrganizationsStoreEnum.MEMBERS);

    this.orgPort
      .listMembers(orgId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (members) => {
          this.store.update(OrganizationsStoreEnum.MEMBERS, {
            data: { orgId, members },
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(OrganizationsStoreEnum.MEMBERS, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LIST_MEMBERS_ERROR', message }],
          });
        },
      });
  }

  invite(orgId: string, email: string, role: OrgRole): void {
    this.orgPort
      .invite(orgId, email, role)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            status: 'Error',
            errors: [{ code: 'INVITE_ERROR', message }],
          });
        },
      });
  }

  acceptInvitation(orgId: string, invitationId: string): void {
    this.orgPort
      .acceptInvitation(orgId, invitationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            status: 'Error',
            errors: [{ code: 'ACCEPT_INVITATION_ERROR', message }],
          });
        },
      });
  }

  revokeInvitation(orgId: string, invitationId: string): void {
    this.orgPort
      .revokeInvitation(orgId, invitationId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            status: 'Error',
            errors: [{ code: 'REVOKE_INVITATION_ERROR', message }],
          });
        },
      });
  }

  removeMember(orgId: string, userId: string): void {
    this.orgPort
      .removeMember(orgId, userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            status: 'Error',
            errors: [{ code: 'REMOVE_MEMBER_ERROR', message }],
          });
        },
      });
  }

  changeMemberRole(orgId: string, userId: string, role: OrgRole): void {
    this.orgPort
      .changeMemberRole(orgId, userId, role)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(OrganizationsStoreEnum.ORGANIZATIONS, {
            status: 'Error',
            errors: [{ code: 'CHANGE_ROLE_ERROR', message }],
          });
        },
      });
  }
}
