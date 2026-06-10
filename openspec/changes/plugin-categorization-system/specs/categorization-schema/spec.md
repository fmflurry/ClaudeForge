# categorization-schema Specification

## Purpose

Defines the three-layer categorization taxonomy for plugin marketplace discovery. Establishes controlled vocabularies for domain category and structural type, validation rules for marketplace.json, and keyword format constraints.

## Requirements

### Requirement: Three-layer taxonomy structure

The plugin categorization system SHALL use three distinct layers: Layer 1 — Domain category (single-select, required), Layer 2 — Structural type (multi-select, optional tags), Layer 3 — Free-form keywords (multi-select, optional).

#### Scenario: Plugin manifest contains all three layers
- **WHEN** a plugin is published with a valid marketplace.json
- **THEN** the manifest SHALL contain a `category` field (Layer 1), and a `keywords` array (Layers 2 + 3 merged)

### Requirement: Domain category is a required single-select

Every plugin SHALL have exactly one domain category. The `category` field in marketplace.json is REQUIRED. Only one value is allowed — multi-select is explicitly NOT supported.

#### Scenario: Plugin with no category rejected
- **WHEN** a plugin is published without a `category` field in its marketplace.json
- **THEN** publishing SHALL fail with a validation error indicating category is required

#### Scenario: Plugin with multiple categories rejected
- **WHEN** a plugin is published with multiple values in the `category` field
- **THEN** publishing SHALL fail with a validation error indicating category must be a single value

### Requirement: Domain category vocabulary

The domain category vocabulary SHALL contain exactly 11 values: `code-intelligence`, `language-framework`, `external-service`, `workflow-orchestration`, `security`, `testing-qa`, `devops-infrastructure`, `data-analytics`, `documentation`, `productivity-utilities`, `domain-vertical`.

#### Scenario: Valid domain category accepted
- **WHEN** a plugin is published with `category` set to one of the 11 vocabulary values
- **THEN** publishing SHALL accept the category value without error

#### Scenario: Invalid domain category rejected
- **WHEN** a plugin is published with `category` set to a value not in the controlled vocabulary
- **THEN** publishing SHALL fail with a validation error naming the invalid value and listing allowed values

### Requirement: Structural type is a multi-select keyword subset

Structural type tags SHALL be a subset of the `keywords` array. The structural vocabulary contains exactly 5 values: `skill`, `subagent`, `command`, `hook`, `mcp-server`. These values are recognized keywords but stored in the same `keywords` array as free-form keywords.

#### Scenario: Structural keywords recognized
- **WHEN** a plugin's `keywords` array contains values from the structural vocabulary
- **THEN** the system SHALL recognize those entries as structural type tags for filtering purposes

#### Scenario: Multiple structural types allowed
- **WHEN** a plugin is published with multiple structural vocabulary values in its `keywords` array
- **THEN** all structural values SHALL be persisted and filterable

### Requirement: Free-form keywords have no vocabulary constraint

Keywords that are NOT in the structural vocabulary SHALL be accepted as free-form discovery tags with no validation constraint beyond being non-empty strings.

#### Scenario: Free-form keyword accepted
- **WHEN** a plugin is published with a keyword like `typescript` that is not in the structural vocabulary
- **THEN** publishing SHALL accept the keyword and it SHALL be searchable via keyword search

### Requirement: Structural keyword values are validated

Structural vocabulary values in the `keywords` array SHALL be validated against the structural vocabulary. Unknown structural values SHALL be accepted silently (they become free-form keywords) — no rejection occurs for unrecognized keywords.

#### Scenario: Unknown keyword becomes free-form
- **WHEN** a plugin is published with a keyword not in the structural vocabulary and not matching any known pattern
- **THEN** the keyword SHALL be persisted as a free-form keyword without error

### Requirement: marketplace.json schema validation

The system SHALL validate marketplace.json against a schema that enforces the `category` field type (string), the `keywords` field type (array of strings), and rejects old deprecated fields (`type`, `use-case`, `kind`).

#### Scenario: Deprecated fields rejected
- **WHEN** a marketplace.json contains old fields `type`, `use-case`, or `kind`
- **THEN** publishing SHALL fail with a validation error indicating these fields are deprecated and pointing to the migration guide

#### Scenario: Valid schema accepted
- **WHEN** a marketplace.json contains only `category` (string) and `keywords` (array of strings)
- **THEN** schema validation SHALL pass

### Requirement: Keyword array values are non-empty strings

Every entry in the `keywords` array SHALL be a non-empty string. Empty strings or whitespace-only strings SHALL be rejected.

#### Scenario: Empty keyword rejected
- **WHEN** a plugin is published with an empty string in the `keywords` array
- **THEN** publishing SHALL fail with a validation error identifying the empty value
