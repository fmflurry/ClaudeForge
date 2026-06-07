/**
 * Signal-based store for the Auth domain.
 * The access token lives ONLY in the in-memory signal.
 * It must NEVER be written to localStorage, sessionStorage, or any cookie.
 */

import { Injectable } from '@angular/core';
import { BaseStore } from '../../../../shared/application/store/base-store';
import type { ResourceState } from '../../../../shared/application/store/resource-state.model';
import type { AuthStatus, AuthToken, CurrentUser } from '../../domain/models/auth.models';

/**
 * Const object (not enum) so values are string literals — required for the
 * spec's `'AUTH' as 'AUTH'` cast to satisfy the BaseStore key constraint.
 */
export const AuthStoreEnum = {
  AUTH: 'AUTH',
} as const;

export type AuthStoreEnumType = typeof AuthStoreEnum;

export interface AuthStoreData {
  status: AuthStatus;
  user: CurrentUser | undefined;
  token: AuthToken | undefined; // access token — in memory only
  activeOrgId: string | undefined;
  errorMessage: string | undefined;
}

export interface AuthState {
  [AuthStoreEnum.AUTH]: ResourceState<AuthStoreData>;
}

@Injectable({ providedIn: 'root' })
export class AuthStore extends BaseStore<AuthStoreEnumType, AuthState> {
  constructor() {
    super(AuthStoreEnum);
  }
}
