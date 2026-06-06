import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

export interface FilterChipsOutput {
  readonly types: readonly string[];
  readonly languages: readonly string[];
  readonly useCases: readonly string[];
}

@Component({
  selector: 'cf-filter-chips',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-filter-chips">
      @for (type of activeTypes(); track type) {
        <span class="cf-filter-chips__chip" data-testid="chip-type">{{ type }}</span>
      }
      @for (lang of activeLanguages(); track lang) {
        <span class="cf-filter-chips__chip" data-testid="chip-language">{{ lang }}</span>
      }
      @for (useCase of activeUseCases(); track useCase) {
        <span class="cf-filter-chips__chip" data-testid="chip-usecase">{{ useCase }}</span>
      }
    </div>
  `,
})
export class FilterChipsComponent {
  readonly activeTypes = input<readonly string[]>([]);
  readonly activeLanguages = input<readonly string[]>([]);
  readonly activeUseCases = input<readonly string[]>([]);

  readonly filtersChanged = output<FilterChipsOutput>();
}
