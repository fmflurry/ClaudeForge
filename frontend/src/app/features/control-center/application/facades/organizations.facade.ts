import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlCenterStore, ControlCenterStoreEnum } from '../store/control-center.store';
import { ControlCenterPort } from '../../domain/ports/control-center.port';
import type { Organization, OrgMember } from '../../domain/models/control-center.models';

@Injectable()
export class OrganizationsFacade {
  private readonly store = inject(ControlCenterStore);
  private readonly port = inject(ControlCenterPort);
  private readonly destroyRef = inject(DestroyRef);

  get organizations(): Signal<Organization[]> {
    return computed(() => this.store.get(ControlCenterStoreEnum.ORGANIZATIONS)().data ?? []);
  }

  get selectedOrg(): Signal<Organization | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.ORG_DETAIL)().data);
  }

  get orgMembers(): Signal<OrgMember[]> {
    return computed(() => this.store.get(ControlCenterStoreEnum.ORG_MEMBERS)().data ?? []);
  }

  get isLoadingOrgs(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.ORGANIZATIONS)().isLoading ?? false);
  }

  get isLoadingMembers(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.ORG_MEMBERS)().isLoading ?? false);
  }

  get error(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.ORGANIZATIONS)().errors);
  }

  loadOrganizations(): void {
    this.store.startLoading(ControlCenterStoreEnum.ORGANIZATIONS);
    this.port
      .getOrganizations()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (orgs) => {
          this.store.update(ControlCenterStoreEnum.ORGANIZATIONS, {
            data: orgs,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.ORGANIZATIONS, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }

  selectOrg(orgId: string): void {
    this.store.startLoading(ControlCenterStoreEnum.ORG_DETAIL);
    this.store.startLoading(ControlCenterStoreEnum.ORG_MEMBERS);
    this.port
      .getOrgDetail(orgId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (org) => {
          this.store.update(ControlCenterStoreEnum.ORG_DETAIL, {
            data: org,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.ORG_DETAIL, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
    this.port
      .getOrgMembers(orgId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (members) => {
          this.store.update(ControlCenterStoreEnum.ORG_MEMBERS, {
            data: members,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.ORG_MEMBERS, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }

  inviteMember(orgId: string, email: string, role: string): void {
    this.port
      .inviteMember(orgId, email, role)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.selectOrg(orgId);
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.ORG_MEMBERS, {
            status: 'Error',
            errors: [{ code: 'INVITE_ERROR', message }],
          });
        },
      });
  }

  removeMember(orgId: string, userId: string): void {
    this.port
      .removeMember(orgId, userId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.selectOrg(orgId);
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.ORG_MEMBERS, {
            status: 'Error',
            errors: [{ code: 'REMOVE_ERROR', message }],
          });
        },
      });
  }
}
