# Analysis Configuration Options

## Overview

The plugin security analysis pipeline uses a configurable scoring algorithm with adjustable weights and thresholds. Configuration changes apply to **new submissions only** — already-completed analyses retain their original configuration.

---

## Configurable Options

### Score Weights

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `static_weight` | float (0.0–1.0) | 0.6 | Weight assigned to static analysis score in total calculation |
| `dynamic_weight` | float (0.0–1.0) | 0.4 | Weight assigned to dynamic analysis score in total calculation |

**Constraint**: `static_weight + dynamic_weight` MUST equal exactly `1.0`.

```
total_score = (static_score × static_weight) + (dynamic_score × dynamic_weight)
```

### Decision Thresholds

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `pass_threshold` | integer (0–100) | 80 | Minimum score for automatic catalog acceptance |
| `fail_threshold` | integer (0–100) | 50 | Maximum score for automatic rejection |

**Constraint**: `pass_threshold` MUST be greater than `fail_threshold`.

**Decision Matrix**:

| Score Range | Status | Action |
|-------------|--------|--------|
| >= `pass_threshold` | `passed` | Plugin accepted to catalog |
| >= `fail_threshold` AND < `pass_threshold` | `in_review` | Queued for manual admin review |
| < `fail_threshold` | `failed` | Plugin rejected, author notified |

### Pipeline Parallelism

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `max_workers` | integer | 2 | Maximum concurrent analysis workers |
| `retry_limit` | integer | 3 | Number of retries for failed analysis jobs |

### Analysis Timeout

| Stage | Timeout |
|-------|---------|
| Total analysis (per plugin) | 5 minutes |
| Static analysis | 2 minutes |
| Dynamic analysis | 3 minutes |

---

## Viewing Configuration

### GET /api/v1/control-center/config

Returns the full current configuration object.

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
      "eslint": { "enabled": true, "weight": 0.25 },
      "semgrep": { "enabled": true, "weight": 0.25 },
      "gitleaks": { "enabled": true, "weight": 0.3, "auto_fail": true },
      "trivy": { "enabled": true, "weight": 0.2 }
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
  }
}
```

---

## Updating Configuration

### PUT /api/v1/control-center/config/analysis

Updates analysis configuration. Only properties included in the request body are changed; omitted properties retain current values.

**Request**:
```
Headers:
  Authorization: Bearer <admin_token>

Body:
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
  "updatedFields": [
    "analysis.static_weight",
    "analysis.dynamic_weight",
    "analysis.pass_threshold",
    "analysis.fail_threshold",
    "analysis.max_workers"
  ]
}
```

---

## Configuration History

### GET /api/v1/control-center/config/history

Returns a paginated history of configuration changes for auditing purposes.

**Query Parameters**:
| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number |
| `limit` | integer | 20 | Items per page (max 100) |
| `startDate` | string (ISO 8601) | — | Filter by date range start |
| `endDate` | string (ISO 8601) | — | Filter by date range end |

**Response (200 OK)**:
```json
{
  "data": [
    {
      "id": "cfg-001",
      "changedBy": "admin@example.com",
      "changedAt": "2026-01-15T10:30:00Z",
      "changes": {
        "analysis.pass_threshold": { "from": 80, "to": 85 },
        "analysis.fail_threshold": { "from": 50, "to": 45 }
      },
      "reason": "Tightening thresholds after spike in false positives"
    }
  ],
  "totalCount": 25,
  "page": 1,
  "limit": 20,
  "totalPages": 2
}
```

**Error Codes**:
| Code | Meaning |
|------|---------|
| 400 | Validation failure (e.g., weights don't sum to 1.0) |
| 401 | Authentication required |
| 403 | Insufficient permissions (admin only) |

---

## Validation Rules

The system enforces these validation rules on every configuration update:

1. **Weight Sum**: `static_weight + dynamic_weight == 1.0` (within floating-point tolerance of 0.0001)
2. **Threshold Order**: `pass_threshold > fail_threshold`
3. **Range**: All thresholds and weights must be in range [0, 100] (weights as percentage-equivalent)
4. **Max Workers**: Must be >= 1 (no upper bound enforced, but system performance degrades beyond ~20)
5. **Retry Limit**: Must be >= 0

### Example: Invalid Configuration

```json
{
  "error": "Configuration validation failed",
  "details": [
    "static_weight + dynamic_weight must equal 1.0 (got 0.6 + 0.3 = 0.9)",
    "pass_threshold (75) must be greater than fail_threshold (80)"
  ]
}
```

---

## Example Payloads

### Tighter Security

```json
{
  "pass_threshold": 90,
  "fail_threshold": 60,
  "static_weight": 0.7,
  "dynamic_weight": 0.3,
  "max_workers": 3,
  "retry_limit": 5
}
```

### Relaxed Policy (Trusted Ecosystem)

```json
{
  "pass_threshold": 70,
  "fail_threshold": 40,
  "static_weight": 0.5,
  "dynamic_weight": 0.5
}
```

---

## Static Tool Weights (Sub-Configuration)

Within the static analysis score, individual tools contribute at these configurable weights:

| Tool | Default Weight | Auto-Fail Enabled |
|------|---------------|-------------------|
| ESLint | 0.25 | No |
| Semgrep | 0.25 | No |
| Gitleaks | 0.30 | Yes (secrets → auto fail) |
| Trivy | 0.20 | No |

Static sub-tool weights are configured separately from the main weights shown above.
