# Plugin Versioning Specification

## ADDED Requirements

### Requirement: Publish new versions of existing plugins with semantic versioning

The system SHALL allow authors to publish new versions of a plugin using semantic versioning (MAJOR.MINOR.PATCH), with each version immutable once published.

#### Scenario: Publish new minor version
**WHEN** a client POSTs to `/api/plugins/{pluginId}/versions` with:
- `versionNumber="1.1.0"`
- `package` (file): updated plugin package
- `releaseNotes="Added new feature X"`
**THEN** the system SHALL return HTTP 201 with the new version record
**AND** create a version object with `versionNumber="1.1.0"` and `isLatest=true`
**AND** update the previous version's `isLatest` flag to false

#### Scenario: Publish patch version
**WHEN** a client POSTs to `/api/plugins/{pluginId}/versions` with `versionNumber="1.0.1"`
**THEN** the system SHALL accept the version
**AND** return HTTP 201
**AND** the new version SHALL be marked as `isLatest=true`

#### Scenario: Publish version to non-existent plugin
**WHEN** a client POSTs to `/api/plugins/{invalidPluginId}/versions`
**THEN** the system SHALL return HTTP 404 with error message `"Plugin not found"`

### Requirement: Enforce semantic versioning format and prevent duplicate version numbers

The system SHALL validate version format as MAJOR.MINOR.PATCH and reject submissions with invalid or duplicate versions.

#### Scenario: Valid semantic version format
**WHEN** a client POSTs with `versionNumber="2.3.4"`
**THEN** the system SHALL validate the format as valid semver
**AND** return HTTP 201

#### Scenario: Invalid version format
**WHEN** a client POSTs with `versionNumber="2.3"` or `versionNumber="v2.3.4"` or `versionNumber="2.3.4-beta"`
**THEN** the system SHALL return HTTP 400 with error message `"Version must be in format MAJOR.MINOR.PATCH (e.g., 1.2.3)"`

#### Scenario: Attempt to publish duplicate version
**WHEN** version "1.5.0" already exists for a plugin
**AND** a client POSTs to `/api/plugins/{pluginId}/versions` with `versionNumber="1.5.0"`
**THEN** the system SHALL return HTTP 409 (Conflict) with error message `"Version 1.5.0 already exists"`

#### Scenario: No leading 'v' or '+' modifiers allowed
**WHEN** a client POSTs with `versionNumber="v1.0.0"` or `versionNumber="1.0.0+build123"`
**THEN** the system SHALL return HTTP 400 rejecting the format

### Requirement: Mark latest version and maintain version history

The system SHALL track which version is latest and provide complete, sortable version history for each plugin.

#### Scenario: Latest flag is set correctly when new version published
**WHEN** a plugin has versions ["1.0.0", "1.1.0"] with "1.0.0" as latest
**AND** a new version "2.0.0" is published
**THEN** version "2.0.0" SHALL have `isLatest=true`
**AND** version "1.1.0" SHALL have `isLatest=false`
**AND** only one version per plugin SHALL have `isLatest=true`

#### Scenario: Retrieve full version history sorted by semver
**WHEN** a client requests GET `/api/plugins/{pluginId}/versions`
**THEN** the system SHALL return HTTP 200 with an array of all versions
**AND** versions SHALL be sorted by semver descending (newest first)
**AND** each version entry SHALL include: `versionNumber`, `releasedAt`, `releaseNotes`, `downloadCount`, `isLatest`

#### Scenario: Version history includes download counts per version
**WHEN** version "1.2.0" has been downloaded 5 times
**AND** version "2.0.0" has been downloaded 10 times
**AND** a client requests GET `/api/plugins/{pluginId}/versions`
**THEN** each version object SHALL include its individual `downloadCount`

### Requirement: Store and display release notes for each version

The system SHALL accept and store release notes for each version, making them available for display to users.

#### Scenario: Publish version with release notes
**WHEN** a client POSTs to `/api/plugins/{pluginId}/versions` with:
- `versionNumber="1.5.0"`
- `releaseNotes="- Fixed bug #123\n- Added support for TypeScript 5.1\n- Improved performance by 20%"`
**THEN** the system SHALL store the release notes as-is
**AND** return HTTP 201 with the version object containing the full `releaseNotes`

#### Scenario: Retrieve release notes for specific version
**WHEN** a client requests GET `/api/plugins/{pluginId}/versions/1.5.0`
**THEN** the system SHALL return HTTP 200 with the version object
**AND** the response SHALL include the complete `releaseNotes` field

#### Scenario: Publish version without release notes
**WHEN** a client POSTs to `/api/plugins/{pluginId}/versions` with `versionNumber="1.0.2"` but no `releaseNotes`
**THEN** the system SHALL set `releaseNotes` to empty string
**AND** return HTTP 201 with the version

#### Scenario: Update release notes for published version (immutable)
**WHEN** version "1.2.0" is already published
**AND** a client attempts to PATCH `/api/plugins/{pluginId}/versions/1.2.0` with new release notes
**THEN** the system SHALL return HTTP 405 (Method Not Allowed) or HTTP 400 with error message `"Published versions are immutable"`

### Requirement: List version history with pagination support

The system SHALL provide a paginated endpoint for retrieving version history of a plugin.

#### Scenario: Retrieve version history with pagination
**WHEN** a client requests GET `/api/plugins/{pluginId}/versions?page=1&limit=10`
**THEN** the system SHALL return HTTP 200 with up to 10 version records (paginated)
**AND** the response SHALL include pagination metadata: `totalCount`, `page`, `limit`, `totalPages`
**AND** versions SHALL be sorted by semver descending

#### Scenario: Request version history page beyond available pages
**WHEN** a plugin has 5 versions total
**AND** a client requests GET `/api/plugins/{pluginId}/versions?page=3&limit=10`
**THEN** the system SHALL return HTTP 200 with an empty versions array
**AND** pagination metadata SHALL show `page=3`, `totalPages=1`

#### Scenario: Omit pagination parameters (use defaults)
**WHEN** a client requests GET `/api/plugins/{pluginId}/versions` with no page/limit
**THEN** the system SHALL default to `page=1`, `limit=20`
**AND** return the first page of version history

### Requirement: Support querying specific version details

The system SHALL provide an endpoint to retrieve detailed information about a specific version of a plugin.

#### Scenario: Fetch details for specific version
**WHEN** a client requests GET `/api/plugins/{pluginId}/versions/1.2.3`
**THEN** the system SHALL return HTTP 200 with the version object containing:
- `versionNumber="1.2.3"`
- `releasedAt` (timestamp)
- `releaseNotes`
- `downloadCount`
- `isLatest` flag
- `pluginId` reference

#### Scenario: Request non-existent version
**WHEN** a client requests GET `/api/plugins/{pluginId}/versions/9.9.9` (version does not exist)
**THEN** the system SHALL return HTTP 404 with error message `"Version not found"`

#### Scenario: Request version using case-insensitive version number
**WHEN** a client requests GET `/api/plugins/{pluginId}/versions/1.2.3` (lowercase is standard; version stored as "1.2.3")
**THEN** the system SHALL return HTTP 200 with the version
**AND** the comparison SHALL be case-insensitive for the numeric part
