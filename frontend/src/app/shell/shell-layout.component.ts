import { ChangeDetectionStrategy, Component, inject, OnDestroy, OnInit, Signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
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
import { ZardButtonComponent } from '../shared/components/button';

/**
 * Main application shell — header, primary navigation, and router outlet.
 * Used as the top-level layout wrapper for all feature routes.
 * Includes current-user indicator: shows email + sign-out when authenticated,
 * sign-in link otherwise.
 */
@Component({
  selector: 'cf-shell-layout',
  standalone: true,
  providers: [AuthFacade, OrgContextFacade],
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    OrgSwitcherComponent,
    LanguageSwitcherComponent,
    ThemeToggleComponent,
    ZardButtonComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-shell">
      <header class="cf-shell__header">
        <a routerLink="/" class="cf-shell__brand-link" aria-label="ClaudeForge home">
          <img src="/logo-assets/claudeforge-header-logo-fit.png" alt="ClaudeForge" class="cf-shell__logo-img" />
        </a>
        <nav class="cf-shell__nav" [attr.aria-label]="i18n.t('shell.nav-aria')">
          <a routerLink="/catalog" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            {{ i18n.t('shell.nav.catalog') }}
          </a>
          @if (currentUser()) {
            <a routerLink="/dashboard" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
              {{ i18n.t('shell.nav.dashboard') }}
            </a>
          }
          <a routerLink="/docs" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            {{ i18n.t('shell.nav.docs') }}
          </a>
        </nav>
        <div class="cf-shell__orgs">
          <cf-org-switcher />
        </div>
        <div class="cf-shell__controls">
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
                z-button
                zType="outline"
                zSize="sm"
                type="button"
                class="cf-shell__sign-out cf-shell__sign-out--zard"
                (click)="onSignOut()"
                [attr.aria-label]="i18n.t('shell.auth.sign-out')"
              >
                {{ i18n.t('shell.auth.sign-out') }}
              </button>
            } @else {
              <a
                z-button
                zType="outline"
                zSize="sm"
                routerLink="/login"
                class="cf-shell__sign-in cf-shell__sign-in--zard"
                [attr.aria-label]="i18n.t('shell.auth.sign-in')"
              >
                {{ i18n.t('shell.auth.sign-in') }}
              </a>
            }
          </div>
        </div>
      </header>
      <main class="cf-shell__content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [
    `
      /*
       * Shell layout styles — all colors reference semantic CSS custom property tokens (D3 rule).
       * No hardcoded hex/rgb values; use var(--token-name) for all theme-able colors.
       * The header uses --sidebar tokens (a dark/branded surface) to provide a distinct
       * background separate from the main --background content area.
       */

      .cf-shell {
        display: flex;
        flex-direction: column;
        min-height: 100vh;
      }

      .cf-shell__header {
        display: flex;
        align-items: center;
        gap: 2rem;
        padding: 0.875rem 1.5rem;
        min-height: 80px;
        background: var(--sidebar);
        color: var(--sidebar-foreground);
        border-bottom: 1px solid var(--sidebar-border);
      }

      .cf-shell__brand-link {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        text-decoration: none;
        color: inherit;
      }

      .cf-shell__brand-link:focus-visible {
        outline: 2px solid var(--sidebar-ring);
        outline-offset: 2px;
        border-radius: 0.25rem;
      }

      .cf-shell__logo-img {
        display: block;
        height: 64px;
        width: auto;
        object-fit: contain;
        flex-shrink: 0;
      }

      .cf-shell__nav {
        display: flex;
        gap: 1rem;
      }

      .cf-shell__nav-link {
        color: var(--sidebar-foreground);
        opacity: 0.8;
        text-decoration: none;
        padding: 0.25rem 0.5rem;
        border-radius: var(--radius-sm, 0.25rem);
        transition:
          color 0.2s ease,
          background-color 0.2s ease,
          opacity 0.2s ease;
      }

      .cf-shell__nav-link:hover,
      .cf-shell__nav-link--active {
        opacity: 1;
        color: var(--sidebar-foreground);
        background: var(--sidebar-accent);
      }

      .cf-shell__nav-link:focus-visible {
        outline: 2px solid var(--sidebar-ring);
        outline-offset: 2px;
        opacity: 1;
      }

      .cf-shell__content {
        flex: 1;
        padding: 1.5rem;
      }

      /* Right-aligned cluster: language → theme → auth (D8 revamp-landing-page).
         margin-left:auto pushes this entire cluster to the far right of the header. */
      .cf-shell__controls {
        margin-left: auto;
        display: flex;
        align-items: center;
        gap: 0.5rem;
      }

      .cf-shell__auth {
        display: flex;
        align-items: center;
        gap: 0.75rem;
      }

      .cf-shell__user-email {
        font-size: 0.875rem;
        color: var(--sidebar-foreground);
        opacity: 0.85;
        max-width: 14rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      /*
       * ZardUI button overrides for the shell header surface.
       * The header uses --sidebar tokens so we need the button border/text
       * to contrast against the sidebar background rather than --background.
       * --zard suffix classes co-exist with the existing selector classes so
       * specs that look up .cf-shell__sign-out / .cf-shell__sign-in still work.
       */
      .cf-shell__sign-out--zard,
      .cf-shell__sign-in--zard {
        /* Transparent so the navy header shows through behind the light text/border.
           Without this, the ZardUI outline default (--background) renders a light fill,
           putting near-white --sidebar-foreground text on a near-white surface. */
        background-color: transparent;
        border-color: var(--sidebar-foreground);
        color: var(--sidebar-foreground);
      }

      .cf-shell__sign-out--zard:hover,
      .cf-shell__sign-in--zard:hover {
        background-color: var(--sidebar-accent);
        color: var(--sidebar-accent-foreground);
      }

      .cf-shell__sign-out--zard:focus-visible,
      .cf-shell__sign-in--zard:focus-visible {
        outline: 2px solid var(--sidebar-ring);
        outline-offset: 2px;
      }
    `,
  ],
})
export class ShellLayoutComponent implements OnInit, OnDestroy {
  private readonly authFacade = inject(AuthFacade);
  private readonly catalogFacade = inject(CatalogFacade);
  protected readonly i18n = inject(I18nFacade);

  readonly currentUser: Signal<CurrentUser | undefined> = this.authFacade.currentUser;

  private unsubscribeOrgSwitch: (() => void) | undefined;

  ngOnInit(): void {
    // Subscribe to org-switch events via the contextRegistry singleton.
    // No cross-domain facade injection — catalog reload is triggered by the event.
    this.unsubscribeOrgSwitch = contextRegistry.subscribe<ActiveOrgSwitchedPayload>(ORG_ACTIVE_ORG_SWITCHED, () => {
      this.catalogFacade.loadAddOns();
    });
  }

  ngOnDestroy(): void {
    this.unsubscribeOrgSwitch?.();
  }

  onSignOut(): void {
    this.authFacade.logout();
  }
}
