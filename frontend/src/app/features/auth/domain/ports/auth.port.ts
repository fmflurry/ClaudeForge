/**
 * Abstract port for the Auth domain.
 * Infrastructure adapters implement this interface.
 * The facade and interceptor depend on this abstract class, not the adapter.
 */

import { Observable } from 'rxjs';
import type { AuthProvider, AuthToken, CurrentUser } from '../models/auth.models';

export abstract class AuthPort {
  abstract getAuthorizeUrl(provider: AuthProvider): Observable<string>;

  abstract exchangeToken(code: string, state: string, codeVerifier: string): Observable<AuthToken>;

  abstract refreshToken(): Observable<AuthToken>;

  abstract getCurrentUser(): Observable<CurrentUser>;

  abstract signOut(): Observable<void>;
}
