/**
 * RED tests — Task 10.1 (part A): Organizations domain models
 *
 * These tests assert the SHAPE and IMMUTABILITY of domain types.
 * All production files referenced below DO NOT EXIST YET — the suite
 * will fail (RED) until the coder creates them.
 *
 * GREEN contract — exact types the coder MUST define in
 *   src/app/features/organizations/domain/models/organizations.models.ts
 *
 *   export type OrgRole = 'owner' | 'admin' | 'member';
 *   export type InvitationStatus = 'pending' | 'accepted' | 'revoked' | 'expired';
 *   export type PluginVisibility = 'public' | 'private';
 *
 *   export type Organization = {
 *     readonly orgId: string;
 *     readonly name: string;
 *     readonly slug: string;
 *     readonly createdByUserId: string;
 *     readonly createdAt: Date;
 *   };
 *
 *   export type OrgMember = {
 *     readonly orgId: string;
 *     readonly userId: string;
 *     readonly email: string;
 *     readonly displayName: string;
 *     readonly role: OrgRole;
 *     readonly joinedAt: Date;
 *   };
 *
 *   export type OrgInvitation = {
 *     readonly invitationId: string;
 *     readonly orgId: string;
 *     readonly email: string;
 *     readonly role: OrgRole;
 *     readonly status: InvitationStatus;
 *     readonly createdAt: Date;
 *     readonly expiresAt: Date;
 *   };
 *
 *   export type OrgSummary = {
 *     readonly orgId: string;
 *     readonly name: string;
 *     readonly slug: string;
 *     readonly role: OrgRole;   // caller's role in the org
 *   };
 */

import type {
  InvitationStatus,
  OrgInvitation,
  OrgMember,
  OrgRole,
  OrgSummary,
  Organization,
  PluginVisibility,
} from './organizations.models';

// ---------------------------------------------------------------------------
// OrgRole — exhaustive union checks
// ---------------------------------------------------------------------------

describe('OrgRole — type-level checks', () => {
  it('should accept "owner" as a valid OrgRole', () => {
    const role: OrgRole = 'owner';
    expect(role).toBe('owner');
  });

  it('should accept "admin" as a valid OrgRole', () => {
    const role: OrgRole = 'admin';
    expect(role).toBe('admin');
  });

  it('should accept "member" as a valid OrgRole', () => {
    const role: OrgRole = 'member';
    expect(role).toBe('member');
  });
});

// ---------------------------------------------------------------------------
// InvitationStatus — exhaustive union checks
// ---------------------------------------------------------------------------

describe('InvitationStatus — type-level checks', () => {
  it('should accept "pending" as a valid InvitationStatus', () => {
    const s: InvitationStatus = 'pending';
    expect(s).toBe('pending');
  });

  it('should accept "accepted" as a valid InvitationStatus', () => {
    const s: InvitationStatus = 'accepted';
    expect(s).toBe('accepted');
  });

  it('should accept "revoked" as a valid InvitationStatus', () => {
    const s: InvitationStatus = 'revoked';
    expect(s).toBe('revoked');
  });

  it('should accept "expired" as a valid InvitationStatus', () => {
    const s: InvitationStatus = 'expired';
    expect(s).toBe('expired');
  });
});

// ---------------------------------------------------------------------------
// PluginVisibility — exhaustive union checks
// ---------------------------------------------------------------------------

describe('PluginVisibility — type-level checks', () => {
  it('should accept "public" as a valid PluginVisibility', () => {
    const v: PluginVisibility = 'public';
    expect(v).toBe('public');
  });

  it('should accept "private" as a valid PluginVisibility', () => {
    const v: PluginVisibility = 'private';
    expect(v).toBe('private');
  });
});

// ---------------------------------------------------------------------------
// Organization — shape assertions
// ---------------------------------------------------------------------------

describe('Organization — shape', () => {
  const org: Organization = {
    orgId: 'org-uuid-1',
    name: 'Acme Corp',
    slug: 'acme-corp',
    createdByUserId: 'user-uuid-1',
    createdAt: new Date('2024-01-15T10:00:00.000Z'),
  };

  it('should have orgId field', () => {
    expect(org.orgId).toBe('org-uuid-1');
  });

  it('should have name field', () => {
    expect(org.name).toBe('Acme Corp');
  });

  it('should have slug field', () => {
    expect(org.slug).toBe('acme-corp');
  });

  it('should have createdByUserId field', () => {
    expect(org.createdByUserId).toBe('user-uuid-1');
  });

  it('should have createdAt as a Date', () => {
    expect(org.createdAt).toBeInstanceOf(Date);
  });

  it('should produce a new object on spread (immutability)', () => {
    const copy: Organization = { ...org, name: 'New Name' };
    expect(copy).not.toBe(org);
    expect(copy.name).toBe('New Name');
    expect(org.name).toBe('Acme Corp');
  });
});

// ---------------------------------------------------------------------------
// OrgMember — shape assertions
// ---------------------------------------------------------------------------

describe('OrgMember — shape', () => {
  const member: OrgMember = {
    orgId: 'org-uuid-1',
    userId: 'user-uuid-2',
    email: 'alice@example.com',
    displayName: 'Alice',
    role: 'admin',
    joinedAt: new Date('2024-02-01T09:00:00.000Z'),
  };

  it('should have orgId field', () => {
    expect(member.orgId).toBe('org-uuid-1');
  });

  it('should have userId field', () => {
    expect(member.userId).toBe('user-uuid-2');
  });

  it('should have email field', () => {
    expect(member.email).toBe('alice@example.com');
  });

  it('should have displayName field', () => {
    expect(member.displayName).toBe('Alice');
  });

  it('should have role field typed as OrgRole', () => {
    const r: OrgRole = member.role;
    expect(r).toBe('admin');
  });

  it('should have joinedAt as a Date', () => {
    expect(member.joinedAt).toBeInstanceOf(Date);
  });

  it('should produce a new object on spread (immutability)', () => {
    const copy: OrgMember = { ...member, role: 'member' };
    expect(copy).not.toBe(member);
    expect(copy.role).toBe('member');
    expect(member.role).toBe('admin');
  });
});

// ---------------------------------------------------------------------------
// OrgInvitation — shape assertions
// ---------------------------------------------------------------------------

describe('OrgInvitation — shape', () => {
  const invitation: OrgInvitation = {
    invitationId: 'inv-uuid-1',
    orgId: 'org-uuid-1',
    email: 'bob@example.com',
    role: 'member',
    status: 'pending',
    createdAt: new Date('2024-03-01T08:00:00.000Z'),
    expiresAt: new Date('2024-03-08T08:00:00.000Z'),
  };

  it('should have invitationId field', () => {
    expect(invitation.invitationId).toBe('inv-uuid-1');
  });

  it('should have orgId field', () => {
    expect(invitation.orgId).toBe('org-uuid-1');
  });

  it('should have email field', () => {
    expect(invitation.email).toBe('bob@example.com');
  });

  it('should have role field typed as OrgRole', () => {
    const r: OrgRole = invitation.role;
    expect(r).toBe('member');
  });

  it('should have status field typed as InvitationStatus', () => {
    const s: InvitationStatus = invitation.status;
    expect(s).toBe('pending');
  });

  it('should have createdAt as a Date', () => {
    expect(invitation.createdAt).toBeInstanceOf(Date);
  });

  it('should have expiresAt as a Date', () => {
    expect(invitation.expiresAt).toBeInstanceOf(Date);
  });

  it('should produce a new object on spread (immutability)', () => {
    const copy: OrgInvitation = { ...invitation, status: 'accepted' };
    expect(copy).not.toBe(invitation);
    expect(copy.status).toBe('accepted');
    expect(invitation.status).toBe('pending');
  });
});

// ---------------------------------------------------------------------------
// OrgSummary — shape assertions
// ---------------------------------------------------------------------------

describe('OrgSummary — shape', () => {
  const summary: OrgSummary = {
    orgId: 'org-uuid-3',
    name: 'Widgets Inc',
    slug: 'widgets-inc',
    role: 'owner',
  };

  it('should have orgId field', () => {
    expect(summary.orgId).toBe('org-uuid-3');
  });

  it('should have name field', () => {
    expect(summary.name).toBe('Widgets Inc');
  });

  it('should have slug field', () => {
    expect(summary.slug).toBe('widgets-inc');
  });

  it('should have role field typed as OrgRole', () => {
    const r: OrgRole = summary.role;
    expect(r).toBe('owner');
  });

  it('should produce a new object on spread (immutability)', () => {
    const copy: OrgSummary = { ...summary, role: 'member' };
    expect(copy).not.toBe(summary);
    expect(copy.role).toBe('member');
    expect(summary.role).toBe('owner');
  });
});
