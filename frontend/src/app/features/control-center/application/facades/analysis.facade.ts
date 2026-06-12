import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlCenterStore, ControlCenterStoreEnum } from '../store/control-center.store';
import { ControlCenterPort } from '../../domain/ports/control-center.port';

@Injectable()
export class AnalysisFacade {
  private readonly store = inject(ControlCenterStore);
  private readonly port = inject(ControlCenterPort);
  private readonly destroyRef = inject(DestroyRef);

  get queueStatus(): Signal<{ queued: number; processing: number }> {
    return computed(() => {
      const m = this.store.get(ControlCenterStoreEnum.METRICS)().data;
      return {
        queued: m?.queue.queuedCount ?? 0,
        processing: m?.queue.processingCount ?? 0,
      };
    });
  }

  get isLoading(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.METRICS)().isLoading ?? false);
  }

  loadQueue(): void {
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
