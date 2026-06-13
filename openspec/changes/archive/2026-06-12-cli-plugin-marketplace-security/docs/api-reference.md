# API Reference

Base URL: `https://<marketplace-domain>/api/v1`

All endpoints require authentication unless marked otherwise. Authenticate with Bearer token:
```
Authorization: Bearer <token>
```

Error responses follow [RFC 7807 Problem Details](https://tools.ietf.org/html/rfc7807):
```json
{
  "type": "https://example.com/errors/validation",
  "title": "Validation Error",
  "status": 400,
  "detail": "Required field missing: name",
  "instance": "/api/v1/plugins/submit"
}
```

---

## Submission

### POST /api/v1/plugins/submit

Submit a plugin for security analysis.

**Auth**: Required (author)

**Request**: `multipart/form-data`

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `package` | file | Yes | Plugin package (tar.gz or zip), max 50MB |
| `name` | string | Yes | Plugin name, 1–128 chars |
| `description` | string | Yes | Plugin description, 1–500 chars |
| `author` | string | Yes | Author email or handle |
| `version` | string | Yes | Semantic version (e.g., "1.0.0") |
| `types` | array | Yes | Plugin types (e.g., ["skill"]) |
| `languages` | array | Yes | Languages used (e.g., ["typescript"]) |

**Response (202 Accepted)**:
```json
{
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "pluginId": "550e8400-e29b-41d4-a716-446655440001",
  "status": "queued",
  "message": "Plugin submitted for security analysis"
}
```

**Error Codes**:

| Code | Meaning |
|------|---------|
| 400 | Missing/invalid field, corrupted package, invalid format |
| 409 | Duplicate submission (same name + version) |
| 429 | Rate limit exceeded |

---

## Analysis

### GET /api/v1/plugins/{pluginId}/analysis

Get analysis results for a submitted plugin.

**Auth**: Required (author or admin)

**Response (200 OK)** — Analysis in progress:
```json
{
  "status": "processing",
  "progress": 50,
  "currentStep": "static_analysis",
  "queuePosition": 3
}
```

**Response (200 OK)** — Analysis completed:
```json
{
  "id": "analysis_r1s2t3u4",
  "pluginId": "plugin_p1b2c3d4",
  "pluginVersion": "1.0.0",
  "status": "completed",
  "result": "passed",
  "scores": {
    "total": 85.5,
    "static": 90.0,
    "dynamic": 75.0
  },
  "staticAnalysis": {
    "eslint": {
      "score": 95.0,
      "errors": 0,
      "warnings": 2,
      "findings": [...]
    },
    "semgrep": {
      "score": 90.0,
      "findings": [...]
    },
    "gitleaks": {
      "score": 100.0,
      "findings": []
    },
    "trivy": {
      "score": 80.0,
      "findings": [...]
    }
  },
  "dynamicAnalysis": {
    "score": 75.0,
    "findings": [...],
    "behaviors": {
      "fileAccess": [...],
      "networkAccess": [],
      "processSpawn": [...]
    },
    "timeout": false,
    "error": null
  },
  "thresholds": {
    "pass": 80,
    "fail": 50,
    "staticWeight": 0.6,
    "dynamicWeight": 0.4
  },
  "inCatalog": true,
  "canAppeal": false,
  "createdAt": "2026-06-08T00:00:00Z",
  "completedAt": "2026-06-08T00:05:00Z"
}
```

**Error Codes**:

| Code | Meaning |
|------|---------|
| 404 | Plugin not found |

---

## Appeals

### POST /api/v1/plugins/{pluginId}/appeal

Submit an appeal for a failed analysis.

**Auth**: Required (plugin author)

**Request**:
```json
{
  "reason": "False positive on network access detection",
  "evidence": "The network access is to localhost:3000 for development purposes",
  "findingId": "finding_f1g2h3i4"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reason` | string | Yes | Explanation of why analysis was incorrect |
| `evidence` | string | No | Supporting evidence or arguments |
| `findingId` | string | No | Specific finding being disputed |

**Response (201 Created)**:
```json
{
  "appealId": "appeal_a1b2c3d4",
  "pluginId": "plugin_p1b2c3d4",
  "status": "pending",
  "createdAt": "2026-06-08T12:00:00Z",
  "message": "Appeal submitted for review"
}
```

### GET /api/v1/plugins/{pluginId}/appeal

Get the current appeal status for a plugin.

**Auth**: Required (plugin author or admin)

**Response (200 OK)**:
```json
{
  "id": "appeal_a1b2c3d4",
  "pluginId": "plugin_p1b2c3d4",
  "reason": "False positive on network access",
  "evidence": "The network access is to localhost:3000...",
  "status": "pending",
  "createdAt": "2026-06-08T12:00:00Z",
  "resolvedAt": null,
  "resolution": null
}
```

**Error Codes**:

| Code | Meaning |
|------|---------|
| 404 | No appeal found for this plugin |
| 403 | Not the plugin author |

---

## Safe Zone

### GET /api/v1/safe-zone/{orgId}/plugins

List plugins approved for an organization's safe zone.

**Auth**: Required (org member)

**Query Parameters**:
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number |
| `limit` | integer | 20 | Items per page (max 100) |
| `status` | string | — | Filter by security status |
| `search` | string | — | Search by name or author |

**Response (200 OK)**:
```json
{
  "data": [
    {
      "pluginId": "plugin_p1b2c3d4",
      "name": "My Plugin",
      "version": "1.0.0",
      "description": "Plugin description",
      "author": "plugin-author",
      "securityScore": 85.5,
      "securityStatus": "passed",
      "approvedAt": "2026-06-01T00:00:00Z",
      "approvedBy": "admin@org.com",
      "isGlobal": false
    }
  ],
  "totalCount": 1,
  "page": 1,
  "limit": 20,
  "totalPages": 1
}
```

### POST /api/v1/safe-zone/{orgId}/plugins/{pluginId}/versions/{version}/approve

Approve a plugin version for an organization.

**Auth**: Required (org admin)

**Request**:
```json
{
  "reason": "Approved for team use"
}
```

**Response (200 OK)**:
```json
{
  "message": "Plugin approved for organization",
  "pluginId": "plugin_p1b2c3d4",
  "version": "1.0.0",
  "orgId": "org_o1p2q3r4",
  "approvedAt": "2026-06-08T10:00:00Z"
}
```

### POST /api/v1/safe-zone/{orgId}/plugins/{pluginId}/versions/{version}/reject

Reject a plugin version for an organization.

**Auth**: Required (org admin)

**Request**:
```json
{
  "reason": "Security concerns — does not meet org standards"
}
```

**Response (200 OK)**:
```json
{
  "message": "Plugin rejected for organization",
  "pluginId": "plugin_p1b2c3d4",
  "version": "1.0.0",
  "orgId": "org_o1p2q3r4",
  "rejectedAt": "2026-06-08T10:00:00Z"
}
```

### DELETE /api/v1/safe-zone/{orgId}/plugins/{pluginId}/versions/{version}

Remove a plugin from an organization's safe zone.

**Auth**: Required (org admin)

**Response (200 OK)**:
```json
{
  "message": "Plugin removed from organization safe zone",
  "pluginId": "plugin_p1b2c3d4",
  "version": "1.0.0",
  "orgId": "org_o1p2q3r4"
}
```

### GET /api/v1/safe-zone/{orgId}/pending

List pending plugin approval requests.

**Auth**: Required (org admin)

**Response (200 OK)**:
```json
{
  "data": [
    {
      "requestId": "req_r1s2t3u4",
      "pluginId": "plugin_p1b2c3d4",
      "pluginName": "My Plugin",
      "version": "1.0.0",
      "requestedBy": "member@org.com",
      "requestedAt": "2026-06-07T00:00:00Z",
      "status": "pending"
    }
  ],
  "totalCount": 1
}
```

### POST /api/v1/safe-zone/{orgId}/plugins/{pluginId}/versions/{version}/request

Request approval for a plugin.

**Auth**: Required (org member)

**Request**:
```json
{
  "reason": "Plugin required for CI pipeline"
}
```

**Response (201 Created)**:
```json
{
  "requestId": "req_r1s2t3u4",
  "status": "pending",
  "message": "Approval request submitted"
}
```

### GET /api/v1/safe-zone/global

List globally approved plugins.

**Auth**: Required (any authenticated user)

**Response (200 OK)**:
```json
{
  "data": [
    {
      "pluginId": "plugin_g1h2i3j4",
      "name": "Global Plugin",
      "version": "2.0.0",
      "securityScore": 95.0,
      "approvedAt": "2026-01-01T00:00:00Z"
    }
  ]
}
```

### POST /api/v1/safe-zone/global/block

Block a global plugin for your org.

**Auth**: Required (org admin)

**Request**:
```json
{
  "pluginId": "plugin_g1h2i3j4",
  "orgId": "org_o1p2q3r4"
}
```

**Response (200 OK)**:
```json
{
  "message": "Global plugin blocked for organization"
}
```

### DELETE /api/v1/safe-zone/global/block

Unblock a global plugin for your org.

**Auth**: Required (org admin)

**Query Parameters**:
| Parameter | Required | Description |
|-----------|----------|-------------|
| `pluginId` | Yes | Plugin to unblock |
| `orgId` | Yes | Organization to unblock for |

**Response (200 OK)**:
```json
{
  "message": "Global plugin unblocked for organization"
}
```

**Safe Zone Error Codes**:

| Code | Meaning |
|------|---------|
| 403 | Not a member of this org, or plugin not in safe zone |
| 404 | Org not found, plugin not in catalog |
| 409 | Plugin already approved for this org |

---

## Control Center

### GET /api/v1/control-center/metrics

System-wide metrics for admin dashboard.

**Auth**: Required (admin)

**Response (200 OK)**:
```json
{
  "system": {
    "totalPlugins": 150,
    "totalOrgs": 25,
    "totalUsers": 500,
    "totalAnalyses": 1000,
    "totalAppeals": 50,
    "totalSafeZoneApprovals": 750
  },
  "analysis": {
    "queueLength": 5,
    "inProgress": 2,
    "completedToday": 45,
    "failedToday": 2,
    "avgProcessingTimeMs": 120000,
    "failureRate": 0.04
  },
  "appeals": {
    "pending": 5,
    "approvedToday": 3,
    "rejectedToday": 1,
    "avgResolutionTimeHours": 18.5,
    "falsePositiveRate": 0.08
  },
  "security": {
    "totalFindings": 250,
    "bySeverity": {
      "critical": 5,
      "high": 40,
      "medium": 120,
      "low": 85
    },
    "secretDetections": 12
  }
}
```

### GET /api/v1/control-center/appeals

List appeals with filtering.

**Auth**: Required (admin)

**Query Parameters**:
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `status` | string | — | `pending`, `approved`, `rejected` |
| `page` | integer | 1 | Page number |
| `limit` | integer | 20 | Items per page |

**Response (200 OK)**: Array of appeal objects (see Appeals section).

### GET /api/v1/control-center/appeals/{appealId}

Get detailed appeal with full analysis results and plugin source.

**Auth**: Required (admin)

### POST /api/v1/control-center/appeals/{appealId}/approve

Approve an appeal.

**Auth**: Required (super-admin)

**Request**:
```json
{
  "resolution": "Network access to localhost is acceptable behavior",
  "overrideScore": 85.0
}
```

**Response (200 OK)**: Appeal approved confirmation.

### POST /api/v1/control-center/appeals/{appealId}/reject

Reject an appeal.

**Auth**: Required (super-admin)

**Request**:
```json
{
  "resolution": "Network access to external servers violates security policy",
  "reason": "security_policy_violation"
}
```

**Response (200 OK)**: Appeal rejected confirmation.

### GET /api/v1/control-center/config

Get full system configuration.

**Auth**: Required (admin)

**Response (200 OK)**: See [Analysis Configuration](analysis-configuration.md) for schema.

### PUT /api/v1/control-center/config/analysis

Update analysis configuration. Partial update — omitted fields retain current values.

**Auth**: Required (super-admin)

**Request**:
```json
{
  "static_weight": 0.7,
  "dynamic_weight": 0.3,
  "pass_threshold": 85,
  "fail_threshold": 45,
  "max_workers": 4,
  "retry_limit": 3
}
```

**Response (200 OK)**:
```json
{
  "message": "Configuration updated",
  "updatedFields": ["analysis.static_weight", "analysis.dynamic_weight", "analysis.pass_threshold", "analysis.fail_threshold"]
}
```

### GET /api/v1/control-center/config/history

Get configuration change history.

**Auth**: Required (admin)

**Query Parameters**: `page`, `limit`, `startDate`, `endDate`

### GET /api/v1/control-center/audit-log

Get paginated, filterable audit log.

**Auth**: Required (admin)

**Query Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| `action` | string | Filter by action type |
| `startDate` | string (ISO 8601) | Range start |
| `endDate` | string (ISO 8601) | Range end |
| `page` | integer | Page number |
| `limit` | integer | Items per page |

**Response (200 OK)**:
```json
{
  "data": [
    {
      "id": "log_l0g1d2a3",
      "timestamp": "2026-06-08T12:00:00Z",
      "adminId": "admin_a1b2c3d4",
      "adminName": "Super Admin",
      "action": "appeal_approve",
      "resourceType": "appeal",
      "resourceId": "appeal_a1b2c3d4",
      "details": {
        "pluginId": "plugin_p1b2c3d4",
        "resolution": "Appeal approved"
      },
      "ipAddress": "192.168.1.1"
    }
  ],
  "totalCount": 100,
  "page": 1,
  "limit": 50,
  "totalPages": 2
}
```

---

## Reputation

### GET /api/v1/reputation/leaderboard

Global or org-filtered leaderboard.

**Auth**: Optional

**Query Parameters**:
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `orgId` | string (UUID) | — | Filter by org |
| `timeRange` | enum | `all-time` | `all-time`, `weekly`, `monthly` |
| `page` | integer | 1 | Page number |
| `limit` | integer | 50 | Items per page (max 100) |

**Response (200 OK)**:
```json
{
  "data": [
    {
      "rank": 1,
      "authorId": "author_u1v2w3x4",
      "authorName": "Top Author",
      "karma": 1520,
      "level": 16,
      "badges": ["100 Submissions", "Security Champion"],
      "pluginCount": 120,
      "totalDownloads": 85000,
      "avgScore": 93.1
    }
  ],
  "totalCount": 500,
  "page": 1,
  "limit": 50,
  "totalPages": 10
}
```

### GET /api/v1/reputation/authors/{authorId}

Get author's full reputation profile.

**Auth**: Optional

**Response (200 OK)**:
```json
{
  "id": "author_u1v2w3x4",
  "name": "Plugin Author",
  "karma": 350,
  "level": 4,
  "levelName": "Veteran",
  "badges": [
    {
      "name": "10 Submissions",
      "description": "Submitted 10 plugins",
      "rarity": "Common",
      "earnedAt": "2026-03-15T10:00:00Z"
    }
  ],
  "stats": {
    "pluginCount": 15,
    "totalDownloads": 3200,
    "avgScore": 87.3,
    "passRate": 0.85,
    "appealSuccessRate": 0.75
  },
  "karmaHistory": [
    {
      "date": "2026-06-01",
      "karma": 340,
      "change": 10,
      "reason": "plugin_submitted",
      "pluginId": "plugin_p1b2c3d4"
    }
  ],
  "joinedAt": "2025-06-01T00:00:00Z"
}
```

### GET /api/v1/reputation/authors/{authorId}/plugins

List an author's submitted plugins.

**Auth**: Optional

### GET /api/v1/reputation/badges

List all available badges.

**Auth**: Optional

**Response (200 OK)**:
```json
{
  "data": [
    {
      "name": "Security Champion",
      "description": "5 consecutive clean passes",
      "rarity": "Rare",
      "requirement": "5+ plugins with zero findings",
      "icon": "security-champion.png"
    }
  ],
  "totalCount": 10
}
```

---

## Notifications

### GET /api/v1/notifications

List notifications for the authenticated user.

**Auth**: Required

**Query Parameters**:
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number |
| `limit` | integer | 20 | Items per page |
| `unreadOnly` | boolean | false | Show only unread notifications |

**Response (200 OK)**:
```json
{
  "data": [
    {
      "id": "notif_n1o2p3q4",
      "type": "appeal_resolved",
      "title": "Appeal Approved",
      "message": "Your appeal for My Plugin (v1.0.0) has been approved",
      "data": {
        "appealId": "appeal_a1b2c3d4",
        "pluginId": "plugin_p1b2c3d4"
      },
      "read": false,
      "createdAt": "2026-06-08T12:00:00Z"
    }
  ],
  "totalCount": 25,
  "unreadCount": 3
}
```

### PUT /api/v1/notifications/{notificationId}/read

Mark a single notification as read.

**Auth**: Required

**Response (200 OK)**:
```json
{
  "message": "Notification marked as read"
}
```

### PUT /api/v1/notifications/read-all

Mark all notifications as read.

**Auth**: Required

### PUT /api/v1/notifications/preferences

Update notification preferences.

**Auth**: Required

**Request**:
```json
{
  "channels": {
    "in_app": true,
    "email": false
  },
  "types": {
    "appeal_submitted": true,
    "appeal_resolved": true,
    "analysis_completed": true,
    "system_alert": true,
    "config_changed": false
  }
}
```

**Response (200 OK)**:
```json
{
  "message": "Notification preferences updated"
}
```

---

## Organizations

### PUT /api/v1/organizations/active

Set the active organization context for the current user.

**Auth**: Required

**Request**:
```json
{
  "orgId": "org_o1p2q3r4"
}
```

**Response (200 OK)**:
```json
{
  "message": "Active organization set",
  "orgId": "org_o1p2q3r4",
  "orgName": "My Org"
}
```

### GET /api/v1/organizations/active

Get the current active organization.

**Auth**: Required

**Response (200 OK)**:
```json
{
  "orgId": "org_o1p2q3r4",
  "name": "My Org",
  "role": "admin"
}
```

### GET /api/v1/organizations/{orgId}/requests

List approval requests for the organization (admin only).

**Auth**: Required (org admin)

---

## Common Error Codes

| Code | Meaning |
|------|---------|
| 400 | Bad request — validation failure, missing field, invalid format |
| 401 | Authentication required — missing or invalid token |
| 403 | Forbidden — insufficient permissions |
| 404 | Resource not found |
| 409 | Conflict — duplicate submission, already approved |
| 429 | Rate limit exceeded — wait for `Retry-After` header |
| 500 | Internal server error |
