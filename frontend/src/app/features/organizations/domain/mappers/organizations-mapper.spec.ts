/**
 * RED tests — Task 10.1 (part B): Organizations domain mappers
 *
 * All production files referenced below DO NOT EXIST YET — the suite
 * will fail (RED) until the coder creates them.
 *
 * GREEN contract — exact types and functions the coder MUST define:
 *
 *   // organizations-mapper.ts
 *   //
 *   // DTOs (wire shapes from backend API):
 *   export interface OrgDto {
 *     org_id: string;
 *     name: string;
 *     slug: string;
 *     created_by_user_id: string;
 *     created_at: string;   // ISO-8601
 *   }
 *
 *   export interface OrgMemberDto {
 *     org_id: string;
 *     user_id: string;
 *     email: string;
 *     display_name: string;
 *     role: 'owner' | 'admin' | 'member';
 *     joined_at: string;   // ISO-8601
 *   }
 *
 *   export interface OrgInvitationDto {
 *     invitation_id: string;
 *     org_id: string;
 *     email: string;
 *     role: 'owner' | 'admin' | 'member';
 *     status: 'pending' | 'accepted' | 'revoked' | 'expired';
 *     created_at: string;   // ISO-8601
 *     expires_at: string;   // ISO-8601
 *   }
 *
 *   export interface OrgSummaryDto {
 *     org_id: string;
 *     name: string;
 *     slug: string;
 *     role: 'owner' | 'admin' | 'member';
 *   }
 *
 *   // Mapper functions — pure, immutable, no side-effects:
 *   export function mapOrgDtoToOrganization(dto: OrgDto): Organization
 *   export function mapOrgMemberDtoToOrgMember(dto: OrgMemberDto): OrgMember
 *   export function mapOrgInvitationDtoToOrgInvitation(dto: OrgInvitationDto): OrgInvitation
 *   export function mapOrgSummaryDtoToOrgSummary(dto: OrgSummaryDto): OrgSummary
 */

import {
  mapOrgDtoToOrganization,
  mapOrgInvitationDtoToOrgInvitation,
  mapOrgMemberDtoToOrgMember,
  mapOrgSummaryDtoToOrgSummary,
} from './organizations-mapper';
import type { OrgDto, OrgInvitationDto, OrgMemberDto, OrgSummaryDto } from './organizations-mapper';
import type { OrgInvitation, OrgMember, OrgRole, OrgSummary, Organization } from '../models/organizations.models';

// ---------------------------------------------------------------------------
// DTO fixtures
// ---------------------------------------------------------------------------

const orgDto: OrgDto = {
  org_id: 'org-uuid-1',
  name: 'Acme Corp',
  slug: 'acme-corp',
  created_by_user_id: 'user-uuid-1',
  created_at: '2024-01-15T10:00:00.000Z',
};

const memberDto: OrgMemberDto = {
  org_id: 'org-uuid-1',
  user_id: 'user-uuid-2',
  email: 'alice@example.com',
  display_name: 'Alice',
  role: 'admin',
  joined_at: '2024-02-01T09:00:00.000Z',
};

const invitationDto: OrgInvitationDto = {
  invitation_id: 'inv-uuid-1',
  org_id: 'org-uuid-1',
  email: 'bob@example.com',
  role: 'member',
  status: 'pending',
  created_at: '2024-03-01T08:00:00.000Z',
  expires_at: '2024-03-08T08:00:00.000Z',
};

const summaryDto: OrgSummaryDto = {
  org_id: 'org-uuid-3',
  name: 'Widgets Inc',
  slug: 'widgets-inc',
  role: 'owner',
};

// ---------------------------------------------------------------------------
// mapOrgDtoToOrganization
// ---------------------------------------------------------------------------

describe('mapOrgDtoToOrganization', () => {
  let result: Organization;

  beforeEach(() => {
    result = mapOrgDtoToOrganization(orgDto);
  });

  it('should map org_id to orgId', () => {
    expect(result.orgId).toBe('org-uuid-1');
  });

  it('should map name', () => {
    expect(result.name).toBe('Acme Corp');
  });

  it('should map slug', () => {
    expect(result.slug).toBe('acme-corp');
  });

  it('should map created_by_user_id to createdByUserId', () => {
    expect(result.createdByUserId).toBe('user-uuid-1');
  });

  it('should convert created_at ISO string to a Date object', () => {
    expect(result.createdAt).toBeInstanceOf(Date);
    expect(result.createdAt.toISOString()).toBe('2024-01-15T10:00:00.000Z');
  });

  it('should return a new object each call (immutability)', () => {
    const r1 = mapOrgDtoToOrganization(orgDto);
    const r2 = mapOrgDtoToOrganization(orgDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the source DTO', () => {
    const copy = { ...orgDto };
    mapOrgDtoToOrganization(orgDto);
    expect(orgDto).toEqual(copy);
  });
});

// ---------------------------------------------------------------------------
// mapOrgMemberDtoToOrgMember
// ---------------------------------------------------------------------------

describe('mapOrgMemberDtoToOrgMember', () => {
  let result: OrgMember;

  beforeEach(() => {
    result = mapOrgMemberDtoToOrgMember(memberDto);
  });

  it('should map org_id to orgId', () => {
    expect(result.orgId).toBe('org-uuid-1');
  });

  it('should map user_id to userId', () => {
    expect(result.userId).toBe('user-uuid-2');
  });

  it('should map email', () => {
    expect(result.email).toBe('alice@example.com');
  });

  it('should map display_name to displayName', () => {
    expect(result.displayName).toBe('Alice');
  });

  it('should map role as OrgRole', () => {
    const r: OrgRole = result.role;
    expect(r).toBe('admin');
  });

  it('should convert joined_at ISO string to a Date object', () => {
    expect(result.joinedAt).toBeInstanceOf(Date);
    expect(result.joinedAt.toISOString()).toBe('2024-02-01T09:00:00.000Z');
  });

  it('should return a new object each call (immutability)', () => {
    const r1 = mapOrgMemberDtoToOrgMember(memberDto);
    const r2 = mapOrgMemberDtoToOrgMember(memberDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the source DTO', () => {
    const copy = { ...memberDto };
    mapOrgMemberDtoToOrgMember(memberDto);
    expect(memberDto).toEqual(copy);
  });

  it('should map owner role correctly', () => {
    const ownerDto: OrgMemberDto = { ...memberDto, role: 'owner' };
    expect(mapOrgMemberDtoToOrgMember(ownerDto).role).toBe('owner');
  });

  it('should map member role correctly', () => {
    const memDto: OrgMemberDto = { ...memberDto, role: 'member' };
    expect(mapOrgMemberDtoToOrgMember(memDto).role).toBe('member');
  });
});

// ---------------------------------------------------------------------------
// mapOrgInvitationDtoToOrgInvitation
// ---------------------------------------------------------------------------

describe('mapOrgInvitationDtoToOrgInvitation', () => {
  let result: OrgInvitation;

  beforeEach(() => {
    result = mapOrgInvitationDtoToOrgInvitation(invitationDto);
  });

  it('should map invitation_id to invitationId', () => {
    expect(result.invitationId).toBe('inv-uuid-1');
  });

  it('should map org_id to orgId', () => {
    expect(result.orgId).toBe('org-uuid-1');
  });

  it('should map email', () => {
    expect(result.email).toBe('bob@example.com');
  });

  it('should map role as OrgRole', () => {
    const r: OrgRole = result.role;
    expect(r).toBe('member');
  });

  it('should map status as InvitationStatus', () => {
    expect(result.status).toBe('pending');
  });

  it('should convert created_at ISO string to a Date', () => {
    expect(result.createdAt).toBeInstanceOf(Date);
    expect(result.createdAt.toISOString()).toBe('2024-03-01T08:00:00.000Z');
  });

  it('should convert expires_at ISO string to a Date', () => {
    expect(result.expiresAt).toBeInstanceOf(Date);
    expect(result.expiresAt.toISOString()).toBe('2024-03-08T08:00:00.000Z');
  });

  it('should return a new object each call (immutability)', () => {
    const r1 = mapOrgInvitationDtoToOrgInvitation(invitationDto);
    const r2 = mapOrgInvitationDtoToOrgInvitation(invitationDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the source DTO', () => {
    const copy = { ...invitationDto };
    mapOrgInvitationDtoToOrgInvitation(invitationDto);
    expect(invitationDto).toEqual(copy);
  });

  it('should map accepted status', () => {
    const accepted: OrgInvitationDto = { ...invitationDto, status: 'accepted' };
    expect(mapOrgInvitationDtoToOrgInvitation(accepted).status).toBe('accepted');
  });

  it('should map revoked status', () => {
    const revoked: OrgInvitationDto = { ...invitationDto, status: 'revoked' };
    expect(mapOrgInvitationDtoToOrgInvitation(revoked).status).toBe('revoked');
  });

  it('should map expired status', () => {
    const expired: OrgInvitationDto = { ...invitationDto, status: 'expired' };
    expect(mapOrgInvitationDtoToOrgInvitation(expired).status).toBe('expired');
  });
});

// ---------------------------------------------------------------------------
// mapOrgSummaryDtoToOrgSummary
// ---------------------------------------------------------------------------

describe('mapOrgSummaryDtoToOrgSummary', () => {
  let result: OrgSummary;

  beforeEach(() => {
    result = mapOrgSummaryDtoToOrgSummary(summaryDto);
  });

  it('should map org_id to orgId', () => {
    expect(result.orgId).toBe('org-uuid-3');
  });

  it('should map name', () => {
    expect(result.name).toBe('Widgets Inc');
  });

  it('should map slug', () => {
    expect(result.slug).toBe('widgets-inc');
  });

  it('should map role as OrgRole', () => {
    const r: OrgRole = result.role;
    expect(r).toBe('owner');
  });

  it('should return a new object each call (immutability)', () => {
    const r1 = mapOrgSummaryDtoToOrgSummary(summaryDto);
    const r2 = mapOrgSummaryDtoToOrgSummary(summaryDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the source DTO', () => {
    const copy = { ...summaryDto };
    mapOrgSummaryDtoToOrgSummary(summaryDto);
    expect(summaryDto).toEqual(copy);
  });

  it('should map admin role for summary', () => {
    const adminDto: OrgSummaryDto = { ...summaryDto, role: 'admin' };
    expect(mapOrgSummaryDtoToOrgSummary(adminDto).role).toBe('admin');
  });

  it('should map member role for summary', () => {
    const memberSummaryDto: OrgSummaryDto = { ...summaryDto, role: 'member' };
    expect(mapOrgSummaryDtoToOrgSummary(memberSummaryDto).role).toBe('member');
  });
});
