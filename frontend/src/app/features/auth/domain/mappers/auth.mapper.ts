/**
 * Mappers for the Auth domain.
 * Maps API DTOs to domain models.
 */

import type { AuthToken, CurrentUser, OrgMembership } from '../models/auth.models';

// ---------------------------------------------------------------------------
// DTOs (API wire shapes)
// ---------------------------------------------------------------------------

export interface TokenResponseDto {
  access_token: string;
}

export interface OrgMembershipDto {
  org_id: string;
  org_name: string;
  role: 'owner' | 'admin' | 'member';
}

export interface CurrentUserDto {
  user_id: string;
  email: string;
  display_name: string;
  org_memberships: OrgMembershipDto[];
}

// ---------------------------------------------------------------------------
// Mapper functions
// ---------------------------------------------------------------------------

export function mapTokenResponseToAuthToken(dto: TokenResponseDto): AuthToken {
  return {
    accessToken: dto.access_token,
  };
}

export function mapOrgMembershipDtoToOrgMembership(dto: OrgMembershipDto): OrgMembership {
  return {
    orgId: dto.org_id,
    orgName: dto.org_name,
    role: dto.role,
  };
}

export function mapCurrentUserDtoToCurrentUser(dto: CurrentUserDto): CurrentUser {
  return {
    userId: dto.user_id,
    email: dto.email,
    displayName: dto.display_name,
    orgMemberships: dto.org_memberships.map(mapOrgMembershipDtoToOrgMembership),
  };
}
