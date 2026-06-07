/**
 * Unit tests for auth.mapper.ts — pure mapping functions.
 */

import {
  mapTokenResponseToAuthToken,
  mapOrgMembershipDtoToOrgMembership,
  mapCurrentUserDtoToCurrentUser,
  type TokenResponseDto,
  type OrgMembershipDto,
  type CurrentUserDto,
} from './auth.mapper';

// ---------------------------------------------------------------------------
// mapTokenResponseToAuthToken
// ---------------------------------------------------------------------------

describe('mapTokenResponseToAuthToken', () => {
  it('should map access_token to accessToken', () => {
    const dto: TokenResponseDto = { access_token: 'tok-abc-123' };
    const result = mapTokenResponseToAuthToken(dto);
    expect(result.accessToken).toBe('tok-abc-123');
  });

  it('should return a new object (immutable — not the DTO)', () => {
    const dto: TokenResponseDto = { access_token: 'tok-abc' };
    const result = mapTokenResponseToAuthToken(dto);
    expect(result).not.toBe(dto);
  });

  it('should produce an object with only the accessToken property', () => {
    const dto: TokenResponseDto = { access_token: 'tok-xyz' };
    const result = mapTokenResponseToAuthToken(dto);
    expect(Object.keys(result)).toEqual(['accessToken']);
  });

  it('should preserve an empty string access_token', () => {
    const dto: TokenResponseDto = { access_token: '' };
    const result = mapTokenResponseToAuthToken(dto);
    expect(result.accessToken).toBe('');
  });

  it('should handle a JWT-shaped token string', () => {
    const jwt = 'eyJhbGciOiJSUzI1NiJ9.eyJzdWIiOiJ1c2VyLTEifQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c';
    const dto: TokenResponseDto = { access_token: jwt };
    const result = mapTokenResponseToAuthToken(dto);
    expect(result.accessToken).toBe(jwt);
  });
});

// ---------------------------------------------------------------------------
// mapOrgMembershipDtoToOrgMembership
// ---------------------------------------------------------------------------

describe('mapOrgMembershipDtoToOrgMembership', () => {
  it('should map org_id to orgId', () => {
    const dto: OrgMembershipDto = { org_id: 'org-1', org_name: 'Acme', role: 'owner' };
    expect(mapOrgMembershipDtoToOrgMembership(dto).orgId).toBe('org-1');
  });

  it('should map org_name to orgName', () => {
    const dto: OrgMembershipDto = { org_id: 'org-1', org_name: 'Acme Corp', role: 'admin' };
    expect(mapOrgMembershipDtoToOrgMembership(dto).orgName).toBe('Acme Corp');
  });

  it('should preserve the role', () => {
    const dto: OrgMembershipDto = { org_id: 'org-1', org_name: 'Acme', role: 'member' };
    expect(mapOrgMembershipDtoToOrgMembership(dto).role).toBe('member');
  });

  it('should handle owner role', () => {
    const dto: OrgMembershipDto = { org_id: 'org-2', org_name: 'Widgets', role: 'owner' };
    expect(mapOrgMembershipDtoToOrgMembership(dto).role).toBe('owner');
  });

  it('should handle admin role', () => {
    const dto: OrgMembershipDto = { org_id: 'org-3', org_name: 'Tools', role: 'admin' };
    expect(mapOrgMembershipDtoToOrgMembership(dto).role).toBe('admin');
  });

  it('should return a new object, not the DTO', () => {
    const dto: OrgMembershipDto = { org_id: 'org-1', org_name: 'Acme', role: 'member' };
    expect(mapOrgMembershipDtoToOrgMembership(dto)).not.toBe(dto);
  });
});

// ---------------------------------------------------------------------------
// mapCurrentUserDtoToCurrentUser
// ---------------------------------------------------------------------------

describe('mapCurrentUserDtoToCurrentUser', () => {
  const baseDto: CurrentUserDto = {
    user_id: 'u-1',
    email: 'alice@example.com',
    display_name: 'Alice',
    org_memberships: [{ org_id: 'org-1', org_name: 'Acme', role: 'owner' }],
  };

  it('should map user_id to userId', () => {
    expect(mapCurrentUserDtoToCurrentUser(baseDto).userId).toBe('u-1');
  });

  it('should map email verbatim', () => {
    expect(mapCurrentUserDtoToCurrentUser(baseDto).email).toBe('alice@example.com');
  });

  it('should map display_name to displayName', () => {
    expect(mapCurrentUserDtoToCurrentUser(baseDto).displayName).toBe('Alice');
  });

  it('should map org_memberships array', () => {
    const result = mapCurrentUserDtoToCurrentUser(baseDto);
    expect(result.orgMemberships).toHaveLength(1);
  });

  it('should map nested orgId from org membership', () => {
    const result = mapCurrentUserDtoToCurrentUser(baseDto);
    expect(result.orgMemberships[0].orgId).toBe('org-1');
  });

  it('should handle empty org_memberships array', () => {
    const dto: CurrentUserDto = { ...baseDto, org_memberships: [] };
    const result = mapCurrentUserDtoToCurrentUser(dto);
    expect(result.orgMemberships).toEqual([]);
  });

  it('should map multiple org memberships', () => {
    const dto: CurrentUserDto = {
      ...baseDto,
      org_memberships: [
        { org_id: 'org-1', org_name: 'Acme', role: 'owner' },
        { org_id: 'org-2', org_name: 'Widgets', role: 'member' },
      ],
    };
    const result = mapCurrentUserDtoToCurrentUser(dto);
    expect(result.orgMemberships).toHaveLength(2);
    expect(result.orgMemberships[1].orgId).toBe('org-2');
    expect(result.orgMemberships[1].orgName).toBe('Widgets');
    expect(result.orgMemberships[1].role).toBe('member');
  });

  it('should return a new object, not the DTO', () => {
    expect(mapCurrentUserDtoToCurrentUser(baseDto)).not.toBe(baseDto);
  });

  it('should produce immutable org_memberships (new array, not same reference)', () => {
    const result = mapCurrentUserDtoToCurrentUser(baseDto);
    expect(result.orgMemberships).not.toBe(baseDto.org_memberships);
  });
});
