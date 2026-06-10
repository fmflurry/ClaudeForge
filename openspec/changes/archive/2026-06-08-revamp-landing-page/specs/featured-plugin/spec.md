## ADDED Requirements

### Requirement: Featured plugin flag

The system SHALL allow exactly one plugin to be marked as the "featured" plugin at any time. The featured state SHALL be persisted in the database, and the single-featured invariant SHALL be enforced at the data layer (not only in application code).

#### Scenario: At most one featured plugin
- **WHEN** a plugin is marked as featured while another plugin is already featured
- **THEN** the data layer SHALL prevent two plugins from being featured simultaneously

#### Scenario: Rotating the featured plugin
- **WHEN** an administrator changes which plugin is featured
- **THEN** the previously featured plugin SHALL no longer be featured and the newly selected plugin SHALL become the single featured plugin

#### Scenario: No plugin featured is valid
- **WHEN** no plugin is flagged as featured
- **THEN** the system SHALL remain in a valid state with zero featured plugins

### Requirement: Featured plugin read API

The system SHALL expose a read endpoint that returns the currently featured plugin, including at least its name and slug and its latest version. When no plugin is featured, the endpoint SHALL indicate the absence rather than returning an arbitrary plugin.

#### Scenario: Returns the featured plugin
- **WHEN** a client requests the featured plugin and one is flagged
- **THEN** the endpoint SHALL return that plugin's identifying details (name, slug, latest version)

#### Scenario: Indicates absence when none featured
- **WHEN** a client requests the featured plugin and none is flagged
- **THEN** the endpoint SHALL signal that there is no featured plugin (e.g. 404 or an empty result), and SHALL NOT return a non-featured plugin

### Requirement: Featured plugin install command exposure

The featured plugin's details SHALL be sufficient for a client to compose the canonical CLI install command for that plugin without additional lookups.

#### Scenario: Client composes install command
- **WHEN** a client receives the featured plugin from the read API
- **THEN** the response SHALL include the identifier needed to render the CLI install command for that plugin
