/**
 * Organizations domain models.
 * All types are readonly — immutable by design.
 */

export type OrgRole = 'owner' | 'admin' | 'member';

export type InvitationStatus = 'pending' | 'accepted' | 'revoked' | 'expired';

export type PluginVisibility = 'public' | 'private';

export interface Organization {
  readonly orgId: string;
  readonly name: string;
  readonly slug: string;
  readonly createdByUserId: string;
  readonly createdAt: Date;
}

export interface OrgMember {
  readonly orgId: string;
  readonly userId: string;
  readonly email: string;
  readonly displayName: string;
  readonly role: OrgRole;
  readonly joinedAt: Date;
}

export interface OrgInvitation {
  readonly invitationId: string;
  readonly orgId: string;
  readonly email: string;
  readonly role: OrgRole;
  readonly status: InvitationStatus;
  readonly createdAt: Date;
  readonly expiresAt: Date;
}

export interface OrgSummary {
  readonly orgId: string;
  readonly name: string;
  readonly slug: string;
  readonly role: OrgRole; // caller's role in the org
}
