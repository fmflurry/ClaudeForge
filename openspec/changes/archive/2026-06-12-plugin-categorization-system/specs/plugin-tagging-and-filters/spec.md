# plugin-tagging-and-filters Specification

## Purpose

Defines how plugins are tagged and discovered in the marketplace. This spec is MODIFIED by the plugin-categorization-system change — domain category becomes required single-select, structural tags replace old type dimension, and filter logic changes from flat OR-within/AND-across to domain-first hierarchy.

## MODIFIED Requirements

### Requirement: Multiple category tags on plugin upload

When a plugin is published, the system SHALL persist the domain category (single-select, required) and keyword array from the plugin manifest. The domain category SHALL be a single value from the controlled domain vocabulary. The keywords array SHALL contain both structural type tags and free-form discovery keywords. The old multi-tag model (type, language, use-case dimensions) is removed.

#### Scenario: Manifest tags are persisted
- **WHEN** a plugin is published with a valid `category` and `keywords` in its manifest
- **THEN** the system SHALL persist the domain category association and all keyword values for the plugin so they are queryable

#### Scenario: Single domain category enforced
- **WHEN** a plugin declares its domain category on upload
- **THEN** exactly one domain category value SHALL be associated with the plugin (not multiple, not zero)

### Requirement: Controlled category vocabulary on upload

The domain category accepted at upload SHALL be validated against the controlled domain vocabulary of 11 values. The structural type values in the keywords array SHALL be validated against the structural vocabulary of 5 values. Unknown free-form keywords SHALL be accepted without validation. Unknown domain category values SHALL be rejected.

#### Scenario: Unknown domain category rejected
- **WHEN** a plugin is published with a `category` value not present in the domain vocabulary
- **THEN** publishing SHALL fail with a validation error naming the invalid value and listing the allowed domain categories

#### Scenario: Valid domain category accepted
- **WHEN** a plugin is published with a `category` value in the domain vocabulary
- **THEN** publishing SHALL succeed and persist the domain category association

#### Scenario: Structural keywords accepted
- **WHEN** a plugin is published with structural vocabulary values (e.g., `skill`, `hook`) in its `keywords` array
- **THEN** those values SHALL be accepted and stored as keywords

### Requirement: Category vocabulary covers plugin kinds

The domain category vocabulary SHALL include values representing the primary functional domains users browse by: `code-intelligence`, `language-framework`, `external-service`, `workflow-orchestration`, `security`, `testing-qa`, `devops-infrastructure`, `data-analytics`, `documentation`, `productivity-utilities`, `domain-vertical`.

#### Scenario: All domain categories available
- **WHEN** the category vocabulary is queried
- **THEN** it SHALL contain the 11 domain values specified in the design

### Requirement: Discovery filtering by category

The system SHALL allow plugins to be discovered/filtered using domain-first hierarchy. Domain category is the primary filter (AND, exact match). Structural type is secondary (OR within selection, AND with domain). Keywords are tertiary (OR match, AND with domain + structural). Old dimension-based filtering (type, use-case, kind) is deprecated but accepted during transition with deprecation headers.

#### Scenario: Filter by domain category only
- **WHEN** a user filters the catalog by one domain category value
- **THEN** only plugins with that domain category SHALL be returned

#### Scenario: Filter by domain + structural
- **WHEN** a user selects a domain category and one or more structural types
- **THEN** only plugins matching the domain AND having at least one selected structural keyword SHALL be returned

#### Scenario: Filter by domain + keyword
- **WHEN** a user selects a domain category and enters a keyword search term
- **THEN** only plugins in that domain with a matching keyword SHALL be returned

#### Scenario: Old filter params accepted with deprecation
- **WHEN** a client sends old filter params (`type`, `use-case`, `kind`) to the API
- **THEN** the API SHALL accept them, return results, AND include a deprecation header indicating the params are deprecated
