/**
 * Main Docs page — composes the search, tree, and viewer components.
 * Injects DocsFacade only (no store or use-case access).
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DocsFacade } from '../application/facades/docs.facade';
import { DocsSearchComponent } from './search/docs-search.component';
import { DocsTreeComponent } from './tree/docs-tree.component';
import { DocViewerComponent } from './viewer/doc-viewer.component';

@Component({
  selector: 'cf-docs-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DocsSearchComponent, DocsTreeComponent, DocViewerComponent],
  template: `
    <div class="docs-layout">
      <aside class="docs-sidebar">
        <cf-docs-search (docSelected)="onDocSelected($event)" />
        <cf-docs-tree (docSelected)="onDocSelected($event)" />
      </aside>
      <main class="docs-content">
        <cf-doc-viewer />
      </main>
    </div>
  `,
})
export class DocsPageComponent {
  private readonly facade = inject(DocsFacade);

  onDocSelected(_slug: string): void {
    // Doc is already opened by child components via facade
  }
}
