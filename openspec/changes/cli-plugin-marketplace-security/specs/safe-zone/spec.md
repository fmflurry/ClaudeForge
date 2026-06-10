# Safe Zone Specification

## ADDED Requirements

### Requirement: Provide org-isolated plugin access

The system SHALL allow organizations to create safe zones with approved plugins.

#### Scenario: Org admin creates safe zone
**WHEN** an org admin creates a new organization

**THEN** the system SHALL:
- Create a new organization record
- Create an empty safe zone for that organization
- Add the admin as first member with admin role

#### Scenario: Org admin approves plugin for safe zone
**WHEN** an org admin approves a plugin for their organization's safe zone

**THEN** the system SHALL:
- Add plugin to safe zone with approved status
- Record approver and timestamp
- Make plugin available to all org members

### Requirement: Filter plugin listings by safe zone

The system SHALL filter plugin listings based on user's org membership.

#### Scenario: User lists plugins for their org
**WHEN** a user requests plugins for their organization

**THEN** the system SHALL:
- Verify user's org membership
- Return only plugins approved for that org's safe zone
- Include security scores and badges

#### Scenario: User from different org requests plugins
**WHEN** a user from Org A requests plugins

**THEN** the system SHALL:
- Return only plugins approved for Org A's safe zone
- NOT include plugins from Org B's safe zone

### Requirement: Enforce safe zone access control

The system SHALL enforce that users can only access plugins approved for their organization.

#### Scenario: User attempts to access unapproved plugin
**WHEN** a user from Org A attempts to access a plugin not in Org A's safe zone

**THEN** the system SHALL return HTTP 403 with error message `"Plugin not available for your organization"`

#### Scenario: User attempts to install unapproved plugin via CLI
**WHEN** a user from Org A runs `claude plugin install <plugin>`
**AND** plugin is not in Org A's safe zone

**THEN** the CLI SHALL:
- Check safe zone membership
- Return error: "Plugin not approved for your organization. Contact your admin."
- Exit with non-zero status

### Requirement: Support multi-org membership

The system SHALL support users belonging to multiple organizations.

#### Scenario: User belongs to multiple organizations
**WHEN** a user is member of Org A and Org B
**AND** requests plugins

**THEN** the system SHALL:
- Return union of plugins from both org safe zones
- Deduplicate plugins
- Indicate which org approved each plugin

#### Scenario: User switches organization context
**WHEN** a user switches from Org A to Org B context
**AND** requests plugins

**THEN** the system SHALL:
- Return only plugins from Org B's safe zone
- Remember user's org context for subsequent requests

### Requirement: Provide safe zone management for admins

The system SHALL provide endpoints for org admins to manage their safe zone.

#### Scenario: Org admin lists pending approvals
**WHEN** an org admin requests pending approvals

**THEN** the system SHALL return:
- List of plugins submitted for approval
- Submission date
- Requester
- Current status

#### Scenario: Org admin approves plugin for safe zone
**WHEN** an org admin approves a plugin

**THEN** the system SHALL:
- Add plugin to safe zone
- Record approval
- Notify requester
- Make plugin available to all org members

#### Scenario: Org admin rejects plugin for safe zone
**WHEN** an org admin rejects a plugin

**THEN** the system SHALL:
- Record rejection
- Notify requester with reason
- Plugin remains unavailable to org members

#### Scenario: Org admin removes plugin from safe zone
**WHEN** an org admin removes a plugin from safe zone

**THEN** the system SHALL:
- Remove plugin from safe zone
- Record removal
- Plugin becomes unavailable to org members
- Existing installations continue to work (no forced removal)

### Requirement: Support plugin versioning in safe zone

The system SHALL track which versions of plugins are approved for each org.

#### Scenario: Org admin approves specific plugin version
**WHEN** an org admin approves plugin "MyPlugin" version "1.0.0"

**THEN** the system SHALL:
- Approve only version "1.0.0" for the org
- Org members can access version "1.0.0"
- Org members CANNOT access other versions unless also approved

#### Scenario: New version of approved plugin released
**WHEN** plugin "MyPlugin" version "2.0.0" is released
**AND** version "1.0.0" is approved for Org A

**THEN** the system SHALL:
- Version "2.0.0" is NOT automatically approved
- Org admin must explicitly approve version "2.0.0"
- Org members can still access version "1.0.0"

### Requirement: Inherit global safe plugins

The system SHALL support a global safe zone with plugins available to all organizations.

#### Scenario: Plugin approved for global safe zone
**WHEN** a super-admin approves a plugin for global safe zone

**THEN** the system SHALL:
- Make plugin available to all organizations
- Org admins can still override (block) for their org
- Plugin appears in all org listings

#### Scenario: Org admin blocks global plugin
**WHEN** an org admin blocks a globally approved plugin

**THEN** the system SHALL:
- Plugin is NOT available to that org's members
- Override takes precedence over global approval

---

## API Contract

### GET /api/v1/organizations/{orgId}/plugins
**Request**:
```
Headers:
  Authorization: Bearer <user_token>
```

**Response (200 OK)**:
```json
{
  "data": [
    {
      "pluginId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "My Plugin",
      "version": "1.0.0",
      "description": "Plugin description",
      "author": "plugin-author",
      "securityScore": 85.5,
      "securityStatus": "passed",
      "approvedAt": "2024-01-01T00:00:00Z",
      "approvedBy": "org-admin",
      "isGlobal": false
    }
  ],
  "totalCount": 1,
  "page": 1,
  "limit": 20,
  "totalPages": 1
}
```

### POST /api/v1/organizations/{orgId}/plugins/{pluginId}/versions/{version}/approve
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>

Body:
{
  "reason": "Approved for team use"
}
```

**Response (200 OK)**:
```json
{
  "message": "Plugin approved for organization",
  "pluginId": "550e8400-e29b-41d4-a716-446655440000",
  "version": "1.0.0",
  "orgId": "550e8400-e29b-41d4-a716-446655440002",
  "approvedAt": "2024-01-01T00:00:00Z"
}
```

### POST /api/v1/organizations/{orgId}/plugins/{pluginId}/versions/{version}/reject
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>

Body:
{
  "reason": "Security concerns"
}
```

**Response (200 OK)**:
```json
{
  "message": "Plugin rejected for organization",
  "pluginId": "550e8400-e29b-41d4-a716-446655440000",
  "version": "1.0.0",
  "orgId": "550e8400-e29b-41d4-a716-446655440002",
  "rejectedAt": "2024-01-01T00:00:00Z"
}
```

### DELETE /api/v1/organizations/{orgId}/plugins/{pluginId}/versions/{version}
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>
```

**Response (200 OK)**:
```json
{
  "message": "Plugin removed from organization safe zone",
  "pluginId": "550e8400-e29b-41d4-a716-446655440000",
  "version": "1.0.0",
  "orgId": "550e8400-e29b-41d4-a716-446655440002"
}
```

### GET /api/v1/organizations/{orgId}/pending
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>
```

**Response (200 OK)**:
```json
{
  "data": [
    {
      "requestId": "550e8400-e29b-41d4-a716-446655440003",
      "pluginId": "550e8400-e29b-41d4-a716-446655440000",
      "pluginName": "My Plugin",
      "version": "1.0.0",
      "requestedBy": "user-123",
      "requestedAt": "2024-01-01T00:00:00Z",
      "status": "pending"
    }
  ],
  "totalCount": 1
}
```

---

## CLI Integration

### claude plugin list
**Command**:
```bash
claude plugin list [--org <org>] [--safe-zone] [--all]
```

**Behavior**:
- Without `--org`: Lists plugins for user's current org context
- With `--org <org>`: Lists plugins for specified org
- With `--safe-zone`: Only shows safe zone approved plugins
- With `--all`: Shows all plugins (ignores safe zone)

**Output**:
```
Name          Version   Author          Score   Status    Approved
────────────  ───────  ──────────────  ──────  ───────  ─────────
my-plugin     1.0.0     author@test.com  85.5    passed   2024-01-01
another-plugin 2.0.0   author2@test.com 78.0   passed   2024-01-02
```

### claude plugin install
**Command**:
```bash
claude plugin install <plugin> [--version <version>] [--org <org>]
```

**Behavior**:
- Checks if plugin is in safe zone for user's org (or specified org)
- If not approved: Error with instructions to contact admin
- If approved: Proceeds with installation
- If `--version` specified: Checks that specific version is approved

**Error Messages**:
- "Plugin not found"
- "Plugin not approved for your organization. Contact your admin to request approval."
- "Version X.Y.Z not approved. Approved versions: A.B.C, D.E.F"

---

## Access Control Rules

### User Roles
| Role | Permissions |
|------|-------------|
| member | View approved plugins, request new plugins |
| admin | Approve/reject plugins, manage members |
| super-admin | All permissions, manage all orgs |

### Permission Matrix
| Action | Member | Admin | Super-Admin |
|--------|--------|-------|-------------|
| View safe zone plugins | ✅ | ✅ | ✅ |
| Request plugin approval | ✅ | ✅ | ✅ |
| Approve plugin | ❌ | ✅ | ✅ |
| Reject plugin | ❌ | ✅ | ✅ |
| Remove plugin | ❌ | ✅ | ✅ |
| Manage org members | ❌ | ✅ | ✅ |
| Manage org settings | ❌ | ✅ | ✅ |
| Manage global safe zone | ❌ | ❌ | ✅ |

---

## Organization Context

### Context Storage
- CLI: `~/.claude-plugins/context.json`
- Web: Browser localStorage under `plugin-marketplace:org-context`

### Context Structure
```json
{
  "currentOrgId": "550e8400-e29b-41d4-a716-446655440002",
  "orgMemberships": [
    {
      "orgId": "550e8400-e29b-41d4-a716-446655440002",
      "role": "admin",
      "name": "My Org"
    },
    {
      "orgId": "550e8400-e29b-41d4-a716-446655440003",
      "role": "member",
      "name": "Other Org"
    }
  ]
}
```

### Context Switching
**CLI Command**:
```bash
claude plugin org switch <org>
claude plugin org list
claude plugin org current
```

---

## Plugin Request Workflow

### User Requests Plugin
1. User finds plugin in catalog
2. User clicks "Request for my org" or runs `claude plugin request <plugin>`
3. System creates pending approval request
4. Org admin notified

### Admin Approves/Rejects
1. Admin views pending requests
2. Admin reviews plugin details and analysis results
3. Admin approves or rejects with reason
4. User notified of decision

### Automatic Approval (Optional)
**Configuration**:
```yaml
safe_zone:
  auto_approve:
    enabled: true
    min_score: 90  # Auto-approve plugins with score ≥ 90
    max_severity: "medium"  # Auto-approve if no high/critical findings
```

---

## Error Messages

| Error | HTTP Status | Message |
|-------|-------------|---------|
| Org not found | 404 | "Organization not found" |
| User not member | 403 | "You are not a member of this organization" |
| Insufficient permissions | 403 | "Insufficient permissions. Admin access required." |
| Plugin not in safe zone | 403 | "Plugin not available for your organization" |
| Version not approved | 403 | "Version not approved for your organization" |
| Plugin already approved | 409 | "Plugin already approved for this organization" |
| Plugin not in catalog | 404 | "Plugin not found in catalog" |

---

## Audit Logging

The system SHALL log all safe zone actions for auditing.

### Logged Actions
- Plugin approval
- Plugin rejection
- Plugin removal
- Member addition/removal
- Role changes
- Context switches

### Log Structure
```json
{
  "timestamp": "2024-01-01T00:00:00Z",
  "action": "plugin_approve",
  "orgId": "550e8400-e29b-41d4-a716-446655440002",
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "pluginId": "550e8400-e29b-41d4-a716-446655440001",
  "version": "1.0.0",
  "metadata": {
    "reason": "Approved for team use"
  }
}
```

---

## Performance Requirements

- **Plugin listing**: ≤ 500ms for org with 1000 approved plugins
- **Approval/rejection**: ≤ 200ms
- **Access check**: ≤ 100ms
- **Audit log write**: ≤ 50ms
