/**
 * Organizations role rules — pure functions, no side-effects.
 * Privilege order: owner > admin > member
 */

import type { OrgRole } from '../models/organizations.models';

const ROLE_RANK: Record<OrgRole, number> = {
  owner: 2,
  admin: 1,
  member: 0,
};

/**
 * Returns true when the given role can invite members.
 * Only 'owner' and 'admin' may issue invitations.
 */
export function canInviteMembers(role: OrgRole): boolean {
  return role === 'owner' || role === 'admin';
}

/**
 * Returns true when the given role can remove members.
 * Only 'owner' and 'admin' may remove members.
 */
export function canRemoveMembers(role: OrgRole): boolean {
  return role === 'owner' || role === 'admin';
}

/**
 * Returns true when the given role can change another member's role.
 * Only 'owner' may change roles (owner-only operation per design).
 */
export function canChangeMemberRole(role: OrgRole): boolean {
  return role === 'owner';
}

/**
 * Returns true when the given role can revoke a pending invitation.
 * Only 'owner' and 'admin' may revoke invitations.
 */
export function canRevokeInvitation(role: OrgRole): boolean {
  return role === 'owner' || role === 'admin';
}

/**
 * Returns true when the given role can delete/manage the organisation.
 * Only 'owner' may manage the org (delete, rename, etc.)
 */
export function canManageOrg(role: OrgRole): boolean {
  return role === 'owner';
}

/**
 * Returns true when the given caller role is at least as privileged
 * as the required minimum role.
 * Privilege order: owner > admin > member
 */
export function hasMinimumRole(callerRole: OrgRole, minRole: OrgRole): boolean {
  return ROLE_RANK[callerRole] >= ROLE_RANK[minRole];
}
