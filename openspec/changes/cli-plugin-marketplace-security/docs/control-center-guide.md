# Control Center User Guide

## Access

The Control Center is a web-based admin dashboard accessible at:

```
https://<marketplace-domain>/control-center
```

**Authentication**: Requires a valid admin session (admin or super-admin role). Non-admin users receive HTTP 403.

### Admin Roles

| Role | Access Level |
|------|-------------|
| **Super-Admin** | Full access — all dashboards, configuration, orgs |
| **Org Admin** | Org-scoped access — their org's members and plugins only |
| **View-Only Admin** | Read-only access — can view dashboards but cannot perform actions |

---

## Overview Dashboard

**URL**: `/control-center`

The landing page shows system health at a glance:

| Widget | Description |
|--------|-------------|
| **System Health** | Status indicator (green/yellow/red) for queue, services, database |
| **Total Plugins** | Count of all plugins in catalog, with passed/failed/in-review breakdown |
| **Pending Analysis** | Number of plugins waiting in analysis queue |
| **In Review Queue** | Plugins scored in the review range (50–79) needing manual admin review |
| **Pending Appeals** | Appeals submitted by authors awaiting admin decision |
| **Recent Activity** | Last 10 actions (analyses completed, appeals filed, config changes) |
| **Alerts** | Active system alerts (queue backlog, failure rate spikes, sandbox errors) |

---

## Analysis Pipeline Monitoring

**URL**: `/control-center/analysis`

### Queue Monitor

Real-time status of the analysis pipeline:

| Metric | Description |
|--------|-------------|
| Queue Length | Jobs waiting to be picked up by workers |
| In Progress | Jobs currently being processed |
| Completed Today | Jobs completed in the last 24 hours |
| Failed Today | Jobs that failed in the last 24 hours |
| Average Processing Time | Mean time from queue to completion |
| Failure Rate | Percentage of jobs that failed (target < 10%) |

### Recent Jobs

Table of the last 50 analysis jobs with status (`queued`, `processing`, `completed`, `failed`) and drill-down to detailed results.

### Failure Analysis

List of recent failures with error messages and tool-specific diagnostics.

---

## Appeals Management

**URL**: `/control-center/appeals`

### View Pending Appeals

Filterable list of all open appeals showing:

- Plugin name and version
- Author name and reputation (karma, level)
- Appeal reason and evidence summary
- Original analysis score and status
- Submission date

### Review Appeal Detail

Click an appeal to see:

- Full appeal submission (reason, evidence)
- Complete analysis results (static + dynamic findings)
- Read-only view of plugin source code
- Previous appeal history for the same plugin
- Author's karma and trust level

### Approve an Appeal

When an admin determines the analysis was a false positive:

1. Click **Approve** on the appeal detail page
2. Enter resolution notes (e.g., "Network access to localhost is acceptable behavior")
3. Optionally override the analysis score
4. Submit

**Effects**:
- Plugin status updated to `passed`
- Plugin added to catalog
- Author notified
- Author karma adjusted: +30 (appeal_won)
- Original analysis deduction reversed

### Reject an Appeal

When an admin upholds the analysis decision:

1. Click **Reject** on the appeal detail page
2. Enter resolution notes and rejection reason
3. Submit

**Effects**:
- Plugin status remains `failed`
- Author notified with rejection reason
- Author karma adjusted: -10 (appeal_lost)

### Appeals Metrics

| Metric | Description |
|--------|-------------|
| Pending | Open appeals awaiting review |
| Approved Today | Appeals approved today |
| Rejected Today | Appeals rejected today |
| Average Resolution Time | Mean time from submission to resolution |
| Approval Rate | % of appeals approved |
| False Positive Rate | Approved appeals / total rejected plugins |

---

## Metrics Dashboard

**URL**: `/control-center/metrics`

### System Metrics

| Chart | Description |
|-------|-------------|
| Total Plugins (over time) | Cumulative plugin catalog growth |
| Total Organizations | Org registration trend |
| Total Users | User growth over time |
| Daily Submissions | Plugin submission volume per day |

### Analysis Metrics

| Chart | Description |
|-------|-------------|
| Throughput | Analyses completed per hour/day |
| Latency | P50/P95/P99 analysis processing time |
| Pass/Fail/Review | Breakdown of analysis outcomes |
| Tool-specific Findings | Findings by tool (ESLint, Semgrep, Gitleaks, Trivy) |

### Security Metrics

| Chart | Description |
|-------|-------------|
| Findings by Severity | Critical/High/Medium/Low finding trends |
| Secret Detections | Gitleaks secret findings over time |
| False Positive Rate | Appeals approved / total rejected plugins |
| Top Vulnerabilities | Most common finding types |

### Time-Series Filtering

Selectable date ranges (24h, 7d, 30d, 90d, custom) with trend analysis.

---

## Configuration Management

**URL**: `/control-center/config`

### Analysis Configuration

Editable fields with inline validation:

| Field | Type | Validation |
|-------|------|------------|
| Static Weight | float (0.0–1.0) | Must sum to 1.0 with dynamic weight |
| Dynamic Weight | float (0.0–1.0) | Must sum to 1.0 with static weight |
| Pass Threshold | integer (0–100) | Must be > fail threshold |
| Fail Threshold | integer (0–100) | Must be < pass threshold |
| Max Workers | integer | Must be >= 1 |
| Retry Limit | integer | Must be >= 0 |

### Tool Configuration

Enable/disable individual static analysis tools and adjust their weights.

### Sandbox Configuration

| Field | Description |
|-------|-------------|
| Sandbox Type | Docker (default) or Firecracker (future) |
| Timeout | Dynamic analysis timeout in seconds |
| Memory Limit | Container memory limit |

### Rate Limits

| Field | Description |
|-------|-------------|
| Submissions per IP | Max submissions per IP per hour |
| Submissions per Author | Max submissions per author per day |

### Save & Validate

Configuration is validated before saving. Invalid configurations show specific error messages (e.g., "Weights must sum to 1.0"). Changes apply immediately to new submissions.

---

## Organization Management

**URL**: `/control-center/organizations` (super-admin only)

### Organization List

All organizations with summary statistics:

- Name, slug, creation date
- Member count
- Approved plugin count
- Last activity date

### Organization Detail

Drill into an org to see:

- **Members**: Full member list with roles, join dates, last activity
- **Plugins**: Approved plugins, versions, approvers, approval dates
- **Pending Requests**: Plugin approval requests awaiting admin action

### Member Management

Super-admins can:

- Add members (by user ID or email invite)
- Remove members
- Change roles (member ↔ admin)
- Transfer admin rights

---

## Audit Log

**URL**: `/control-center/audit`

### Log Viewer

Filterable, searchable, paginated list of all admin actions:

| Column | Description |
|--------|-------------|
| Timestamp | When the action occurred |
| Admin | Who performed the action |
| Action | Action type (plugin_approve, appeal_resolve, config_update, etc.) |
| Resource | Affected resource ID and type |
| Details | Action-specific metadata |

### Filters

- Action type
- Admin/user
- Date range
- Resource ID
- Organization

### Export

Download audit logs as CSV or JSON for external analysis.

### Retention

Configurable log retention period (default: 90 days).

---

## Notifications

### In-App Bell Icon

Dashboard header shows notification count with dropdown of recent notifications.

### Notification Types

| Type | Trigger | Recipients |
|------|---------|------------|
| Appeal Submitted | Author files an appeal | Super-admins |
| Appeal Resolved | Admin approves/rejects appeal | Author |
| Analysis Completed | Plugin finishes analysis | Author |
| System Alert | Queue backlog, failure rate high | Super-admins |
| Config Changed | Admin updates configuration | Super-admins |
| New Org Created | Someone registers an org | Super-admins |

### Notification Preferences

Configure which notification types you receive and through which channels (in-app only for now; email/webhook planned).

**API**: `PUT /api/v1/notifications/preferences`

```json
{
  "channels": {
    "in_app": true,
    "email": false
  },
  "types": {
    "appeal_submitted": true,
    "system_alert": true,
    "analysis_completed": false
  }
}
```

---

## Common Workflows

### Resolving a Plugin in Review Queue

1. Navigate to **Analysis Pipeline** dashboard
2. Filter by status `in_review`
3. Click a plugin to see full analysis results
4. Review findings and evidence
5. **Override to pass** (if false positive) or **confirm failure**
6. If confirming failure, author can still file an appeal

### Handling an Appeal

1. Receive notification or see pending count on overview
2. Open **Appeals** dashboard
3. Review appeal details, analysis results, and plugin source
4. Make decision: **Approve** (override analysis) or **Reject** (uphold analysis)
5. Enter resolution notes for audit trail

### Responding to a Queue Backlog

1. Check **Analysis Pipeline** for queue length and worker count
2. Increase `max_workers` in **Configuration** dashboard
3. Monitor processing time to ensure backpressure resolves
4. If persistent, scale horizontal worker infrastructure

---

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `g` `o` | Go to Overview |
| `g` `a` | Go to Analysis Pipeline |
| `g` `p` | Go to Appeals |
| `g` `m` | Go to Metrics |
| `g` `c` | Go to Configuration |
| `g` `r` | Go to Organizations |
| `g` `l` | Go to Audit Log |
| `?` | Show keyboard shortcuts |
