import { ChangeDetectionStrategy, Component, computed, inject, output, Signal } from '@angular/core';
import { CatalogFacade } from '../../application/facades/catalog.facade';
import type { PluginDetail } from '../../domain/models/catalog.models';

@Component({
  selector: 'cf-plugin-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [],
  template: `
    <div class="cf-plugin-detail">
      <button aria-label="Back to list" data-testid="back-button" (click)="onBack()" class="cf-plugin-detail__back">
        &larr; Back
      </button>

      @if (isLoading()) {
        <div aria-busy="true" data-testid="loading" class="loading">Loading plugin…</div>
      }

      @if (!isLoading() && hasError()) {
        <div role="alert" class="error" data-testid="error-message">
          Failed to load plugin details. Please try again.
        </div>
      }

      @if (!isLoading() && !hasError() && plugin()) {
        <article class="cf-plugin-detail__content">
          <h2 class="cf-plugin-detail__name">{{ plugin()!.name }}</h2>
          <p class="cf-plugin-detail__description">{{ plugin()!.description }}</p>

          <dl class="cf-plugin-detail__meta">
            <dt>Author</dt>
            <dd>{{ plugin()!.author }}</dd>

            <dt>Latest Version</dt>
            <dd>{{ plugin()!.latestVersion }}</dd>

            <dt>Downloads</dt>
            <dd>{{ plugin()!.downloadCount }}</dd>
          </dl>

          <div class="cf-plugin-detail__tags">
            <h3>Types</h3>
            @for (type of plugin()!.types; track type) {
              <span class="cf-badge">{{ type }}</span>
            }
          </div>

          <div class="cf-plugin-detail__languages">
            <h3>Languages</h3>
            @for (lang of plugin()!.languages; track lang) {
              <span class="cf-badge">{{ lang }}</span>
            }
          </div>

          <section class="cf-plugin-detail__versions">
            <h3>Version History</h3>
            <table class="cf-versions-table">
              <thead>
                <tr>
                  <th scope="col">Version</th>
                  <th scope="col">Status</th>
                  <th scope="col">Downloads</th>
                  <th scope="col">Release Notes</th>
                </tr>
              </thead>
              <tbody>
                @for (v of plugin()!.versions; track v.version) {
                  <tr>
                    <td>{{ v.version }}</td>
                    <td>{{ v.isLatest ? 'latest' : '' }}</td>
                    <td>{{ v.downloadCount }}</td>
                    <td>{{ v.releaseNotes }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </section>
        </article>
      }
    </div>
  `,
})
export class PluginDetailComponent {
  private readonly facade = inject(CatalogFacade);

  readonly plugin: Signal<PluginDetail | undefined> = this.facade.selectedPlugin;
  readonly isLoading: Signal<boolean> = this.facade.isLoadingDetail;
  readonly hasError: Signal<boolean> = computed(() => this.facade.detailError() !== undefined);

  readonly backRequested = output<void>();

  onBack(): void {
    this.backRequested.emit();
  }
}
