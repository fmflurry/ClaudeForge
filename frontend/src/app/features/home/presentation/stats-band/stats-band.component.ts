/**
 * Stats band component — GREEN implementation.
 *
 * Displays marketplace-wide metrics (totalPlugins, totalDownloads,
 * publisherCount, categoryCount) in four accessible metric cards.
 *
 * State machine:
 *  - LOADING  → role="status" aria-live="polite", no cards
 *  - ERROR    → role="alert" + retry button, no cards
 *  - NULL     → no cards (initial / unloaded state)
 *  - SUCCESS  → four [data-testid="stat-card"] article elements
 */

import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { HomeMetricsFacade } from '../../application/facades/home-metrics.facade';
import { formatMetricCount } from '../../../../shared/utils/format-metric-count';
import { I18nFacade } from '../../../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-stats-band',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (facade.isLoadingStats()) {
      <div role="status" aria-live="polite" class="sb-status">
        {{ i18n.t('home.stats.loading') }}
      </div>
    } @else if (facade.statsError()) {
      <div role="alert" class="sb-error">
        <p>{{ i18n.t('home.stats.error') }}</p>
        <button type="button" data-testid="retry-btn" class="sb-retry-btn" (click)="facade.loadStats()">
          {{ i18n.t('home.stats.retry') }}
        </button>
      </div>
    } @else if (facade.stats() !== null) {
      <section [attr.aria-label]="i18n.t('home.stats.section-aria')" class="sb-band">
        <article data-testid="stat-card" class="sb-stat-card" [attr.aria-label]="i18n.t('home.stats.total-plugins')">
          <p class="sb-stat-card__label">{{ i18n.t('home.stats.total-plugins') }}</p>
          <p class="sb-stat-card__value">{{ formatCount(facade.stats()!.totalPlugins) }}</p>
        </article>
        <article data-testid="stat-card" class="sb-stat-card" [attr.aria-label]="i18n.t('home.stats.total-downloads')">
          <p class="sb-stat-card__label">{{ i18n.t('home.stats.total-downloads') }}</p>
          <p class="sb-stat-card__value">{{ formatCount(facade.stats()!.totalDownloads) }}</p>
        </article>
        <article data-testid="stat-card" class="sb-stat-card" [attr.aria-label]="i18n.t('home.stats.publishers')">
          <p class="sb-stat-card__label">{{ i18n.t('home.stats.publishers') }}</p>
          <p class="sb-stat-card__value">{{ formatCount(facade.stats()!.publisherCount) }}</p>
        </article>
        <article data-testid="stat-card" class="sb-stat-card" [attr.aria-label]="i18n.t('home.stats.categories')">
          <p class="sb-stat-card__label">{{ i18n.t('home.stats.categories') }}</p>
          <p class="sb-stat-card__value">{{ formatCount(facade.stats()!.categoryCount) }}</p>
        </article>
      </section>
    }
  `,
  styles: [
    `
      .sb-status {
        text-align: center;
        padding: 1.5rem;
        color: #6b7280;
      }

      .sb-error {
        text-align: center;
        padding: 1.5rem;
        color: #b91c1c;
      }

      .sb-retry-btn {
        margin-top: 0.5rem;
        padding: 0.5rem 1.25rem;
        border: 1px solid #b91c1c;
        border-radius: 0.375rem;
        background: transparent;
        color: #b91c1c;
        cursor: pointer;
        font-size: 0.9375rem;
      }

      .sb-retry-btn:hover {
        background: rgba(185, 28, 28, 0.06);
      }

      .sb-band {
        display: flex;
        flex-wrap: wrap;
        gap: 1.5rem;
        justify-content: center;
        padding: 2rem 1.5rem;
        background: #f9fafb;
      }

      .sb-stat-card {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 0.25rem;
        min-width: 8rem;
      }

      .sb-stat-card__label {
        margin: 0;
        font-size: 0.875rem;
        color: #6b7280;
        text-transform: uppercase;
        letter-spacing: 0.05em;
      }

      .sb-stat-card__value {
        margin: 0;
        font-size: 1.75rem;
        font-weight: 700;
        color: #111827;
      }
    `,
  ],
})
export class StatsBandComponent implements OnInit {
  readonly facade = inject(HomeMetricsFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly formatCount = formatMetricCount;

  ngOnInit(): void {
    this.facade.loadStats();
  }
}
