import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CatalogFacade } from '../application/facades/catalog.facade';
import { PluginListComponent } from './list/plugin-list.component';
import { PluginDetailComponent } from './detail/plugin-detail.component';

@Component({
  selector: 'cf-catalog-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PluginListComponent, PluginDetailComponent],
  template: `
    @if (showDetail()) {
      <cf-plugin-detail (backRequested)="onBack()" />
    } @else {
      <cf-plugin-list (pluginSelected)="onPluginSelected($event)" />
    }
  `,
})
export class CatalogPageComponent implements OnInit {
  private readonly facade = inject(CatalogFacade);

  readonly showDetail = signal(false);

  ngOnInit(): void {
    this.facade.loadPlugins();
    this.facade.loadCategories();
  }

  onPluginSelected(_pluginId: string): void {
    this.showDetail.set(true);
  }

  onBack(): void {
    this.showDetail.set(false);
  }
}
