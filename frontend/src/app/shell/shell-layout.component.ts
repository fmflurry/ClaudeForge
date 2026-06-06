import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/**
 * Main application shell — header, primary navigation, and router outlet.
 * Used as the top-level layout wrapper for all feature routes.
 */
@Component({
  selector: 'cf-shell-layout',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-shell">
      <header class="cf-shell__header">
        <div class="cf-shell__brand">
          <span class="cf-shell__logo">ClaudeForge</span>
        </div>
        <nav class="cf-shell__nav" aria-label="Main navigation">
          <a routerLink="/catalog" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            Catalog
          </a>
          <a routerLink="/search" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            Search
          </a>
          <a routerLink="/dashboard" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            Dashboard
          </a>
          <a routerLink="/docs" routerLinkActive="cf-shell__nav-link--active" class="cf-shell__nav-link">
            Docs
          </a>
        </nav>
      </header>
      <main class="cf-shell__content">
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
    `,
  ],
})
export class ShellLayoutComponent {}
