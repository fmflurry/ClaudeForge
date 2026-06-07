/**
 * Abstract port for the Organizations domain.
 * Infrastructure adapters implement this; the facade depends on this abstract class.
 */

import { Observable } from 'rxjs';
import type { OrgInvitation, OrgMember, OrgRole, OrgSummary, Organization } from '../models/organizations.models';

export abstract class OrgPort {
  abstract createOrganization(name: string, slug: string): Observable<Organization>;
  abstract listOrganizations(): Observable<OrgSummary[]>;
  abstract listMembers(orgId: string): Observable<OrgMember[]>;
  abstract invite(orgId: string, email: string, role: OrgRole): Observable<OrgInvitation>;
  abstract acceptInvitation(orgId: string, invitationId: string): Observable<void>;
  abstract revokeInvitation(orgId: string, invitationId: string): Observable<void>;
  abstract removeMember(orgId: string, userId: string): Observable<void>;
  abstract changeMemberRole(orgId: string, userId: string, role: OrgRole): Observable<void>;
}
