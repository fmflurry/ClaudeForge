/**
 * Add-on "Docs" tab component.
 * Shows the documentation for a specific add-on.
 * Calls facade.openPluginDoc when addOnSlug input changes.
 */

import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { DocsFacade } from '../../application/facades/docs.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-addon-docs-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  template: `
    @if (facade.isLoadingDoc()) {
      <div aria-busy="true" data-testid="loading" class="loading">{{ i18n.t('docs.loading-addon-doc') }}</div>
    } @else if (facade.docError(); as errors) {
      <div role="alert" data-testid="error-message" class="error">
        {{ addOnErrorMessage(errors) }}
      </div>
    } @else if (facade.currentDoc(); as doc) {
      @if (doc.contentMarkdown) {
        <article>
          <header>
            <h2>{{ doc.title }}</h2>
            <time>{{ doc.lastUpdated | date: 'mediumDate' }}</time>
          </header>
          <pre data-testid="doc-content">{{ doc.contentMarkdown }}</pre>
        </article>
      } @else {
        <div data-testid="missing-doc">
          <p>{{ i18n.t('docs.no-documentation') }}</p>
        </div>
      }
    } @else {
      <div data-testid="missing-doc">
        <p>{{ i18n.t('docs.no-documentation') }}</p>
      </div>
    }
  `,
})
export class AddOnDocsTabComponent {
  protected readonly facade = inject(DocsFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly addOnSlug = input<string>();

  constructor() {
    effect(() => {
      const slug = this.addOnSlug();
      if (slug) {
        this.facade.openPluginDoc(slug);
      }
    });
  }

  protected addOnErrorMessage(errors: { code: string; message: string }[]): string {
    return errors[0]?.message ?? this.i18n.t('docs.addon-doc-error');
  }
}
