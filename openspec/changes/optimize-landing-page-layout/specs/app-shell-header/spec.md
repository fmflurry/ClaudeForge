## ADDED Requirements

### Requirement: Header brand and navigation labels

The application header SHALL preserve the existing header logo and the `Documentation` navigation item. The header SHALL present the plugin discovery navigation item with visible label `Plugins` instead of `Catalogue`. The header SHALL NOT present a `Rechercher` navigation item.

#### Scenario: Header logo remains visible
- **WHEN** the application header renders
- **THEN** the existing header logo SHALL remain visible and available in its header position

#### Scenario: Plugins label replaces Catalogue
- **WHEN** the application header navigation renders
- **THEN** the plugin discovery navigation item SHALL be labelled `Plugins` and SHALL NOT be labelled `Catalogue`

#### Scenario: Rechercher navigation item absent
- **WHEN** the application header navigation renders
- **THEN** no `Rechercher` navigation item SHALL be present

#### Scenario: Documentation navigation item remains
- **WHEN** the application header navigation renders
- **THEN** the `Documentation` navigation item SHALL remain visible and available

### Requirement: Dashboard navigation is auth-gated

The application header SHALL render the `Tableau de bord` navigation item only when the current user is logged in. The item SHALL be conditionally rendered from the existing authentication state rather than disabled or visually hidden for anonymous visitors.

#### Scenario: Dashboard visible for logged-in user
- **WHEN** the application header renders for a logged-in user
- **THEN** the `Tableau de bord` navigation item SHALL be visible and available

#### Scenario: Dashboard hidden for anonymous visitor
- **WHEN** the application header renders for an anonymous visitor
- **THEN** the `Tableau de bord` navigation item SHALL NOT be present

#### Scenario: Dashboard waits for auth state
- **WHEN** the application header is resolving the existing authentication state
- **THEN** the `Tableau de bord` navigation item SHALL NOT be exposed as disabled or visually hidden anonymous-only markup
