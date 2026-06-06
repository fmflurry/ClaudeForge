import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cf-search-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h2>Search</h2>`,
})
export class SearchPageComponent {}
