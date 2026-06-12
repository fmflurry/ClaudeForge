import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'cf-control-center-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cc-shell">
      <aside class="cc-shell__sidebar">
        <h2 class="cc-shell__title">Control Center</h2>
        <nav class="cc-shell__nav">
          <a
            routerLink="/control-center"
            routerLinkActive="cc-shell__nav-link--active"
            [routerLinkActiveOptions]="{ exact: true }"
            class="cc-shell__nav-link"
          >
            Overview
          </a>
          <a
            routerLink="/control-center/analysis"
            routerLinkActive="cc-shell__nav-link--active"
            class="cc-shell__nav-link"
            >Analysis</a
          >
          <a
            routerLink="/control-center/appeals"
            routerLinkActive="cc-shell__nav-link--active"
            class="cc-shell__nav-link"
            >Appeals</a
          >
          <a
            routerLink="/control-center/metrics"
            routerLinkActive="cc-shell__nav-link--active"
            class="cc-shell__nav-link"
            >Metrics</a
          >
          <a
            routerLink="/control-center/config"
            routerLinkActive="cc-shell__nav-link--active"
            class="cc-shell__nav-link"
            >Configuration</a
          >
          <a
            routerLink="/control-center/organizations"
            routerLinkActive="cc-shell__nav-link--active"
            class="cc-shell__nav-link"
            >Organizations</a
          >
          <a routerLink="/control-center/audit" routerLinkActive="cc-shell__nav-link--active" class="cc-shell__nav-link"
            >Audit Log</a
          >
        </nav>
      </aside>
      <main class="cc-shell__content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: [
    `
      .cc-shell {
        display: flex;
        min-height: calc(100vh - 80px);
      }
      .cc-shell__sidebar {
        width: 240px;
        background: var(--sidebar);
        color: var(--sidebar-foreground);
        padding: 1rem;
        border-right: 1px solid var(--sidebar-border);
      }
      .cc-shell__title {
        font-size: 1.25rem;
        font-weight: 600;
        margin: 0 0 1rem;
      }
      .cc-shell__nav {
        display: flex;
        flex-direction: column;
        gap: 0.25rem;
      }
      .cc-shell__nav-link {
        color: var(--sidebar-foreground);
        opacity: 0.8;
        text-decoration: none;
        padding: 0.5rem 0.75rem;
        border-radius: var(--radius-sm, 0.25rem);
        transition:
          background 0.2s,
          opacity 0.2s;
      }
      .cc-shell__nav-link:hover {
        opacity: 1;
        background: var(--sidebar-accent);
      }
      .cc-shell__nav-link--active {
        opacity: 1;
        background: var(--sidebar-accent);
        font-weight: 600;
      }
      .cc-shell__content {
        flex: 1;
        padding: 1.5rem;
        overflow-y: auto;
      }
    `,
  ],
})
export class ControlCenterShellComponent {}
