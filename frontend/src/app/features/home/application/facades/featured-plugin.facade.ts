import { DestroyRef, inject, Injectable, Signal, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FeaturedAddOnPort } from '../../domain/ports/featured-plugin.port';
import type { FeaturedAddOn } from '../../domain/models/featured-plugin.model';

@Injectable()
export class FeaturedAddOnFacade {
  private readonly port = inject(FeaturedAddOnPort);
  private readonly destroyRef = inject(DestroyRef);

  private readonly _featuredAddOn = signal<FeaturedAddOn | null>(null);
  private readonly _isLoading = signal<boolean>(false);

  /** The currently featured add-on, or null when none is featured / fetch failed. */
  readonly featuredAddOn: Signal<FeaturedAddOn | null> = this._featuredAddOn.asReadonly();

  /** True while the fetch is in-flight. */
  readonly isLoading: Signal<boolean> = this._isLoading.asReadonly();

  load(): void {
    this._isLoading.set(true);
    this.port
      .getFeaturedAddOn()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (addOn) => {
          this._featuredAddOn.set(addOn);
          this._isLoading.set(false);
        },
        error: () => {
          // The adapter already maps errors to null; this branch is a safety net.
          this._featuredAddOn.set(null);
          this._isLoading.set(false);
        },
      });
  }
}
