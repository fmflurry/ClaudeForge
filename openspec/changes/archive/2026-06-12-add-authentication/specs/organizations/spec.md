# Organizations Specification

## ADDED Requirements

### Requirement: Create Organization

An authenticated user SHALL create a new organization by providing a unique name and slug. The creator SHALL automatically become a member with owner role.

#### Scenario: User creates organization successfully
- **WHEN** an authenticated user submits a create-organization request with a unique name and slug
- **THEN** the organization is persisted with owner role assigned to the creator
- **AND** a 201 Created response is returned with the organization details

#### Scenario: Duplicate organization name
- **WHEN** a user attempts to create an organization with a name that already exists
- **THEN** a 409 Conflict response is returned
- **AND** the organization is not created

#### Scenario: Unauthenticated create attempt
- **WHEN** an unauthenticated user attempts to create an organization
- **THEN** a 401 Unauthorized response is returned
- **AND** the organization is not created

### Requirement: Organization Membership Model

An organization SHALL maintain a collection of members, each with an assigned role (owner, admin, member). A user can belong to multiple organizations.

#### Scenario: User has multiple organization memberships
- **WHEN** a user is a member of multiple organizations
- **THEN** the system tracks each membership independently
- **AND** the user can list all their organizations

#### Scenario: Retrieve organization members
- **WHEN** an authenticated member requests the members list for their organization
- **THEN** a list of members with their roles is returned
- **AND** each member entry includes email, name, and role

#### Scenario: Non-member attempts to list members
- **WHEN** an unauthenticated or non-member user requests the members list
- **THEN** a 403 Forbidden response is returned
- **AND** the members list is not disclosed

### Requirement: Issue Organization Invitation

An organization owner or admin SHALL issue invitations to users by email. Invitations SHALL track state (pending, accepted, revoked, expired).

#### Scenario: Owner sends valid invitation
- **WHEN** an organization owner submits an invite request with a valid email address
- **THEN** an invitation record is created with pending status
- **AND** an invitation email is sent to the recipient
- **AND** a 201 Created response is returned

#### Scenario: Invite existing member
- **WHEN** an owner attempts to invite a user already in the organization
- **THEN** a 409 Conflict response is returned
- **AND** no new invitation is created

#### Scenario: Non-authorized user attempts to invite
- **WHEN** a member (non-owner/admin) attempts to send an invitation
- **THEN** a 403 Forbidden response is returned
- **AND** the invitation is not created

#### Scenario: Unauthenticated invite attempt
- **WHEN** an unauthenticated user attempts to send an invitation
- **THEN** a 401 Unauthorized response is returned

### Requirement: Accept Organization Invitation

An invited user SHALL accept a pending invitation to become a member of the organization.

#### Scenario: User accepts valid pending invitation
- **WHEN** an authenticated user accepts a pending invitation addressed to their email
- **THEN** the invitation status changes to accepted
- **AND** the user becomes a member of the organization with member role
- **AND** a 200 OK response is returned

#### Scenario: Accept invalid invitation
- **WHEN** a user attempts to accept an invitation that does not exist or is not addressed to their email
- **THEN** a 404 Not Found response is returned
- **AND** no membership is created

#### Scenario: Accept revoked invitation
- **WHEN** a user attempts to accept an invitation with revoked status
- **THEN** a 410 Gone response is returned
- **AND** membership is not created

#### Scenario: Accept expired invitation
- **WHEN** a user attempts to accept an invitation past its expiration time
- **THEN** a 410 Gone response is returned
- **AND** membership is not created

### Requirement: Revoke and Remove Organization Members

An organization owner or admin SHALL revoke pending invitations or remove existing members.

#### Scenario: Owner revokes pending invitation
- **WHEN** an organization owner revokes a pending invitation
- **THEN** the invitation status changes to revoked
- **AND** the recipient cannot accept it
- **AND** a 200 OK response is returned

#### Scenario: Owner removes member
- **WHEN** an organization owner removes an existing member
- **THEN** the membership record is deleted
- **AND** the member no longer has access to organization resources
- **AND** a 204 No Content response is returned

#### Scenario: Member attempts to remove other member
- **WHEN** a member (non-owner/admin) attempts to remove another member
- **THEN** a 403 Forbidden response is returned
- **AND** no member is removed

#### Scenario: Owner removes themselves (sole owner)
- **WHEN** the sole owner attempts to remove themselves from the organization
- **THEN** a 400 Bad Request response is returned
- **AND** the membership is not removed

### Requirement: List User Organizations

An authenticated user SHALL list all organizations they are a member of.

#### Scenario: User lists their organizations
- **WHEN** an authenticated user requests their organization list
- **THEN** a 200 OK response returns a list of all organizations they belong to
- **AND** each organization includes name, slug, and user's role in that org

#### Scenario: Unauthenticated list attempt
- **WHEN** an unauthenticated user attempts to list organizations
- **THEN** a 401 Unauthorized response is returned

### Requirement: Role-Based Authorization for Organization Operations

Organization operations (invite, revoke, remove members) SHALL enforce role-based access control. Owner and admin roles are authorized; member role is not.

#### Scenario: Admin can invite members
- **WHEN** an organization admin submits an invite request
- **THEN** the invitation is created and sent
- **AND** a 201 Created response is returned

#### Scenario: Member cannot invite
- **WHEN** an organization member (non-admin/owner) attempts to invite
- **THEN** a 403 Forbidden response is returned

#### Scenario: Promote member to admin (owner-only)
- **WHEN** an organization owner changes a member's role to admin
- **THEN** the member gains admin privileges
- **AND** the role change is persisted
- **AND** a 200 OK response is returned
