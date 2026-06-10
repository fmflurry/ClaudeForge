# Safe Zone Setup Guide for Organization Admins

## What Is a Safe Zone?

A **safe zone** is an org-isolated collection of approved plugins. Each organization has its own safe zone containing only plugins that have been vetted and approved by the org admin. This prevents org members from installing unverified or potentially malicious plugins.

**Key principles:**
- **Org isolation**: One org's safe zone is invisible to other orgs
- **Admin-gated**: Only org admins approve plugins for their safe zone
- **Version-pinned**: Approval is per-plugin-version; new versions require re-approval
- **Global safe zone**: Super-admin can designate plugins as globally available to all orgs

---

## Creating an Organization

### Via CLI

```bash
claudeforge org register "My Org" --slug my-org
```

**Response**:
```
Organization registered:
  ID:        org_a1b2c3d4
  Name:      My Org
  Slug:      my-org
  Role:      admin
  Created:   2026-06-08T10:00:00Z
```

### Via API

```http
POST /api/v1/organizations
Content-Type: application/json
Authorization: Bearer <token>

{
  "name": "My Org",
  "slug": "my-org"
}
```

**Response (201 Created)**:
```json
{
  "id": "org_a1b2c3d4",
  "name": "My Org",
  "slug": "my-org",
  "createdAt": "2026-06-08T10:00:00Z"
}
```

---

## Setting the Active Organization

Most CLI commands operate in the context of your **active org**. Set it with:

```bash
claudeforge org use org_a1b2c3d4
```

View the current active org:

```bash
claudeforge org show
```

List all orgs you belong to:

```bash
claudeforge org list
```

The active org is persisted in `~/.claude-plugins/context.json`.

**Output example**:
```
Active Organization:
  ID:     org_a1b2c3d4
  Name:   My Org
  Role:   admin

Your Organizations:
  org_a1b2c3d4  My Org              admin
  org_x1y2z3    Community Plugins   member
```

---

## Approving Plugins for Your Org

### View Pending Approval Requests

```bash
claudeforge org list --pending
```

This shows plugins that org members have requested.

### Approve a Plugin

```bash
# Approve latest version
claudeforge approve plugin_p1b2c3d4

# Approve specific version
claudeforge approve plugin_p1b2c3d4 --version 2.1.0
```

### Via API

```http
POST /api/v1/organizations/{orgId}/plugins/{pluginId}/versions/{version}/approve
Content-Type: application/json
Authorization: Bearer <admin_token>

{
  "reason": "Approved for team use — passes all security checks"
}
```

**Response**:
```json
{
  "message": "Plugin approved for organization",
  "pluginId": "plugin_p1b2c3d4",
  "version": "2.1.0",
  "orgId": "org_a1b2c3d4",
  "approvedAt": "2026-06-08T10:00:00Z"
}
```

### Reject a Plugin

```http
POST /api/v1/organizations/{orgId}/plugins/{pluginId}/versions/{version}/reject
Content-Type: application/json
Authorization: Bearer <admin_token>

{
  "reason": "Security concerns — analysis score below org threshold"
}
```

---

## Viewing Safe Zone Plugins

### Via CLI

```bash
claudeforge list --safe-zone
```

**Output**:
```
Name            Version   Author        Score   Status    Approved
──────────────  ────────  ────────────  ──────  ────────  ──────────
my-plugin       2.1.0     bob@test.com  91.5    passed    2026-06-01
another-plugin  1.0.0     alice@test.c  88.0    passed    2026-06-05
```

### Via API

```http
GET /api/v1/organizations/{orgId}/plugins
Authorization: Bearer <user_token>
```

---

## Requesting Plugin Approval

Org members can request plugin approval without admin intervention:

### Via CLI

```bash
claudeforge org request-approval plugin_p1b2c3d4 --reason "Needed for CI pipeline"
```

**Response**:
```
Approval request submitted:
  Plugin:  my-plugin (v2.1.0)
  Org:     My Org
  Status:  pending
  Request: req_r1s2t3u4
```

### Via API

```http
POST /api/v1/organizations/{orgId}/plugins/{pluginId}/versions/{version}/request
Content-Type: application/json
Authorization: Bearer <user_token>

{
  "reason": "Plugin is required for our CI pipeline"
}
```

After submission, the org admin receives a notification and can approve or reject.

---

## Global Safe Zone and Blocking Globals

The **global safe zone** contains plugins that super-admins have approved for all organizations. These appear in every org's safe zone automatically.

### View Global Safe Zone

```bash
claudeforge list --global-safe-zone
```

### Blocking a Global Plugin for Your Org

Org admins can block a globally-approved plugin from their org's safe zone:

```bash
claudeforge org block plugin_p1b2c3d4 --reason "Not compatible with our infrastructure"
```

### Via API

```http
POST /api/v1/safe-zone/global/block
Content-Type: application/json
Authorization: Bearer <admin_token>

{
  "pluginId": "plugin_p1b2c3d4",
  "orgId": "org_a1b2c3d4"
}
```

**Response**:
```json
{
  "message": "Global plugin blocked for organization",
  "pluginId": "plugin_p1b2c3d4",
  "orgId": "org_a1b2c3d4"
}
```

To unblock:

```http
DELETE /api/v1/safe-zone/global/block?pluginId=plugin_p1b2c3d4&orgId=org_a1b2c3d4
```

---

## Installing from Safe Zone

### Via CLI

Members install plugins exclusively from their org's safe zone:

```bash
claudeforge install --safe-zone-only my-plugin
```

If the plugin is not approved for the active org's safe zone, the CLI returns:

```
Error: Plugin "my-plugin" not approved for your organization.
Contact your admin to request approval.
```

Or to request approval directly from the error:

```bash
claudeforge install --safe-zone-only --request-if-missing my-plugin
```

This auto-creates an approval request.

### Default Behavior

- `claudeforge install <plugin>` checks safe zone first, falls back to global catalog
- `claudeforge install --safe-zone-only` strictly restricts to safe zone
- `claudeforge install --no-safe-zone` bypasses safe zone (requires super-admin)

---

## Inviting Members and Managing Roles

### Guest Membership

Org admins can invite members to their org:

```bash
claudeforge org invite user@example.com --role member
```

### Role Types

| Role | Permissions |
|------|-------------|
| `member` | View approved plugins, request new plugins |
| `admin` | Approve/reject plugins, manage members |

### Member Management via API

```http
# Add member
POST /api/v1/organizations/{orgId}/members
Authorization: Bearer <admin_token>
{
  "userId": "user_u1v2w3x4",
  "role": "member"
}

# Remove member
DELETE /api/v1/organizations/{orgId}/members/user_u1v2w3x4

# Change role
PATCH /api/v1/organizations/{orgId}/members/user_u1v2w3x4
{
  "role": "admin"
}

# List members
GET /api/v1/organizations/{orgId}/members
```

### Permission Matrix

| Action | Member | Admin | Super-Admin |
|--------|--------|-------|-------------|
| View safe zone plugins | ✅ | ✅ | ✅ |
| Request plugin approval | ✅ | ✅ | ✅ |
| Approve/reject plugins | ❌ | ✅ | ✅ |
| Remove plugins from safe zone | ❌ | ✅ | ✅ |
| Manage org members | ❌ | ✅ | ✅ |
| Manage global safe zone | ❌ | ❌ | ✅ |
| Configure system settings | ❌ | ❌ | ✅ |

---

## Audit Logging

All safe zone actions are logged:

- Plugin approval/rejection/removal
- Member addition/removal and role changes
- Org context switches
- Global safe zone changes

View the audit log:

### Via CLI

```bash
claudeforge org audit-log
```

### Via API

```http
GET /api/v1/control-center/audit-log?action=plugin_approve&orgId=org_a1b2c3d4
Authorization: Bearer <admin_token>
```

---

## Best Practices

1. **Pin versions**: Always approve specific plugin versions; avoid blanket approvals
2. **Review analysis results**: Check findings before approving — don't rely on score alone
3. **Periodic review**: Re-evaluate safe zone plugins quarterly
4. **Least privilege**: Grant admin role only to users who need it
5. **Use block list**: Block globally-available plugins that don't fit your org's security posture
