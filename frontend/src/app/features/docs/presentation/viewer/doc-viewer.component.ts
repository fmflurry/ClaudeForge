/**
 * Doc viewer component.
 * Renders contentMarkdown as preformatted text (MVP — no heavy markdown lib).
 * Shows a placeholder when no doc is loaded or contentMarkdown is empty.
 */

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { DocsFacade } from '../../application/facades/docs.facade';

@Component({
  selector: 'cf-doc-viewer',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe],
  template: `
    @if (facade.isLoadingDoc()) {
      <div aria-busy="true" data-testid="loading" class="loading">Loading documentation…</div>
    } @else if (facade.docError(); as errors) {
      <div role="alert" data-testid="error-message" class="error">
        {{ errorMessage(errors) }}
      </div>
    } @else if (facade.currentDoc(); as doc) {
      @if (doc.contentMarkdown) {
        <article>
          <header>
            <h1>{{ doc.title }}</h1>
            <time>{{ doc.lastUpdated | date: 'mediumDate' }}</time>
          </header>
          <pre data-testid="markdown-content">{{ doc.contentMarkdown }}</pre>
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
export class DocViewerComponent {
  protected readonly facade = inject(DocsFacade);

  protected errorMessage(errors: { code: string; message: string }[]): string {
    return errors[0]?.message ?? 'An error occurred loading the documentation.';
  }
}
