/**
 * Domain models for the Auth feature.
 * Access tokens live ONLY in the in-memory AuthStore signal —
 * they must NEVER be written to localStorage, sessionStorage, or any cookie.
 */

export type AuthProvider = 'google' | 'microsoft';

export type AuthStatus = 'idle' | 'authenticating' | 'authenticated' | 'error';

export interface OrgMembership {
  orgId: string;
  orgName: string;
  role: 'owner' | 'admin' | 'member';
}

export interface CurrentUser {
  userId: string;
  email: string;
  displayName: string;
  orgMemberships: OrgMembership[];
}

/**
 * CRITICAL: accessToken is stored IN-MEMORY ONLY.
 * It must never be persisted to localStorage, sessionStorage, or cookies.
 */
export interface AuthToken {
  accessToken: string;
}
