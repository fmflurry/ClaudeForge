# category-migration Specification

## Purpose

Maps existing plugin tags (use-case, kind, type dimensions) to the new domain category system. Provides fallback logic for ambiguous mappings and generates a migration script for the marketplace registry.

## Requirements

### Requirement: Static mapping table for existing tags

The migration system SHALL use a static lookup table that maps each existing tag value to a new domain category. The mapping SHALL be deterministic — same input always produces same output.

#### Scenario: Known use-case tag mapped correctly
- **WHEN** a plugin has `use-case: "code-review"` in its old manifest
- **THEN** the migration SHALL map it to `category: "code-intelligence"`

#### Scenario: Known kind tag mapped correctly
- **WHEN** a plugin has `kind: "DevOps"` in its old manifest
- **THEN** the migration SHALL map it to `category: "devops-infrastructure"`

#### Scenario: Known type tag becomes structural keyword
- **WHEN** a plugin has `type: "skill"` in its old manifest
- **THEN** the migration SHALL move `skill` into the `keywords` array as a structural keyword and NOT set it as the domain category

### Requirement: Ambiguous tag fallback to nearest category

Plugins with ambiguous or multi-valued tags that don't map cleanly SHALL receive the nearest domain category via a defined fallback rule. The fallback SHALL be deterministic and documented.

#### Scenario: Ambiguous use-case mapped to closest domain
- **WHEN** a plugin has `use-case: "testing"` which could map to `testing-qa` or `quality-assurance`
- **THEN** the migration SHALL map it to `testing-qa` (the closest domain)

#### Scenario: Missing tags default to productivity-utilities
- **WHEN** a plugin has no recognizable tags or all tags are unknown
- **THEN** the migration SHALL assign `category: "productivity-utilities"` as the safe default

### Requirement: Migration produces before/after report

The migration script SHALL generate a report listing each plugin's old tags and new categorization. The report SHALL be reviewable before the migration is applied.

#### Scenario: Migration report generated
- **WHEN** the migration script runs against the plugin registry
- **THEN** it SHALL produce a report file showing each plugin's old tag values, new category, and new keywords

#### Scenario: Report available for manual review
- **WHEN** the migration report is generated
- **THEN** the report SHALL be written to a file that can be reviewed in a PR before merge

### Requirement: Migration script rewrites marketplace.json files

The migration script SHALL rewrite each plugin's marketplace.json to replace old dimension fields with the new `category` and `keywords` structure. Old fields (`type`, `use-case`, `kind`) SHALL be removed.

#### Scenario: Old fields removed after migration
- **WHEN** a plugin's marketplace.json is migrated
- **THEN** the old `type`, `use-case`, and `kind` fields SHALL be absent from the migrated file

#### Scenario: New fields present after migration
- **WHEN** a plugin's marketplace.json is migrated
- **THEN** the file SHALL contain a `category` field (string) and a `keywords` field (array of strings)

### Requirement: Migration is idempotent

Running the migration script on already-migrated files SHALL produce no changes. The script SHALL detect already-migrated manifests and skip them.

#### Scenario: Already-migrated plugin skipped
- **WHEN** the migration script runs on a marketplace.json that already has `category` and no deprecated fields
- **THEN** the script SHALL leave the file unchanged

### Requirement: Plugin authors can override post-migration categorization

After migration runs, plugin authors SHALL be able to change the assigned `category` via normal publishing flow. Migration-assigned categories are not permanent.

#### Scenario: Author overrides migrated category
- **WHEN** a plugin author publishes a new version after migration with a different `category` value
- **THEN** the new category SHALL be accepted and replace the migration-assigned category
