/**
 * Organizations domain mappers.
 * Pure functions — no side-effects, no mutation.
 */

import type {
  InvitationStatus,
  OrgInvitation,
  OrgMember,
  OrgRole,
  OrgSummary,
  Organization,
} from '../models/organizations.models';

// ---------------------------------------------------------------------------
// DTOs (wire shapes from backend API)
// ---------------------------------------------------------------------------

export interface OrgDto {
  org_id: string;
  name: string;
  slug: string;
  created_by_user_id: string;
  created_at: string; // ISO-8601
}

export interface OrgMemberDto {
  org_id: string;
  user_id: string;
  email: string;
  display_name: string;
  role: OrgRole;
  joined_at: string; // ISO-8601
}

export interface OrgInvitationDto {
  invitation_id: string;
  org_id: string;
  email: string;
  role: OrgRole;
  status: InvitationStatus;
  created_at: string; // ISO-8601
  expires_at: string; // ISO-8601
}

export interface OrgSummaryDto {
  org_id: string;
  name: string;
  slug: string;
  role: OrgRole;
}

// ---------------------------------------------------------------------------
// Mapper functions — pure, immutable, no side-effects
// ---------------------------------------------------------------------------

export function mapOrgDtoToOrganization(dto: OrgDto): Organization {
  return {
    orgId: dto.org_id,
    name: dto.name,
    slug: dto.slug,
    createdByUserId: dto.created_by_user_id,
    createdAt: new Date(dto.created_at),
  };
}

export function mapOrgMemberDtoToOrgMember(dto: OrgMemberDto): OrgMember {
  return {
    orgId: dto.org_id,
    userId: dto.user_id,
    email: dto.email,
    displayName: dto.display_name,
    role: dto.role,
    joinedAt: new Date(dto.joined_at),
  };
}

export function mapOrgInvitationDtoToOrgInvitation(dto: OrgInvitationDto): OrgInvitation {
  return {
    invitationId: dto.invitation_id,
    orgId: dto.org_id,
    email: dto.email,
    role: dto.role,
    status: dto.status,
    createdAt: new Date(dto.created_at),
    expiresAt: new Date(dto.expires_at),
  };
}

export function mapOrgSummaryDtoToOrgSummary(dto: OrgSummaryDto): OrgSummary {
  return {
    orgId: dto.org_id,
    name: dto.name,
    slug: dto.slug,
    role: dto.role,
  };
}
