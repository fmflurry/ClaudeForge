/**
 * Functional HTTP interceptor for auth:
 *
 * 1. ATTACH: Adds Authorization: Bearer <token> from the in-memory AuthStore.
 * 2. SKIP-LIST: Does not attach Bearer or intercept 401 for:
 *    - Public catalog paths: /api/v1/plugins, /api/v1/search, /api/v1/categories
 *    - Auth endpoints: /auth/authorize, /auth/token, /auth/refresh
 *    - External origins
 * 3. ON 401: Single-flight refresh via shared shareReplay(1) observable.
 *    - Success: updates in-memory token, retries request with new Bearer header.
 *    - Failure: clears AuthStore, navigates to /login, propagates the error.
 * 4. Non-401 errors pass through unchanged.
 */

import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, shareReplay, switchMap, tap, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AuthPort } from '../../domain/ports/auth.port';
import { AuthStore, AuthStoreEnum } from '../../application/store/auth.store';
import type { AuthToken } from '../../domain/models/auth.models';

// ---------------------------------------------------------------------------
// Skip-list configuration
// ---------------------------------------------------------------------------

/**
 * Path prefixes that should bypass Bearer attachment and 401 interception.
 * These are matched as prefix matches against the request URL path.
 */
const SKIP_PREFIXES: readonly string[] = [
  // Public catalog/search/download paths
  '/api/v1/plugins',
  '/api/v1/search',
  '/api/v1/categories',
  // Auth endpoints (must not intercept their own 401s)
  '/auth/authorize',
  '/auth/token',
  '/auth/refresh',
];

function isSkipped(req: HttpRequest<unknown>): boolean {
  let path: string;
  try {
    const url = new URL(req.url, window.location.origin);
    // External origin → skip
    if (url.origin !== window.location.origin) {
      return true;
    }
    path = url.pathname;
  } catch {
    path = req.url;
  }

  return SKIP_PREFIXES.some((prefix) => path.startsWith(prefix));
}

// ---------------------------------------------------------------------------
// Single-flight refresh state
// ---------------------------------------------------------------------------

let pendingRefresh$: Observable<AuthToken> | null = null;

function clearPendingRefresh(): void {
  pendingRefresh$ = null;
}

// ---------------------------------------------------------------------------
// Interceptor
// ---------------------------------------------------------------------------

export function authInterceptor(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> {
  const store = inject(AuthStore);
  const port = inject(AuthPort);
  const router = inject(Router);

  const skip = isSkipped(req);

  // Attach Bearer header if a token is in-memory and the request is not skipped
  const token = store.get(AuthStoreEnum.AUTH)().data?.token?.accessToken;
  const authorizedReq =
    !skip && token
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(authorizedReq).pipe(
    catchError((err: unknown) => {
      if (
        !skip &&
        err instanceof HttpErrorResponse &&
        err.status === 401
      ) {
        return handle401(req, next, store, port, router);
      }
      return throwError(() => err);
    }),
  );
}

function handle401(
  originalReq: HttpRequest<unknown>,
  next: HttpHandlerFn,
  store: AuthStore,
  port: AuthPort,
  router: Router,
): Observable<HttpEvent<unknown>> {
  // Single-flight: reuse the pending refresh observable for concurrent 401s
  if (!pendingRefresh$) {
    pendingRefresh$ = port.refreshToken().pipe(
      tap((newToken: AuthToken) => {
        // Update the in-memory token — NEVER write to localStorage/sessionStorage
        const currentData = store.get(AuthStoreEnum.AUTH)().data;
        store.update(AuthStoreEnum.AUTH, {
          data: {
            status: currentData?.status ?? 'authenticated',
            user: currentData?.user,
            token: newToken,
            activeOrgId: currentData?.activeOrgId,
            errorMessage: currentData?.errorMessage,
          },
          status: 'Success',
          isLoading: false,
        });
      }),
      shareReplay(1),
    );
  }

  return pendingRefresh$.pipe(
    switchMap((newToken: AuthToken) => {
      clearPendingRefresh();
      const retryReq = originalReq.clone({
        setHeaders: { Authorization: `Bearer ${newToken.accessToken}` },
      });
      return next(retryReq);
    }),
    catchError((refreshErr: unknown) => {
      clearPendingRefresh();
      store.clearAll();
      void router.navigate(['/login']);
      return throwError(() => refreshErr);
    }),
  );
}
