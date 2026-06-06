import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cf-catalog-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h2>Catalog</h2>`,
})
export class CatalogPageComponent {}
