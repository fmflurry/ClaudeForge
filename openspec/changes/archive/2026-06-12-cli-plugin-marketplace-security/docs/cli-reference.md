# CLI Reference

## Overview

New and modified CLI commands for the plugin marketplace security system. All commands are prefixed with `claudeforge`.

---

## Security Commands

### `claudeforge security submit <plugin-id>`

Submit a plugin for security analysis. Triggers the analysis pipeline (static + dynamic scanning).

**Alias**: `claudeforge plugin submit`

**Arguments**:
| Argument | Required | Description |
|----------|----------|-------------|
| `<plugin-id>` | Yes | ID or path to the plugin package |

**Options**:
| Option | Description |
|--------|-------------|
| `--version` | Specify plugin version (defaults to manifest version) |
| `--wait` | Block and wait for analysis completion |
| `--timeout <seconds>` | Max wait time when `--wait` is used (default: 300) |

**Examples**:

```bash
# Submit and return immediately (async)
claudeforge security submit my-plugin

# Submit and wait for result
claudeforge security submit ./plugin.tar.gz --wait

# Submit specific version with timeout
claudeforge security submit my-plugin --version 2.1.0 --wait --timeout 600
```

**Output**:
```
Plugin submitted for analysis:
  Plugin ID:    plugin_p1b2c3d4
  Version:      1.0.0
  Job ID:       job_j1k2l3m4
  Status:       queued
  Position:     3
```

---

### `claudeforge security status <plugin-id>`

Check the current analysis status and results for a plugin.

**Alias**: `claudeforge plugin status`

**Arguments**:
| Argument | Required | Description |
|----------|----------|-------------|
| `<plugin-id>` | Yes | Plugin ID or name |

**Options**:
| Option | Description |
|--------|-------------|
| `--watch` | Poll continuously until analysis completes |
| `--interval <seconds>` | Poll interval when watching (default: 5) |
| `--json` | Output raw JSON instead of formatted table |
| `--findings` | Show detailed findings breakdown |

**Examples**:

```bash
# Single status check
claudeforge security status plugin_p1b2c3d4

# Watch with default 5s polling
claudeforge security status my-plugin --watch

# Watch with custom interval
claudeforge security status my-plugin --watch --interval 10

# Show full findings
claudeforge security status my-plugin --findings
```

**Output (in progress)**:
```
Plugin: my-plugin (v1.0.0)
Status:  processing (50%)
Step:    dynamic_analysis
Queue:   position 3
Started: 2026-06-08T10:00:00Z
Elapsed: 2m 30s
```

**Output (completed)**:
```
Plugin: my-plugin (v1.0.0)
Status:  ✅ PASSED
Score:   85.5 / 100

Scores:
  Static:  90.0  (weight: 0.6)
  Dynamic: 75.0  (weight: 0.4)

Findings:
  ESLint:   2 warnings
  Semgrep:  0 findings
  Gitleaks: 0 secrets
  Trivy:    3 low vulnerabilities

Catalog:  Available
Appeal:   Not eligible (passed)
```

**Output (failed)**:
```
Plugin: my-plugin (v1.0.0)
Status:  ❌ FAILED
Score:   45.0 / 100

Scores:
  Static:  40.0  (weight: 0.6)
  Dynamic: 55.0  (weight: 0.4)

Findings:
  ESLint:   5 errors, 3 warnings
  Semgrep:  2 medium security issues
  Gitleaks: 1 secret detected ⚠️
  Trivy:    1 critical CVE

Catalog:  Not available
Appeal:   Eligible — run `claudeforge security appeal my-plugin --reason "..." --finding <id>`
```

---

### `claudeforge security appeal <plugin-id>`

File an appeal against a failed analysis result.

**Alias**: `claudeforge plugin appeal`

**Arguments**:
| Argument | Required | Description |
|----------|----------|-------------|
| `<plugin-id>` | Yes | Plugin ID or name |

**Options**:
| Option | Required | Description |
|--------|----------|-------------|
| `--reason` | Yes | Explanation of why analysis was incorrect |
| `--finding <id>` | Yes | Specific finding ID being disputed |
| `--evidence <text>` | No | Supporting evidence for the appeal |
| `--evidence-file <path>` | No | Path to a file containing evidence text |

**Examples**:

```bash
# Appeal a specific finding
claudeforge security appeal my-plugin \
  --reason "Network access to localhost:3000 is part of dev tools, not malicious" \
  --finding finding_f1g2h3i4

# Appeal with evidence file
claudeforge security appeal my-plugin \
  --reason "The detected secret is a test API key for local development" \
  --finding finding_a1b2c3d4 \
  --evidence-file /path/to/evidence.txt
```

**Output**:
```
Appeal submitted:
  Appeal ID:  appeal_a1b2c3d4
  Plugin:     my-plugin (v1.0.0)
  Finding:    finding_f1g2h3i4
  Status:     pending
  Created:    2026-06-08T12:00:00Z
  Message:    Appeal submitted for admin review
```

---

## Organization Commands

### `claudeforge org register <name>`

Create a new organization.

**Arguments**:
| Argument | Required | Description |
|----------|----------|-------------|
| `<name>` | Yes | Organization display name |

**Options**:
| Option | Description |
|--------|-------------|
| `--slug <slug>` | URL-friendly slug (auto-generated from name if omitted) |

**Example**:
```bash
claudeforge org register "Acme Corp" --slug acme-corp
```

**Output**:
```
Organization registered:
  ID:        org_o1p2q3r4
  Name:      Acme Corp
  Slug:      acme-corp
  Role:      admin
  Created:   2026-06-08T10:00:00Z
```

---

### `claudeforge org use <org-id>`

Set the active organization context for CLI operations.

**Arguments**:
| Argument | Required | Description |
|----------|----------|-------------|
| `<org-id>` | Yes | Organization ID |

**Example**:
```bash
claudeforge org use org_o1p2q3r4
```

**Output**:
```
Active organization set to: Acme Corp (org_o1p2q3r4)
Role: admin
```

---

### `claudeforge org show`

Display currently active organization context.

**Example**:
```bash
claudeforge org show
```

**Output**:
```
Active Organization:
  ID:     org_o1p2q3r4
  Name:   Acme Corp
  Role:   admin
```

---

### `claudeforge org list`

List all organizations the current user belongs to.

**Example**:
```bash
claudeforge org list
```

**Output**:
```
Your Organizations:
  org_o1p2q3r4  Acme Corp            admin      (active)
  org_x1y2z3    Open Source Hub      member
  org_a9b8c7    Community Plugins    member
```

---

### `claudeforge org request-approval <plugin-id>`

Request approval for a plugin to be added to the active org's safe zone.

**Arguments**:
| Argument | Required | Description |
|----------|----------|-------------|
| `<plugin-id>` | Yes | Plugin ID or name to request |

**Options**:
| Option | Required | Description |
|--------|----------|-------------|
| `--reason` | Yes | Why this plugin should be approved |
| `--version` | No | Specific version (defaults to latest) |

**Example**:
```bash
claudeforge org request-approval my-plugin \
  --reason "Required for CI/CD pipeline automation" \
  --version 2.1.0
```

**Output**:
```
Approval request submitted:
  Plugin:   my-plugin (v2.1.0)
  Org:      Acme Corp
  Status:   pending
  Request:  req_r1s2t3u4
  Message:  Your org admin has been notified
```

---

### `claudeforge org approve <plugin-id>`

Approve a plugin for the active org's safe zone (admin only).

**Arguments**:
| Argument | Required | Description |
|----------|----------|-------------|
| `<plugin-id>` | Yes | Plugin ID or name |

**Options**:
| Option | Description |
|--------|-------------|
| `--version` | Specific version to approve (required if multiple exist) |
| `--reason` | Approval reason |

**Example**:
```bash
claudeforge org approve my-plugin --version 2.1.0 --reason "Approved for team use"
```

---

### `claudeforge org block <plugin-id>`

Block a globally-available plugin from the active org's safe zone (admin only).

---

### `claudeforge org invite <email>`

Invite a user to the active org (admin only).

**Options**:
| Option | Description |
|--------|-------------|
| `--role` | `member` (default) or `admin` |

---

### `claudeforge org audit-log`

View the audit log for the active org.

---

## Listing Commands

### `claudeforge list`

List available plugins. Behavior changes based on context.

**Options**:
| Option | Description |
|--------|-------------|
| `--safe-zone` | Show only plugins approved for the active org |
| `--global-safe-zone` | Show globally approved plugins |
| `--all` | Show all plugins (ignores safe zone, requires admin) |
| `--org <org-id>` | List plugins for a specific org (overrides active) |
| `--author <author-id>` | Filter by author |
| `--status <status>` | Filter by security status (`passed`, `failed`, `pending`, `in_review`) |
| `--score-min <n>` | Minimum security score filter |

**Examples**:

```bash
# Safe zone only (requires active org)
claudeforge list --safe-zone

# Global safe zone
claudeforge list --global-safe-zone

# All plugins (admin only)
claudeforge list --all

# Filter by status and score
claudeforge list --safe-zone --status passed --score-min 80
```

**Output**:
```
Name            Version   Author         Score  Status    Approved
──────────────  ────────  ─────────────  ─────  ────────  ───────────
my-plugin       2.1.0     bob@test.com   91.5   passed    2026-06-01
another-plugin  1.0.0     alice@test.co  88.0   passed    2026-06-05
```

---

## Install Commands

### `claudeforge install`

Install a plugin from the marketplace.

**Modified behavior**: Now checks safe zone first.

**Options**:
| Option | Description |
|--------|-------------|
| `--safe-zone-only` | Restrict installation to safe zone-approved plugins |
| `--request-if-missing` | Auto-create approval request if plugin not in safe zone |
| `--no-safe-zone` | Bypass safe zone check (super-admin only) |
| `--org <org-id>` | Use specific org's safe zone |

**Examples**:

```bash
# Standard install (checks safe zone, falls back to catalog)
claudeforge install my-plugin

# Safe zone only
claudeforge install --safe-zone-only my-plugin

# Auto-request approval if not in safe zone
claudeforge install --safe-zone-only --request-if-missing my-plugin

# Install specific version from org
claudeforge install my-plugin --version 2.1.0 --org org_o1p2q3r4
```
