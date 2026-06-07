/**
 * Docs sidebar category tree component.
 * Displays docs grouped by category and emits when a doc is selected.
 */

import { ChangeDetectionStrategy, Component, inject, output } from '@angular/core';
import { DocsFacade } from '../../application/facades/docs.facade';
import type { DocCategoryNode, DocSearchResult } from '../../domain/models/docs.models';

@Component({
  selector: 'cf-docs-tree',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (node of facade.categoryTree(); track node.category) {
      <div class="category-node">
        <h3 class="category-heading">{{ node.category }}</h3>
        <ul>
          @for (doc of node.docs; track doc.slug) {
            <li>
              <button type="button" [attr.data-testid]="'doc-link-' + doc.slug" (click)="selectDoc(doc.slug)">
                {{ doc.title }}
              </button>
            </li>
          }
        </ul>
      </div>
    }
  `,
})
export class DocsTreeComponent {
  protected readonly facade = inject(DocsFacade);

  readonly docSelected = output<string>();

  selectDoc(slug: string): void {
    this.facade.openDoc(slug);
    this.docSelected.emit(slug);
  }

  // Needed for type narrowing in template
  protected trackByCategory(_index: number, node: DocCategoryNode): string {
    return node.category;
  }

  protected trackBySlug(_index: number, doc: DocSearchResult): string {
    return doc.slug;
  }
}
