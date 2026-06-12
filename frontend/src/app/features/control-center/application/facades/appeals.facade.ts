import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlCenterStore, ControlCenterStoreEnum } from '../store/control-center.store';
import { ControlCenterPort } from '../../domain/ports/control-center.port';
import type { Appeal, AppealDetail, AppealFilter } from '../../domain/models/control-center.models';

@Injectable()
export class AppealsFacade {
  private readonly store = inject(ControlCenterStore);
  private readonly port = inject(ControlCenterPort);
  private readonly destroyRef = inject(DestroyRef);

  get pendingAppeals(): Signal<Appeal[]> {
    return computed(() => this.store.get(ControlCenterStoreEnum.APPEALS)().data ?? []);
  }

  get appealDetail(): Signal<AppealDetail | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.APPEAL_DETAIL)().data);
  }

  get approvalRate(): Signal<number> {
    return computed(() => {
      const appeals = this.pendingAppeals();
      if (appeals.length === 0) return 0;
      const approved = appeals.filter((a) => a.status === 'approved').length;
      return Math.round((approved / appeals.length) * 100);
    });
  }

  get isLoadingList(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.APPEALS)().isLoading ?? false);
  }

  get isLoadingDetail(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.APPEAL_DETAIL)().isLoading ?? false);
  }

  get error(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.APPEALS)().errors);
  }

  loadAppeals(filter: AppealFilter = {}): void {
    this.store.startLoading(ControlCenterStoreEnum.APPEALS);
    this.port
      .getAppeals(filter)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.store.update(ControlCenterStoreEnum.APPEALS, {
            data: response.items,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.APPEALS, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }

  loadAppealDetail(appealId: string): void {
    this.store.startLoading(ControlCenterStoreEnum.APPEAL_DETAIL);
    this.port
      .getAppealDetail(appealId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (detail) => {
          this.store.update(ControlCenterStoreEnum.APPEAL_DETAIL, {
            data: detail,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.APPEAL_DETAIL, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }

  resolveAppeal(appealId: string, resolution: string, notes?: string): void {
    this.port
      .resolveAppeal(appealId, resolution, notes)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.store.clear(ControlCenterStoreEnum.APPEAL_DETAIL);
          this.loadAppeals();
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.APPEAL_DETAIL, {
            status: 'Error',
            errors: [{ code: 'RESOLVE_ERROR', message }],
          });
        },
      });
  }
}
