<!-- slug: api-reference | category: reference -->

# API Reference

ClaudeForge exposes a REST API at `/api/v1` for programmatic access to the plugin marketplace. All endpoints return JSON responses.

## Base URL

```
https://api.claudeforge.com/api/v1
```

## Response Format

All successful responses follow this envelope:

```json
{
  "data": { /* ... */ },
  "totalCount": 42,
  "page": 1,
  "limit": 20,
  "totalPages": 3
}
```

### Fields

| Field | Type | Description |
|-------|------|-------------|
| `data` | array or object | Paginated results or single resource |
| `totalCount` | integer | Total number of items matching the query |
| `page` | integer | Current page (1-indexed) |
| `limit` | integer | Items per page |
| `totalPages` | integer | Total number of pages |

## Error Responses

Errors return RFC 7807 ProblemDetails format:

```json
{
  "type": "https://api.claudeforge.com/errors/plugin-not-found",
  "title": "Plugin Not Found",
  "status": 404,
  "detail": "Plugin with ID '550e8400-e29b-41d4-a716-446655440000' does not exist.",
  "instance": "/api/v1/plugins/550e8400-e29b-41d4-a716-446655440000"
}
```

### Common HTTP Status Codes

| Status | Meaning |
|--------|---------|
| 200 | OK — Request succeeded |
| 400 | Bad Request — Invalid parameters |
| 404 | Not Found — Resource does not exist |
| 409 | Conflict — Duplicate or versioning error |
| 500 | Internal Server Error — Server-side problem |

---

## Catalog Endpoints

### List All Plugins

```
GET /api/v1/plugins
```

Retrieve paginated list of all plugins.

#### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number (1-indexed) |
| `limit` | integer | 20 | Items per page (1-100) |
| `sort` | string | "createdAt" | Sort field: `createdAt`, `downloadCount`, `name` |
| `order` | string | "desc" | Sort order: `asc` or `desc` |
| `type` | string[] | — | Filter by type: skill, hook, agent, command, plugin |
| `language` | string[] | — | Filter by language: typescript, python, go, rust |
| `useCase` | string[] | — | Filter by use case: dev-team, product-owner, product-manager, devops, security, data-analyst |

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/plugins?limit=10&type=skill&language=typescript"
```

#### Response

```json
{
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "code-analyzer",
      "version": "1.2.3",
      "description": "Analyzes code quality",
      "author": "Jane Doe",
      "types": ["skill"],
      "languages": ["typescript"],
      "useCaseTags": ["dev-team"],
      "downloadCount": 1250,
      "createdAt": "2025-06-01T10:00:00Z",
      "updatedAt": "2025-06-05T14:30:00Z"
    }
  ],
  "totalCount": 42,
  "page": 1,
  "limit": 10,
  "totalPages": 5
}
```

---

### Get Plugin by ID

```
GET /api/v1/plugins/{pluginId}
```

Retrieve full details of a specific plugin.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pluginId` | UUID | Plugin identifier |

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/plugins/550e8400-e29b-41d4-a716-446655440000"
```

#### Response

```json
{
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "code-analyzer",
    "version": "1.2.3",
    "description": "Analyzes code quality and returns insights",
    "author": "Jane Doe <jane@example.com>",
    "types": ["skill"],
    "languages": ["typescript"],
    "useCaseTags": ["dev-team", "security"],
    "entrypoints": [
      {
        "name": "analyzeCode",
        "type": "skill",
        "description": "Analyze TypeScript code",
        "language": "typescript"
      }
    ],
    "dependencies": {
      "typescript": "^5.0.0"
    },
    "license": "MIT",
    "docsUrl": "https://github.com/janedoe/code-analyzer#readme",
    "downloadCount": 1250,
    "createdAt": "2025-06-01T10:00:00Z",
    "updatedAt": "2025-06-05T14:30:00Z"
  }
}
```

---

### List Categories

```
GET /api/v1/categories
```

Retrieve available plugin categories (types, languages, use cases).

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/categories"
```

#### Response

```json
{
  "data": {
    "types": ["skill", "hook", "agent", "command", "plugin"],
    "languages": ["typescript", "python", "go", "rust"],
    "useCases": [
      "dev-team",
      "product-owner",
      "product-manager",
      "devops",
      "security",
      "data-analyst"
    ]
  }
}
```

---

## Search & Discovery

### Search Plugins

```
GET /api/v1/plugins/search
GET /api/v1/search
```

Search the plugin catalog (both endpoints are equivalent).

#### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `q` | string | — | Search query (name, description, keywords) |
| `type` | string[] | — | Filter by type |
| `language` | string[] | — | Filter by language |
| `useCase` | string[] | — | Filter by use case |
| `page` | integer | 1 | Page number |
| `limit` | integer | 20 | Items per page |

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/plugins/search?q=testing&type=skill&language=typescript&limit=5"
```

#### Response

Same structure as [List All Plugins](#list-all-plugins).

---

### Discover Plugins

```
GET /api/v1/discovery
```

Get curated plugin recommendations and trending plugins.

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `keyword` | string | Optional keyword for discovery context |
| `language` | string[] | Filter by language |
| `useCase` | string[] | Filter by use case |
| `type` | string[] | Filter by type |

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/discovery?keyword=testing&language=typescript"
```

#### Response

```json
{
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "name": "code-analyzer",
      "version": "1.2.3",
      "description": "Analyzes code quality",
      "author": "Jane Doe",
      "types": ["skill"],
      "languages": ["typescript"],
      "useCaseTags": ["dev-team"],
      "downloadCount": 1250,
      "createdAt": "2025-06-01T10:00:00Z",
      "updatedAt": "2025-06-05T14:30:00Z"
    }
  ],
  "totalCount": 15,
  "page": 1,
  "limit": 20,
  "totalPages": 1
}
```

---

## Distribution

### Download Plugin

```
GET /api/v1/plugins/{pluginId}/download
```

Download a plugin archive (.tar.gz or .zip).

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pluginId` | UUID | Plugin identifier |

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `version` | string | Optional specific version (defaults to latest) |

#### Example

```bash
curl -O "https://api.claudeforge.com/api/v1/plugins/550e8400-e29b-41d4-a716-446655440000/download"
```

#### Response

Binary file (plugin archive).

---

## Publishing & Versioning

### Upload Plugin

```
POST /api/v1/plugins/upload
```

Upload a new plugin to the marketplace (requires multipart form data).

#### Request Body

| Field | Type | Description |
|-------|------|-------------|
| `file` | file | Plugin archive (.tar.gz or .zip, max 50 MB) |
| `manifest` | JSON | plugin.json content |

#### Example

```bash
curl -X POST \
  -F "file=@my-plugin.tar.gz" \
  -F 'manifest=@plugin.json' \
  "https://api.claudeforge.com/api/v1/plugins/upload"
```

#### Response

```json
{
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "my-plugin",
    "version": "1.0.0",
    "message": "Plugin uploaded successfully"
  }
}
```

---

### Publish Plugin Version

```
POST /api/v1/plugins/{pluginId}/versions
```

Publish a new version of an existing plugin.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pluginId` | UUID | Plugin identifier |

#### Request Body

```json
{
  "version": "1.1.0",
  "releaseNotes": "## v1.1.0\n\nNew features:\n- Add JSX support",
  "file": "<binary archive>"
}
```

#### Response

```json
{
  "data": {
    "pluginId": "550e8400-e29b-41d4-a716-446655440000",
    "version": "1.1.0",
    "releaseNotes": "## v1.1.0\n\nNew features:\n- Add JSX support",
    "createdAt": "2025-06-06T12:00:00Z"
  }
}
```

---

### Get Version History

```
GET /api/v1/plugins/{pluginId}/versions
```

Retrieve paginated version history for a plugin.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pluginId` | UUID | Plugin identifier |

#### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | integer | 1 | Page number |
| `limit` | integer | 20 | Items per page |

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/plugins/550e8400-e29b-41d4-a716-446655440000/versions?limit=5"
```

#### Response

```json
{
  "data": [
    {
      "version": "1.2.3",
      "releaseNotes": "Bug fixes and improvements",
      "createdAt": "2025-06-05T14:30:00Z"
    },
    {
      "version": "1.2.2",
      "releaseNotes": "Fix security issue",
      "createdAt": "2025-06-01T10:00:00Z"
    }
  ],
  "totalCount": 8,
  "page": 1,
  "limit": 5,
  "totalPages": 2
}
```

---

### Get Specific Version

```
GET /api/v1/plugins/{pluginId}/versions/{version}
```

Retrieve details of a specific plugin version.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pluginId` | UUID | Plugin identifier |
| `version` | string | Semantic version (e.g., "1.2.3") |

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/plugins/550e8400-e29b-41d4-a716-446655440000/versions/1.2.3"
```

#### Response

```json
{
  "data": {
    "version": "1.2.3",
    "releaseNotes": "## v1.2.3\n\nBug fixes",
    "createdAt": "2025-06-05T14:30:00Z",
    "manifest": {
      "name": "code-analyzer",
      "version": "1.2.3",
      "description": "Analyzes code quality",
      "author": "Jane Doe"
    }
  }
}
```

---

### Patch Version Metadata

```
PATCH /api/v1/plugins/{pluginId}/versions/{version}
```

Update release notes or metadata for an existing version (does not re-upload the binary).

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pluginId` | UUID | Plugin identifier |
| `version` | string | Semantic version |

#### Request Body

```json
{
  "releaseNotes": "Updated release notes"
}
```

#### Response

```json
{
  "data": {
    "version": "1.2.3",
    "releaseNotes": "Updated release notes",
    "updatedAt": "2025-06-06T15:00:00Z"
  }
}
```

---

## Telemetry

### Post Telemetry Event

```
POST /api/v1/telemetry/events
```

Send an anonymized telemetry event (download, install, etc.).

#### Request Body

```json
{
  "eventType": "install",
  "pluginId": "550e8400-e29b-41d4-a716-446655440000",
  "version": "1.2.3",
  "anonClientId": "sha256:a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6",
  "clientOs": "darwin",
  "clientArch": "x86_64"
}
```

#### Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `eventType` | string | Yes | "install" or "download" |
| `pluginId` | UUID | Yes | Plugin identifier |
| `version` | string | No | Plugin version |
| `anonClientId` | string | No | Hashed anonymous client identifier |
| `clientOs` | string | No | Operating system (darwin, linux, win32, etc.) |
| `clientArch` | string | No | CPU architecture (x86_64, arm64, etc.) |

#### Example

```bash
curl -X POST "https://api.claudeforge.com/api/v1/telemetry/events" \
  -H "Content-Type: application/json" \
  -d '{
    "eventType": "install",
    "pluginId": "550e8400-e29b-41d4-a716-446655440000",
    "version": "1.2.3",
    "anonClientId": "sha256:a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6",
    "clientOs": "darwin",
    "clientArch": "x86_64"
  }'
```

#### Response

```json
{
  "data": {
    "success": true,
    "message": "Telemetry event recorded"
  }
}
```

---

### Get Telemetry Summary

```
GET /api/v1/plugins/{pluginId}/telemetry/summary
```

Retrieve aggregated telemetry for a plugin (download/install counts, OS distribution).

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pluginId` | UUID | Plugin identifier |

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/plugins/550e8400-e29b-41d4-a716-446655440000/telemetry/summary"
```

#### Response

```json
{
  "data": {
    "pluginId": "550e8400-e29b-41d4-a716-446655440000",
    "totalDownloads": 1250,
    "totalInstalls": 980,
    "osDistribution": {
      "darwin": 0.45,
      "linux": 0.35,
      "win32": 0.20
    },
    "archDistribution": {
      "x86_64": 0.80,
      "arm64": 0.20
    }
  }
}
```

---

## Documentation

### Search Documentation

```
GET /api/v1/docs
```

Search ClaudeForge documentation.

#### Query Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `search` | string | — | Search query |
| `page` | integer | 1 | Page number |
| `limit` | integer | 20 | Items per page |

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/docs?search=publishing"
```

#### Response

```json
{
  "data": [
    {
      "slug": "contributing",
      "title": "Contributing & Publishing Plugins",
      "category": "guide",
      "excerpt": "Learn how to create, validate, version, and publish plugins..."
    }
  ],
  "totalCount": 2,
  "page": 1,
  "limit": 20,
  "totalPages": 1
}
```

---

### Get Documentation by Slug

```
GET /api/v1/docs/{slug}
```

Retrieve full documentation page by slug.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `slug` | string | Documentation slug (e.g., "getting-started") |

#### Example

```bash
curl "https://api.claudeforge.com/api/v1/docs/getting-started"
```

#### Response

```json
{
  "data": {
    "slug": "getting-started",
    "title": "Getting Started",
    "category": "guide",
    "content": "# Getting Started\n\nWelcome to ClaudeForge...",
    "createdAt": "2025-06-01T00:00:00Z",
    "updatedAt": "2025-06-05T14:30:00Z"
  }
}
```

---

## Rate Limits

Rate limits are not currently enforced. Fair-use policies will be implemented in future releases.

## Authentication

Authentication is not yet implemented. All endpoints are publicly accessible.

## Versioning

The current API version is `v1`. Breaking changes will be communicated in advance, and new versions will be available at `/api/v2`, etc.
