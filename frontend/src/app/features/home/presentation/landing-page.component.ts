/**
 * Landing page for the ClaudeForge plugin marketplace.
 *
 * Sections:
 * - Hero: tagline, primary CTAs (browse, publish) and a sign-in placeholder
 * - Featured plugins: real data via CatalogFacade (top-6 by downloadCount)
 * - How-it-works: 4 value-prop cards
 * - Search entry: keyboard-accessible form that navigates to /search
 * - Footer: links to catalog and docs
 *
 * NOTE: Sign-in CTA is rendered as a disabled button linked to /auth/login.
 * Auth is not yet implemented; the affordance signals the intent without
 * routing to a broken page (button is aria-disabled and tabIndex=-1).
 */

import { ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, Signal, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CatalogFacade } from '../../catalog/application/facades/catalog.facade';
import type { PluginSummary } from '../../catalog/domain/models/catalog.models';
import { EmptyStateComponent } from '../../../shared/design-system/empty-state.component';
import { BadgeComponent } from '../../../shared/design-system/badge.component';
import { formatMetricCount } from '../../../shared/utils/format-metric-count';
import { StatsBandComponent } from './stats-band/stats-band.component';
import { SeoMetadataService } from '../../../shared/infrastructure/seo/seo-metadata.service';
import { StructuredDataService } from '../../../shared/infrastructure/seo/structured-data.service';
import { I18nFacade } from '../../../application/i18n/i18n.facade';

/** Number of plugins shown in the featured section. */
const FEATURED_LIMIT = 6;

@Component({
  selector: 'cf-landing-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, EmptyStateComponent, BadgeComponent, StatsBandComponent],
  template: `
    <!-- ================================================================= -->
    <!-- HERO                                                                -->
    <!-- ================================================================= -->
    <section class="lp-hero" aria-labelledby="lp-hero-title">
      <div class="lp-hero__inner">
        <h1 id="lp-hero-title" class="lp-hero__title">{{ i18n.t('home.hero-title') }}</h1>
        <p class="lp-hero__tagline">
          {{ i18n.t('home.hero-tagline') }}
        </p>

        <div class="lp-hero__ctas" role="group" [attr.aria-label]="i18n.t('home.aria.primary-actions')">
          <a routerLink="/catalog" class="lp-btn lp-btn--primary" [attr.aria-label]="i18n.t('home.aria.browse-all')">{{
            i18n.t('home.browse-plugins')
          }}</a>

          <a routerLink="/docs" class="lp-btn lp-btn--secondary" [attr.aria-label]="i18n.t('home.aria.learn-publish')">{{
            i18n.t('home.publish-plugin')
          }}</a>

          <!-- Sign-in CTA — routes to the login page -->
          <button
            type="button"
            class="lp-btn lp-btn--ghost"
            aria-disabled="true"
            tabindex="-1"
            [title]="i18n.t('home.sign-in')"
            (click)="onSignIn()"
          >
            {{ i18n.t('home.sign-in') }}
          </button>
        </div>
      </div>
    </section>

    <!-- ================================================================= -->
    <!-- SEARCH ENTRY                                                        -->
    <!-- ================================================================= -->
    <section class="lp-search-entry" [attr.aria-label]="i18n.t('home.search-aria')">
      <form class="lp-search-entry__form" role="search" (submit)="onSearchSubmit($event)">
        <label for="lp-search-input" class="lp-sr-only">{{ i18n.t('home.search-aria') }}</label>
        <input
          id="lp-search-input"
          type="search"
          class="lp-search-entry__input"
          [placeholder]="i18n.t('home.search-placeholder')"
          [value]="searchQuery()"
          (input)="onSearchInput($event)"
          autocomplete="off"
          [attr.aria-label]="i18n.t('home.search-aria')"
        />
        <button
          type="submit"
          class="lp-btn lp-btn--primary lp-search-entry__btn"
          [attr.aria-label]="i18n.t('home.search-btn')"
        >
          {{ i18n.t('home.search-btn') }}
        </button>
      </form>
    </section>

    <!-- ================================================================= -->
    <!-- STATS BAND                                                          -->
    <!-- ================================================================= -->
    <cf-stats-band />

    <!-- ================================================================= -->
    <!-- FEATURED PLUGINS                                                    -->
    <!-- ================================================================= -->
    <section class="lp-featured" aria-labelledby="lp-featured-title">
      <div class="lp-section-inner">
        <h2 id="lp-featured-title" class="lp-section-title">
          {{ i18n.t('home.popular-heading') }}
        </h2>

        @if (isLoadingPlugins()) {
          <div class="lp-featured__loading" aria-busy="true" role="status" [attr.aria-label]="i18n.t('home.aria.loading-plugins')">
            {{ i18n.t('home.loading-plugins') }}
          </div>
        }

        @if (!isLoadingPlugins() && hasError()) {
          <div role="alert" class="lp-featured__error">
            {{ i18n.t('home.error-plugins') }}
            <a routerLink="/catalog" class="lp-link">{{ i18n.t('home.error.browse-catalog') }}</a>.
          </div>
        }

        @if (!isLoadingPlugins() && !hasError() && featuredPlugins().length === 0) {
          <cf-empty-state [message]="i18n.t('home.empty-plugins')" />
        }

        @if (!isLoadingPlugins() && !hasError() && featuredPlugins().length > 0) {
          <ul class="lp-featured__grid" role="list" [attr.aria-label]="i18n.t('home.aria.popular-plugins')">
            @for (plugin of featuredPlugins(); track plugin.pluginId) {
              <li class="lp-plugin-card">
                <div class="lp-plugin-card__header">
                  <span class="lp-plugin-card__name">{{ plugin.name }}</span>
                  @if (plugin.latestVersion) {
                    <cf-badge variant="info">v{{ plugin.latestVersion }}</cf-badge>
                  }
                </div>
                <p class="lp-plugin-card__description">{{ plugin.description }}</p>
                <div class="lp-plugin-card__meta">
                  <span class="lp-plugin-card__author">{{ i18n.t('home.plugin-card.by') }} {{ plugin.author }}</span>
                  <span class="lp-plugin-card__downloads" [attr.aria-label]="i18n.t('home.aria.plugin-downloads', { count: plugin.downloadCount })">
                    {{ formatDownloads(plugin.downloadCount) }} {{ i18n.t('home.plugin-card.downloads') }}
                  </span>
                </div>
                @if (plugin.types.length > 0) {
                  <div class="lp-plugin-card__tags" [attr.aria-label]="i18n.t('home.aria.plugin-types')">
                    @for (type of plugin.types; track type) {
                      <cf-badge>{{ type }}</cf-badge>
                    }
                  </div>
                }
              </li>
            }
          </ul>

          <div class="lp-featured__cta">
            <a routerLink="/catalog" class="lp-btn lp-btn--secondary">{{ i18n.t('home.view-all-plugins') }}</a>
          </div>
        }
      </div>
    </section>

    <!-- ================================================================= -->
    <!-- HOW IT WORKS                                                        -->
    <!-- ================================================================= -->
    <section class="lp-how" aria-labelledby="lp-how-title">
      <div class="lp-section-inner">
        <h2 id="lp-how-title" class="lp-section-title">{{ i18n.t('home.how-heading') }}</h2>
        <ol class="lp-how__steps" role="list">
          @for (step of howItWorksSteps; track step.titleKey) {
            <li class="lp-how__step">
              <span class="lp-how__icon" aria-hidden="true">{{ step.icon }}</span>
              <h3 class="lp-how__step-title">{{ i18n.t(step.titleKey) }}</h3>
              <p class="lp-how__step-desc">{{ i18n.t(step.descKey) }}</p>
            </li>
          }
        </ol>
      </div>
    </section>

    <!-- ================================================================= -->
    <!-- FOOTER                                                              -->
    <!-- ================================================================= -->
    <footer class="lp-footer" role="contentinfo">
      <nav class="lp-footer__nav" [attr.aria-label]="i18n.t('home.aria.footer-nav')">
        <a routerLink="/catalog" class="lp-footer__link">{{ i18n.t('home.footer-catalog') }}</a>
        <a routerLink="/docs" class="lp-footer__link">{{ i18n.t('home.footer-docs') }}</a>
        <a routerLink="/search" class="lp-footer__link">{{ i18n.t('home.footer-search') }}</a>
        <a routerLink="/dashboard" class="lp-footer__link">{{ i18n.t('home.footer-my-plugins') }}</a>
      </nav>
      <p class="lp-footer__copy">&copy; {{ currentYear }} {{ i18n.t('home.footer-copy') }}</p>
    </footer>
  `,
  styles: [
    `
      .lp-sr-only {
        position: absolute;
        width: 1px;
        height: 1px;
        padding: 0;
        margin: -1px;
        overflow: hidden;
        clip: rect(0, 0, 0, 0);
        white-space: nowrap;
        border-width: 0;
      }
      .lp-link {
        color: #3b82f6;
        text-decoration: underline;
      }
      .lp-link:hover {
        color: #1d4ed8;
      }

      .lp-btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        padding: 0.625rem 1.25rem;
        border-radius: 0.375rem;
        font-size: 0.9375rem;
        font-weight: 600;
        line-height: 1.25;
        text-decoration: none;
        border: 2px solid transparent;
        cursor: pointer;
        transition:
          background-color 0.2s ease,
          color 0.2s ease,
          border-color 0.2s ease;
      }
      .lp-btn--primary {
        background: #1a1a2e;
        color: #fff;
        border-color: #1a1a2e;
      }
      .lp-btn--primary:hover {
        background: #16213e;
        border-color: #16213e;
      }
      .lp-btn--primary:focus-visible,
      .lp-btn--secondary:focus-visible {
        outline: 3px solid #60a5fa;
        outline-offset: 2px;
      }
      .lp-btn--secondary {
        background: transparent;
        color: #1a1a2e;
        border-color: #1a1a2e;
      }
      .lp-btn--secondary:hover {
        background: rgba(26, 26, 46, 0.06);
      }
      .lp-btn--ghost {
        background: transparent;
        color: rgba(255, 255, 255, 0.75);
        border-color: rgba(255, 255, 255, 0.45);
        cursor: pointer;
      }
      .lp-btn--ghost:hover {
        background: rgba(255, 255, 255, 0.1);
      }

      .lp-section-inner {
        max-width: 72rem;
        margin: 0 auto;
        padding: 0 1.5rem;
      }
      .lp-section-title {
        font-size: 1.625rem;
        font-weight: 700;
        color: #111827;
        margin: 0 0 2rem;
        letter-spacing: -0.02em;
      }

      .lp-hero {
        background: #1a1a2e;
        color: #fff;
        padding: 5rem 1.5rem 4rem;
        text-align: center;
      }
      .lp-hero__inner {
        max-width: 56rem;
        margin: 0 auto;
      }
      .lp-hero__title {
        font-size: clamp(2rem, 5vw, 3.25rem);
        font-weight: 800;
        line-height: 1.1;
        letter-spacing: -0.03em;
        margin: 0 0 1.25rem;
      }
      .lp-hero__brand {
        color: #818cf8;
      }
      .lp-hero__tagline {
        font-size: 1.125rem;
        color: rgba(255, 255, 255, 0.78);
        max-width: 40rem;
        margin: 0 auto 2.5rem;
        line-height: 1.7;
      }
      .lp-hero__ctas {
        display: flex;
        flex-wrap: wrap;
        gap: 0.875rem;
        justify-content: center;
      }
      .lp-hero .lp-btn--primary {
        background: #818cf8;
        border-color: #818cf8;
        color: #fff;
      }
      .lp-hero .lp-btn--primary:hover {
        background: #6366f1;
        border-color: #6366f1;
      }
      .lp-hero .lp-btn--secondary {
        color: #fff;
        border-color: rgba(255, 255, 255, 0.55);
      }
      .lp-hero .lp-btn--secondary:hover {
        background: rgba(255, 255, 255, 0.1);
      }

      .lp-search-entry {
        background: #f3f4f6;
        padding: 2rem 1.5rem;
      }
      .lp-search-entry__form {
        max-width: 48rem;
        margin: 0 auto;
        display: flex;
        gap: 0.625rem;
      }
      .lp-search-entry__input {
        flex: 1;
        padding: 0.625rem 1rem;
        border: 2px solid #d1d5db;
        border-radius: 0.375rem;
        font-size: 0.9375rem;
        background: #fff;
        color: #111827;
        transition: border-color 0.2s;
        outline: none;
      }
      .lp-search-entry__input:focus {
        border-color: #818cf8;
        box-shadow: 0 0 0 3px rgba(129, 140, 248, 0.2);
      }

      .lp-featured {
        padding: 4rem 0;
        background: #fff;
      }
      .lp-featured__loading,
      .lp-featured__error {
        text-align: center;
        padding: 3rem 0;
        color: #6b7280;
        font-size: 0.9375rem;
      }
      .lp-featured__error {
        color: #b91c1c;
      }
      .lp-featured__grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(18rem, 1fr));
        gap: 1.5rem;
        list-style: none;
        margin: 0 0 2.5rem;
        padding: 0;
      }
      .lp-featured__cta {
        text-align: center;
      }

      .lp-plugin-card {
        border: 1px solid #e5e7eb;
        border-radius: 0.75rem;
        padding: 1.25rem;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        background: #fff;
        transition: box-shadow 0.2s;
      }
      .lp-plugin-card:hover {
        box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
      }
      .lp-plugin-card__header {
        display: flex;
        align-items: flex-start;
        justify-content: space-between;
        gap: 0.5rem;
      }
      .lp-plugin-card__name {
        font-size: 1rem;
        font-weight: 700;
        color: #111827;
        line-height: 1.3;
      }
      .lp-plugin-card__description {
        font-size: 0.875rem;
        color: #4b5563;
        line-height: 1.6;
        margin: 0;
        overflow: hidden;
        max-height: 4.8em;
      }
      .lp-plugin-card__meta {
        display: flex;
        justify-content: space-between;
        font-size: 0.8125rem;
        color: #6b7280;
        gap: 0.5rem;
        flex-wrap: wrap;
      }
      .lp-plugin-card__tags {
        display: flex;
        flex-wrap: wrap;
        gap: 0.375rem;
      }

      .lp-how {
        padding: 4rem 0;
        background: #f9fafb;
      }
      .lp-how__steps {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
        gap: 2rem;
        list-style: none;
        margin: 0;
        padding: 0;
      }
      .lp-how__step {
        display: flex;
        flex-direction: column;
        gap: 0.625rem;
      }
      .lp-how__icon {
        font-size: 2rem;
        line-height: 1;
      }
      .lp-how__step-title {
        font-size: 1.0625rem;
        font-weight: 700;
        color: #111827;
        margin: 0;
      }
      .lp-how__step-desc {
        font-size: 0.9375rem;
        color: #4b5563;
        line-height: 1.6;
        margin: 0;
      }

      .lp-footer {
        background: #1a1a2e;
        color: rgba(255, 255, 255, 0.75);
        padding: 2.5rem 1.5rem;
        text-align: center;
      }
      .lp-footer__nav {
        display: flex;
        flex-wrap: wrap;
        justify-content: center;
        gap: 0.25rem 1.5rem;
        margin-bottom: 1.25rem;
      }
      .lp-footer__link {
        color: rgba(255, 255, 255, 0.75);
        text-decoration: none;
        font-size: 0.9375rem;
        transition: color 0.2s ease;
      }
      .lp-footer__link:hover {
        color: #fff;
      }
      .lp-footer__link:focus-visible {
        outline: 2px solid #818cf8;
        outline-offset: 2px;
        border-radius: 2px;
      }
      .lp-footer__copy {
        font-size: 0.8125rem;
        margin: 0;
        color: rgba(255, 255, 255, 0.5);
      }

      @media (max-width: 640px) {
        .lp-hero {
          padding: 3rem 1rem 2.5rem;
        }
        .lp-hero__ctas {
          flex-direction: column;
          align-items: center;
        }
        .lp-search-entry__form {
          flex-direction: column;
        }
        .lp-featured__grid,
        .lp-how__steps {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class LandingPageComponent implements OnInit {
  private readonly catalogFacade = inject(CatalogFacade);
  private readonly router = inject(Router);
  private readonly seoMetadata = inject(SeoMetadataService);
  private readonly structuredData = inject(StructuredDataService);
  protected readonly i18n = inject(I18nFacade);

  readonly isLoadingPlugins: Signal<boolean> = this.catalogFacade.isLoadingPlugins;
  readonly hasError: Signal<boolean> = computed(() => this.catalogFacade.pluginsError() !== undefined);

  readonly featuredPlugins: Signal<PluginSummary[]> = computed(() => {
    const all = this.catalogFacade.plugins();
    return [...all].sort((a, b) => b.downloadCount - a.downloadCount).slice(0, FEATURED_LIMIT);
  });

  readonly searchQuery = signal('');

  readonly currentYear: number = new Date().getFullYear();

  constructor() {
    effect(() => {
      const plugins = this.featuredPlugins();
      if (plugins.length > 0) {
        this.structuredData.injectPluginItemList(plugins);
      }
    });
  }

  readonly howItWorksSteps: readonly {
    readonly icon: string;
    readonly titleKey: string;
    readonly descKey: string;
  }[] = [
    {
      icon: '🔍',
      titleKey: 'home.how.step-discover-title',
      descKey: 'home.how.step-discover-desc',
    },
    {
      icon: '⚡',
      titleKey: 'home.how.step-install-title',
      descKey: 'home.how.step-install-desc',
    },
    {
      icon: '🚀',
      titleKey: 'home.how.step-publish-title',
      descKey: 'home.how.step-publish-desc',
    },
    {
      icon: '👥',
      titleKey: 'home.how.step-share-title',
      descKey: 'home.how.step-share-desc',
    },
  ];

  ngOnInit(): void {
    this.catalogFacade.loadPlugins({ sort: 'downloadCount', order: 'desc' });

    this.seoMetadata.setMetadata({
      title: this.i18n.t('home.seo.title'),
      description: this.i18n.t('home.seo.description'),
      ogTitle: this.i18n.t('home.seo.og-title'),
      ogDescription: this.i18n.t('home.seo.og-description'),
      ogType: 'website',
      ogUrl: 'https://claudeforge.dev/',
      ogImage: 'https://claudeforge.dev/assets/og-image.png',
      twitterCard: 'summary_large_image',
      twitterTitle: this.i18n.t('home.seo.twitter-title'),
      twitterDescription: this.i18n.t('home.seo.twitter-description'),
    });

    this.structuredData.injectOrganizationAndWebSite({
      organizationName: 'ClaudeForge',
      siteUrl: 'https://claudeforge.dev',
      logoUrl: 'https://claudeforge.dev/assets/logo.png',
      searchActionTemplate: 'https://claudeforge.dev/search?q={search_term_string}',
    });
  }

  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchQuery.set(input.value);
  }

  onSearchSubmit(event: Event): void {
    event.preventDefault();
    const q = this.searchQuery().trim();
    if (q.length > 0) {
      void this.router.navigate(['/search'], { queryParams: { q } });
    } else {
      void this.router.navigate(['/search']);
    }
  }

  onSignIn(): void {
    void this.router.navigate(['/login']);
  }

  formatDownloads(count: number): string {
    return formatMetricCount(count);
  }
}
