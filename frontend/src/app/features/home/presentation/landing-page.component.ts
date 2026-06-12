/**
 * Landing page for the ClaudeForge plugin marketplace.
 *
 * Sections:
 * - Hero: warm cream surface with amber/blue gradients, headline, amber-toned
 *   install-showcase card, primary CTAs (browse, publish)
 * - Stats band: marketplace-wide metrics (warm light surface)
 * - Featured plugins: real data via CatalogFacade (top-6 by downloadCount)
 * - How-it-works: 4 value-prop cards (warm light surface)
 * - Footer: dark navy with amber link text (intentionally kept dark)
 *
 * Color scheme (body lightness pass):
 * - Hero: warm cream (--lp-cream #fff8ee) with amber/blue radial gradients
 * - Body sections (stats-band, how-it-works): swapped from dark slate to
 *   warm cream/yellow surfaces with blue accents for a lighter, brighter look
 * - Featured plugin slug drives the CLI command; falls back to generic placeholder
 */

import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnInit,
  PLATFORM_ID,
  Signal,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { CatalogFacade } from '../../catalog/application/facades/catalog.facade';
import type { AddOnSummary } from '../../catalog/domain/models/catalog.models';
import { EmptyStateComponent } from '../../../shared/design-system/empty-state.component';
import { ZardBadgeComponent } from '../../../shared/components/badge/badge.component';
import { ZardButtonComponent } from '../../../shared/components/button/button.component';
import { ZardCardComponent } from '../../../shared/components/card/card.component';
import { formatMetricCount } from '../../../shared/utils/format-metric-count';
import { StatsBandComponent } from './stats-band/stats-band.component';
import { SeoMetadataService } from '../../../shared/infrastructure/seo/seo-metadata.service';
import { StructuredDataService } from '../../../shared/infrastructure/seo/structured-data.service';
import { I18nFacade } from '../../../application/i18n/i18n.facade';
import { FeaturedAddOnFacade } from '../application/facades/featured-plugin.facade';
import type { FeaturedAddOn } from '../domain/models/featured-plugin.model';
import { AuthFacade } from '../../auth/application/facades/auth.facade';
import type { CurrentUser } from '../../auth/domain/models/auth.models';

/** Number of add-ons shown in the featured section. */
const FEATURED_LIMIT = 6;

/** Fallback CLI command identifier shown when no add-on is featured. */
const FALLBACK_PLUGIN_SLUG = '<plugin-name>';

@Component({
  selector: 'cf-landing-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    EmptyStateComponent,
    ZardBadgeComponent,
    ZardButtonComponent,
    ZardCardComponent,
    StatsBandComponent,
  ],
  template: `
    <!-- ================================================================= -->
    <!-- HERO (warm cream surface with amber/blue gradients)                 -->
    <!-- ================================================================= -->
    <section class="lp-hero" aria-labelledby="lp-hero-title">
      <div class="lp-hero__inner">
        <div class="lp-hero__copy">
          <h1 id="lp-hero-title" class="lp-hero__title">{{ i18n.t('home.hero-title') }}</h1>
          <p class="lp-hero__tagline">
            {{ i18n.t('home.hero-tagline') }}
          </p>

          <form class="lp-search-entry" role="search" (submit)="onSearchSubmit($event)">
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
              z-button
              zType="default"
              class="lp-search-entry__btn"
              [attr.aria-label]="i18n.t('home.search-btn')"
            >
              {{ i18n.t('home.search-btn') }}
            </button>
          </form>

          <div class="lp-hero__ctas" role="group" [attr.aria-label]="i18n.t('home.aria.primary-actions')">
            <a
              routerLink="/docs"
              z-button
              zType="default"
              zSize="lg"
              class="lp-hero-cta-primary"
              [attr.aria-label]="i18n.t('home.aria.learn-publish')"
              >{{ i18n.t('home.publish-plugin') }}</a
            >
          </div>
        </div>

        <!-- Install Showcase Card -->
        <div class="lp-showcase" [attr.aria-label]="i18n.t('home.install-showcase.heading')">
          <p class="lp-showcase__heading">{{ i18n.t('home.install-showcase.heading') }}</p>
          @if (featuredAddOn(); as addOn) {
            <p class="lp-showcase__plugin">
              <span>{{ addOn.name }}</span>
              @if (addOn.latestVersion) {
                <span>v{{ addOn.latestVersion }}</span>
              }
            </p>
          }
          <div class="lp-showcase__code-block" role="region" [attr.aria-label]="installCommand()">
            <code class="lp-showcase__code">{{ installCommand() }}</code>
            <button
              type="button"
              class="lp-showcase__copy-btn"
              [attr.aria-label]="
                copied() ? i18n.t('home.install-showcase.copied') : i18n.t('home.install-showcase.copy-btn')
              "
              (click)="copyInstallCommand()"
            >
              {{ copied() ? i18n.t('home.install-showcase.copied') : i18n.t('home.install-showcase.copy-btn') }}
            </button>
          </div>
          <span class="lp-sr-only" aria-live="polite">{{
            copied() ? i18n.t('home.install-showcase.copied') : ''
          }}</span>
          <a routerLink="/docs" class="lp-showcase__caption">{{ i18n.t('home.install-showcase.caption') }}</a>
        </div>
      </div>
    </section>

    <div class="lp-supporting">
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

          @if (isLoadingAddOns()) {
            <div
              class="lp-featured__loading"
              aria-busy="true"
              role="status"
              [attr.aria-label]="i18n.t('home.aria.loading-addons')"
            >
              {{ i18n.t('home.loading-addons') }}
            </div>
          }

          @if (!isLoadingAddOns() && hasError()) {
            <div role="alert" class="lp-featured__error">
              {{ i18n.t('home.error-addons') }}
              <a routerLink="/catalog" class="lp-link">{{ i18n.t('home.error.browse-catalog') }}</a
              >.
            </div>
          }

          @if (!isLoadingAddOns() && !hasError() && featuredAddOns().length === 0) {
            <cf-empty-state [message]="i18n.t('home.empty-addons')" />
          }

          @if (!isLoadingAddOns() && !hasError() && featuredAddOns().length > 0) {
            <ul class="lp-featured__grid" role="list" [attr.aria-label]="i18n.t('home.aria.popular-addons')">
              @for (addOn of featuredAddOns(); track addOn.pluginId) {
                <li class="lp-plugin-card">
                  <z-card class="lp-plugin-card__zcard">
                    <div class="lp-plugin-card__header">
                      <span class="lp-plugin-card__name">{{ addOn.name }}</span>
                      @if (addOn.latestVersion) {
                        <z-badge zType="secondary" zShape="pill">v{{ addOn.latestVersion }}</z-badge>
                      }
                    </div>
                    <p class="lp-plugin-card__description">{{ addOn.description }}</p>
                    <div class="lp-plugin-card__meta">
                      <span class="lp-plugin-card__author">{{ i18n.t('home.plugin-card.by') }} {{ addOn.author }}</span>
                      <span
                        class="lp-plugin-card__downloads"
                        [attr.aria-label]="i18n.t('home.aria.addon-downloads', { count: addOn.downloadCount })"
                      >
                        {{ formatDownloads(addOn.downloadCount) }} {{ i18n.t('home.plugin-card.downloads') }}
                      </span>
                    </div>
                    @if (addOn.types.length > 0) {
                      <div class="lp-plugin-card__tags" [attr.aria-label]="i18n.t('home.aria.addon-types')">
                        @for (type of addOn.types; track type) {
                          <z-badge zType="outline">{{ type }}</z-badge>
                        }
                      </div>
                    }
                  </z-card>
                </li>
              }
            </ul>

            <div class="lp-featured__cta">
              <a routerLink="/catalog" z-button zType="default" zSize="lg">{{ i18n.t('home.view-all-addons') }}</a>
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
    </div>

    <!-- ================================================================= -->
    <!-- FOOTER (dark accent — intentionally kept dark)                     -->
    <!-- ================================================================= -->
    <footer class="lp-footer" role="contentinfo">
      <nav class="lp-footer__nav" [attr.aria-label]="i18n.t('home.aria.footer-nav')">
        <a routerLink="/catalog" class="lp-footer__link">{{ i18n.t('home.footer-catalog') }}</a>
        <a routerLink="/docs" class="lp-footer__link">{{ i18n.t('home.footer-docs') }}</a>
        <a routerLink="/search" class="lp-footer__link">{{ i18n.t('home.footer-search') }}</a>
        @if (currentUser()) {
          <a routerLink="/dashboard" class="lp-footer__link">{{ i18n.t('home.footer-my-plugins') }}</a>
        }
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

      :host {
        --lp-cream: #fff8ee;
        --lp-cream-soft: #fffdf1;
        --lp-amber-soft: #fff7c2;
        --lp-amber-rgb: 250 204 21;
        --lp-slate: #0f172a;
        --lp-slate-soft: #334155;
        --lp-mint-rgb: 34 197 94;
        --lp-coral-rgb: 251 140 90;
        --lp-blue-rgb: 59 130 246;
        --lp-border: color-mix(in oklch, var(--lp-slate) 14%, transparent);
        --lp-shadow-soft: 0 18px 45px rgb(15 23 42 / 0.07);
        --lp-shadow-lifted: 0 28px 80px rgb(15 23 42 / 0.13);
        display: flex;
        flex-direction: column;
        min-height: 100%;
      }

      .lp-link {
        color: var(--primary);
        text-decoration: underline;
      }
      .lp-link:hover {
        color: color-mix(in oklch, var(--primary) 80%, black);
      }

      .lp-section-inner {
        max-width: 90rem;
        margin: 0 auto;
        padding: 0 1.5rem;
      }
      .lp-section-title {
        font-size: 1.5rem;
        font-weight: 700;
        color: var(--lp-slate);
        margin: 0 0 1.25rem;
        letter-spacing: -0.02em;
      }

      /* ── Hero (warm cream surface with amber/blue gradients) ──────────── */
      .lp-hero {
        position: relative;
        overflow: hidden;
        background:
          radial-gradient(circle at 14% 18%, rgb(var(--lp-amber-rgb) / 0.35), transparent 24rem),
          radial-gradient(circle at 78% 16%, rgb(var(--lp-blue-rgb) / 0.1), transparent 23rem),
          radial-gradient(circle at 55% 95%, rgb(var(--lp-mint-rgb) / 0.08), transparent 25rem), var(--lp-cream);
        color: var(--lp-slate);
        padding: 1.5rem 1.5rem 1.25rem;
      }
      .lp-hero__inner {
        position: relative;
        z-index: 1;
        max-width: 90rem;
        margin: 0 auto;
        display: grid;
        grid-template-columns: minmax(0, 1fr) auto;
        gap: 2rem;
        align-items: center;
      }
      .lp-hero__copy {
        min-width: 0;
      }
      .lp-hero__title {
        font-size: clamp(2rem, 3vw, 2.75rem);
        font-weight: 800;
        line-height: 1.1;
        letter-spacing: -0.03em;
        margin: 0 0 0.875rem;
        color: var(--lp-slate);
      }
      .lp-hero__tagline {
        font-size: 1rem;
        color: var(--lp-slate-soft);
        max-width: 40rem;
        margin: 0 0 1rem;
        line-height: 1.45;
      }
      .lp-hero__ctas {
        display: flex;
        flex-wrap: wrap;
        gap: 0.875rem;
      }

      /* ── Search entry ─────────────────────────────────────────────────── */
      .lp-search-entry {
        display: flex;
        align-items: center;
        gap: 0.625rem;
        width: 100%;
        max-width: 40rem;
        margin: 0 0 1rem;
        padding: 0.375rem;
        box-sizing: border-box;
        border: 1px solid rgb(15 23 42 / 0.15);
        border-radius: 0.875rem;
        background: #ffffff;
        box-shadow: var(--lp-shadow-soft);
      }
      .lp-search-entry__input {
        flex: 1;
        min-width: 0;
        padding: 0.625rem 0.75rem;
        border: 1px solid transparent;
        border-radius: 0.625rem;
        background: rgb(255 248 238 / 0.6);
        color: var(--lp-slate);
        font-size: 0.9375rem;
        outline: none;
      }
      .lp-search-entry__input::placeholder {
        color: rgb(15 23 42 / 0.4);
      }
      .lp-search-entry__input:focus {
        border-color: rgb(var(--lp-amber-rgb) / 0.6);
        box-shadow: 0 0 0 3px rgb(var(--lp-amber-rgb) / 0.18);
      }
      .lp-search-entry__btn {
        flex-shrink: 0;
        background: rgb(var(--lp-amber-rgb));
        color: var(--lp-slate);
        border-color: rgb(var(--lp-amber-rgb));
        font-weight: 600;
      }
      .lp-search-entry__btn:hover {
        background: rgb(253 224 71);
        border-color: rgb(253 224 71);
      }

      /* Override z-button defaults for hero CTAs on dark background */
      .lp-hero .lp-hero-cta-primary {
        background: rgb(var(--lp-amber-rgb));
        color: var(--lp-slate);
        border-color: rgb(var(--lp-amber-rgb));
        font-weight: 600;
        box-shadow: 0 18px 40px rgb(0 0 0 / 0.25);
      }
      .lp-hero .lp-hero-cta-primary:hover {
        background: rgb(253 224 71);
        border-color: rgb(253 224 71);
      }
      .lp-hero .lp-hero-cta-primary:focus-visible {
        outline: 3px solid rgb(var(--lp-amber-rgb));
        outline-offset: 2px;
      }
      .lp-hero .lp-hero-cta-secondary {
        color: var(--lp-slate);
        border-color: rgb(15 23 42 / 0.25);
        background: transparent;
      }
      .lp-hero .lp-hero-cta-secondary:hover {
        border-color: rgb(15 23 42 / 0.5);
        background: rgb(15 23 42 / 0.06);
      }
      .lp-hero .lp-hero-cta-secondary:focus-visible {
        outline: 3px solid rgb(var(--lp-amber-rgb));
        outline-offset: 2px;
      }
      /* ── Install Showcase Card (slightly lighter code block on dark hero) */
      .lp-showcase {
        display: inline-flex;
        flex-direction: column;
        align-items: center;
        gap: 0.5rem;
        margin: 0;
        max-width: 100%;
        min-width: 0;
        text-align: center;
      }
      .lp-showcase__heading {
        font-size: 1rem;
        font-weight: 600;
        color: var(--lp-slate);
        margin: 0 0 0.375rem;
      }
      .lp-showcase__plugin {
        display: flex;
        align-items: center;
        justify-content: center;
        gap: 0.5rem;
        margin: -0.25rem 0 0.125rem;
        color: var(--lp-slate-soft);
        font-size: 0.8125rem;
        font-weight: 600;
      }
      .lp-showcase__code-block {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        max-width: 100%;
        min-width: 0;
        box-sizing: border-box;
        overflow-x: auto;
        background: #1a273d;
        color: var(--lp-amber-soft);
        padding: 0.75rem 1.25rem;
        border-radius: 0.5rem;
        font-family: ui-monospace, SFMono-Regular, 'SF Mono', Menlo, Consolas, 'Liberation Mono', monospace;
        box-shadow: 0 28px 80px rgb(0 0 0 / 0.3);
      }
      .lp-showcase__code {
        display: block;
        flex: 1 1 auto;
        min-width: 0;
        max-width: 100%;
        overflow-x: auto;
        font-size: 0.9375rem;
        color: inherit;
        user-select: all;
        white-space: nowrap;
      }
      .lp-showcase__copy-btn {
        flex-shrink: 0;
        padding: 0.25rem 0.625rem;
        border: 1px solid color-mix(in oklch, var(--background) 35%, transparent);
        border-radius: 0.25rem;
        background: color-mix(in oklch, var(--background) 12%, transparent);
        color: var(--background);
        font-size: 0.8125rem;
        cursor: pointer;
        transition:
          background 0.15s,
          color 0.15s;
        line-height: 1.4;
      }
      .lp-showcase__copy-btn:hover {
        background: color-mix(in oklch, var(--background) 22%, transparent);
      }
      .lp-showcase__copy-btn:focus-visible {
        outline: 2px solid var(--background);
        outline-offset: 2px;
      }
      .lp-showcase__caption {
        font-size: 0.8125rem;
        color: var(--lp-slate-soft);
        margin: 0;
        text-decoration: underline;
        text-underline-offset: 2px;
        transition: color 0.15s;
      }
      .lp-showcase__caption:hover {
        color: var(--lp-slate);
      }
      .lp-showcase__caption:focus-visible {
        outline: 2px solid var(--ring);
        outline-offset: 2px;
        border-radius: 2px;
      }

      .lp-supporting {
        flex: 1;
        max-width: 90rem;
        margin: 0 auto;
        width: 100%;
        box-sizing: border-box;
        padding: 0.75rem 1.5rem 1.5rem;
        display: grid;
        grid-template-columns: minmax(0, 1.2fr) minmax(18rem, 0.8fr);
        gap: 1rem 1.5rem;
      }
      .lp-supporting cf-stats-band {
        grid-column: 1 / -1;
      }
      .lp-supporting .lp-section-inner {
        max-width: none;
        padding: 0;
      }

      /* ── Featured plugins (popular plugins) ──────────────────────────── */
      .lp-featured {
        padding: 1rem;
        background: var(--lp-cream-soft);
        border-radius: 1rem;
        box-shadow: var(--lp-shadow-lifted);
        display: flex;
        flex-direction: column;
      }
      .lp-featured .lp-section-inner {
        flex: 1;
        display: flex;
        flex-direction: column;
      }
      .lp-featured__loading,
      .lp-featured__error {
        text-align: center;
        padding: 1rem 0;
        color: var(--lp-slate-soft);
        font-size: 0.9375rem;
      }
      .lp-featured__error {
        color: #dc2626;
      }
      .lp-featured__grid {
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        grid-auto-rows: 1fr;
        align-items: stretch;
        gap: 1rem;
        list-style: none;
        margin: 0 0 1.25rem;
        padding: 0;
        flex: 1;
      }
      .lp-featured__cta {
        text-align: center;
      }
      .lp-featured__cta a {
        background: rgb(var(--lp-amber-rgb));
        color: var(--lp-slate);
        border-color: rgb(var(--lp-amber-rgb));
        font-weight: 600;
      }
      .lp-featured__cta a:hover {
        background: rgb(253 224 71);
        border-color: rgb(253 224 71);
      }

      /* ── Plugin card ──────────────────────────────────────────────────── */
      .lp-plugin-card {
        display: contents;
      }
      .lp-plugin-card__zcard {
        height: 100%;
        box-sizing: border-box;
        border-color: var(--lp-border);
        box-shadow: var(--lp-shadow-soft);
        transition:
          box-shadow 0.2s,
          transform 0.2s;
      }
      .lp-plugin-card__zcard:hover {
        transform: translateY(-1px);
        box-shadow: var(--lp-shadow-lifted);
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
        color: var(--lp-slate);
        line-height: 1.3;
      }
      .lp-plugin-card__description {
        font-size: 0.875rem;
        color: var(--lp-slate-soft);
        line-height: 1.6;
        margin: 0;
        overflow: hidden;
        max-height: 4.8em;
      }
      .lp-plugin-card__meta {
        display: flex;
        justify-content: space-between;
        font-size: 0.8125rem;
        color: var(--lp-slate-soft);
        gap: 0.5rem;
        flex-wrap: wrap;
      }
      .lp-plugin-card__tags {
        display: flex;
        flex-wrap: wrap;
        gap: 0.375rem;
      }

      /* ── How it works ─────────────────────────────────────────────────── */
      .lp-how {
        padding: 1rem;
        background: var(--lp-cream-soft);
        border-radius: 1rem;
        box-shadow: var(--lp-shadow-lifted);
        display: flex;
        flex-direction: column;
      }
      .lp-how .lp-section-inner {
        flex: 1;
        display: flex;
        flex-direction: column;
        /* Compensate for the addon section's CTA (grid-margin 1.25rem + button h-9 2.25rem)
           so both grids have the same available height and their rows align. */
        padding-bottom: 3.5rem;
      }
      .lp-how__steps {
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        grid-auto-rows: 1fr;
        align-items: stretch;
        gap: 1rem;
        list-style: none;
        margin: 0;
        padding: 0;
        flex: 1;
      }
      .lp-how__step {
        display: flex;
        flex-direction: column;
        gap: 0.5rem;
        padding: 0.875rem;
        border: 1px solid rgb(15 23 42 / 0.1);
        border-radius: 0.875rem;
        background: #ffffff;
        box-shadow: 0 4px 16px rgb(15 23 42 / 0.06);
        height: 100%;
        box-sizing: border-box;
        transition:
          box-shadow 0.2s,
          transform 0.2s;
      }
      .lp-how__step:hover {
        transform: translateY(-1px);
        box-shadow: var(--lp-shadow-lifted);
      }
      .lp-how__icon {
        font-size: 1.5rem;
        line-height: 1;
        width: 2.25rem;
        height: 2.25rem;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        border-radius: 0.75rem;
        background: linear-gradient(135deg, rgb(var(--lp-blue-rgb) / 0.15), rgb(var(--lp-mint-rgb) / 0.15));
      }
      .lp-how__step-title {
        font-size: 1rem;
        font-weight: 700;
        color: var(--lp-slate);
        margin: 0;
      }
      .lp-how__step-desc {
        font-size: 0.875rem;
        color: var(--lp-slate-soft);
        line-height: 1.6;
        margin: 0;
      }
      /* ── Footer (dark accent — intentional) ───────────────────────────── */
      .lp-footer {
        position: sticky;
        bottom: 0;
        left: 0;
        right: 0;
        z-index: 20;
        width: 100%;
        box-sizing: border-box;
        background: var(--lp-slate);
        color: var(--lp-amber-soft);
        padding: 1rem 1.5rem calc(1rem + env(safe-area-inset-bottom));
        text-align: center;
      }
      .lp-footer__nav {
        display: flex;
        flex-wrap: wrap;
        justify-content: center;
        gap: 0.25rem 1.5rem;
        margin-bottom: 0.625rem;
      }
      .lp-footer__link {
        color: var(--lp-amber-soft);
        text-decoration: none;
        font-size: 0.9375rem;
        transition: color 0.2s ease;
      }
      .lp-footer__link:hover {
        color: #ffffff;
      }
      .lp-footer__link:focus-visible {
        outline: 2px solid var(--ring);
        outline-offset: 2px;
        border-radius: 2px;
      }
      .lp-footer__copy {
        font-size: 0.8125rem;
        margin: 0;
        color: rgb(255 255 255 / 0.64);
      }

      /* ── Responsive ───────────────────────────────────────────────────── */
      @media (max-width: 640px) {
        .lp-hero {
          padding: 3rem 1rem 2.5rem;
        }
        .lp-hero__inner,
        .lp-supporting {
          grid-template-columns: 1fr;
        }
        .lp-hero__inner {
          text-align: center;
        }
        .lp-hero__ctas {
          flex-direction: column;
          align-items: center;
        }
        .lp-search-entry {
          flex-direction: column;
          margin-inline: auto;
        }
        .lp-showcase {
          margin: 0 auto;
        }
        .lp-showcase__code-block {
          flex-direction: column;
          gap: 0.5rem;
          text-align: center;
          width: 100%;
        }
        .lp-featured__grid {
          grid-template-columns: 1fr;
        }
        .lp-how__steps {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }
      }

      @media (max-width: 420px) {
        .lp-footer__nav {
          row-gap: 0.5rem;
        }
      }
    `,
  ],
})
export class LandingPageComponent implements OnInit {
  private readonly catalogFacade = inject(CatalogFacade);
  private readonly featuredAddOnFacade = inject(FeaturedAddOnFacade);
  private readonly authFacade = inject(AuthFacade);
  private readonly router = inject(Router);
  private readonly seoMetadata = inject(SeoMetadataService);
  private readonly structuredData = inject(StructuredDataService);
  private readonly platformId = inject(PLATFORM_ID);
  protected readonly i18n = inject(I18nFacade);

  readonly isLoadingAddOns: Signal<boolean> = this.catalogFacade.isLoadingAddOns;
  readonly hasError: Signal<boolean> = computed(() => this.catalogFacade.addOnsError() !== undefined);
  readonly currentUser: Signal<CurrentUser | undefined> = this.authFacade.currentUser;
  readonly featuredAddOn: Signal<FeaturedAddOn | null> = this.featuredAddOnFacade.featuredAddOn;

  readonly featuredAddOns: Signal<AddOnSummary[]> = computed(() => {
    const all = this.catalogFacade.addOns();
    return [...all].sort((a, b) => b.downloadCount - a.downloadCount).slice(0, FEATURED_LIMIT);
  });

  /**
   * The CLI install command to display in the showcase.
   * Uses the featured add-on's slug when available; otherwise a generic fallback.
   */
  readonly installCommand: Signal<string> = computed(() => {
    const addOn = this.featuredAddOn();
    const identifier = addOn?.slug ?? FALLBACK_PLUGIN_SLUG;
    return `claude-plugin install ${identifier}`;
  });

  readonly copied = signal(false);
  readonly searchQuery = signal('');

  readonly currentYear: number = new Date().getFullYear();

  constructor() {
    effect(() => {
      const addOns = this.featuredAddOns();
      if (addOns.length > 0) {
        this.structuredData.injectPluginItemList(addOns);
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
    this.catalogFacade.loadAddOns({ sort: 'downloadCount', order: 'desc' });
    this.featuredAddOnFacade.load();

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
    const target = event.target;
    if (target instanceof HTMLInputElement) {
      this.searchQuery.set(target.value);
    }
  }

  onSearchSubmit(event: Event): void {
    event.preventDefault();
    const q = this.searchQuery().trim();
    if (q.length > 0) {
      void this.router.navigate(['/search'], { queryParams: { q } });
      return;
    }
    void this.router.navigate(['/search']);
  }

  /**
   * Copies the install command to clipboard.
   * SSR-safe: guards navigator.clipboard usage behind isPlatformBrowser.
   */
  copyInstallCommand(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    const command = this.installCommand();
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      void navigator.clipboard.writeText(command).then(() => {
        this.copied.set(true);
        setTimeout(() => this.copied.set(false), 2000);
      });
    }
  }

  formatDownloads(count: number): string {
    return formatMetricCount(count);
  }
}
