import { DestroyRef, inject, Injectable, Signal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import type { MarketplaceMetrics } from '../../domain/models/marketplace-metrics.model';
import { MarketplaceStatsPort } from '../../domain/ports/marketplace-stats.port';

@Injectable()
export class HomeMetricsFacade {
  private readonly port = inject(MarketplaceStatsPort);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _isLoadingStats = signal<boolean>(true);
  private readonly _stats = signal<MarketplaceMetrics | null>(null);
  private readonly _statsError = signal<string | undefined>(undefined);

  readonly isLoadingStats: Signal<boolean> = this._isLoadingStats.asReadonly();
  readonly stats: Signal<MarketplaceMetrics | null> = this._stats.asReadonly();
  readonly statsError: Signal<string | undefined> = this._statsError.asReadonly();

  loadStats(): void {
    this._isLoadingStats.set(true);
    this._statsError.set(undefined);
    this.port
      .getStats()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (metrics) => {
          this._stats.set({ ...metrics });
          this._isLoadingStats.set(false);
        },
        error: (err: unknown) => {
          this._statsError.set(err instanceof Error ? err.message : 'Unknown error');
          this._isLoadingStats.set(false);
        },
      });
  }
}
