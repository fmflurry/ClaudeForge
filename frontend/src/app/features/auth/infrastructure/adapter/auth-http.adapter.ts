/**
 * HTTP adapter implementing AuthPort.
 * Maps API DTOs to domain models via auth mappers.
 *
 * Endpoint conventions:
 *   GET  /auth/authorize?provider=<provider>   → returns { authorize_url: string }
 *   POST /auth/token                           → exchanges code for token
 *   POST /auth/refresh                         → refreshes token via HttpOnly cookie (withCredentials)
 *   GET  /auth/me                              → returns the current user
 *   POST /auth/signout                         → signs the user out
 */

import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../../../core/config/api-config';
import { AuthPort } from '../../domain/ports/auth.port';
import type { AuthProvider, AuthToken, CurrentUser } from '../../domain/models/auth.models';
import type { CurrentUserDto, TokenResponseDto } from '../../domain/mappers/auth.mapper';
import {
  mapCurrentUserDtoToCurrentUser,
  mapTokenResponseToAuthToken,
} from '../../domain/mappers/auth.mapper';

interface AuthorizeUrlResponse {
  authorize_url: string;
}

interface TokenRequestBody {
  code: string;
  state: string;
  code_verifier: string;
}

@Injectable()
export class AuthHttpAdapter extends AuthPort {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getAuthorizeUrl(provider: AuthProvider): Observable<string> {
    return this.http
      .get<AuthorizeUrlResponse>(`${this.baseUrl}/auth/authorize`, {
        params: { provider },
      })
      .pipe(map((res) => res.authorize_url));
  }

  exchangeToken(code: string, state: string, codeVerifier: string): Observable<AuthToken> {
    const body: TokenRequestBody = {
      code,
      state,
      code_verifier: codeVerifier,
    };
    return this.http
      .post<TokenResponseDto>(`${this.baseUrl}/auth/token`, body, {
        withCredentials: true,
      })
      .pipe(map(mapTokenResponseToAuthToken));
  }

  refreshToken(): Observable<AuthToken> {
    return this.http
      .post<TokenResponseDto>(`${this.baseUrl}/auth/refresh`, {}, {
        withCredentials: true,
      })
      .pipe(map(mapTokenResponseToAuthToken));
  }

  getCurrentUser(): Observable<CurrentUser> {
    return this.http
      .get<CurrentUserDto>(`${this.baseUrl}/auth/me`)
      .pipe(map(mapCurrentUserDtoToCurrentUser));
  }

  signOut(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/auth/signout`, {}, {
      withCredentials: true,
    });
  }
}
