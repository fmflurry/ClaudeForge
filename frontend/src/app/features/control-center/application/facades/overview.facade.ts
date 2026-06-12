import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval, switchMap } from 'rxjs';
import { ControlCenterPort } from '../../domain/ports/control-center.port';
import { ControlCenterStore, ControlCenterStoreEnum } from '../store/control-center.store';
import type { ControlCenterMetrics } from '../../domain/models/control-center.models';

@Injectable()
export class OverviewFacade {
  private readonly store = inject(ControlCenterStore);
  private readonly port = inject(ControlCenterPort);
  private readonly destroyRef = inject(DestroyRef);

  get metrics(): Signal<ControlCenterMetrics | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.METRICS)().data);
  }

  get totalAnalyzed(): Signal<number> {
    return computed(() => this.metrics()?.overview.totalAnalyzed ?? 0);
  }

  get passRate(): Signal<number> {
    return computed(() => {
      const m = this.metrics();
      if (!m || m.overview.totalAnalyzed === 0) return 0;
      return Math.round((m.overview.totalPassed / m.overview.totalAnalyzed) * 100);
    });
  }

  get queueLength(): Signal<number> {
    return computed(() => {
      const m = this.metrics();
      return m ? m.queue.queuedCount + m.queue.processingCount : 0;
    });
  }

  get pendingAppeals(): Signal<number> {
    return computed(() => this.metrics()?.appeals.pendingAppeals ?? 0);
  }

  get recentAnalyses(): Signal<number> {
    return computed(() => this.metrics()?.recentAnalyses ?? 0);
  }

  get topFindings(): Signal<readonly { finding: string; count: number }[]> {
    return computed(() => this.metrics()?.topFindings ?? []);
  }

  get isLoading(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.METRICS)().isLoading ?? false);
  }

  get error(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.METRICS)().errors);
  }

  private startPolling(): void {
    interval(30000)
      .pipe(
        switchMap(() => this.port.getMetrics()),
        takeUntilDestroyed(this.destroyRef),
      )
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
          const message = err instanceof Error ? err.message : 'Polling failed';
          this.store.update(ControlCenterStoreEnum.METRICS, {
            status: 'Error',
            errors: [{ code: 'POLL_ERROR', message }],
          });
        },
      });
  }

  loadMetrics(): void {
    this.store.startLoading(ControlCenterStoreEnum.METRICS);
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
          this.startPolling();
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
