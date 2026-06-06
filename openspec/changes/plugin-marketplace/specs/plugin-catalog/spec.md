# Plugin Catalog Specification

## ADDED Requirements

### Requirement: List all plugins with pagination

The system SHALL provide an endpoint that returns a paginated list of all available plugins, including core metadata (name, description, author, category, tags, latest version, download count).

#### Scenario: Fetch first page of plugins with default page size
**WHEN** a client requests GET `/api/plugins?page=1&limit=20`
**THEN** the system SHALL return HTTP 200 with a JSON array of up to 20 plugins
**AND** each plugin object SHALL contain: `id`, `name`, `description`, `author`, `category`, `tags`, `latestVersion`, `downloadCount`, and `createdAt`
**AND** the response SHALL include pagination metadata: `totalCount`, `page`, `limit`, `totalPages`

#### Scenario: Request page beyond available plugins
**WHEN** a client requests GET `/api/plugins?page=100&limit=20` (assuming fewer than 100 pages exist)
**THEN** the system SHALL return HTTP 200 with an empty plugins array
**AND** pagination metadata SHALL correctly reflect `page=100` with `totalPages` less than 100

#### Scenario: Omitted or invalid pagination parameters
**WHEN** a client requests GET `/api/plugins` (no page/limit) or GET `/api/plugins?page=abc&limit=20`
**THEN** the system SHALL use default values: `page=1`, `limit=20`
**AND** the system SHALL return HTTP 200 with the first page of results

### Requirement: Retrieve single plugin details with version history

The system SHALL provide an endpoint to fetch detailed information for a specific plugin, including all released versions with release notes.

#### Scenario: Fetch existing plugin by ID
**WHEN** a client requests GET `/api/plugins/{pluginId}`
**THEN** the system SHALL return HTTP 200 with the complete plugin object
**AND** the response SHALL include a `versions` array sorted by semver in descending order (latest first)
**AND** each version entry SHALL contain: `versionNumber`, `releaseDate`, `releaseNotes`, `downloadCount`, and `isLatest` flag

#### Scenario: Request non-existent plugin
**WHEN** a client requests GET `/api/plugins/{invalidPluginId}`
**THEN** the system SHALL return HTTP 404 with error message `"Plugin not found"`

#### Scenario: Plugin with no versions yet submitted
**WHEN** a client requests GET `/api/plugins/{pluginId}` for a plugin with no versions
**THEN** the system SHALL return HTTP 200 with the plugin object
**AND** the `versions` array SHALL be empty
**AND** the response SHALL include `latestVersion=null`

### Requirement: Filter plugins by category and tags

The system SHALL support filtering the plugin list by one or more categories and/or tags.

#### Scenario: Filter plugins by single category
**WHEN** a client requests GET `/api/plugins?category=skill`
**THEN** the system SHALL return HTTP 200 with only plugins where `category` equals `"skill"`
**AND** pagination metadata SHALL reflect the filtered count

#### Scenario: Filter plugins by multiple tags
**WHEN** a client requests GET `/api/plugins?tags=authentication&tags=security`
**THEN** the system SHALL return HTTP 200 with plugins that contain ALL specified tags
**AND** pagination metadata SHALL reflect the filtered count

#### Scenario: Filter by category and tags together
**WHEN** a client requests GET `/api/plugins?category=hook&tags=git&tags=workflow`
**THEN** the system SHALL return HTTP 200 with plugins matching the category AND all specified tags
**AND** the system SHALL apply both filters simultaneously (intersection, not union)

### Requirement: Display empty state when no plugins match criteria

The system SHALL gracefully handle and communicate empty search or filter results.

#### Scenario: Filtered search returns no results
**WHEN** a client requests GET `/api/plugins?tags=nonexistent-tag`
**THEN** the system SHALL return HTTP 200 with an empty plugins array
**AND** pagination metadata SHALL show `totalCount=0`, `totalPages=0`
**AND** the frontend SHALL render an empty-state message: "No plugins found. Try adjusting your filters."

#### Scenario: Catalog is completely empty (zero plugins in system)
**WHEN** a client requests GET `/api/plugins` and no plugins exist in the database
**THEN** the system SHALL return HTTP 200 with an empty plugins array
**AND** pagination metadata SHALL show `totalCount=0`, `page=1`, `totalPages=0`

### Requirement: Sort plugins by relevance criteria

The system SHALL support sorting the plugin list by download count, creation date, or name.

#### Scenario: Sort plugins by download count descending
**WHEN** a client requests GET `/api/plugins?sort=downloads&order=desc`
**THEN** the system SHALL return HTTP 200 with plugins ordered by `downloadCount` from highest to lowest
**AND** pagination SHALL apply to the sorted result set

#### Scenario: Sort plugins by creation date ascending
**WHEN** a client requests GET `/api/plugins?sort=createdAt&order=asc`
**THEN** the system SHALL return HTTP 200 with plugins ordered by `createdAt` from oldest to newest

#### Scenario: Invalid sort parameter
**WHEN** a client requests GET `/api/plugins?sort=invalid_field`
**THEN** the system SHALL return HTTP 400 with error message `"Invalid sort parameter"`
**OR** the system SHALL default to sorting by `createdAt` descending and return HTTP 200

### Requirement: Provide plugin search by name and description

The system SHALL support case-insensitive keyword search across plugin names and descriptions.

#### Scenario: Search for plugin by partial name match
**WHEN** a client requests GET `/api/plugins/search?q=auth`
**THEN** the system SHALL return HTTP 200 with all plugins containing "auth" in name or description (case-insensitive)
**AND** pagination metadata SHALL reflect the search result count

#### Scenario: Search with empty or whitespace-only query
**WHEN** a client requests GET `/api/plugins/search?q=` or GET `/api/plugins/search?q=%20%20`
**THEN** the system SHALL return HTTP 200 with all plugins (equivalent to unfiltered catalog)

#### Scenario: Search returns no matching plugins
**WHEN** a client requests GET `/api/plugins/search?q=xyz123nonexistent`
**THEN** the system SHALL return HTTP 200 with an empty plugins array
**AND** pagination metadata SHALL show `totalCount=0`
