import { ChangeDetectionStrategy, Component, computed, inject, output, Signal } from '@angular/core';
import { provideTranslocoScope } from '@jsverse/transloco';
import { CatalogFacade } from '../../application/facades/catalog.facade';
import type { PluginDetail } from '../../domain/models/catalog.models';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-plugin-detail',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [],
  providers: [provideTranslocoScope('catalog')],
  template: `
    <div class="cf-plugin-detail">
      <button
        [attr.aria-label]="i18n.t('catalog.back-button-aria')"
        data-testid="back-button"
        (click)="onBack()"
        class="cf-plugin-detail__back"
      >
        &larr; {{ i18n.t('catalog.back-button') }}
      </button>

      @if (isLoading()) {
        <div aria-busy="true" data-testid="loading" class="loading">{{ i18n.t('catalog.loading-plugin') }}</div>
      }

      @if (!isLoading() && hasError()) {
        <div role="alert" class="error" data-testid="error-message">
          {{ i18n.t('catalog.error-plugin') }}
        </div>
      }

      @if (!isLoading() && !hasError() && plugin()) {
        <article class="cf-plugin-detail__content">
          <h2 class="cf-plugin-detail__name">{{ plugin()!.name }}</h2>
          <p class="cf-plugin-detail__description">{{ plugin()!.description }}</p>

          <dl class="cf-plugin-detail__meta">
            <dt>{{ i18n.t('catalog.meta-author') }}</dt>
            <dd>{{ plugin()!.author }}</dd>

            <dt>{{ i18n.t('catalog.meta-latest-version') }}</dt>
            <dd>{{ plugin()!.latestVersion }}</dd>

            <dt>{{ i18n.t('catalog.meta-downloads') }}</dt>
            <dd>{{ plugin()!.downloadCount }}</dd>
          </dl>

          <div class="cf-plugin-detail__tags">
            <h3>{{ i18n.t('catalog.types-heading') }}</h3>
            @for (type of plugin()!.types; track type) {
              <span class="cf-badge">{{ type }}</span>
            }
          </div>

          <div class="cf-plugin-detail__languages">
            <h3>{{ i18n.t('catalog.languages-heading') }}</h3>
            @for (lang of plugin()!.languages; track lang) {
              <span class="cf-badge">{{ lang }}</span>
            }
          </div>

          <section class="cf-plugin-detail__versions">
            <h3>{{ i18n.t('catalog.version-history-heading') }}</h3>
            <table class="cf-versions-table">
              <thead>
                <tr>
                  <th scope="col">{{ i18n.t('catalog.version-col') }}</th>
                  <th scope="col">{{ i18n.t('catalog.status-col') }}</th>
                  <th scope="col">{{ i18n.t('catalog.downloads-col') }}</th>
                  <th scope="col">{{ i18n.t('catalog.release-notes-col') }}</th>
                </tr>
              </thead>
              <tbody>
                @for (v of plugin()!.versions; track v.version) {
                  <tr>
                    <td>{{ v.version }}</td>
                    <td>{{ v.isLatest ? i18n.t('catalog.version-latest') : '' }}</td>
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
  protected readonly i18n = inject(I18nFacade);

  readonly plugin: Signal<PluginDetail | undefined> = this.facade.selectedPlugin;
  readonly isLoading: Signal<boolean> = this.facade.isLoadingDetail;
  readonly hasError: Signal<boolean> = computed(() => this.facade.detailError() !== undefined);

  readonly backRequested = output<void>();

  onBack(): void {
    this.backRequested.emit();
  }
}
