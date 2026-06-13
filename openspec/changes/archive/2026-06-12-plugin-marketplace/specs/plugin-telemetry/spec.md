# Plugin Telemetry Specification

## ADDED Requirements

### Requirement: Anonymized Event Ingestion

The telemetry system SHALL accept anonymized plugin events (downloads, installs, usage) without capturing personally identifiable information. Client IDs SHALL be hashed or generated anonymously; no user accounts, emails, or device identifiers are permitted. Events are persisted to PostgreSQL for aggregation.

#### Scenario: Download event recorded with anonymous client ID
**WHEN** a user downloads a plugin via the web UI or CLI
**THEN** the system records an event containing:
- Hashed/anonymous client ID (no PII)
- Plugin name and version
- Download timestamp
- Client OS/architecture (if telemetry is enabled)
**AND** the event is persisted to the telemetry table

#### Scenario: Install event recorded without PII
**WHEN** a user installs a plugin from the CLI or web UI
**THEN** the system records:
- Anonymous client ID
- Plugin name, version, and target installation path (anonymized)
- Install timestamp
**AND** no user name, email, or system hostname is captured

#### Scenario: Malformed event rejected
**WHEN** an event is missing required fields (plugin name, version, anonymous ID)
**THEN** the system rejects the event with HTTP 400
**AND** logs the rejection without storing the incomplete event

---

### Requirement: Privacy-First Anonymization

All client identification SHALL be anonymous. The system SHALL use hashed identifiers (e.g., SHA-256 of a random UUID, not derived from hardware) or browser-local generated IDs. No persistent tracking across browsers is permitted unless the user explicitly opts in and consents.

#### Scenario: Client generates anonymous session ID
**WHEN** a client first interacts with the telemetry system
**THEN** the system generates a new random UUID
**AND** hashes it (SHA-256) to create an anonymous client ID
**AND** stores it in browser localStorage for persistence within the same browser session/profile

#### Scenario: Anonymous ID changes when browser data is cleared
**WHEN** the user clears browser storage (localStorage, cookies)
**THEN** the next telemetry event generates a fresh anonymous ID
**AND** no link exists between the old and new ID

#### Scenario: No cross-site or cross-browser tracking
**WHEN** the same user browses the plugin marketplace from two different browsers
**THEN** each browser receives a different anonymous ID
**AND** the telemetry system cannot correlate events across browsers

---

### Requirement: Opt-Out Mechanism

Users SHALL have a documented, accessible way to disable telemetry collection. When disabled, no events are recorded. The opt-out preference SHALL persist across sessions.

#### Scenario: User disables telemetry via UI
**WHEN** a user toggles the "Disable Telemetry" setting in the marketplace dashboard
**THEN** telemetry collection stops immediately
**AND** the preference is stored in browser localStorage
**AND** subsequent plugin downloads/installs generate no telemetry events

#### Scenario: Opt-out preference persists across sessions
**WHEN** a user returns to the marketplace after closing and reopening the browser
**THEN** the telemetry setting remains disabled
**AND** no events are collected until the user re-enables telemetry

#### Scenario: User re-enables telemetry after opting out
**WHEN** a user toggles "Enable Telemetry" from the disabled state
**THEN** the system resumes event collection with a new anonymous client ID
**AND** no historical data is leaked or correlated

---

### Requirement: Aggregate Metrics Exposure

The system SHALL expose aggregated, read-only telemetry metrics (e.g., total downloads, recent activity) via API endpoints for display in the catalog and dashboard. Aggregation SHALL be performed server-side; individual events are never exposed.

#### Scenario: Download counter retrieved for plugin display
**WHEN** the web UI requests telemetry metrics for a plugin (e.g., GET /api/plugins/{id}/telemetry/summary)
**THEN** the system returns:
- Total downloads (all versions)
- Total installs (all versions)
- Last 7 days activity count
**AND** no individual event details or client IDs are included

#### Scenario: Version-specific metrics aggregated
**WHEN** a user views version history for a plugin
**THEN** each version displays its aggregated download/install count
**AND** counts are calculated server-side from telemetry table
**AND** no individual events are exposed

#### Scenario: Metrics response cached for performance
**WHEN** multiple concurrent requests ask for the same plugin's metrics
**THEN** the system caches aggregated results for 5 minutes (configurable)
**AND** subsequent requests within the cache window return instantly

---

### Requirement: Download/Install Counter (MVP)

The system SHALL track and aggregate the number of plugin downloads and installs per plugin and version. These counters are the core MVP telemetry metric.

#### Scenario: Download counter incremented on successful download
**WHEN** a plugin file is successfully downloaded
**THEN** the download_count for that plugin version is incremented
**AND** an anonymized event is recorded with timestamp

#### Scenario: Install counter incremented via CLI
**WHEN** a CLI install command succeeds (npm install, etc.)
**THEN** an install event is recorded asynchronously (fire-and-forget)
**AND** the install_count for that version is incremented in the aggregate table

#### Scenario: Counters separate by version
**WHEN** multiple versions of the same plugin exist
**THEN** each version has independent download and install counters
**AND** plugin-level totals are calculated as sum of all version counts

---

### Requirement: Usage Frequency & Efficiency Metrics (Phase 2 Enhancement)

The system SHALL support collection and aggregation of usage frequency (how often a plugin is invoked) and efficiency metrics (execution time, success rate) in Phase 2. These are marked as enhancements beyond MVP and require additional schema/infrastructure planning.

#### Scenario: Usage frequency event structure defined
**WHEN** Phase 2 implementation begins
**THEN** the event schema includes fields for:
- Plugin execution start/end timestamps
- Execution duration (milliseconds)
- Success/failure status
- Input data size (anonymized)
**AND** no user output or plugin-specific data is captured

#### Scenario: Efficiency metrics aggregated and visualized
**WHEN** a plugin author views analytics for their plugin (Phase 2)
**THEN** they see:
- Average execution duration (milliseconds)
- Success rate (%)
- Invocation frequency (calls/day, week, month)
**AND** metrics are aggregated across all anonymous clients
**AND** no individual invocation details are exposed

#### Scenario: Efficiency metrics enhance discovery
**WHEN** users browse plugins in the marketplace (Phase 2)
**THEN** they see "average execution time" and "success rate" badges
**AND** filters allow "fast plugins only" or "most reliable plugins"
**AND** these metrics influence semantic search ranking

---

## CONSTRAINTS

- No PII (personally identifiable information) captured or inferred.
- Opt-out mechanism is always available and respected.
- Anonymous client IDs are generated client-side or hashed server-side; no persistent identification across browsers.
- Aggregation is server-side only; individual events never exposed to end users.
- Download/install counters are MVP; usage/efficiency metrics are Phase 2 enhancements.
