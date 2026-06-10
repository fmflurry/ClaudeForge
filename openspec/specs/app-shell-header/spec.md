# app-shell-header Specification

## Purpose
TBD - created by archiving change revamp-landing-page. Update Purpose after archive.
## Requirements
### Requirement: Header secondary surface color

The application header SHALL render on a distinct non-yellow secondary surface (a blue/violet field) so the yellow brand identity reads as primary against it. The header surface, its foreground text, borders, and focus indicators SHALL meet WCAG 2.1 AA contrast (text ≥ 4.5:1, non-text UI/focus indicators ≥ 3:1) in both light and dark modes.

#### Scenario: Header uses a non-yellow secondary surface
- **WHEN** the application shell renders in either light or dark mode
- **THEN** the header background SHALL be a blue/violet secondary surface, not the primary yellow and not the page content background

#### Scenario: Header text and controls remain legible
- **WHEN** the header is displayed on its secondary surface
- **THEN** navigation links, the login control, and the focus ring SHALL meet WCAG 2.1 AA contrast against that surface in both light and dark modes

### Requirement: Header control ordering

The header SHALL place the language switcher and the dark/light theme toggle immediately before the login (authentication) control, as a single right-aligned cluster.

#### Scenario: Language and theme precede login
- **WHEN** the header renders for an unauthenticated visitor
- **THEN** the language switcher and theme toggle SHALL appear immediately to the left of the login button, in the order language → theme → login

#### Scenario: Authenticated user controls ordering
- **WHEN** the header renders for an authenticated user
- **THEN** the language switcher and theme toggle SHALL appear immediately to the left of the account/sign-out control

### Requirement: No team selection in the header

The header SHALL NOT present any team switcher, team selector, or team-related control. The application SHALL NOT present a team onboarding/welcome overlay on load.

#### Scenario: Team switcher absent
- **WHEN** the application shell renders
- **THEN** no team switcher or team selection control SHALL be present in the header

#### Scenario: No team welcome overlay
- **WHEN** the application loads for any visitor
- **THEN** no team selection / team naming welcome overlay SHALL be shown

### Requirement: Organization controls preserved

The header SHALL continue to present the organization switcher; removing team functionality SHALL NOT remove or alter organization functionality.

#### Scenario: Org switcher still present
- **WHEN** the application shell renders
- **THEN** the organization switcher SHALL remain available in the header

