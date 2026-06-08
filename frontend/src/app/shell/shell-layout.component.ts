import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, Signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TeamContextFacade } from '../features/team-context/application/facades/team-context.facade';
import { TeamContextStore } from '../features/team-context/application/store/team-context.store';
import { TeamWelcomeOverlayComponent } from '../features/team-context/presentation/welcome-overlay/team-welcome-overlay.component';
import { TeamSwitcherComponent } from '../features/team-context/presentation/team-switcher/team-switcher.component';
import { TelemetrySettingsComponent } from '../features/telemetry/presentation/settings/telemetry-settings.component';
import { AuthFacade } from '../features/auth/application/facades/auth.facade';
import type { CurrentUser } from '../features/auth/domain/models/auth.models';
import { OrgSwitcherComponent } from '../features/organizations/presentation/org-switcher/org-switcher.component';
import { OrgContextFacade } from '../features/organizations/application/facades/org-context.facade';
import { contextRegistry } from '../core/context/context-registry';
import { ORG_ACTIVE_ORG_SWITCHED } from '../features/organizations/application/facades/org-context.facade';
import type { ActiveOrgSwitchedPayload } from '../features/organizations/application/facades/org-context.facade';
import { CatalogFacade } from '../features/catalog/application/facades/catalog.facade';
import { LanguageSwitcherComponent } from './language-switcher/language-switcher.component';
import { ThemeToggleComponent } from './theme-toggle/theme-toggle.component';
import { I18nFacade } from '../application/i18n/i18n.facade';

/**
 * Main application shell — header, primary navigation, and router outlet.
 * Used as the top-level layout wrapper for all feature routes.
 * Includes current-user indicator: shows email + sign-out when authenticated,
 * sign-in link otherwise.
 */
@Component({
  selector: 'cf-shell-layout',
  standalone: true,
  providers: [TeamContextStore, TeamContextFacade, AuthFacade, OrgContextFacade],
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TeamWelcomeOverlayComponent,
    TeamSwitcherComponent,
    TelemetrySettingsComponent,
    OrgSwitcherComponent,
    LanguageSwitcherComponent,
    ThemeToggleComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-shell">
      <header class="cf-shell__header">
        <a routerLink="/" class="cf-shell__brand-link" aria-label="ClaudeForge home">
          <img src="hero.png" alt="ClaudeForge logo" class="cf-shell__logo-img" width="32" height="32" />
          <span class="cf-shell__logo">ClaudeForge</span>
        </a>
        <nav class="cf-shell__nav" [attr.aria-label]="i18n.t('shell.nav-aria')">
          <a routerLink="/catalog" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            {{ i18n.t('shell.nav.catalog') }}
          </a>
          <a routerLink="/search" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            {{ i18n.t('shell.nav.search') }}
          </a>
          <a routerLink="/dashboard" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            {{ i18n.t('shell.nav.dashboard') }}
          </a>
          <a routerLink="/docs" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            {{ i18n.t('shell.nav.docs') }}
          </a>
        </nav>
        <div class="cf-shell__team">
          <cf-team-switcher />
        </div>
        <div class="cf-shell__orgs">
          <cf-org-switcher />
        </div>
        <div class="cf-shell__settings">
          <cf-telemetry-settings />
        </div>
        <div class="cf-shell__lang">
          <cf-language-switcher />
        </div>
        <div class="cf-shell__theme">
          <cf-theme-toggle />
        </div>
        <div class="cf-shell__auth" [attr.aria-label]="i18n.t('shell.aria.user-account')">
          @if (currentUser()) {
            <span class="cf-shell__user-email">{{ currentUser()!.email }}</span>
            <button
              type="button"
              class="cf-shell__sign-out"
              (click)="onSignOut()"
              [attr.aria-label]="i18n.t('shell.auth.sign-out')"
            >
              {{ i18n.t('shell.auth.sign-out') }}
            </button>
          } @else {
            <a routerLink="/login" class="cf-shell__sign-in" [attr.aria-label]="i18n.t('shell.auth.sign-in')">
              {{ i18n.t('shell.auth.sign-in') }}
            </a>
          }
        </div>
      </header>
      <main class="cf-shell__content">
        @if (facade.needsInit()) {
          <cf-team-welcome />
        }
        <router-outlet />
      </main>
    </div>
  `,
  styles: [
    `
      .cf-shell {
        display: flex;
        flex-direction: column;
        min-height: 100vh;
      }

      .cf-shell__header {
        display: flex;
        align-items: center;
        gap: 2rem;
        padding: 0.75rem 1.5rem;
        background: #1a1a2e;
        color: #fff;
      }

      .cf-shell__logo {
        font-weight: 700;
        font-size: 1.125rem;
        letter-spacing: -0.025rem;
      }

      .cf-shell__brand-link {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        text-decoration: none;
        color: inherit;
      }

      .cf-shell__brand-link:focus-visible {
        outline: 2px solid #818cf8;
        outline-offset: 2px;
        border-radius: 0.25rem;
      }

      .cf-shell__logo-img {
        display: block;
        width: 32px;
        height: 32px;
        flex-shrink: 0;
      }

      .cf-shell__nav {
        display: flex;
        gap: 1rem;
      }

      .cf-shell__nav-link {
        color: rgba(255, 255, 255, 0.8);
        text-decoration: none;
        padding: 0.25rem 0.5rem;
        border-radius: 0.25rem;
        transition: color 0.2s ease;
      }

      .cf-shell__nav-link:hover,
      .cf-shell__nav-link--active {
        color: #fff;
        background: rgba(255, 255, 255, 0.1);
      }

      .cf-shell__content {
        flex: 1;
        padding: 1.5rem;
      }

      .cf-shell__auth {
        margin-left: auto;
        display: flex;
        align-items: center;
        gap: 0.75rem;
      }

      .cf-shell__user-email {
        font-size: 0.875rem;
        color: rgba(255, 255, 255, 0.85);
        max-width: 14rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .cf-shell__sign-out {
        background: transparent;
        border: 1px solid rgba(255, 255, 255, 0.35);
        color: rgba(255, 255, 255, 0.85);
        padding: 0.25rem 0.75rem;
        border-radius: 0.25rem;
        font-size: 0.875rem;
        cursor: pointer;
        transition: background-color 0.2s ease;
      }

      .cf-shell__sign-out:hover {
        background: rgba(255, 255, 255, 0.1);
      }

      .cf-shell__sign-in {
        color: rgba(255, 255, 255, 0.85);
        text-decoration: none;
        padding: 0.25rem 0.75rem;
        border: 1px solid rgba(255, 255, 255, 0.35);
        border-radius: 0.25rem;
        font-size: 0.875rem;
        transition: background-color 0.2s ease;
      }

      .cf-shell__sign-in:hover {
        background: rgba(255, 255, 255, 0.1);
      }
    `,
  ],
})
export class ShellLayoutComponent implements OnInit, OnDestroy {
  protected readonly facade = inject(TeamContextFacade);
  private readonly authFacade = inject(AuthFacade);
  private readonly catalogFacade = inject(CatalogFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly currentUser: Signal<CurrentUser | undefined> = this.authFacade.currentUser;

  private unsubscribeOrgSwitch: (() => void) | undefined;

  ngOnInit(): void {
    this.facade.init();

    // Subscribe to org-switch events via the contextRegistry singleton.
    // No cross-domain facade injection — catalog reload is triggered by the event.
    this.unsubscribeOrgSwitch = contextRegistry.subscribe<ActiveOrgSwitchedPayload>(ORG_ACTIVE_ORG_SWITCHED, () => {
      this.catalogFacade.loadPlugins();
    });
  }

  ngOnDestroy(): void {
    this.unsubscribeOrgSwitch?.();
  }

  onSignOut(): void {
    this.authFacade.logout();
  }
}
