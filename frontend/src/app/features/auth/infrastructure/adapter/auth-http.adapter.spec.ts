/**
 * Unit tests for AuthHttpAdapter.
 * Uses HttpTestingController to verify URL/body/withCredentials shapes
 * and response mapping to domain models.
 */

import { TestBed } from '@angular/core/testing';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthHttpAdapter } from './auth-http.adapter';
import { API_BASE_URL } from '../../../../core/config/api-config';
import { AuthPort } from '../../domain/ports/auth.port';
import type { AuthProvider, AuthToken, CurrentUser } from '../../domain/models/auth.models';
import type { TokenResponseDto, CurrentUserDto } from '../../domain/mappers/auth.mapper';

const BASE = 'https://api.test';

function setup(): { adapter: AuthHttpAdapter; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: BASE },
      { provide: AuthPort, useClass: AuthHttpAdapter },
      AuthHttpAdapter,
    ],
  });
  return {
    adapter: TestBed.inject(AuthHttpAdapter),
    http: TestBed.inject(HttpTestingController),
  };
}

// ---------------------------------------------------------------------------
// getAuthorizeUrl
// ---------------------------------------------------------------------------

describe('AuthHttpAdapter — getAuthorizeUrl', () => {
  it('should GET /auth/authorize with provider=google param', () => {
    const { adapter, http } = setup();
    let result: string | undefined;
    adapter.getAuthorizeUrl('google').subscribe((url) => (result = url));

    const req = http.expectOne(
      (r) => r.url === `${BASE}/auth/authorize` && r.method === 'GET',
    );
    expect(req.request.params.get('provider')).toBe('google');
    req.flush({ authorize_url: 'https://accounts.google.com/oauth' });

    expect(result).toBe('https://accounts.google.com/oauth');
    http.verify();
  });

  it('should GET /auth/authorize with provider=microsoft param', () => {
    const { adapter, http } = setup();
    adapter.getAuthorizeUrl('microsoft').subscribe();

    const req = http.expectOne(
      (r) => r.url === `${BASE}/auth/authorize` && r.method === 'GET',
    );
    expect(req.request.params.get('provider')).toBe('microsoft');
    req.flush({ authorize_url: 'https://login.microsoftonline.com/oauth2/v2.0/authorize' });
    http.verify();
  });

  it('should extract authorize_url from response body', () => {
    const { adapter, http } = setup();
    let url = '';
    adapter.getAuthorizeUrl('google').subscribe((u) => (url = u));

    const req = http.expectOne((r) => r.url === `${BASE}/auth/authorize`);
    req.flush({ authorize_url: 'https://provider.example.com/auth' });

    expect(url).toBe('https://provider.example.com/auth');
    http.verify();
  });

  it('should NOT send withCredentials on getAuthorizeUrl', () => {
    const { adapter, http } = setup();
    adapter.getAuthorizeUrl('google').subscribe();

    const req = http.expectOne((r) => r.url === `${BASE}/auth/authorize`);
    expect(req.request.withCredentials).toBe(false);
    req.flush({ authorize_url: 'https://provider.example.com/auth' });
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// exchangeToken (POST /auth/token)
// ---------------------------------------------------------------------------

describe('AuthHttpAdapter — exchangeToken', () => {
  it('should POST to /auth/token', () => {
    const { adapter, http } = setup();
    adapter.exchangeToken('code-1', 'state-1', 'verifier-1').subscribe();

    const req = http.expectOne(`${BASE}/auth/token`);
    expect(req.request.method).toBe('POST');
    req.flush({ access_token: 'tok-abc' });
    http.verify();
  });

  it('should send code, state, and code_verifier in body', () => {
    const { adapter, http } = setup();
    adapter.exchangeToken('the-code', 'the-state', 'the-verifier').subscribe();

    const req = http.expectOne(`${BASE}/auth/token`);
    expect(req.request.body).toEqual({
      code: 'the-code',
      state: 'the-state',
      code_verifier: 'the-verifier',
    });
    req.flush({ access_token: 'tok-abc' });
    http.verify();
  });

  it('should map access_token response to AuthToken.accessToken', () => {
    const { adapter, http } = setup();
    let token: AuthToken | undefined;
    adapter.exchangeToken('c', 's', 'v').subscribe((t) => (token = t));

    const req = http.expectOne(`${BASE}/auth/token`);
    const dto: TokenResponseDto = { access_token: 'my-access-token' };
    req.flush(dto);

    expect(token?.accessToken).toBe('my-access-token');
    http.verify();
  });

  it('should send withCredentials: true on POST /auth/token', () => {
    const { adapter, http } = setup();
    adapter.exchangeToken('c', 's', 'v').subscribe();

    const req = http.expectOne(`${BASE}/auth/token`);
    expect(req.request.withCredentials).toBe(true);
    req.flush({ access_token: 'tok' });
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// refreshToken (POST /auth/refresh)
// ---------------------------------------------------------------------------

describe('AuthHttpAdapter — refreshToken', () => {
  it('should POST to /auth/refresh', () => {
    const { adapter, http } = setup();
    adapter.refreshToken().subscribe();

    const req = http.expectOne(`${BASE}/auth/refresh`);
    expect(req.request.method).toBe('POST');
    req.flush({ access_token: 'refreshed-tok' });
    http.verify();
  });

  it('should send withCredentials: true on refreshToken (HttpOnly cookie flow)', () => {
    const { adapter, http } = setup();
    adapter.refreshToken().subscribe();

    const req = http.expectOne(`${BASE}/auth/refresh`);
    expect(req.request.withCredentials).toBe(true);
    req.flush({ access_token: 'refreshed-tok' });
    http.verify();
  });

  it('should map response to AuthToken', () => {
    const { adapter, http } = setup();
    let token: AuthToken | undefined;
    adapter.refreshToken().subscribe((t) => (token = t));

    const req = http.expectOne(`${BASE}/auth/refresh`);
    req.flush({ access_token: 'new-token-xyz' });

    expect(token?.accessToken).toBe('new-token-xyz');
    http.verify();
  });

  it('should POST an empty body to /auth/refresh', () => {
    const { adapter, http } = setup();
    adapter.refreshToken().subscribe();

    const req = http.expectOne(`${BASE}/auth/refresh`);
    expect(req.request.body).toEqual({});
    req.flush({ access_token: 'tok' });
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// getCurrentUser (GET /auth/me)
// ---------------------------------------------------------------------------

describe('AuthHttpAdapter — getCurrentUser', () => {
  it('should GET /auth/me', () => {
    const { adapter, http } = setup();
    adapter.getCurrentUser().subscribe();

    const req = http.expectOne(`${BASE}/auth/me`);
    expect(req.request.method).toBe('GET');
    req.flush({
      user_id: 'u-1',
      email: 'alice@example.com',
      display_name: 'Alice',
      org_memberships: [],
    });
    http.verify();
  });

  it('should map user_id to userId in domain model', () => {
    const { adapter, http } = setup();
    let user: CurrentUser | undefined;
    adapter.getCurrentUser().subscribe((u) => (user = u));

    const dto: CurrentUserDto = {
      user_id: 'user-uuid-1',
      email: 'alice@example.com',
      display_name: 'Alice Smith',
      org_memberships: [],
    };
    const req = http.expectOne(`${BASE}/auth/me`);
    req.flush(dto);

    expect(user?.userId).toBe('user-uuid-1');
    expect(user?.email).toBe('alice@example.com');
    expect(user?.displayName).toBe('Alice Smith');
    http.verify();
  });

  it('should map org_memberships array', () => {
    const { adapter, http } = setup();
    let user: CurrentUser | undefined;
    adapter.getCurrentUser().subscribe((u) => (user = u));

    const req = http.expectOne(`${BASE}/auth/me`);
    req.flush({
      user_id: 'u-1',
      email: 'a@b.com',
      display_name: 'Alice',
      org_memberships: [
        { org_id: 'org-1', org_name: 'Acme', role: 'owner' },
      ],
    });

    expect(user?.orgMemberships).toHaveLength(1);
    expect(user?.orgMemberships[0].orgId).toBe('org-1');
    http.verify();
  });

  it('should NOT send withCredentials on getCurrentUser', () => {
    const { adapter, http } = setup();
    adapter.getCurrentUser().subscribe();

    const req = http.expectOne(`${BASE}/auth/me`);
    expect(req.request.withCredentials).toBe(false);
    req.flush({
      user_id: 'u-1',
      email: 'a@b.com',
      display_name: 'Alice',
      org_memberships: [],
    });
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// signOut (POST /auth/signout)
// ---------------------------------------------------------------------------

describe('AuthHttpAdapter — signOut', () => {
  it('should POST to /auth/signout', () => {
    const { adapter, http } = setup();
    adapter.signOut().subscribe();

    const req = http.expectOne(`${BASE}/auth/signout`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
    http.verify();
  });

  it('should send withCredentials: true on signOut', () => {
    const { adapter, http } = setup();
    adapter.signOut().subscribe();

    const req = http.expectOne(`${BASE}/auth/signout`);
    expect(req.request.withCredentials).toBe(true);
    req.flush(null);
    http.verify();
  });

  it('should complete without error on 200', () => {
    const { adapter, http } = setup();
    let completed = false;
    adapter.signOut().subscribe({ complete: () => (completed = true) });

    const req = http.expectOne(`${BASE}/auth/signout`);
    req.flush(null);

    expect(completed).toBe(true);
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// Architecture — extends AuthPort
// ---------------------------------------------------------------------------

describe('AuthHttpAdapter — architecture', () => {
  it('should be an instance of AuthPort', () => {
    const { adapter } = setup();
    expect(adapter).toBeInstanceOf(AuthPort);
  });

  it('should not inject HttpClient directly on its public surface', () => {
    // Verify no http property is exposed (encapsulated as private)
    const { adapter } = setup();
    const proto = Object.getPrototypeOf(adapter);
    expect(Object.getOwnPropertyNames(proto)).not.toContain('http');
  });
});
