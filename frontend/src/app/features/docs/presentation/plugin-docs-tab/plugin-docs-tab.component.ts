/**
 * Plugin "Docs" tab component.
 * Shows the documentation for a specific plugin.
 * Calls facade.openPluginDoc when pluginSlug input changes.
 */

import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { provideTranslocoScope } from '@jsverse/transloco';
import { DocsFacade } from '../../application/facades/docs.facade';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-plugin-docs-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  providers: [provideTranslocoScope('docs')],
  template: `
    @if (facade.isLoadingDoc()) {
      <div aria-busy="true" data-testid="loading" class="loading">{{ i18n.t('docs.loading-plugin-doc') }}</div>
    } @else if (facade.docError(); as errors) {
      <div role="alert" data-testid="error-message" class="error">
        {{ pluginErrorMessage(errors) }}
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
export class PluginDocsTabComponent {
  protected readonly facade = inject(DocsFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly pluginSlug = input<string>();

  constructor() {
    effect(() => {
      const slug = this.pluginSlug();
      if (slug) {
        this.facade.openPluginDoc(slug);
      }
    });
  }

  protected pluginErrorMessage(errors: { code: string; message: string }[]): string {
    return errors[0]?.message ?? this.i18n.t('docs.plugin-doc-error');
  }
}
