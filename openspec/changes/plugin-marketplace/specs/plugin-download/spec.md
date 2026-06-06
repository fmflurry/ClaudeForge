# Plugin Download Specification

## ADDED Requirements

### Requirement: Download plugin package by ID with automatic version resolution

The system SHALL provide an endpoint that downloads a plugin package, automatically resolving to the latest version if no version is specified.

#### Scenario: Download latest version without specifying version
**WHEN** a client requests GET `/api/plugins/{pluginId}/download`
**THEN** the system SHALL return HTTP 200 with the plugin package file (tar.gz or zip)
**AND** the response SHALL include `Content-Disposition: attachment; filename="{pluginName}-latest.tar.gz"`
**AND** the package SHALL be the content from the latest released version

#### Scenario: Download specific version
**WHEN** a client requests GET `/api/plugins/{pluginId}/download?version=1.2.3`
**THEN** the system SHALL return HTTP 200 with the plugin package matching version "1.2.3"
**AND** the response SHALL include `Content-Disposition: attachment; filename="{pluginName}-1.2.3.tar.gz"`

#### Scenario: Download from non-existent plugin
**WHEN** a client requests GET `/api/plugins/{invalidPluginId}/download`
**THEN** the system SHALL return HTTP 404 with error message `"Plugin not found"`

#### Scenario: Request specific version that does not exist
**WHEN** a client requests GET `/api/plugins/{pluginId}/download?version=9.9.9` (version does not exist)
**THEN** the system SHALL return HTTP 404 with error message `"Plugin version 9.9.9 not found"`

### Requirement: Increment download counter on successful download

The system SHALL atomically increment a plugin's download counter each time a package is successfully downloaded.

#### Scenario: Download increments counter
**WHEN** a client successfully downloads a plugin package via GET `/api/plugins/{pluginId}/download`
**AND** the system returns HTTP 200 with the file
**THEN** the system SHALL increment `plugins.downloadCount` by 1 for the plugin
**AND** the next GET `/api/plugins/{pluginId}` SHALL reflect the incremented count

#### Scenario: Multiple downloads increment counter correctly
**WHEN** Client A downloads a plugin, then Client B downloads the same plugin
**THEN** `downloadCount` SHALL be incremented twice
**AND** `downloadCount` on GET `/api/plugins/{pluginId}` SHALL show the total accumulated downloads

#### Scenario: Failed download does not increment counter
**WHEN** a client requests GET `/api/plugins/{pluginId}/download` but receives HTTP 404
**THEN** the system SHALL NOT increment `downloadCount`

### Requirement: Support downloading via version-specific endpoint

The system SHALL allow explicit version specification via query parameter for CLI and programmatic access.

#### Scenario: CLI requests version with explicit query parameter
**WHEN** a CLI tool sends GET `/api/plugins/{pluginId}/download?version=1.5.0`
**THEN** the system SHALL resolve to version "1.5.0" and return HTTP 200 with the package file

#### Scenario: Version query parameter with invalid format
**WHEN** a client requests GET `/api/plugins/{pluginId}/download?version=not-a-version`
**THEN** the system SHALL return HTTP 400 with error message `"Invalid version format. Expected semver (e.g., 1.0.0)"`

#### Scenario: Omitted version parameter defaults to latest
**WHEN** a client requests GET `/api/plugins/{pluginId}/download` (no version parameter)
**THEN** the system SHALL treat this equivalently to GET `/api/plugins/{pluginId}/download?version=latest`
**AND** return the latest published version

### Requirement: Return correct content type and file headers

The system SHALL return appropriate HTTP headers and content type for package downloads.

#### Scenario: Download response includes correct headers
**WHEN** a client requests GET `/api/plugins/{pluginId}/download`
**AND** receives HTTP 200
**THEN** the response SHALL include headers:
- `Content-Type: application/gzip` (or `application/zip` if zip format)
- `Content-Disposition: attachment; filename="{name}-{version}.tar.gz"`
- `Content-Length: <file-size-in-bytes>`
**AND** the response body SHALL be the raw file binary

#### Scenario: Download response includes caching headers
**WHEN** a client downloads a published plugin package
**THEN** the system MAY include `Cache-Control` header to enable browser caching (e.g., `public, max-age=86400`)

#### Scenario: Content-Disposition prevents in-browser rendering
**WHEN** a client opens the download URL in a browser
**THEN** the browser SHALL treat `Content-Disposition: attachment` as a download trigger
**AND** NOT render the file in-browser

### Requirement: Handle concurrent downloads without race conditions

The system SHALL safely handle multiple simultaneous download requests and counter increments.

#### Scenario: Two clients download simultaneously
**WHEN** Client A and Client B both request GET `/api/plugins/{pluginId}/download` at the same time
**THEN** the system SHALL serve both downloads correctly
**AND** `downloadCount` SHALL be incremented exactly twice (no race condition)

#### Scenario: Large number of concurrent downloads
**WHEN** 100 concurrent requests are made to GET `/api/plugins/{pluginId}/download`
**THEN** all requests SHALL receive HTTP 200 with the plugin package
**AND** `downloadCount` SHALL be incremented exactly 100 times
