## ADDED Requirements

### Requirement: Multiple category tags on plugin upload

When a plugin is published, the system SHALL persist the multiple category tags declared in the plugin manifest (type, language, and use-case dimensions) as associations between the plugin and the category vocabulary. Publishing SHALL support more than one tag per plugin.

#### Scenario: Manifest tags are persisted
- **WHEN** a plugin is published with multiple category tags in its manifest (e.g. several use-case tags)
- **THEN** the system SHALL persist all of those tag associations for the plugin so they are queryable

#### Scenario: Multiple use-case tags retained
- **WHEN** a plugin declares more than one use-case tag on upload
- **THEN** all declared use-case tags SHALL be associated with the plugin, not just the first

### Requirement: Controlled category vocabulary on upload

The category tags accepted at upload SHALL be validated against the controlled category vocabulary for each dimension. Unknown tag values SHALL be rejected with a clear validation error identifying the invalid values, rather than silently dropped or auto-created.

#### Scenario: Unknown tag rejected
- **WHEN** a plugin is published with a tag value not present in the controlled vocabulary for its dimension
- **THEN** publishing SHALL fail with a validation error naming the invalid value(s)

#### Scenario: Known tags accepted
- **WHEN** a plugin is published with tag values that all exist in the controlled vocabulary
- **THEN** publishing SHALL succeed and persist the tag associations

### Requirement: Category vocabulary covers plugin kinds

The use-case category vocabulary SHALL include values representing the plugin "kind" axis used for discovery (for example SWE/Engineering, Product, UX/UI, DevOps), so that plugins can be tagged and filtered by these kinds.

#### Scenario: Kind categories are available
- **WHEN** the category vocabulary is queried for the use-case dimension
- **THEN** it SHALL include the plugin-kind values used by the discovery filters (such as SWE/Engineering, Product, UX/UI, DevOps)

### Requirement: Discovery filtering by category

The system SHALL allow plugins to be discovered/filtered by their category tags. Filtering SHALL combine selections as OR within a single dimension and AND across dimensions.

#### Scenario: Filter by a single category
- **WHEN** a user filters the catalog by one category value
- **THEN** only plugins associated with that category SHALL be returned

#### Scenario: Combining categories across dimensions
- **WHEN** a user selects categories from more than one dimension
- **THEN** results SHALL include only plugins matching at least one selected value in each active dimension (OR within dimension, AND across dimensions)
