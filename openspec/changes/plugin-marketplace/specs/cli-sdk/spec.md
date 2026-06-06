# CLI-SDK Specification

## ADDED Requirements

### Requirement: Install Plugin from Marketplace

The CLI SHALL enable developers to install a plugin from the marketplace with a single command, writing plugin metadata and location to local storage.

#### Scenario: Install plugin by name
**WHEN** a developer runs `claude plugin install @namespace/plugin-name`
**THEN** the CLI SHALL:
- Resolve the plugin name against the marketplace API
- Fetch plugin metadata and the latest published version
- Download the plugin package to the configured local plugins directory
- Record the plugin name, version, and installation timestamp in local storage
- Display success message with installed version and plugin entrypoints

#### Scenario: Install specific version
**WHEN** a developer runs `claude plugin install @namespace/plugin-name@1.2.3`
**THEN** the CLI SHALL:
- Fetch the specified version from the marketplace
- Download and install it
- Record the exact version in local storage
- Warn if a newer version is available

#### Scenario: Network error during installation
**WHEN** the marketplace API is unreachable or the download fails
**THEN** the CLI SHALL:
- Halt gracefully before writing to local storage
- Report the error (e.g., "Could not reach marketplace at https://...")
- Exit with a non-zero code
- Suggest retry or manual configuration of API URL

### Requirement: Remove Installed Plugin

The CLI SHALL enable removal of a locally-installed plugin, deleting it from disk and local storage.

#### Scenario: Remove plugin by name
**WHEN** a developer runs `claude plugin remove @namespace/plugin-name`
**THEN** the CLI SHALL:
- Locate the plugin in local storage
- Delete the plugin directory from the filesystem
- Remove the entry from local storage
- Display confirmation: "Removed @namespace/plugin-name v1.2.3"

#### Scenario: Remove non-existent plugin
**WHEN** a developer runs `claude plugin remove @namespace/nonexistent`
**THEN** the CLI SHALL:
- Report "Plugin @namespace/nonexistent is not installed"
- Suggest listing installed plugins with `claude plugin list`
- Exit with a non-zero code

### Requirement: List Installed Plugins

The CLI SHALL list all locally-installed plugins with versions and basic metadata.

#### Scenario: List installed plugins
**WHEN** a developer runs `claude plugin list`
**THEN** the CLI SHALL:
- Read installed plugin records from local storage
- Display a table with columns: Name, Version, Installed Date, Status
- Indicate plugin status (up-to-date or update-available) by checking marketplace
- Display "No plugins installed" if the list is empty

#### Scenario: Check for updates while listing
**WHEN** a developer runs `claude plugin list --check-updates`
**THEN** the CLI SHALL:
- Fetch latest versions from the marketplace for each installed plugin
- Mark plugins with newer versions available as "update-available"
- Display recommended actions for each outdated plugin

### Requirement: Update Installed Plugin

The CLI SHALL update a plugin to a newer version, handling version conflicts and rollback on error.

#### Scenario: Update plugin to latest version
**WHEN** a developer runs `claude plugin update @namespace/plugin-name`
**THEN** the CLI SHALL:
- Fetch the latest version from the marketplace
- If already at latest, report "Plugin is already up-to-date at v1.5.0"
- Otherwise, download the new version
- Verify the new version is compatible (check dependency declarations)
- Update local storage with new version
- Display: "Updated @namespace/plugin-name from v1.2.3 to v1.5.0"

#### Scenario: Version conflict during update
**WHEN** the new plugin version declares incompatible dependencies
**THEN** the CLI SHALL:
- Halt before installation
- Report the conflict (e.g., "v2.0.0 requires framework-x >= 3.0, but you have 2.5")
- Suggest manual resolution or rollback
- Exit without modifying local storage

#### Scenario: Update fails and plugin becomes corrupted
**WHEN** an error occurs mid-download or mid-extraction
**THEN** the CLI SHALL:
- Restore the previous version from backup
- Report the failure and the rollback action
- Retain the old version in local storage
- Suggest reporting the error to the plugin author

### Requirement: Configure Marketplace API URL

The CLI SHALL allow developers to specify or override the marketplace API base URL, with sensible defaults and validation.

#### Scenario: Set marketplace API URL via config command
**WHEN** a developer runs `claude plugin config set api-url https://custom-marketplace.local`
**THEN** the CLI SHALL:
- Validate the URL format
- Store the URL in a config file (e.g., ~/.claude-plugins/config.json)
- Test connectivity to the URL
- Report success or "Could not connect to API at https://custom-marketplace.local"

#### Scenario: Use default marketplace URL
**WHEN** a developer has never set a custom API URL
**THEN** the CLI SHALL:
- Default to the official marketplace (e.g., https://plugins.claudeforge.dev)
- Allow override via environment variable `CLAUDE_PLUGINS_API_URL`
- Display the current API URL when running `claude plugin config show`

### Requirement: Search Plugins in Marketplace

The CLI SHALL search the marketplace for plugins by keyword and display results with descriptions.

#### Scenario: Search by keyword
**WHEN** a developer runs `claude plugin search "authentication"`
**THEN** the CLI SHALL:
- Query the marketplace search endpoint with the keyword
- Display results as a table with columns: Name, Version, Description, Downloads
- Order by relevance (or download count if available)
- Limit to 10 results by default
- Suggest `--limit 20` to fetch more results

#### Scenario: Search returns no results
**WHEN** a developer searches for a term with no matching plugins
**THEN** the CLI SHALL:
- Report "No plugins found matching 'xyz'"
- Suggest browsing all plugins with `claude plugin list-available`
- Exit with code 0 (not an error)

### Requirement: Publish Plugin to Marketplace

The CLI SHALL allow plugin authors to upload and publish a plugin package to the marketplace, with validation and version conflict detection.

#### Scenario: Publish new plugin
**WHEN** a developer runs `claude plugin publish` from a plugin directory with valid plugin.json
**THEN** the CLI SHALL:
- Validate the plugin manifest (required fields: name, version, description, type, entrypoints)
- Check that the version does not already exist in the marketplace
- Compress the plugin directory
- Upload to the marketplace API
- Return a publish token and marketplace URL
- Display: "Published @namespace/plugin-name@1.0.0 at https://marketplace.local/plugins/@namespace/plugin-name"

#### Scenario: Publish without required metadata
**WHEN** a developer runs `claude plugin publish` but plugin.json is missing required fields
**THEN** the CLI SHALL:
- Report which fields are missing (e.g., "Missing required field: type")
- Exit with a non-zero code
- Suggest using `claude plugin scaffold` to generate a template

#### Scenario: Publish duplicate version
**WHEN** a developer attempts to publish a version that already exists in the marketplace
**THEN** the CLI SHALL:
- Report "Version 1.0.0 of @namespace/plugin-name already exists"
- Suggest incrementing the version in plugin.json
- Suggest using `--force` to overwrite (if allowed by policy)
- Exit without uploading
