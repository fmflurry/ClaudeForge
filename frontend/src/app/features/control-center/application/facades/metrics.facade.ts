import { computed, DestroyRef, inject, Injectable, Signal, signal, WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlCenterStore, ControlCenterStoreEnum } from '../store/control-center.store';
import { ControlCenterPort } from '../../domain/ports/control-center.port';

@Injectable()
export class MetricsFacade {
  private readonly store = inject(ControlCenterStore);
  private readonly port = inject(ControlCenterPort);
  private readonly destroyRef = inject(DestroyRef);

  readonly dateRange: WritableSignal<'7d' | '30d' | '90d'> = signal('7d');

  get analysisMetrics(): Signal<{
    totalAnalyzed: number;
    totalPassed: number;
    totalFailed: number;
    totalInReview: number;
  }> {
    return computed(() => {
      const m = this.store.get(ControlCenterStoreEnum.METRICS)().data;
      return {
        totalAnalyzed: m?.overview.totalAnalyzed ?? 0,
        totalPassed: m?.overview.totalPassed ?? 0,
        totalFailed: m?.overview.totalFailed ?? 0,
        totalInReview: m?.overview.totalInReview ?? 0,
      };
    });
  }

  get securityMetrics(): Signal<readonly { finding: string; count: number }[]> {
    return computed(() => this.store.get(ControlCenterStoreEnum.METRICS)().data?.topFindings ?? []);
  }

  get appealMetrics(): Signal<{ pendingAppeals: number; avgResolutionTimeHours: number }> {
    return computed(() => {
      const m = this.store.get(ControlCenterStoreEnum.METRICS)().data;
      return {
        pendingAppeals: m?.appeals.pendingAppeals ?? 0,
        avgResolutionTimeHours: m?.appeals.avgResolutionTimeHours ?? 0,
      };
    });
  }

  get isLoading(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.METRICS)().isLoading ?? false);
  }

  loadMetrics(): void {
    this.port
      .getMetrics()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (metrics) => {
          this.store.update(ControlCenterStoreEnum.METRICS, {
            data: metrics,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.METRICS, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }
}
