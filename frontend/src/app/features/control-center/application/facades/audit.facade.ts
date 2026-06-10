import { computed, DestroyRef, inject, Injectable, Signal, signal, WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ControlCenterStore, ControlCenterStoreEnum } from '../store/control-center.store';
import { ControlCenterPort } from '../../domain/ports/control-center.port';
import type { AuditLogEntry, AuditLogFilter } from '../../domain/models/control-center.models';

@Injectable()
export class AuditFacade {
  private readonly store = inject(ControlCenterStore);
  private readonly port = inject(ControlCenterPort);
  private readonly destroyRef = inject(DestroyRef);

  readonly filters: WritableSignal<AuditLogFilter> = signal({});

  get logs(): Signal<AuditLogEntry[]> {
    return computed(() => this.store.get(ControlCenterStoreEnum.AUDIT_LOGS)().data ?? []);
  }

  get isLoading(): Signal<boolean> {
    return computed(() => this.store.get(ControlCenterStoreEnum.AUDIT_LOGS)().isLoading ?? false);
  }

  get error(): Signal<{ code: string; message: string }[] | undefined> {
    return computed(() => this.store.get(ControlCenterStoreEnum.AUDIT_LOGS)().errors);
  }

  loadLogs(filter?: AuditLogFilter): void {
    const mergedFilter = { ...this.filters(), ...filter };
    this.filters.set(mergedFilter);
    this.store.startLoading(ControlCenterStoreEnum.AUDIT_LOGS);
    this.port.getAuditLogs(mergedFilter).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (response) => {
        this.store.update(ControlCenterStoreEnum.AUDIT_LOGS, {
          data: [...response.items],
          status: 'Success',
          isLoading: false,
          errors: undefined,
        });
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Unknown error';
        this.store.update(ControlCenterStoreEnum.AUDIT_LOGS, {
          status: 'Error',
          isLoading: false,
          errors: [{ code: 'LOAD_ERROR', message }],
        });
      },
    });
  }

  setFilters(filter: Partial<AuditLogFilter>): void {
    this.filters.update((prev) => ({ ...prev, ...filter }));
    this.loadLogs();
  }

  exportLogs(format: 'csv' | 'json'): void {
    const data = this.logs();
    if (data.length === 0) return;

    if (format === 'json') {
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
      this.downloadBlob(blob, 'audit-logs.json');
    } else {
      const headers = 'timestamp,eventType,description,actorId';
      const rows = data.map((l) => `"${l.timestamp}","${l.eventType}","${l.description}","${l.actorId ?? ''}"`);
      const csv = [headers, ...rows].join('\n');
      const blob = new Blob([csv], { type: 'text/csv' });
      this.downloadBlob(blob, 'audit-logs.csv');
    }
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }
}
