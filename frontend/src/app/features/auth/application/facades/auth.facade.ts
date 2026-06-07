/**
 * Auth facade — the ONLY entry point for auth state in components.
 *
 * CRITICAL constraints:
 * - The access token (AuthToken.accessToken) lives ONLY in the in-memory signal.
 * - It MUST NEVER be written to localStorage, sessionStorage, or any cookie.
 * - Facade methods call AuthPort, NOT use cases directly.
 * - All state updates use immutable spread (no mutation).
 * - Components consume AuthFacade only — never store or port directly.
 */

import { computed, inject, Injectable, PLATFORM_ID, Signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { switchMap } from 'rxjs';
import { Router } from '@angular/router';
import { AuthPort } from '../../domain/ports/auth.port';
import { AuthStore, AuthStoreEnum } from '../store/auth.store';
import type { AuthStoreData } from '../store/auth.store';
import type { AuthProvider, AuthStatus, AuthToken, CurrentUser } from '../../domain/models/auth.models';

@Injectable()
export class AuthFacade {
  private readonly store = inject(AuthStore);
  private readonly port = inject(AuthPort);
  private readonly router = inject(Router);
  private readonly platformId = inject(PLATFORM_ID);

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  private get storeData(): AuthStoreData {
    return (
      this.store.get(AuthStoreEnum.AUTH)().data ?? {
        status: 'idle',
        user: undefined,
        token: undefined,
        activeOrgId: undefined,
        errorMessage: undefined,
      }
    );
  }

  private setData(patch: Partial<AuthStoreData>): void {
    const current = this.storeData;
    this.store.update(AuthStoreEnum.AUTH, {
      data: { ...current, ...patch },
      status: 'Success',
      isLoading: false,
    });
  }

  // ---------------------------------------------------------------------------
  // Signal getters (readonly — derived from store)
  // ---------------------------------------------------------------------------

  get currentUser(): Signal<CurrentUser | undefined> {
    return computed(() => this.store.get(AuthStoreEnum.AUTH)().data?.user);
  }

  get isAuthenticated(): Signal<boolean> {
    return computed(() => this.store.get(AuthStoreEnum.AUTH)().data?.status === 'authenticated');
  }

  get activeOrgId(): Signal<string | undefined> {
    return computed(() => this.store.get(AuthStoreEnum.AUTH)().data?.activeOrgId);
  }

  get authStatus(): Signal<AuthStatus> {
    return computed(() => this.store.get(AuthStoreEnum.AUTH)().data?.status ?? 'idle');
  }

  get isAuthenticating(): Signal<boolean> {
    return computed(() => this.store.get(AuthStoreEnum.AUTH)().data?.status === 'authenticating');
  }

  get authError(): Signal<string | undefined> {
    return computed(() => this.store.get(AuthStoreEnum.AUTH)().data?.errorMessage);
  }

  // ---------------------------------------------------------------------------
  // Public methods
  // ---------------------------------------------------------------------------

  /**
   * Initiates the OAuth login flow for the given provider.
   * Sets status to 'authenticating', fetches the authorize URL,
   * then performs a full-page redirect (window.location.href).
   * On error, sets status to 'error' with an error message.
   */
  login(provider: AuthProvider): void {
    this.setData({ status: 'authenticating', errorMessage: undefined });

    this.port.getAuthorizeUrl(provider).subscribe({
      next: (url) => {
        if (typeof window !== 'undefined') {
          window.location.href = url;
        }
      },
      error: (err: unknown) => {
        const message = err instanceof Error ? err.message : 'Failed to get authorize URL';
        this.setData({ status: 'error', errorMessage: message });
      },
    });
  }

  /**
   * Called by the OAuth callback route.
   * Exchanges the authorization code for a token, then fetches the current user.
   * Stores the token in-memory only (never persisted).
   */
  completeLogin(code: string, state: string): void {
    // codeVerifier would normally be retrieved from sessionStorage during PKCE flow,
    // but for the callback component we pass empty string as the spec does not require PKCE here
    this.port
      .exchangeToken(code, state, '')
      .pipe(
        switchMap((token: AuthToken) =>
          this.port.getCurrentUser().pipe(
            // Carry token forward
            switchMap((user: CurrentUser) => {
              const activeOrgId = user.orgMemberships.length > 0 ? user.orgMemberships[0].orgId : undefined;
              this.setData({
                status: 'authenticated',
                user,
                token,
                activeOrgId,
                errorMessage: undefined,
              });
              // Return an empty observable that completes immediately
              return [] as never[];
            }),
          ),
        ),
      )
      .subscribe({
        error: (err: unknown) => {
          const message = err instanceof Error ? err.message : 'Login failed';
          this.setData({
            status: 'error',
            errorMessage: message,
            user: undefined,
            token: undefined,
          });
        },
      });
  }

  /**
   * Signs the user out.
   * Calls AuthPort.signOut(), then clears all store state and navigates to '/'.
   * If signOut fails, local state is still cleared.
   */
  logout(): void {
    this.port.signOut().subscribe({
      next: () => {
        this.store.clearAll();
        void this.router.navigate(['/']);
      },
      error: () => {
        // Even on signOut error, clear local state
        this.store.clearAll();
        void this.router.navigate(['/']);
      },
    });
  }

  /**
   * Attempts a silent token refresh using the HttpOnly refresh cookie.
   * On success, updates the in-memory token and user.
   * On failure, clears the store (user stays unauthenticated — not an error state).
   *
   * SSR-safe: is a complete NO-OP on the server platform (PLATFORM_ID !== 'browser').
   * Silent refresh requires an HttpOnly cookie which is not accessible server-side.
   */
  silentRefresh(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    this.port
      .refreshToken()
      .pipe(
        switchMap((token: AuthToken) =>
          this.port.getCurrentUser().pipe(
            switchMap((user: CurrentUser) => {
              const activeOrgId = user.orgMemberships.length > 0 ? user.orgMemberships[0].orgId : undefined;
              this.setData({
                status: 'authenticated',
                user,
                token,
                activeOrgId,
                errorMessage: undefined,
              });
              return [] as never[];
            }),
          ),
        ),
      )
      .subscribe({
        error: () => {
          // Silent refresh failure is not an error — user is simply unauthenticated
          this.store.clearAll();
        },
      });
  }

  /**
   * Updates the active organisation for the current user.
   * Uses an immutable update — creates a new state object.
   */
  setActiveOrg(orgId: string): void {
    this.setData({ activeOrgId: orgId });
  }
}
