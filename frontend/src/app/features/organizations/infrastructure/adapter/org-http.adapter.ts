/**
 * HTTP adapter implementing OrgPort.
 * Maps API DTOs to domain models via the organizations mappers.
 * Calls the backend /api/v1/orgs endpoints.
 */

import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../../../core/config/api-config';
import { OrgPort } from '../../domain/ports/org.port';
import type { OrgInvitation, OrgMember, OrgRole, OrgSummary, Organization } from '../../domain/models/organizations.models';
import type { OrgDto, OrgInvitationDto, OrgMemberDto, OrgSummaryDto } from '../../domain/mappers/organizations-mapper';
import {
  mapOrgDtoToOrganization,
  mapOrgInvitationDtoToOrgInvitation,
  mapOrgMemberDtoToOrgMember,
  mapOrgSummaryDtoToOrgSummary,
} from '../../domain/mappers/organizations-mapper';

interface CreateOrgBody {
  readonly name: string;
  readonly slug: string;
}

interface InviteBody {
  readonly email: string;
  readonly role: OrgRole;
}

interface ChangeMemberRoleBody {
  readonly role: OrgRole;
}

@Injectable()
export class OrgHttpAdapter extends OrgPort {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  createOrganization(name: string, slug: string): Observable<Organization> {
    const body: CreateOrgBody = { name, slug };
    return this.http
      .post<OrgDto>(`${this.baseUrl}/api/v1/orgs`, body)
      .pipe(map(mapOrgDtoToOrganization));
  }

  listOrganizations(): Observable<OrgSummary[]> {
    return this.http
      .get<OrgSummaryDto[]>(`${this.baseUrl}/api/v1/orgs`)
      .pipe(map((dtos) => dtos.map(mapOrgSummaryDtoToOrgSummary)));
  }

  listMembers(orgId: string): Observable<OrgMember[]> {
    return this.http
      .get<OrgMemberDto[]>(`${this.baseUrl}/api/v1/orgs/${orgId}/members`)
      .pipe(map((dtos) => dtos.map(mapOrgMemberDtoToOrgMember)));
  }

  invite(orgId: string, email: string, role: OrgRole): Observable<OrgInvitation> {
    const body: InviteBody = { email, role };
    return this.http
      .post<OrgInvitationDto>(`${this.baseUrl}/api/v1/orgs/${orgId}/invitations`, body)
      .pipe(map(mapOrgInvitationDtoToOrgInvitation));
  }

  acceptInvitation(orgId: string, invitationId: string): Observable<void> {
    return this.http.post<void>(
      `${this.baseUrl}/api/v1/orgs/${orgId}/invitations/${invitationId}/accept`,
      {},
    );
  }

  revokeInvitation(orgId: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.baseUrl}/api/v1/orgs/${orgId}/invitations/${invitationId}`,
    );
  }

  removeMember(orgId: string, userId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/api/v1/orgs/${orgId}/members/${userId}`);
  }

  changeMemberRole(orgId: string, userId: string, role: OrgRole): Observable<void> {
    const body: ChangeMemberRoleBody = { role };
    return this.http.patch<void>(
      `${this.baseUrl}/api/v1/orgs/${orgId}/members/${userId}`,
      body,
    );
  }
}
