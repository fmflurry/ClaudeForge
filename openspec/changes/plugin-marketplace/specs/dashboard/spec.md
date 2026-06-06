# Dashboard Specification

## ADDED Requirements

### Requirement: Display Installed Plugins List

The dashboard SHALL render a list of all installed plugins with name, version, installation date, and current status in a clear table format.

#### Scenario: View installed plugins
**WHEN** the dashboard loads for a user with installed plugins
**THEN** the dashboard SHALL display:
- A table with columns: Plugin Name, Version, Installed Date, Status, Actions
- Each row representing one installed plugin
- Status column showing either "up-to-date" or "update-available"
- Action buttons for each plugin: View Details, Update, Remove
- Total installed plugin count displayed at the top

#### Scenario: View dashboard with no installed plugins
**WHEN** a user opens the dashboard and no plugins are installed
**THEN** the dashboard SHALL:
- Display "No plugins installed yet"
- Show a prominent "Browse Marketplace" button
- Display a quick-start guide with suggested popular plugins
- Suggest searching or browsing the marketplace to get started

#### Scenario: Plugin list persists across sessions
**WHEN** a user installs a plugin, closes the browser, and returns later
**THEN** the dashboard SHALL:
- Retrieve the list of installed plugins from browser storage
- Display the same plugins with the same versions and metadata
- Verify plugin status by querying the marketplace (update-available check)

### Requirement: Show Update Availability

The dashboard SHALL indicate when a plugin has a newer version available in the marketplace and enable one-click updates.

#### Scenario: Update available indicator
**WHEN** the dashboard loads and discovers that an installed plugin has a newer version
**THEN** the dashboard SHALL:
- Mark the plugin row with a status badge: "update-available"
- Display the current version and new version (e.g., "1.2.3 → 1.5.0")
- Highlight the row visually (e.g., light yellow background)
- Enable an "Update" button in the Actions column

#### Scenario: Update plugin from dashboard
**WHEN** a user clicks "Update" on a plugin with update-available status
**THEN** the dashboard SHALL:
- Show a confirmation dialog: "Update @namespace/plugin from v1.2.3 to v1.5.0?"
- Display a brief release notes summary if available
- Upon confirmation, download and install the new version
- Update browser storage with the new version
- Refresh the table and show success message: "Updated @namespace/plugin to v1.5.0"
- Mark status as "up-to-date" after completion

#### Scenario: Update fails and dashboard recovers
**WHEN** a plugin update fails during download
**THEN** the dashboard SHALL:
- Display an error modal with details: "Update failed: Connection lost"
- Show the previous version in the table
- Keep the "update-available" badge visible
- Enable a retry button

### Requirement: Display Plugin Details and Documentation Links

The dashboard SHALL provide access to plugin documentation and key metadata for each installed plugin.

#### Scenario: View plugin details modal
**WHEN** a user clicks "View Details" on an installed plugin
**THEN** the dashboard SHALL display a modal with:
- Plugin name, current version, author
- Description (from plugin.json)
- Type/category badges (e.g., "skill", "auth", "api")
- Use-case tags (e.g., "dev-team", "devops", "security")
- Installation date and last update date
- Declared dependencies (if any)
- Documentation URL (if available) as a clickable link
- Remove button to uninstall the plugin

#### Scenario: Navigate to plugin documentation
**WHEN** a user clicks the documentation URL in the plugin details modal
**THEN** the dashboard SHALL:
- Open the docs URL in a new tab
- Track the click for telemetry (anonymized)

#### Scenario: Plugin details for plugin without docs-url
**WHEN** a user views details for a plugin that did not declare a docs-url
**THEN** the dashboard SHALL:
- Display a placeholder: "No documentation URL provided by author"
- Offer a link to browse the marketplace entry for that plugin

### Requirement: Remove Installed Plugin

The dashboard SHALL enable users to uninstall plugins with confirmation and error handling.

#### Scenario: Remove plugin from dashboard
**WHEN** a user clicks "Remove" on an installed plugin
**THEN** the dashboard SHALL:
- Show a confirmation dialog: "Remove @namespace/plugin from your machine?"
- Upon confirmation, delete the plugin from local storage and browser storage
- Refresh the table and remove the row
- Display success message: "Removed @namespace/plugin"

#### Scenario: Remove fails due to locked file
**WHEN** the dashboard attempts to remove a plugin but the file is in use
**THEN** the dashboard SHALL:
- Display an error: "Could not remove plugin: file is in use. Try restarting Claude Code"
- Keep the plugin in the list
- Suggest contacting support if the issue persists

### Requirement: Search and Filter Available Plugins

The dashboard SHALL provide a search interface to browse the marketplace and filter results by type, language, or use-case tag.

#### Scenario: Search available plugins
**WHEN** a user enters a search term (e.g., "authentication") in the "Search Marketplace" box
**THEN** the dashboard SHALL:
- Query the marketplace search API
- Display results in a separate "Available Plugins" section below the installed list
- Show columns: Name, Latest Version, Description, Download Count, Status (e.g., "Install", "Already Installed")
- Order by relevance or download count
- Show up to 10 results with pagination

#### Scenario: Filter by plugin type
**WHEN** a user selects a filter (e.g., "Type: Skill")
**THEN** the dashboard SHALL:
- Filter the available plugins to show only those with matching type
- Update the search results automatically
- Display the number of results matching the filter

#### Scenario: Filter by use-case tag
**WHEN** a user clicks a use-case tag filter (e.g., "dev-team", "devops")
**THEN** the dashboard SHALL:
- Filter available plugins to show those with the selected use-case tag
- Allow multi-select filtering (show plugins matching ANY selected tag)
- Update results in real-time

### Requirement: Install Plugin from Dashboard

The dashboard SHALL enable one-click installation of plugins from search results with status feedback.

#### Scenario: Install plugin from search results
**WHEN** a user clicks "Install" on an available plugin in the search results
**THEN** the dashboard SHALL:
- Show a loading spinner: "Installing @namespace/plugin..."
- Download and install the plugin to local storage
- Update browser storage with the new plugin entry
- Change the button to "Already Installed"
- Refresh the installed plugins list at the top to show the new plugin
- Display success message: "Installed @namespace/plugin v1.0.0"

#### Scenario: Install fails and shows error
**WHEN** an installation fails (e.g., network error, version conflict)
**THEN** the dashboard SHALL:
- Display an error modal with details: "Installation failed: Marketplace unavailable"
- Show a "Retry" button
- Keep the "Install" button enabled for the user to try again

### Requirement: Persist Team Context in Browser Storage

The dashboard SHALL store and retrieve team context (team name, member list, plugin preferences) from browser storage without requiring authentication.

#### Scenario: Initialize team context
**WHEN** a user opens the dashboard for the first time
**THEN** the dashboard SHALL:
- Prompt for team name (e.g., "My Dev Team")
- Store the team name in browser localStorage under a unique key
- Use team name to organize plugin context within the dashboard
- Display team name in the dashboard header

#### Scenario: Retrieve team context on return
**WHEN** a user returns to the dashboard on the same browser/device
**THEN** the dashboard SHALL:
- Load the stored team name from localStorage
- Display team name in the header
- Load the list of installed plugins associated with that team
- Load any team-level plugin preferences or notes

#### Scenario: Switch team context
**WHEN** a user clicks "Switch Team" or manually edits the team name
**THEN** the dashboard SHALL:
- Prompt to confirm the team switch
- Save the new team name
- Isolate plugin installations by team (different teams can have different installed plugins)
- Migrate or preserve existing plugin data based on user choice

### Requirement: Real-Time Status Updates

The dashboard SHALL periodically check the marketplace for plugin updates and refresh status without requiring user action.

#### Scenario: Auto-check for updates on load
**WHEN** the dashboard first loads
**THEN** the dashboard SHALL:
- Query the marketplace API for the latest version of each installed plugin
- Compare versions and set status (up-to-date or update-available)
- Display the refresh status: "Last checked 2 minutes ago"

#### Scenario: Periodic background status check
**WHEN** the user has the dashboard open for an extended period
**THEN** the dashboard SHALL:
- Check for updates every 5 minutes (configurable)
- Silently update the status badges if new versions are available
- Show a subtle notification if updates are available for multiple plugins
- Allow users to toggle auto-check in settings

#### Scenario: Check for updates fails gracefully
**WHEN** the marketplace API is temporarily unavailable during a status check
**THEN** the dashboard SHALL:
- Retain the previous known status
- Display a warning icon: "Could not verify latest versions"
- Offer a "Retry" button
- Continue displaying cached version information
