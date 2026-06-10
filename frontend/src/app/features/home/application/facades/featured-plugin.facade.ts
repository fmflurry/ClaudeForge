import { DestroyRef, inject, Injectable, Signal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FeaturedPluginPort } from '../../domain/ports/featured-plugin.port';
import type { FeaturedPlugin } from '../../domain/models/featured-plugin.model';

@Injectable()
export class FeaturedPluginFacade {
  private readonly port = inject(FeaturedPluginPort);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _featuredPlugin = signal<FeaturedPlugin | null>(null);
  private readonly _isLoading = signal<boolean>(false);

  /** The currently featured plugin, or null when none is featured / fetch failed. */
  readonly featuredPlugin: Signal<FeaturedPlugin | null> = this._featuredPlugin.asReadonly();

  /** True while the fetch is in-flight. */
  readonly isLoading: Signal<boolean> = this._isLoading.asReadonly();

  load(): void {
    this._isLoading.set(true);
    this.port
      .getFeaturedPlugin()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (plugin) => {
          this._featuredPlugin.set(plugin);
          this._isLoading.set(false);
        },
        error: () => {
          // The adapter already maps errors to null; this branch is a safety net.
          this._featuredPlugin.set(null);
          this._isLoading.set(false);
        },
      });
  }
}
