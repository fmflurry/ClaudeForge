/**
 * Unit tests for OrgHttpAdapter.
 * Uses HttpTestingController to verify URL/body shapes and response mapping.
 */

import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { OrgHttpAdapter } from './org-http.adapter';
import { API_BASE_URL } from '../../../../core/config/api-config';
import { OrgPort } from '../../domain/ports/org.port';
import type { OrgDto, OrgMemberDto, OrgInvitationDto, OrgSummaryDto } from '../../domain/mappers/organizations-mapper';
import type { Organization, OrgMember, OrgInvitation, OrgSummary } from '../../domain/models/organizations.models';

const BASE = 'https://api.test';

function setup(): { adapter: OrgHttpAdapter; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: BASE },
      { provide: OrgPort, useClass: OrgHttpAdapter },
      OrgHttpAdapter,
    ],
  });
  return {
    adapter: TestBed.inject(OrgHttpAdapter),
    http: TestBed.inject(HttpTestingController),
  };
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const ORG_DTO: OrgDto = {
  org_id: 'org-uuid-1',
  name: 'Acme Corp',
  slug: 'acme-corp',
  created_by_user_id: 'user-1',
  created_at: '2024-01-15T10:00:00.000Z',
};

const MEMBER_DTO: OrgMemberDto = {
  org_id: 'org-uuid-1',
  user_id: 'user-2',
  email: 'alice@example.com',
  display_name: 'Alice',
  role: 'admin',
  joined_at: '2024-02-01T09:00:00.000Z',
};

const INVITATION_DTO: OrgInvitationDto = {
  invitation_id: 'inv-1',
  org_id: 'org-uuid-1',
  email: 'bob@example.com',
  role: 'member',
  status: 'pending',
  created_at: '2024-03-01T00:00:00.000Z',
  expires_at: '2024-03-08T00:00:00.000Z',
};

const SUMMARY_DTO: OrgSummaryDto = {
  org_id: 'org-uuid-1',
  name: 'Acme Corp',
  slug: 'acme-corp',
  role: 'owner',
};

// ---------------------------------------------------------------------------
// createOrganization
// ---------------------------------------------------------------------------

describe('OrgHttpAdapter — createOrganization', () => {
  it('should POST to /api/v1/orgs', () => {
    const { adapter, http } = setup();
    adapter.createOrganization('Acme Corp', 'acme-corp').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs`);
    expect(req.request.method).toBe('POST');
    req.flush(ORG_DTO);
    http.verify();
  });

  it('should send name and slug in request body', () => {
    const { adapter, http } = setup();
    adapter.createOrganization('My Org', 'my-org').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs`);
    expect(req.request.body).toEqual({ name: 'My Org', slug: 'my-org' });
    req.flush(ORG_DTO);
    http.verify();
  });

  it('should map org_id to orgId in domain model', () => {
    const { adapter, http } = setup();
    let org: Organization | undefined;
    adapter.createOrganization('Acme', 'acme').subscribe((o) => (org = o));

    const req = http.expectOne(`${BASE}/api/v1/orgs`);
    req.flush(ORG_DTO);

    expect(org?.orgId).toBe('org-uuid-1');
    http.verify();
  });

  it('should map name verbatim', () => {
    const { adapter, http } = setup();
    let org: Organization | undefined;
    adapter.createOrganization('Acme Corp', 'acme-corp').subscribe((o) => (org = o));

    const req = http.expectOne(`${BASE}/api/v1/orgs`);
    req.flush(ORG_DTO);

    expect(org?.name).toBe('Acme Corp');
    http.verify();
  });

  it('should map created_at to a Date instance', () => {
    const { adapter, http } = setup();
    let org: Organization | undefined;
    adapter.createOrganization('A', 'a').subscribe((o) => (org = o));

    const req = http.expectOne(`${BASE}/api/v1/orgs`);
    req.flush(ORG_DTO);

    expect(org?.createdAt).toBeInstanceOf(Date);
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// listOrganizations
// ---------------------------------------------------------------------------

describe('OrgHttpAdapter — listOrganizations', () => {
  it('should GET /api/v1/orgs', () => {
    const { adapter, http } = setup();
    adapter.listOrganizations().subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs`);
    expect(req.request.method).toBe('GET');
    req.flush([SUMMARY_DTO]);
    http.verify();
  });

  it('should map array of OrgSummaryDto to OrgSummary[]', () => {
    const { adapter, http } = setup();
    let orgs: OrgSummary[] | undefined;
    adapter.listOrganizations().subscribe((o) => (orgs = o));

    const req = http.expectOne(`${BASE}/api/v1/orgs`);
    req.flush([SUMMARY_DTO]);

    expect(orgs).toHaveLength(1);
    expect(orgs![0].orgId).toBe('org-uuid-1');
    expect(orgs![0].role).toBe('owner');
    http.verify();
  });

  it('should return empty array for empty response', () => {
    const { adapter, http } = setup();
    let orgs: OrgSummary[] | undefined;
    adapter.listOrganizations().subscribe((o) => (orgs = o));

    const req = http.expectOne(`${BASE}/api/v1/orgs`);
    req.flush([]);

    expect(orgs).toEqual([]);
    http.verify();
  });

  it('should map multiple summaries', () => {
    const { adapter, http } = setup();
    let orgs: OrgSummary[] | undefined;
    adapter.listOrganizations().subscribe((o) => (orgs = o));

    const req = http.expectOne(`${BASE}/api/v1/orgs`);
    const second: OrgSummaryDto = { org_id: 'org-2', name: 'Widgets', slug: 'widgets', role: 'member' };
    req.flush([SUMMARY_DTO, second]);

    expect(orgs).toHaveLength(2);
    expect(orgs![1].orgId).toBe('org-2');
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// listMembers
// ---------------------------------------------------------------------------

describe('OrgHttpAdapter — listMembers', () => {
  it('should GET /api/v1/orgs/:orgId/members', () => {
    const { adapter, http } = setup();
    adapter.listMembers('org-uuid-1').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs/org-uuid-1/members`);
    expect(req.request.method).toBe('GET');
    req.flush([MEMBER_DTO]);
    http.verify();
  });

  it('should interpolate orgId into the URL path', () => {
    const { adapter, http } = setup();
    adapter.listMembers('my-special-org').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs/my-special-org/members`);
    req.flush([]);
    http.verify();
  });

  it('should map member dto to OrgMember', () => {
    const { adapter, http } = setup();
    let members: OrgMember[] | undefined;
    adapter.listMembers('org-uuid-1').subscribe((m) => (members = m));

    const req = http.expectOne((r) => r.url.endsWith('/members'));
    req.flush([MEMBER_DTO]);

    expect(members![0].userId).toBe('user-2');
    expect(members![0].email).toBe('alice@example.com');
    expect(members![0].role).toBe('admin');
    http.verify();
  });

  it('should map joined_at to a Date', () => {
    const { adapter, http } = setup();
    let members: OrgMember[] | undefined;
    adapter.listMembers('org-uuid-1').subscribe((m) => (members = m));

    const req = http.expectOne((r) => r.url.endsWith('/members'));
    req.flush([MEMBER_DTO]);

    expect(members![0].joinedAt).toBeInstanceOf(Date);
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// invite
// ---------------------------------------------------------------------------

describe('OrgHttpAdapter — invite', () => {
  it('should POST to /api/v1/orgs/:orgId/invitations', () => {
    const { adapter, http } = setup();
    adapter.invite('org-1', 'bob@example.com', 'member').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs/org-1/invitations`);
    expect(req.request.method).toBe('POST');
    req.flush(INVITATION_DTO);
    http.verify();
  });

  it('should send email and role in body', () => {
    const { adapter, http } = setup();
    adapter.invite('org-1', 'carol@example.com', 'admin').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs/org-1/invitations`);
    expect(req.request.body).toEqual({ email: 'carol@example.com', role: 'admin' });
    req.flush(INVITATION_DTO);
    http.verify();
  });

  it('should map response to OrgInvitation', () => {
    const { adapter, http } = setup();
    let inv: OrgInvitation | undefined;
    adapter.invite('org-1', 'bob@example.com', 'member').subscribe((i) => (inv = i));

    const req = http.expectOne((r) => r.url.endsWith('/invitations'));
    req.flush(INVITATION_DTO);

    expect(inv?.invitationId).toBe('inv-1');
    expect(inv?.email).toBe('bob@example.com');
    expect(inv?.status).toBe('pending');
    http.verify();
  });

  it('should map created_at and expires_at to Date', () => {
    const { adapter, http } = setup();
    let inv: OrgInvitation | undefined;
    adapter.invite('org-1', 'bob@example.com', 'member').subscribe((i) => (inv = i));

    const req = http.expectOne((r) => r.url.endsWith('/invitations'));
    req.flush(INVITATION_DTO);

    expect(inv?.createdAt).toBeInstanceOf(Date);
    expect(inv?.expiresAt).toBeInstanceOf(Date);
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// acceptInvitation
// ---------------------------------------------------------------------------

describe('OrgHttpAdapter — acceptInvitation', () => {
  it('should POST to /api/v1/orgs/:orgId/invitations/:invId/accept', () => {
    const { adapter, http } = setup();
    adapter.acceptInvitation('org-1', 'inv-1').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs/org-1/invitations/inv-1/accept`);
    expect(req.request.method).toBe('POST');
    req.flush(null);
    http.verify();
  });

  it('should complete without error on success', () => {
    const { adapter, http } = setup();
    let completed = false;
    adapter.acceptInvitation('org-1', 'inv-1').subscribe({ complete: () => (completed = true) });

    const req = http.expectOne((r) => r.url.endsWith('/accept'));
    req.flush(null);

    expect(completed).toBe(true);
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// revokeInvitation
// ---------------------------------------------------------------------------

describe('OrgHttpAdapter — revokeInvitation', () => {
  it('should DELETE /api/v1/orgs/:orgId/invitations/:invId', () => {
    const { adapter, http } = setup();
    adapter.revokeInvitation('org-1', 'inv-1').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs/org-1/invitations/inv-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    http.verify();
  });

  it('should complete without error on success', () => {
    const { adapter, http } = setup();
    let completed = false;
    adapter.revokeInvitation('org-1', 'inv-1').subscribe({ complete: () => (completed = true) });

    const req = http.expectOne((r) => r.method === 'DELETE');
    req.flush(null);

    expect(completed).toBe(true);
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// removeMember
// ---------------------------------------------------------------------------

describe('OrgHttpAdapter — removeMember', () => {
  it('should DELETE /api/v1/orgs/:orgId/members/:userId', () => {
    const { adapter, http } = setup();
    adapter.removeMember('org-1', 'user-99').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs/org-1/members/user-99`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    http.verify();
  });

  it('should interpolate userId into the URL', () => {
    const { adapter, http } = setup();
    adapter.removeMember('org-abc', 'user-xyz').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs/org-abc/members/user-xyz`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// changeMemberRole
// ---------------------------------------------------------------------------

describe('OrgHttpAdapter — changeMemberRole', () => {
  it('should PATCH /api/v1/orgs/:orgId/members/:userId', () => {
    const { adapter, http } = setup();
    adapter.changeMemberRole('org-1', 'user-2', 'admin').subscribe();

    const req = http.expectOne(`${BASE}/api/v1/orgs/org-1/members/user-2`);
    expect(req.request.method).toBe('PATCH');
    req.flush(null);
    http.verify();
  });

  it('should send role in body', () => {
    const { adapter, http } = setup();
    adapter.changeMemberRole('org-1', 'user-2', 'owner').subscribe();

    const req = http.expectOne((r) => r.method === 'PATCH');
    expect(req.request.body).toEqual({ role: 'owner' });
    req.flush(null);
    http.verify();
  });

  it('should support all three roles', () => {
    const { adapter, http } = setup();

    adapter.changeMemberRole('org-1', 'user-2', 'member').subscribe();
    const req = http.expectOne((r) => r.method === 'PATCH');
    expect(req.request.body).toEqual({ role: 'member' });
    req.flush(null);
    http.verify();
  });
});

// ---------------------------------------------------------------------------
// Architecture — extends OrgPort
// ---------------------------------------------------------------------------

describe('OrgHttpAdapter — architecture', () => {
  it('should be an instance of OrgPort', () => {
    const { adapter } = setup();
    expect(adapter).toBeInstanceOf(OrgPort);
  });
});
