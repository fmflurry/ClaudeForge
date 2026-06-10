# Plugin Submission Specification

## ADDED Requirements

### Requirement: Submit plugins for security analysis before catalog acceptance

The system SHALL require all plugin submissions to pass through security analysis pipeline before being accepted into the catalog.

#### Scenario: Submit valid plugin package for analysis
**WHEN** a client POSTs to `/api/v1/plugins/submit` with:
- `package` (file): valid tar.gz or zip plugin package
- `name` (string): "My Secure Plugin"
- `description` (string): "Does something useful securely"
- `author` (string): "secure-author@example.com"
- `version` (semver): "1.0.0"
- `types` (array): ["skill"]
- `languages` (array): ["typescript"]

**THEN** the system SHALL:
- Accept the submission
- Return HTTP 202 Accepted with `jobId` and `pluginId`
- Add plugin to analysis queue with status `pending`
- NOT add plugin to catalog until analysis completes

#### Scenario: Submit plugin without required metadata
**WHEN** a client POSTs to `/api/v1/plugins/submit` without `name` field

**THEN** the system SHALL return HTTP 400 with error message `"Required field missing: name"`

#### Scenario: Submit plugin with invalid package format
**WHEN** a client POSTs to `/api/v1/plugins/submit` with `package` file with extension `.exe`

**THEN** the system SHALL return HTTP 400 with error message `"Unsupported package format. Allowed: tar.gz, zip"`

### Requirement: Validate plugin manifest for security submission

The system SHALL validate that the plugin manifest contains all required fields for security analysis.

#### Scenario: Submit plugin with missing manifest
**WHEN** a client POSTs a valid tar.gz that does not contain `plugin.json` or `manifest.json` at root

**THEN** the system SHALL return HTTP 400 with error message `"Package must contain plugin.json or manifest.json at root level"`

#### Scenario: Submit plugin with incomplete manifest for security
**WHEN** a client POSTs a plugin with manifest missing `types` field

**THEN** the system SHALL return HTTP 400 with error message `"Required field missing: types"`

### Requirement: Enforce rate limits on plugin submissions

The system SHALL enforce rate limits to prevent abuse of the submission endpoint.

#### Scenario: Exceed per-IP submission limit
**WHEN** a client from IP address 192.168.1.1 has submitted 10 plugins in the last hour
**AND** attempts to submit an 11th plugin

**THEN** the system SHALL return HTTP 429 with error message `"Rate limit exceeded. Maximum 10 submissions per hour per IP."`

#### Scenario: Exceed per-author submission limit
**WHEN** an author has submitted 50 plugins in the last day
**AND** attempts to submit a 51st plugin

**THEN** the system SHALL return HTTP 429 with error message `"Rate limit exceeded. Maximum 50 submissions per day per author."`

### Requirement: Track submission status

The system SHALL provide endpoints to check the status of plugin submissions.

#### Scenario: Check analysis status for submitted plugin
**WHEN** a client GETs `/api/v1/plugins/{pluginId}/analysis`
**AND** analysis is still in progress

**THEN** the system SHALL return HTTP 200 with:
```json
{
  "status": "processing",
  "progress": 50,
  "currentStep": "static_analysis",
  "queuePosition": 3
}
```

#### Scenario: Check analysis status for completed plugin
**WHEN** a client GETs `/api/v1/plugins/{pluginId}/analysis`
**AND** analysis has completed with pass

**THEN** the system SHALL return HTTP 200 with:
```json
{
  "status": "completed",
  "result": "passed",
  "score": 85.5,
  "staticScore": 90.0,
  "dynamicScore": 75.0,
  "findings": [...],
  "inCatalog": true
}
```

#### Scenario: Check analysis status for failed plugin
**WHEN** a client GETs `/api/v1/plugins/{pluginId}/analysis`
**AND** analysis has completed with fail

**THEN** the system SHALL return HTTP 200 with:
```json
{
  "status": "completed",
  "result": "failed",
  "score": 45.0,
  "staticScore": 40.0,
  "dynamicScore": 55.0,
  "findings": [...],
  "inCatalog": false,
  "canAppeal": true
}
```

### Requirement: Prevent duplicate submissions

The system SHALL prevent the same plugin version from being submitted multiple times.

#### Scenario: Submit same plugin version twice
**WHEN** a plugin with name "MyPlugin" and version "1.0.0" already exists in the system
**AND** a client POSTs to `/api/v1/plugins/submit` with the same name and version

**THEN** the system SHALL return HTTP 409 with error message `"Plugin MyPlugin version 1.0.0 already submitted"`

### Requirement: Handle submission errors gracefully

The system SHALL provide clear error messages for all submission failures.

#### Scenario: Submit corrupted package file
**WHEN** a client POSTs a file named `.tar.gz` but with invalid gzip content

**THEN** the system SHALL return HTTP 400 with error message `"Package file is corrupted or not a valid archive"`

#### Scenario: Submit package that fails to extract
**WHEN** a client POSTs a valid archive that fails to extract (permission issues, etc.)

**THEN** the system SHALL return HTTP 400 with error message `"Failed to extract package: [specific error]"`

### Requirement: Notify author of analysis completion

The system SHALL notify the plugin author when analysis is complete.

#### Scenario: Analysis completes successfully
**WHEN** analysis completes for a plugin submission

**THEN** the system SHALL:
- Update plugin status in database
- If passed: Add plugin to catalog
- Send notification to author (via CLI poll or future email/webhook)

#### Scenario: Analysis fails
**WHEN** analysis completes with fail status for a plugin submission

**THEN** the system SHALL:
- Update plugin status to `failed`
- Store analysis results and findings
- Notify author with failure reason and appeal option

---

## MODIFIED Requirements

### Modified: Plugin Upload Endpoint
The existing `/api/plugins/upload` endpoint is MODIFIED to `/api/v1/plugins/submit` and now:
- Returns 202 Accepted instead of 201 Created (async processing)
- Requires additional metadata for security analysis
- Does NOT add plugin to catalog immediately
- Adds plugin to analysis queue instead

### Modified: Plugin Catalog Inclusion
Plugins are NO LONGER automatically added to the catalog on upload. They must:
1. Pass security analysis (score ≥ pass_threshold)
2. OR have an approved appeal
3. OR be manually approved by admin

---

## API Contract

### POST /api/v1/plugins/submit
**Request**:
```
Content-Type: multipart/form-data

package: <binary data>
name: "My Plugin"
description: "Plugin description"
author: "author@example.com"
version: "1.0.0"
types: ["skill"]
languages: ["typescript"]
useCaseTags: ["dev-team"]
```

**Response (202 Accepted)**:
```json
{
  "jobId": "550e8400-e29b-41d4-a716-446655440000",
  "pluginId": "550e8400-e29b-41d4-a716-446655440001",
  "status": "queued",
  "message": "Plugin submitted for security analysis"
}
```

**Response (400 Bad Request)**:
```json
{
  "type": "https://example.com/errors/validation",
  "title": "Validation Error",
  "status": 400,
  "detail": "Required field missing: name",
  "instance": "/api/v1/plugins/submit"
}
```

### GET /api/v1/plugins/{pluginId}/analysis
**Response (200 OK)**:
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440001",
  "pluginId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "completed",
  "result": "passed",
  "score": 85.5,
  "staticScore": 90.0,
  "dynamicScore": 75.0,
  "staticFindings": [
    {
      "tool": "eslint",
      "severity": "warning",
      "message": "Unused variable 'x'",
      "file": "src/index.ts",
      "line": 10,
      "column": 5
    }
  ],
  "dynamicFindings": [
    {
      "type": "network_access",
      "severity": "high",
      "description": "Plugin attempted network access to example.com",
      "timestamp": "2024-01-01T00:00:00Z"
    }
  ],
  "thresholds": {
    "pass": 80,
    "fail": 50
  },
  "inCatalog": true,
  "canAppeal": false,
  "createdAt": "2024-01-01T00:00:00Z",
  "completedAt": "2024-01-01T00:05:00Z"
}
```

---

## Validation Rules

### Required Fields
- `package` (file): Required, max size 50MB
- `name` (string): Required, 1-128 characters
- `description` (string): Required, 1-500 characters
- `author` (string): Required, valid email or handle
- `version` (string): Required, valid semver
- `types` (array): Required, at least 1 type from enum
- `languages` (array): Required, at least 1 language from enum

### Package Validation
- Format: tar.gz or zip only
- Max size: 50MB
- Must contain plugin.json or manifest.json at root
- Must extract successfully

### Rate Limits
- Per IP: 10 submissions/hour
- Per author: 50 submissions/day
- Burst: 5 submissions in 1 minute

---

## Error Messages

| Error | HTTP Status | Message |
|-------|-------------|---------|
| Missing package | 400 | "Package file is required" |
| Invalid format | 400 | "Unsupported package format. Allowed: tar.gz, zip" |
| Corrupted package | 400 | "Package file is corrupted or not a valid archive" |
| Missing manifest | 400 | "Package must contain plugin.json or manifest.json at root level" |
| Missing field | 400 | "Required field missing: {field}" |
| Invalid semver | 400 | "version must be a valid semantic version (e.g., 1.0.0)" |
| Duplicate submission | 409 | "Plugin {name} version {version} already submitted" |
| Rate limit exceeded | 429 | "Rate limit exceeded. Maximum {limit} submissions per {period} per {type}." |
