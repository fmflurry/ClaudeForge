# category-filter-ui Specification

## Purpose

Marketplace browsing UI with domain category as primary filter (sidebar), structural type as secondary filter (checkboxes), keyword search as tertiary. Combines single-select domain + multi-select structure + keyword search.

## Requirements

### Requirement: Domain category displayed as primary sidebar filter

The marketplace filter UI SHALL display domain categories as a sidebar list with single-select (radio) behavior. This filter is always visible and shows the plugin count per category.

#### Scenario: Domain sidebar visible on marketplace page
- **WHEN** a user navigates to the marketplace browse view
- **THEN** a sidebar SHALL display all 11 domain categories with radio-button selection

#### Scenario: Plugin count shown per category
- **WHEN** the domain sidebar renders
- **THEN** each category entry SHALL show the count of plugins matching that category

#### Scenario: Single domain selection enforced
- **WHEN** a user selects a domain category
- **THEN** any previously selected domain category SHALL be deselected (radio behavior, not checkbox)

### Requirement: Structural type displayed as secondary checkbox filter

Structural type tags (skill, subagent, command, hook, mcp-server) SHALL be displayed as a checkbox group below the domain sidebar. Multi-select is allowed.

#### Scenario: Structural checkboxes visible
- **WHEN** a user navigates to the marketplace browse view
- **THEN** a checkbox group SHALL display the 5 structural type values

#### Scenario: Multiple structural types selectable
- **WHEN** a user checks multiple structural type checkboxes
- **THEN** all checked values SHALL be active filters combined with OR logic

### Requirement: Keyword search input at top

A text input field SHALL be placed at the top of the filter panel for keyword search. This input searches across the `keywords` array of plugins.

#### Scenario: Keyword search input visible
- **WHEN** a user navigates to the marketplace browse view
- **THEN** a text input for keyword search SHALL be displayed at the top of the filter panel

#### Scenario: Keyword search matches across keywords array
- **WHEN** a user types "typescript" in the keyword search
- **THEN** only plugins whose `keywords` array contains a matching value SHALL be returned

### Requirement: Filter combination uses domain-first hierarchy

Filters SHALL combine using domain-first logic: domain (AND, exact match) + structural (OR within selection, AND with domain) + keywords (OR match, AND with domain + structural). This replaces the old OR-within/AND-across model.

#### Scenario: Domain + structural combined
- **WHEN** a user selects domain "code-intelligence" AND structural "skill"
- **THEN** only plugins in code-intelligence domain that also have "skill" in keywords SHALL be returned

#### Scenario: Domain + multiple structural combined
- **WHEN** a user selects domain "code-intelligence" AND structural "skill" + "mcp-server"
- **THEN** only plugins in code-intelligence domain that have either "skill" OR "mcp-server" in keywords SHALL be returned

#### Scenario: All three layers combined
- **WHEN** a user selects domain "testing-qa", structural "hook", and keyword "jest"
- **THEN** only plugins in testing-qa domain, with "hook" in keywords, AND "jest" in keywords SHALL be returned

### Requirement: Structural options scoped to current domain

The structural type checkboxes SHALL only display values that exist in plugins within the currently selected domain. If no domain is selected, all structural values are shown.

#### Scenario: Structural values filtered by domain
- **WHEN** a user selects domain "security"
- **THEN** structural checkboxes SHALL only show types present in security-domain plugins (e.g., if no security plugin uses "mcp-server", that checkbox SHALL NOT appear)

#### Scenario: All structural values shown with no domain
- **WHEN** no domain category is selected
- **THEN** all 5 structural type checkboxes SHALL be visible

### Requirement: Clear all filters control

The filter panel SHALL include a "Clear all" control that resets all active filters (domain, structural, keyword) to their default state.

#### Scenario: Clear all resets filters
- **WHEN** a user has active domain, structural, and keyword filters
- **THEN** clicking "Clear all" SHALL deselect all filters and show the full unfiltered catalog

### Requirement: Deep-link filter state preserved

The current filter state (domain selection, structural selections, keyword text) SHALL be reflected in URL query parameters so that filtered views are bookmarkable and shareable.

#### Scenario: Filter state reflected in URL
- **WHEN** a user selects domain "code-intelligence" and structural "skill"
- **THEN** the URL SHALL contain query parameters reflecting those selections (e.g., `?category=code-intelligence&structural=skill`)

#### Scenario: Deep-link restores filter state
- **WHEN** a user opens a URL with filter query parameters
- **THEN** the filter UI SHALL restore to match those parameters and display the filtered results
