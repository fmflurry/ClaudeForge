# Control Center Specification

## ADDED Requirements

### Requirement: Provide admin dashboard for system oversight

The system SHALL provide a web-based dashboard for administrators to monitor and manage the plugin marketplace.

#### Scenario: Admin views system overview
**WHEN** an admin logs into the control center

**THEN** the system SHALL display:
- Total plugins in catalog
- Plugins awaiting analysis
- Plugins in review queue
- Pending appeals
- System health status

#### Scenario: Admin views analysis pipeline status
**WHEN** an admin navigates to the analysis pipeline dashboard

**THEN** the system SHALL display:
- Queue length (jobs waiting)
- Jobs in progress
- Recent completions
- Failure rate
- Average processing time

### Requirement: Provide appeal management interface

The system SHALL allow admins to view and resolve plugin appeals.

#### Scenario: Admin views pending appeals
**WHEN** an admin navigates to the appeals dashboard

**THEN** the system SHALL display:
- List of all pending appeals
- Plugin name and version
- Author
- Submission date
- Appeal reason
- Evidence provided

#### Scenario: Admin reviews appeal details
**WHEN** an admin clicks on an appeal

**THEN** the system SHALL display:
- Full appeal details
- Original analysis results
- Analysis findings (static and dynamic)
- Author's evidence
- Plugin source code (read-only view)
- Previous appeal history (if any)

#### Scenario: Admin approves appeal
**WHEN** an admin approves an appeal

**THEN** the system SHALL:
- Update analysis result to `passed`
- Add plugin to catalog
- Notify author of approval
- Record resolution in appeal history
- Update admin's metrics

#### Scenario: Admin rejects appeal
**WHEN** an admin rejects an appeal

**THEN** the system SHALL:
- Keep analysis result as `failed`
- Notify author of rejection with reason
- Record resolution in appeal history
- Update admin's metrics

### Requirement: Provide system metrics and analytics

The system SHALL track and display key performance metrics.

#### Scenario: Admin views system metrics
**WHEN** an admin navigates to the metrics dashboard

**THEN** the system SHALL display:
- Plugin adoption metrics (downloads, installs)
- Analysis pipeline metrics (throughput, latency)
- Appeal metrics (volume, resolution time)
- Safe zone metrics (org count, plugin approvals)
- Security metrics (false positive rate, findings by severity)

#### Scenario: Admin views time-series data
**WHEN** an admin selects a date range

**THEN** the system SHALL display:
- Daily plugin submissions
- Daily analysis completions
- Daily appeals
- Daily safe zone approvals
- Trend analysis

### Requirement: Provide configuration management

The system SHALL allow admins to configure system parameters.

#### Scenario: Admin updates analysis thresholds
**WHEN** an admin changes the pass threshold from 80 to 85

**THEN** the system SHALL:
- Validate the new threshold (must be > fail threshold)
- Update configuration
- Apply to new submissions immediately
- NOT affect already-completed analyses

#### Scenario: Admin updates analysis weights
**WHEN** an admin changes static weight from 0.6 to 0.7

**THEN** the system SHALL:
- Validate weights sum to 1.0
- Update configuration
- Apply to new submissions immediately

#### Scenario: Admin enables/disables analysis tools
**WHEN** an admin disables Trivy scanning

**THEN** the system SHALL:
- Update configuration
- Trivy skipped for new submissions
- Existing results unchanged

### Requirement: Provide org management interface

The system SHALL allow super-admins to manage organizations.

#### Scenario: Super-admin creates new organization
**WHEN** a super-admin creates a new org

**THEN** the system SHALL:
- Create organization record
- Create empty safe zone
- Add super-admin as first admin
- Generate invite link or token

#### Scenario: Super-admin views all organizations
**WHEN** a super-admin navigates to org management

**THEN** the system SHALL display:
- List of all organizations
- Member counts
- Plugin counts
- Creation dates
- Last activity

#### Scenario: Super-admin manages org members
**WHEN** a super-admin views an organization

**THEN** the system SHALL allow:
- View all members
- Add new members
- Remove members
- Change member roles
- Transfer admin rights

### Requirement: Provide user management for org admins

The system SHALL allow org admins to manage their organization's users.

#### Scenario: Org admin invites new member
**WHEN** an org admin invites a new member

**THEN** the system SHALL:
- Generate invite token
- Send invite (via email or CLI notification)
- Set token expiration (default: 7 days)
- Allow org admin to resend or revoke invite

#### Scenario: Org admin views member list
**WHEN** an org admin navigates to member management

**THEN** the system SHALL display:
- All org members
- Roles
- Join dates
- Last activity
- Plugin usage statistics

#### Scenario: Org admin changes member role
**WHEN** an org admin changes a member's role from member to admin

**THEN** the system SHALL:
- Update member role
- Notify member of role change
- Log the change

### Requirement: Provide audit logging

The system SHALL maintain a complete audit log of all admin actions.

#### Scenario: Admin performs action
**WHEN** any admin performs any action in the control center

**THEN** the system SHALL:
- Log the action with timestamp
- Include admin identifier
- Include action type and details
- Include affected resources
- Store log permanently (or for configurable retention period)

#### Scenario: Admin views audit log
**WHEN** an admin navigates to the audit log

**THEN** the system SHALL display:
- Filterable list of all admin actions
- Timestamp
- Admin
- Action
- Resource
- Details
- IP address (if available)

---

## API Contract

### GET /api/v1/control-center/metrics
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>
```

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
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>
Query:
  status=pending
  page=1
  limit=20
```

**Response (200 OK)**:
```json
{
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "pluginId": "550e8400-e29b-41d4-a716-446655440001",
      "pluginName": "My Plugin",
      "pluginVersion": "1.0.0",
      "authorId": "550e8400-e29b-41d4-a716-446655440002",
      "authorName": "Plugin Author",
      "reason": "False positive on network access detection",
      "evidence": "The network access is to a local test server...",
      "status": "pending",
      "createdAt": "2024-01-01T00:00:00Z",
      "analysisScore": 75.0,
      "analysisResult": "failed"
    }
  ],
  "totalCount": 5,
  "page": 1,
  "limit": 20,
  "totalPages": 1
}
```

### GET /api/v1/control-center/appeals/{appealId}
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>
```

**Response (200 OK)**:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "pluginId": "550e8400-e29b-41d4-a716-446655440001",
  "pluginName": "My Plugin",
  "pluginVersion": "1.0.0",
  "authorId": "550e8400-e29b-41d4-a716-446655440002",
  "authorName": "Plugin Author",
  "authorEmail": "author@example.com",
  "reason": "False positive on network access detection",
  "evidence": "The network access is to a local test server that is part of the plugin's functionality. It only connects to localhost:3000 for development purposes.",
  "status": "pending",
  "createdAt": "2024-01-01T00:00:00Z",
  "analysis": {
    "id": "550e8400-e29b-41d4-a716-446655440003",
    "score": 75.0,
    "result": "failed",
    "staticScore": 80.0,
    "dynamicScore": 60.0,
    "findings": [...]
  },
  "pluginSource": "<read-only code view>",
  "history": []
}
```

### POST /api/v1/control-center/appeals/{appealId}/approve
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>

Body:
{
  "resolution": "Appeal approved. Network access to localhost is acceptable.",
  "overrideScore": 85.0
}
```

**Response (200 OK)**:
```json
{
  "message": "Appeal approved",
  "appealId": "550e8400-e29b-41d4-a716-446655440000",
  "pluginId": "550e8400-e29b-41d4-a716-446655440001",
  "resolvedAt": "2024-01-01T12:00:00Z",
  "resolvedBy": "550e8400-e29b-41d4-a716-446655440004"
}
```

### POST /api/v1/control-center/appeals/{appealId}/reject
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>

Body:
{
  "resolution": "Appeal rejected. Network access to external servers violates security policy.",
  "reason": "security_policy_violation"
}
```

**Response (200 OK)**:
```json
{
  "message": "Appeal rejected",
  "appealId": "550e8400-e29b-41d4-a716-446655440000",
  "pluginId": "550e8400-e29b-41d4-a716-446655440001",
  "resolvedAt": "2024-01-01T12:00:00Z",
  "resolvedBy": "550e8400-e29b-41d4-a716-446655440004"
}
```

### GET /api/v1/control-center/config
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>
```

**Response (200 OK)**:
```json
{
  "analysis": {
    "static_weight": 0.6,
    "dynamic_weight": 0.4,
    "pass_threshold": 80,
    "fail_threshold": 50,
    "tools": {
      "eslint": {
        "enabled": true,
        "weight": 0.25
      },
      "semgrep": {
        "enabled": true,
        "weight": 0.25
      },
      "gitleaks": {
        "enabled": true,
        "weight": 0.3,
        "auto_fail": true
      },
      "trivy": {
        "enabled": true,
        "weight": 0.2
      }
    }
  },
  "sandbox": {
    "type": "docker",
    "timeout": 180,
    "mem_limit": "512m"
  },
  "rate_limits": {
    "submission_per_ip": 10,
    "submission_per_author": 50
  },
  "safe_zone": {
    "auto_approve": {
      "enabled": false,
      "min_score": 90
    }
  }
}
```

### PATCH /api/v1/control-center/config
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>

Body:
{
  "analysis": {
    "pass_threshold": 85
  }
}
```

**Response (200 OK)**:
```json
{
  "message": "Configuration updated",
  "updatedFields": ["analysis.pass_threshold"]
}
```

### GET /api/v1/control-center/audit-log
**Request**:
```
Headers:
  Authorization: Bearer <admin_token>
Query:
  action=plugin_approve
  startDate=2024-01-01
  endDate=2024-01-31
  page=1
  limit=50
```

**Response (200 OK)**:
```json
{
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "timestamp": "2024-01-01T12:00:00Z",
      "adminId": "550e8400-e29b-41d4-a716-446655440004",
      "adminName": "Super Admin",
      "action": "appeal_approve",
      "resourceType": "appeal",
      "resourceId": "550e8400-e29b-41d4-a716-446655440000",
      "details": {
        "pluginId": "550e8400-e29b-41d4-a716-446655440001",
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

## Dashboard Pages

### Overview Dashboard
**URL**: `/control-center`

**Sections**:
- System Health: Queue status, service health, recent errors
- Quick Stats: Total plugins, orgs, users, analyses
- Recent Activity: Last 10 actions
- Alerts: Active alerts and warnings

### Analysis Pipeline Dashboard
**URL**: `/control-center/analysis`

**Sections**:
- Queue Monitor: Real-time queue length, processing rate
- Recent Jobs: Last 50 analysis jobs with status
- Failure Analysis: Recent failures with reasons
- Performance Metrics: Average time, throughput

### Appeals Dashboard
**URL**: `/control-center/appeals`

**Sections**:
- Pending Appeals: List with quick actions
- Recent Resolutions: Last 20 resolved appeals
- Metrics: Resolution time, approval rate, false positive rate
- Search & Filter: By status, date, plugin, author

### Metrics Dashboard
**URL**: `/control-center/metrics`

**Sections**:
- System Metrics: Plugins, orgs, users, analyses
- Analysis Metrics: Throughput, latency, failure rate
- Appeal Metrics: Volume, resolution time, outcomes
- Security Metrics: Findings by severity, false positive rate
- Time Series: Charts for all metrics over time

### Configuration Dashboard
**URL**: `/control-center/config`

**Sections**:
- Analysis Configuration: Weights, thresholds, tool settings
- Sandbox Configuration: Docker/Firecracker settings
- Rate Limits: Submission limits
- Safe Zone: Auto-approval settings
- Save & Validate: Test configuration before saving

### Organizations Dashboard
**URL**: `/control-center/organizations`

**Sections**:
- Organization List: All orgs with summary stats
- Org Details: Members, plugins, activity
- Member Management: Add, remove, edit members
- Invite Management: Generate, resend, revoke invites

### Audit Log Dashboard
**URL**: `/control-center/audit`

**Sections**:
- Log Viewer: Filterable list of all actions
- Export: Download log as CSV/JSON
- Retention: Configure log retention period

---

## Admin Roles and Permissions

### Role Hierarchy
```
Super-Admin
    └── Can manage all organizations
    └── Can configure system settings
    └── Can view all data
    └── Can perform all actions

Org Admin
    └── Can manage their organization
    └── Can approve/reject plugins for their org
    └── Can manage org members
    └── Can view org-specific data
    └── CANNOT configure system settings
    └── CANNOT view other orgs' data

View-Only Admin
    └── Can view all dashboards
    └── CANNOT perform any actions
    └── Read-only access
```

### Permission Matrix
| Action | Super-Admin | Org Admin | View-Only |
|--------|-------------|-----------|-----------|
| View system overview | ✅ | ✅ | ✅ |
| View analysis pipeline | ✅ | ✅ | ✅ |
| View appeals | ✅ | ❌ | ✅ |
| Resolve appeals | ✅ | ❌ | ❌ |
| View metrics | ✅ | ✅ (org only) | ✅ |
| Configure system | ✅ | ❌ | ❌ |
| Manage organizations | ✅ | ❌ | ❌ |
| Manage org members | ✅ | ✅ (own org) | ❌ |
| Approve plugins | ✅ | ✅ (own org) | ❌ |
| View audit log | ✅ | ✅ (org only) | ✅ |
| Export data | ✅ | ✅ (org only) | ❌ |

---

## Notification System

### Notification Types
- Appeal submitted
- Appeal resolved
- Plugin analysis completed
- System alert
- Configuration change
- New organization created

### Notification Channels
- **In-app**: Bell icon in dashboard header
- **Email**: For critical alerts and appeal resolutions
- **Webhook**: For integration with external systems (future)

### Notification Structure
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "type": "appeal_submitted",
  "title": "New Appeal Submitted",
  "message": "Plugin Author has submitted an appeal for plugin My Plugin (v1.0.0)",
  "data": {
    "appealId": "550e8400-e29b-41d4-a716-446655440000",
    "pluginId": "550e8400-e29b-41d4-a716-446655440001",
    "pluginName": "My Plugin",
    "author": "Plugin Author"
  },
  "read": false,
  "createdAt": "2024-01-01T00:00:00Z"
}
```

---

## Reporting

### Built-in Reports
1. **Daily Activity Report**: Submissions, analyses, appeals, approvals
2. **Weekly Security Report**: Findings by severity, false positives
3. **Monthly Adoption Report**: Plugin downloads, org growth
4. **Appeal Analysis Report**: Resolution times, approval rates

### Report Formats
- HTML (view in browser)
- PDF (download)
- CSV (download)
- JSON (API access)

### Scheduled Reports
- Daily: Sent at 8:00 AM UTC
- Weekly: Sent every Monday at 8:00 AM UTC
- Monthly: Sent on 1st of each month at 8:00 AM UTC

---

## Error Messages

| Error | HTTP Status | Message |
|-------|-------------|---------|
| Unauthorized | 401 | "Authentication required" |
| Forbidden | 403 | "Insufficient permissions" |
| Not found | 404 | "Resource not found" |
| Invalid config | 400 | "Invalid configuration: {reason}" |
| Config validation | 400 | "Configuration validation failed: {errors}" |

---

## Performance Requirements

- **Dashboard load time**: ≤ 2 seconds for main pages
- **API response time**: ≤ 500ms for most endpoints
- **Report generation**: ≤ 10 seconds for complex reports
- **Audit log query**: ≤ 1 second for filtered queries
- **Concurrent admins**: Support 100+ concurrent admin users
