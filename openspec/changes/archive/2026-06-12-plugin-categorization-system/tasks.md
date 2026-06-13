## 1. Schema & Validation (categorization-schema)

- [x] 1.1 Define domain category vocabulary constant with 11 values: `code-intelligence`, `language-framework`, `external-service`, `workflow-orchestration`, `security`, `testing-qa`, `devops-infrastructure`, `data-analytics`, `documentation`, `productivity-utilities`, `domain-vertical` (categorization-schema §Requirement: Domain category vocabulary)
- [x] 1.2 Define structural type vocabulary constant with 5 values: `skill`, `subagent`, `command`, `hook`, `mcp-server` (categorization-schema §Requirement: Structural type is a multi-select keyword subset)
- [x] 1.3 Add `category` field (string, required) to marketplace.json schema — single value, not array (categorization-schema §Requirement: Domain category is a required single-select)
- [x] 1.4 Add `keywords` field (array of strings, optional) to marketplace.json schema — each entry must be non-empty string (categorization-schema §Requirement: Keyword array values are non-empty strings)
- [x] 1.5 Remove deprecated fields `type`, `use-case`, `kind` from marketplace.json schema (categorization-schema §Requirement: marketplace.json schema validation)
- [x] 1.6 Implement domain category validation — reject publish if `category` missing, not string, or not in vocabulary (categorization-schema §Scenario: Plugin with no category rejected, Scenario: Invalid domain category rejected)
- [x] 1.7 Implement keyword validation — reject empty/whitespace-only strings; accept structural vocabulary values silently as recognized; accept unknown values as free-form (categorization-schema §Requirement: Structural keyword values are validated, §Requirement: Free-form keywords have no vocabulary constraint)
- [x] 1.8 Add deprecation errors for `type`, `use-case`, `kind` fields on publish — point to migration guide (categorization-schema §Scenario: Deprecated fields rejected)

## 2. Migration Tooling (category-migration)

- [x] 2.1 Create static mapping table for existing `use-case` values to domain categories (category-migration §Requirement: Static mapping table for existing tags)
- [x] 2.2 Create static mapping table for existing `kind` values to domain categories (category-migration §Requirement: Static mapping table for existing tags)
- [x] 2.3 Create mapping for existing `type` values to structural keywords — move to `keywords` array, not domain category (category-migration §Scenario: Known type tag becomes structural keyword)
- [x] 2.4 Implement fallback rule: ambiguous tags map to nearest domain category; missing/unknown tags default to `productivity-utilities` (category-migration §Requirement: Ambiguous tag fallback to nearest category)
- [x] 2.5 Build migration script that reads each plugin's marketplace.json, applies mapping, writes `category` + `keywords`, removes `type`/`use-case`/`kind` (category-migration §Requirement: Migration script rewrites marketplace.json files)
- [x] 2.6 Implement idempotency check — skip files already migrated (have `category`, no deprecated fields) (category-migration §Requirement: Migration is idempotent)
- [x] 2.7 Generate before/after migration report showing old tags, new category, new keywords for each plugin — write to reviewable file (category-migration §Requirement: Migration produces before/after report)

## 3. Filter API (plugin-tagging-and-filters)

- [x] 3.1 Add new filter params to API: `category` (string), `structural` (array), `keywords` (string) — domain-first hierarchy (plugin-tagging-and-filters §Requirement: Discovery filtering by category)
- [x] 3.2 Implement domain filter: exact match, AND with other filters (plugin-tagging-and-filters §Scenario: Filter by domain category only)
- [x] 3.3 Implement structural filter: OR within selection, AND with domain (plugin-tagging-and-filters §Scenario: Filter by domain + structural)
- [x] 3.4 Implement keyword filter: OR match, AND with domain + structural (plugin-tagging-and-filters §Scenario: Filter by domain + keyword)
- [x] 3.5 Accept deprecated params (`type`, `use-case`, `kind`) but return deprecation header — map internally to new filter logic (plugin-tagging-and-filters §Scenario: Old filter params accepted with deprecation)

## 4. Filter UI (category-filter-ui)

- [x] 4.1 Build domain category sidebar — radio-button single-select list of 11 categories with plugin count per category (category-filter-ui §Requirement: Domain category displayed as primary sidebar filter)
- [x] 4.2 Build structural type checkbox group — multi-select, displayed below domain sidebar (category-filter-ui §Requirement: Structural type displayed as secondary checkbox filter)
- [x] 4.3 Build keyword search text input at top of filter panel (category-filter-ui §Requirement: Keyword search input at top)
- [x] 4.4 Implement domain-first filter combination logic in UI — domain AND + structural OR + keywords OR (category-filter-ui §Requirement: Filter combination uses domain-first hierarchy)
- [x] 4.5 Scope structural checkboxes to current domain selection — hide types with zero plugins in selected domain; show all when no domain selected (category-filter-ui §Requirement: Structural options scoped to current domain)
- [x] 4.6 Add "Clear all" control that resets domain, structural, and keyword filters (category-filter-ui §Requirement: Clear all filters control)
- [x] 4.7 Sync filter state to URL query params — `?category=code-intelligence&structural=skill` — restore on page load (category-filter-ui §Requirement: Deep-link filter state preserved)

## 5. Testing & Verification

- [x] 5.1 Write schema validation unit tests — valid manifest accepted, missing category rejected, invalid category rejected, deprecated fields rejected, empty keywords rejected
- [x] 5.2 Write migration unit tests — known tag mapped correctly, ambiguous tag fallback, missing tags default, idempotent re-run, type → structural keyword
- [x] 5.3 Write filter API unit tests — domain only, domain + structural, domain + keyword, all three combined, deprecated param deprecation header
- [x] 5.4 Write filter UI component tests — domain radio single-select, structural multi-select, keyword search, clear all, URL state sync
- [x] 5.5 Run full test suite and verify all tests pass
