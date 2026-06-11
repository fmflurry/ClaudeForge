import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CatalogFacade } from '../application/facades/catalog.facade';
import { AddOnListComponent } from './list/plugin-list.component';
import { AddOnDetailComponent } from './detail/plugin-detail.component';

@Component({
  selector: 'cf-catalog-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [AddOnListComponent, AddOnDetailComponent],
  template: `
    @if (showDetail()) {
      <cf-addon-detail (backRequested)="onBack()" />
    } @else {
      <cf-addon-list (addOnSelected)="onAddOnSelected($event)" />
    }
  `,
})
export class CatalogPageComponent implements OnInit {
  private readonly facade = inject(CatalogFacade);

  readonly showDetail = signal(false);

  ngOnInit(): void {
    this.facade.loadAddOns();
    this.facade.loadCategories();
  }

  onAddOnSelected(_pluginId: string): void {
    this.showDetail.set(true);
  }

  onBack(): void {
    this.showDetail.set(false);
  }
}
