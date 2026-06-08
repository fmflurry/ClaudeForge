import { Directive, inject, Injectable, input, signal, type Signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
class ZardIdInternalService {
  private counter = 0;
  generate(prefix: string): string {
    return `${prefix}-${++this.counter}`;
  }
}

@Directive({
  selector: '[zardId]',
  exportAs: 'zardId',
})
export class ZardIdDirective {
  private readonly idService = inject(ZardIdInternalService);

  readonly zardId = input('ssr');

  /**
   * A stable read-only signal holding the generated id.
   *
   * The id is generated once, eagerly in the constructor, from the
   * `zardId` input's initial (default) value.  Storing it as a
   * `signal` that is never updated means:
   *   - call-sites that use `id()` continue to work unchanged;
   *   - the id never changes after construction, preventing SSR
   *     hydration mismatches;
   *   - `generate()` is called exactly once per directive instance,
   *     keeping the counter deterministic.
   */
  readonly id: Signal<string>;

  constructor() {
    const prefix = this.zardId();
    this.id = signal(this.idService.generate(prefix)).asReadonly();
  }
}
