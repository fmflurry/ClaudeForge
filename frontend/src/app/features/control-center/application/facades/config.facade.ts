import { computed, DestroyRef, inject, Injectable, Signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlCenterStore, ControlCenterStoreEnum } from '../store/control-center.store';
import { ControlCenterPort } from '../../domain/ports/control-center.port';
import type { AnalysisConfig, ConfigChangeLog } from '../../domain/models/control-center.models';

@Injectable()
export class ConfigFacade {
  private readonly store = inject(ControlCenterStore);
  private readonly port = inject(ControlCenterPort);
  private readonly destroyRef = inject(DestroyRef);

  get analysisConfig(): Signal<AnalysisConfig | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.CONFIG)().data);
  }

  get configHistory(): Signal<ConfigChangeLog[]> {
    return computed(() => this.store.get(ControlCenterStoreEnum.CONFIG_HISTORY)().data ?? []);
  }

  get isSaving(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.CONFIG)().isLoading ?? false);
  }

  get error(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.CONFIG)().errors);
  }

  loadConfig(): void {
    this.store.startLoading(ControlCenterStoreEnum.CONFIG);
    this.port
      .getAnalysisConfig()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (config) => {
          this.store.update(ControlCenterStoreEnum.CONFIG, {
            data: config,
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.CONFIG, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }

  updateConfig(config: Partial<AnalysisConfig>): void {
    this.store.startLoading(ControlCenterStoreEnum.CONFIG);
    this.port
      .updateAnalysisConfig(config)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.store.update(ControlCenterStoreEnum.CONFIG, {
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
          this.loadConfig();
          this.loadHistory();
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.CONFIG, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'UPDATE_ERROR', message }],
          });
        },
      });
  }

  loadHistory(): void {
    this.store.startLoading(ControlCenterStoreEnum.CONFIG_HISTORY);
    this.port
      .getConfigHistory()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.store.update(ControlCenterStoreEnum.CONFIG_HISTORY, {
            data: [...response.items],
            status: 'Success',
            isLoading: false,
            errors: undefined,
          });
        },
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Unknown error';
          this.store.update(ControlCenterStoreEnum.CONFIG_HISTORY, {
            status: 'Error',
            isLoading: false,
            errors: [{ code: 'LOAD_ERROR', message }],
          });
        },
      });
  }
}
