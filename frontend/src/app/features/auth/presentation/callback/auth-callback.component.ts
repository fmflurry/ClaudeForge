/**
 * OAuth callback component.
 * Reads `code` and `state` query parameters from the URL,
 * calls AuthFacade.completeLogin(code, state), then navigates home.
 */

import { ChangeDetectionStrategy, Component, inject, OnInit, Signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthFacade } from '../../application/facades/auth.facade';
import type { AuthStatus } from '../../domain/models/auth.models';

@Component({
  selector: 'cf-auth-callback',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-callback">
      @if (authStatus() === 'error') {
        <div class="cf-callback__error" role="alert">
          <h2 class="cf-callback__error-title">Sign-in failed</h2>
          <p class="cf-callback__error-message">{{ authError() }}</p>
          <a href="/login" class="cf-callback__retry">Try again</a>
        </div>
      } @else {
        <div class="cf-callback__loading" role="status" aria-live="polite" aria-label="Completing sign-in">
          <p>Completing sign-in…</p>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .cf-callback {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: #f9fafb;
        padding: 1.5rem;
      }

      .cf-callback__loading {
        text-align: center;
        color: #6b7280;
        font-size: 1rem;
      }

      .cf-callback__error {
        background: #fff;
        border-radius: 0.75rem;
        box-shadow: 0 4px 24px rgba(0, 0, 0, 0.08);
        padding: 2.5rem;
        max-width: 24rem;
        text-align: center;
      }

      .cf-callback__error-title {
        font-size: 1.25rem;
        font-weight: 700;
        color: #b91c1c;
        margin: 0 0 0.75rem;
      }

      .cf-callback__error-message {
        font-size: 0.9375rem;
        color: #4b5563;
        margin: 0 0 1.5rem;
      }

      .cf-callback__retry {
        color: #3b82f6;
        text-decoration: underline;
        font-size: 0.9375rem;
      }
    `,
  ],
})
export class AuthCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly authFacade = inject(AuthFacade);

  readonly authStatus: Signal<AuthStatus> = this.authFacade.authStatus;
  readonly authError: Signal<string | undefined> = this.authFacade.authError;

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code') ?? '';
    const state = this.route.snapshot.queryParamMap.get('state') ?? '';

    this.authFacade.completeLogin(code, state);

    // Navigate home after the synchronous signal update settles.
    void Promise.resolve().then(() => {
      if (this.authFacade.isAuthenticated()) {
        void this.router.navigate(['/']);
      }
    });
  }
}
