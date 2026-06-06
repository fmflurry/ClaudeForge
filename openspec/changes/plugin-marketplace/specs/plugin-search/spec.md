# Plugin Search Capability Specification

## ADDED Requirements

### Requirement: Full-Text Search by Plugin Name

The system SHALL provide a full-text search endpoint that accepts a query string and returns all plugins whose names match the query in a case-insensitive manner.

#### Scenario: Search returns plugins with matching name

**WHEN** a user searches for plugins with name "auth"
**THEN** the system returns all plugins containing "auth" in their name (e.g., "AuthHelper", "oauth-validator")
**AND** results are sorted by relevance (exact match first, prefix match second, partial match last)
**AND** each result includes plugin name, description, version, and download count

#### Scenario: Search is case-insensitive

**WHEN** a user searches for "AUTH", "auth", or "Auth"
**THEN** the system returns identical results regardless of case

#### Scenario: Search returns empty gracefully

**WHEN** a user searches for a query with no matching plugins
**THEN** the system returns an empty result set with HTTP 200 and message "No plugins found matching your search"
**AND** result includes suggested categories or popular plugins to explore instead

### Requirement: Full-Text Search by Description and Metadata

The system SHALL search plugin descriptions and metadata fields (keywords, tags) in addition to names.

#### Scenario: Search finds plugins by description keyword

**WHEN** a user searches for "machine learning"
**THEN** the system returns plugins that contain "machine learning" in their description or keywords
**AND** the plugin name may not contain the phrase but still matches if description does

#### Scenario: Search combines name and description matches

**WHEN** a user searches for "testing"
**THEN** the system returns plugins with "testing" in name first
**AND** followed by plugins with "testing" only in description or keywords
**AND** each result displays which field matched (name, description, keywords)

### Requirement: Pagination of Search Results

The system SHALL support paginated search results to handle large result sets efficiently.

#### Scenario: Search returns paginated results

**WHEN** a user searches with pagination parameters (page=1, limit=20)
**THEN** the system returns 20 results on page 1
**AND** the response includes totalCount, currentPage, pageSize, and hasNextPage
**AND** requesting page 2 returns the next 20 results

#### Scenario: Invalid pagination parameters are handled

**WHEN** a user requests page=0 or limit=0
**THEN** the system returns HTTP 400 with error message "Page and limit must be greater than 0"
**AND** defaults are applied: page defaults to 1, limit defaults to 20

#### Scenario: Requesting out-of-range page returns empty

**WHEN** a user requests a page beyond the available results (e.g., page=999 when only 3 pages exist)
**THEN** the system returns HTTP 200 with empty results array
**AND** totalCount and pageSize remain accurate

### Requirement: Filter Search Results by Plugin Type

The system SHALL allow filtering search results by plugin type (skill, hook, plugin, command, agent).

#### Scenario: Filter narrows results by type

**WHEN** a user searches for "auth" AND filters by type="skill"
**THEN** the system returns only plugins with type "skill" that match "auth"
**AND** plugins of other types are excluded even if they match the query

#### Scenario: Multiple type filters are combined with OR logic

**WHEN** a user filters by type="skill" AND type="hook"
**THEN** the system returns plugins matching the query that are either skill OR hook type
**AND** plugins of other types are excluded

### Requirement: Filter Search Results by Programming Language

The system SHALL allow filtering search results by programming language.

#### Scenario: Filter returns plugins for specific language

**WHEN** a user searches for "validation" AND filters by language="Python"
**THEN** the system returns only plugins compatible with Python that match "validation"
**AND** plugins for other languages are excluded

#### Scenario: Language filter combined with type filter

**WHEN** a user filters by language="TypeScript" AND type="command"
**THEN** the system returns plugins that are both TypeScript AND of type "command"
**AND** all other combinations are excluded

### Requirement: Optional Semantic Search via Qdrant (Phase 2 Enhancement)

When semantic search is enabled, the system SHALL provide semantic search using Qdrant vector embeddings. When Qdrant is unavailable or disabled, the system SHALL fall back to full-text search without error.

#### Scenario: Semantic search finds conceptually similar plugins

**WHEN** semantic search is enabled AND a user searches for "convert image to text"
**THEN** the system returns plugins that provide optical character recognition or similar functionality
**AND** even if exact keywords do not appear, conceptually related plugins are ranked high
**AND** traditional full-text results are also included and ranked alongside semantic results

#### Scenario: Semantic search gracefully degrades when Qdrant is unavailable

**WHEN** Qdrant service is down or disabled
**THEN** the system falls back to full-text search without error
**AND** users see no degradation in search quality
**AND** backend logs the fallback event for monitoring

### Requirement: Search Result Ranking and Relevance

The system SHALL rank search results by relevance, considering multiple signals.

#### Scenario: Exact name matches rank first

**WHEN** a user searches for "Logger"
**THEN** plugins with exact name "Logger" appear before "LoggerHelper" or plugins with "logger" in description only

#### Scenario: Download count factors into ranking

**WHEN** multiple plugins match a query equally well in name/description
**THEN** the plugin with more downloads is ranked higher (popularity as a tiebreaker)

#### Scenario: Recent versions are prioritized

**WHEN** two plugins match equally well
**THEN** the plugin with a more recent version update is ranked higher than an older plugin

