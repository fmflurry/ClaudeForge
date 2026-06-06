# Plugin Categorization Capability Specification

## ADDED Requirements

### Requirement: Tag Plugins by Type

The system SHALL allow plugins to be tagged with one or more types: skill, hook, plugin, command, or agent.

#### Scenario: Plugin has single type

**WHEN** a plugin is created with type="skill"
**THEN** the plugin is tagged as a skill in the system
**AND** the type is immutable after creation (cannot be changed without admin intervention)

#### Scenario: Plugin has multiple types

**WHEN** a plugin is created with types=["skill", "hook"]
**THEN** the plugin is tagged with both types
**AND** the plugin appears in results when filtering by either type

#### Scenario: Invalid type is rejected

**WHEN** a user attempts to create a plugin with type="widget" (not a valid type)
**THEN** the system returns HTTP 400 with error message "Type must be one of: skill, hook, plugin, command, agent"

### Requirement: Tag Plugins by Programming Language

The system SHALL allow plugins to be tagged with one or more programming languages they support or are written in.

#### Scenario: Plugin supports single language

**WHEN** a plugin is created with language="Python"
**THEN** the plugin is tagged as supporting Python
**AND** the plugin appears in results when filtering by language="Python"

#### Scenario: Plugin supports multiple languages

**WHEN** a plugin is created with languages=["TypeScript", "Python", "Rust"]
**THEN** the plugin is tagged as supporting all three languages
**AND** the plugin appears in results when filtering by any of these languages

#### Scenario: Language list is empty

**WHEN** a user attempts to create a plugin with an empty languages array
**THEN** the system returns HTTP 400 with error message "At least one language must be specified"

### Requirement: Tag Plugins by Use Case

The system SHALL allow plugins to be tagged with one or more use cases: dev team, PO (product owner), PM (project manager), DevOps, or other team roles.

#### Scenario: Plugin tagged with single use case

**WHEN** a plugin is created with useCase="DevOps"
**THEN** the plugin is tagged for the DevOps use case
**AND** the plugin appears in results when filtering by useCase="DevOps"

#### Scenario: Plugin tagged with multiple use cases

**WHEN** a plugin is created with useCases=["dev team", "PO", "PM"]
**THEN** the plugin is tagged for all three use cases
**AND** the plugin appears in results when filtering by any of these use cases

#### Scenario: Plugin with no use case

**WHEN** a plugin is retrieved that has not been tagged with a use case
**THEN** the plugin is returned with useCases=[] or omitted
**AND** the plugin does not appear when filtering by any specific use case

### Requirement: List Available Categories

The system SHALL provide an endpoint to retrieve all available categories and category metadata.

#### Scenario: Request lists all category types

**WHEN** a user requests GET /categories
**THEN** the system returns an object with keys: types, languages, useCases
**AND** each key contains an array of available values
**AND** response includes HTTP 200

#### Scenario: Categories endpoint includes metadata

**WHEN** a user requests GET /categories
**THEN** each category value includes optional display name and description
**AND** example: { value: "dev team", displayName: "Development Team", description: "For software development teams" }

#### Scenario: Empty category response when no plugins exist

**WHEN** the system has no plugins yet
**THEN** the categories endpoint returns empty arrays for each category type
**AND** does not return HTTP 404, still returns HTTP 200 with empty structure

### Requirement: Filter and Browse Plugins by Single Category

The system SHALL support filtering plugins by a single category value.

#### Scenario: Filter by type returns matching plugins

**WHEN** a user filters plugins by type="command"
**THEN** the system returns all plugins with type="command"
**AND** plugins without this type are excluded

#### Scenario: Filter by language returns matching plugins

**WHEN** a user filters by language="Rust"
**THEN** the system returns all plugins supporting Rust
**AND** plugins that do not support Rust are excluded

#### Scenario: Filter by use case returns matching plugins

**WHEN** a user filters by useCase="PM"
**THEN** the system returns all plugins tagged for the PM use case
**AND** plugins not tagged for PM are excluded

#### Scenario: Empty filter result

**WHEN** a user filters by a valid category value with no matching plugins
**THEN** the system returns HTTP 200 with empty array
**AND** response includes totalCount=0

### Requirement: Filter and Browse Plugins by Multiple Categories (Combination)

The system SHALL support filtering plugins by multiple category values, combining them with AND logic.

#### Scenario: Filter by multiple categories returns intersection

**WHEN** a user filters by type="skill" AND language="Python"
**THEN** the system returns plugins that are BOTH type="skill" AND language="Python"
**AND** plugins matching only one criterion are excluded

#### Scenario: Multiple use case filters use OR logic within same dimension

**WHEN** a user filters by useCases=["DevOps", "PO"]
**THEN** the system returns plugins tagged for DevOps OR PO (or both)
**AND** plugins tagged for neither are excluded

#### Scenario: Multiple dimensions use AND logic

**WHEN** a user filters by type="hook" AND language=["TypeScript", "Python"] AND useCase="dev team"
**THEN** the system returns plugins that are ALL: type=hook AND (language=TypeScript OR language=Python) AND useCase=dev team
**AND** any plugin not matching all criteria is excluded

#### Scenario: Invalid category filter value is handled

**WHEN** a user filters by language="FORTRAN" (unsupported language)
**THEN** the system returns HTTP 400 with error message "language 'FORTRAN' is not a valid category value"
**AND** the response includes available language options for correction

### Requirement: Plugin Can Have Multiple Tags in Same Dimension

The system SHALL allow a single plugin to have multiple tags within the same category dimension (e.g., multiple types, multiple languages).

#### Scenario: Plugin retrieved with all tags intact

**WHEN** a plugin is retrieved that was created with types=["skill", "command"] and languages=["TypeScript", "Go"]
**THEN** the response includes all types and all languages as complete arrays
**AND** no tags are lost or deduplicated incorrectly

#### Scenario: Filtering returns multi-tagged plugins

**WHEN** a user filters by type="command"
**THEN** a plugin with types=["skill", "command"] is included
**AND** the response shows all types, not just the matched type

