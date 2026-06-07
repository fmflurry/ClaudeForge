/**
 * Plugin "Docs" tab component.
 * Shows the documentation for a specific plugin.
 * Calls facade.openPluginDoc when pluginSlug input changes.
 */

import { ChangeDetectionStrategy, Component, effect, inject, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { DocsFacade } from '../../application/facades/docs.facade';

@Component({
  selector: 'cf-plugin-docs-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  template: `
    @if (facade.isLoadingDoc()) {
      <div aria-busy="true" data-testid="loading" class="loading">Loading plugin documentation…</div>
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
          <p>No documentation available</p>
        </div>
      }
    } @else {
      <div data-testid="missing-doc">
        <p>No documentation available</p>
      </div>
    }
  `,
})
export class PluginDocsTabComponent {
  protected readonly facade = inject(DocsFacade);

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
    return errors[0]?.message ?? 'Failed to load plugin documentation.';
  }
}
