/**
 * Install page — explains how to install the ClaudeForge CLI, how to install
 * plugins, and provides an introduction to the plugin ecosystem.
 *
 * Static, i18n-driven content. No backend calls required.
 * SSR-safe: clipboard access guarded behind isPlatformBrowser.
 */

import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  PLATFORM_ID,
  signal,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ZardBadgeComponent } from '../../../shared/components/badge/badge.component';
import { ZardButtonComponent } from '../../../shared/components/button/button.component';
import { ZardCardComponent } from '../../../shared/components/card/card.component';
import { SeoMetadataService } from '../../../shared/infrastructure/seo/seo-metadata.service';
import { I18nFacade } from '../../../application/i18n/i18n.facade';

/** npm package name for the ClaudeForge CLI, from cli/package.json */
const CLI_PACKAGE = '@claudeforge/claude-plugin-cli';

@Component({
  selector: 'cf-install-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ZardBadgeComponent, ZardButtonComponent, ZardCardComponent],
  template: `
    <!-- ================================================================= -->
    <!-- HERO                                                                -->
    <!-- ================================================================= -->
    <section class="ip-hero" aria-labelledby="ip-hero-title">
      <div class="ip-hero__inner">
        <z-badge zType="secondary" class="ip-hero__badge">
          {{ i18n.t('install.badge') }}
        </z-badge>
        <h1 id="ip-hero-title" class="ip-hero__title">{{ i18n.t('install.hero-title') }}</h1>
        <p class="ip-hero__tagline">{{ i18n.t('install.hero-tagline') }}</p>
      </div>
    </section>

    <div class="ip-content">

      <!-- ================================================================= -->
      <!-- SECTION 1: Install the CLI                                         -->
      <!-- ================================================================= -->
      <section class="ip-section" aria-labelledby="ip-cli-heading">
        <z-card class="ip-card">
          <div class="ip-card__header">
            <span class="ip-card__icon" aria-hidden="true">⚡</span>
            <h2 id="ip-cli-heading" class="ip-card__heading">
              {{ i18n.t('install.cli.heading') }}
            </h2>
          </div>

          <p class="ip-card__desc">{{ i18n.t('install.cli.desc') }}</p>

          <div class="ip-code-block" role="region" [attr.aria-label]="i18n.t('install.cli.command-aria')">
            <code class="ip-code-block__code">npm i -g {{ cliPackage }}</code>
            <button
              type="button"
              class="ip-code-block__copy-btn"
              [attr.aria-label]="i18n.t('install.copy-btn')"
              (click)="copyToClipboard('npm i -g ' + cliPackage, 'cli')"
            >
              {{ copiedCli() ? i18n.t('install.copied') : i18n.t('install.copy-btn') }}
            </button>
          </div>

          <p class="ip-card__note">{{ i18n.t('install.cli.note') }}</p>
        </z-card>
      </section>

      <!-- ================================================================= -->
      <!-- SECTION 2: Install a plugin                                        -->
      <!-- ================================================================= -->
      <section class="ip-section" aria-labelledby="ip-plugin-heading">
        <z-card class="ip-card">
          <div class="ip-card__header">
            <span class="ip-card__icon" aria-hidden="true">🔌</span>
            <h2 id="ip-plugin-heading" class="ip-card__heading">
              {{ i18n.t('install.plugin.heading') }}
            </h2>
          </div>

          <p class="ip-card__desc">{{ i18n.t('install.plugin.desc') }}</p>

          <div class="ip-code-block" role="region" [attr.aria-label]="i18n.t('install.plugin.command-aria')">
            <code class="ip-code-block__code">claude-plugin install &lt;name&gt;</code>
            <button
              type="button"
              class="ip-code-block__copy-btn"
              [attr.aria-label]="i18n.t('install.copy-btn')"
              (click)="copyToClipboard('claude-plugin install <name>', 'plugin')"
            >
              {{ copiedPlugin() ? i18n.t('install.copied') : i18n.t('install.copy-btn') }}
            </button>
          </div>

          <ul class="ip-steps" role="list">
            @for (step of pluginInstallSteps; track step.key) {
              <li class="ip-steps__item">
                <span class="ip-steps__num" aria-hidden="true">{{ step.num }}</span>
                <span>{{ i18n.t(step.key) }}</span>
              </li>
            }
          </ul>
        </z-card>
      </section>

      <!-- ================================================================= -->
      <!-- SECTION 3: About plugins                                           -->
      <!-- ================================================================= -->
      <section class="ip-section" aria-labelledby="ip-about-heading">
        <z-card class="ip-card">
          <div class="ip-card__header">
            <span class="ip-card__icon" aria-hidden="true">🧩</span>
            <h2 id="ip-about-heading" class="ip-card__heading">
              {{ i18n.t('install.about.heading') }}
            </h2>
          </div>

          <p class="ip-card__desc">{{ i18n.t('install.about.desc') }}</p>

          <div class="ip-about-types">
            @for (type of pluginTypes; track type.key) {
              <div class="ip-about-types__item">
                <z-badge zType="outline">{{ i18n.t(type.key) }}</z-badge>
                <span class="ip-about-types__desc">{{ i18n.t(type.descKey) }}</span>
              </div>
            }
          </div>
        </z-card>
      </section>

      <!-- ================================================================= -->
      <!-- CTA                                                                -->
      <!-- ================================================================= -->
      <div class="ip-cta">
        <a
          routerLink="/catalog"
          z-button
          zType="default"
          zSize="lg"
        >{{ i18n.t('install.cta-browse') }}</a>
        <a
          routerLink="/docs"
          z-button
          zType="outline"
          zSize="lg"
        >{{ i18n.t('install.cta-docs') }}</a>
      </div>

    </div>
  `,
  styles: [
    `
      /* ── Hero ──────────────────────────────────────────────────────────── */
      .ip-hero {
        background: var(--secondary);
        color: var(--foreground);
        padding: 4rem 1.5rem 3rem;
        text-align: center;
      }
      .ip-hero__inner {
        max-width: 52rem;
        margin: 0 auto;
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 1rem;
      }
      .ip-hero__badge {
        display: inline-flex;
      }
      .ip-hero__title {
        font-size: clamp(1.75rem, 4vw, 2.75rem);
        font-weight: 800;
        line-height: 1.15;
        letter-spacing: -0.03em;
        margin: 0;
        color: var(--foreground);
      }
      .ip-hero__tagline {
        font-size: 1.0625rem;
        color: color-mix(in oklch, var(--foreground) 70%, transparent);
        max-width: 38rem;
        margin: 0;
        line-height: 1.7;
      }

      /* ── Content wrapper ───────────────────────────────────────────────── */
      .ip-content {
        max-width: 52rem;
        margin: 0 auto;
        padding: 3rem 1.5rem 4rem;
        display: flex;
        flex-direction: column;
        gap: 2rem;
      }

      /* ── Section + card ────────────────────────────────────────────────── */
      .ip-section {
        display: contents;
      }
      .ip-card {
        display: flex;
        flex-direction: column;
        gap: 1rem;
      }
      .ip-card__header {
        display: flex;
        align-items: center;
        gap: 0.75rem;
      }
      .ip-card__icon {
        font-size: 1.5rem;
        line-height: 1;
      }
      .ip-card__heading {
        font-size: 1.25rem;
        font-weight: 700;
        color: var(--card-foreground);
        margin: 0;
        letter-spacing: -0.02em;
      }
      .ip-card__desc {
        font-size: 0.9375rem;
        color: var(--muted-foreground);
        line-height: 1.7;
        margin: 0;
      }
      .ip-card__note {
        font-size: 0.8125rem;
        color: var(--muted-foreground);
        margin: 0;
        font-style: italic;
      }

      /* ── Code block (dark, matching landing page style) ────────────────── */
      .ip-code-block {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        background: var(--foreground);
        color: var(--background);
        padding: 0.75rem 1.25rem;
        border-radius: 0.5rem;
        font-family: ui-monospace, SFMono-Regular, 'SF Mono', Menlo, Consolas, 'Liberation Mono', monospace;
        box-shadow: 0 2px 12px color-mix(in oklch, var(--foreground) 20%, transparent);
        overflow-x: auto;
      }
      .ip-code-block__code {
        font-size: 0.9375rem;
        color: inherit;
        user-select: all;
        white-space: nowrap;
        flex: 1;
      }
      .ip-code-block__copy-btn {
        flex-shrink: 0;
        padding: 0.25rem 0.625rem;
        border: 1px solid color-mix(in oklch, var(--background) 35%, transparent);
        border-radius: 0.25rem;
        background: color-mix(in oklch, var(--background) 12%, transparent);
        color: var(--background);
        font-size: 0.8125rem;
        cursor: pointer;
        transition: background 0.15s, color 0.15s;
        line-height: 1.4;
      }
      .ip-code-block__copy-btn:hover {
        background: color-mix(in oklch, var(--background) 22%, transparent);
      }
      .ip-code-block__copy-btn:focus-visible {
        outline: 2px solid var(--background);
        outline-offset: 2px;
      }

      /* ── Steps list ────────────────────────────────────────────────────── */
      .ip-steps {
        list-style: none;
        margin: 0;
        padding: 0;
        display: flex;
        flex-direction: column;
        gap: 0.625rem;
      }
      .ip-steps__item {
        display: flex;
        align-items: flex-start;
        gap: 0.75rem;
        font-size: 0.9375rem;
        color: var(--muted-foreground);
      }
      .ip-steps__num {
        display: flex;
        align-items: center;
        justify-content: center;
        width: 1.5rem;
        height: 1.5rem;
        min-width: 1.5rem;
        border-radius: 50%;
        background: var(--primary);
        color: var(--primary-foreground);
        font-size: 0.75rem;
        font-weight: 700;
      }

      /* ── About plugin types grid ───────────────────────────────────────── */
      .ip-about-types {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(14rem, 1fr));
        gap: 0.875rem;
      }
      .ip-about-types__item {
        display: flex;
        align-items: flex-start;
        gap: 0.5rem;
        flex-direction: column;
      }
      .ip-about-types__desc {
        font-size: 0.875rem;
        color: var(--muted-foreground);
        line-height: 1.5;
      }

      /* ── CTA row ───────────────────────────────────────────────────────── */
      .ip-cta {
        display: flex;
        flex-wrap: wrap;
        gap: 0.875rem;
        justify-content: center;
        padding-top: 1rem;
      }

      /* ── Responsive ────────────────────────────────────────────────────── */
      @media (max-width: 640px) {
        .ip-hero {
          padding: 2.5rem 1rem 2rem;
        }
        .ip-code-block {
          flex-direction: column;
          align-items: flex-start;
          gap: 0.5rem;
        }
        .ip-about-types {
          grid-template-columns: 1fr;
        }
      }
    `,
  ],
})
export class InstallPageComponent implements OnInit {
  private readonly seoMetadata = inject(SeoMetadataService);
  private readonly platformId = inject(PLATFORM_ID);
  protected readonly i18n = inject(I18nFacade);

  readonly cliPackage = CLI_PACKAGE;

  readonly copiedCli = signal(false);
  readonly copiedPlugin = signal(false);

  readonly pluginInstallSteps: readonly { readonly num: string; readonly key: string }[] = [
    { num: '1', key: 'install.plugin.step1' },
    { num: '2', key: 'install.plugin.step2' },
    { num: '3', key: 'install.plugin.step3' },
  ];

  readonly pluginTypes: readonly { readonly key: string; readonly descKey: string }[] = [
    { key: 'install.about.type-tool', descKey: 'install.about.type-tool-desc' },
    { key: 'install.about.type-skill', descKey: 'install.about.type-skill-desc' },
    { key: 'install.about.type-formatter', descKey: 'install.about.type-formatter-desc' },
    { key: 'install.about.type-hook', descKey: 'install.about.type-hook-desc' },
  ];

  ngOnInit(): void {
    this.seoMetadata.setMetadata({
      title: this.i18n.t('install.seo.title'),
      description: this.i18n.t('install.seo.description'),
      ogTitle: this.i18n.t('install.seo.og-title'),
      ogDescription: this.i18n.t('install.seo.og-description'),
      ogType: 'website',
      ogUrl: 'https://claudeforge.dev/install',
      ogImage: 'https://claudeforge.dev/assets/og-image.png',
      twitterCard: 'summary_large_image',
      twitterTitle: this.i18n.t('install.seo.twitter-title'),
      twitterDescription: this.i18n.t('install.seo.twitter-description'),
    });
  }

  /**
   * Copies the given text to the clipboard.
   * SSR-safe: guarded by isPlatformBrowser.
   */
  copyToClipboard(text: string, target: 'cli' | 'plugin'): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    if (typeof navigator === 'undefined' || !navigator.clipboard) {
      return;
    }
    void navigator.clipboard.writeText(text).then(() => {
      if (target === 'cli') {
        this.copiedCli.set(true);
        setTimeout(() => this.copiedCli.set(false), 2000);
      } else {
        this.copiedPlugin.set(true);
        setTimeout(() => this.copiedPlugin.set(false), 2000);
      }
    });
  }
}
