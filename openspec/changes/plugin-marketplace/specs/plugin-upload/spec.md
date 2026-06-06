# Plugin Upload Specification

## ADDED Requirements

### Requirement: Accept plugin package submission without authentication

The system SHALL provide an endpoint that accepts a plugin package (tar.gz or zip) along with required metadata, without requiring user authentication.

#### Scenario: Submit valid plugin package with complete metadata
**WHEN** a client POSTs to `/api/plugins/upload` with form-data containing:
- `package` (file): valid tar.gz plugin package
- `name` (string): "My Plugin"
- `description` (string): "Does something useful"
- `author` (string): "john@example.com"
- `category` (enum): "skill"
- `tags` (array): ["utility", "markdown"]
- `initialVersion` (semver): "1.0.0"
- `releaseNotes` (string): "Initial release"
**THEN** the system SHALL return HTTP 201 with a response containing `pluginId` and `message="Plugin uploaded successfully"`

#### Scenario: Submit without optional fields
**WHEN** a client POSTs to `/api/plugins/upload` with only required fields (name, description, author, category, package, initialVersion)
**THEN** the system SHALL accept the submission
**AND** return HTTP 201 with `pluginId`
**AND** `tags` and `releaseNotes` SHALL default to empty array and empty string respectively

#### Scenario: Submit with only required fields and no file
**WHEN** a client POSTs to `/api/plugins/upload` without the `package` file
**THEN** the system SHALL return HTTP 400 with error message `"Package file is required"`

### Requirement: Validate required metadata fields

The system SHALL validate that all required metadata fields are present and meet format constraints before accepting the submission.

#### Scenario: Missing required field (name)
**WHEN** a client POSTs to `/api/plugins/upload` without the `name` field
**THEN** the system SHALL return HTTP 400 with error message `"Required field missing: name"`

#### Scenario: Invalid category value
**WHEN** a client POSTs to `/api/plugins/upload` with `category="invalid_category"`
**THEN** the system SHALL return HTTP 400 with error message `"Invalid category. Allowed values: skill, hook, agent, other"`

#### Scenario: Empty description string
**WHEN** a client POSTs to `/api/plugins/upload` with `description=""` (empty string)
**THEN** the system SHALL return HTTP 400 with error message `"Description must not be empty"`

#### Scenario: Invalid semver format for initialVersion
**WHEN** a client POSTs to `/api/plugins/upload` with `initialVersion="not-a-version"`
**THEN** the system SHALL return HTTP 400 with error message `"initialVersion must be a valid semantic version (e.g., 1.0.0)"`

### Requirement: Validate plugin package format and structure

The system SHALL validate the uploaded package file format and verify it contains required manifest files.

#### Scenario: Valid tar.gz package with manifest
**WHEN** a client POSTs a tar.gz package containing a valid `plugin.json` or `manifest.json` at the root
**THEN** the system SHALL accept the package as valid
**AND** return HTTP 201 with `pluginId`

#### Scenario: Unsupported package format
**WHEN** a client POSTs a package file with extension `.exe`, `.sh`, or other non-archive format
**THEN** the system SHALL return HTTP 400 with error message `"Unsupported package format. Allowed: tar.gz, zip"`

#### Scenario: Package is corrupted or not extractable
**WHEN** a client POSTs a file named `.tar.gz` but with invalid gzip content
**THEN** the system SHALL return HTTP 400 with error message `"Package file is corrupted or not a valid archive"`

#### Scenario: Package missing required manifest file
**WHEN** a client POSTs a valid tar.gz that does not contain `plugin.json` or `manifest.json`
**THEN** the system SHALL return HTTP 400 with error message `"Package must contain plugin.json or manifest.json at root level"`

### Requirement: Assign unique plugin identifier and prevent duplicate submissions

The system SHALL generate a unique identifier for each plugin and reject submissions with duplicate names.

#### Scenario: First submission of plugin with unique name
**WHEN** a client POSTs to `/api/plugins/upload` with `name="UniquePlugin"`
**THEN** the system SHALL generate a unique `pluginId` (e.g., UUID or slug)
**AND** return HTTP 201 with the generated `pluginId`
**AND** store the plugin in the database with this identifier

#### Scenario: Attempt to submit plugin with duplicate name
**WHEN** a plugin named "MyPlugin" already exists in the catalog
**AND** a client POSTs to `/api/plugins/upload` with `name="MyPlugin"`
**THEN** the system SHALL return HTTP 409 (Conflict) with error message `"A plugin with name 'MyPlugin' already exists"`

#### Scenario: Duplicate submission with different casing
**WHEN** a plugin named "myplugin" already exists
**AND** a client POSTs with `name="MyPlugin"` (different case)
**THEN** the system SHALL treat this as a duplicate
**AND** return HTTP 409 with conflict error

### Requirement: Accept and record initial version

The system SHALL create the first version of the plugin from the upload submission, storing version metadata and release notes.

#### Scenario: Upload creates initial 1.0.0 version
**WHEN** a client POSTs to `/api/plugins/upload` with `initialVersion="1.0.0"` and `releaseNotes="Initial release"`
**THEN** the system SHALL create a plugin version record with:
- `versionNumber="1.0.0"`
- `releaseNotes="Initial release"`
- `isLatest=true`
- `releasedAt` set to current timestamp
**AND** return HTTP 201 with `pluginId` and version reference

#### Scenario: Upload with custom initial version (not 1.0.0)
**WHEN** a client POSTs to `/api/plugins/upload` with `initialVersion="2.5.3"`
**THEN** the system SHALL accept the custom version number
**AND** create the plugin with version "2.5.3" as the initial (and latest) version

#### Scenario: Upload without release notes
**WHEN** a client POSTs to `/api/plugins/upload` without `releaseNotes` field
**THEN** the system SHALL set `releaseNotes` to empty string or a default placeholder
**AND** return HTTP 201 with the plugin ID
