# Spec: landing-page

## MODIFIED Requirements

### Requirement: Footer positioning
The landing page footer SHALL be positioned at the bottom of the viewport and remain visible during scrolling.

#### Scenario: Footer stays fixed at bottom
- **WHEN** user scrolls the landing page
- **THEN** footer remains fixed at viewport bottom

#### Scenario: Footer spans full width
- **WHEN** page is rendered
- **THEN** footer width equals viewport width

### Requirement: How-it-works section styling
The "Comment ça marche" (How it works) section SHALL have a yellow-ish background color with proper spacing.

#### Scenario: Section has yellow background
- **WHEN** page is rendered
- **THEN** how-it-works section background color is var(--secondary)

#### Scenario: Section has padding
- **WHEN** page is rendered
- **THEN** how-it-works section has padding of at least 1rem

#### Scenario: Section has rounded corners
- **WHEN** page is rendered
- **THEN** how-it-works section has border-radius of 0.5rem

## ADDED Requirements

### Requirement: Content does not overlap with footer
The main content area SHALL have sufficient bottom padding to prevent overlap with the fixed footer.

#### Scenario: No content hidden behind footer
- **WHEN** page is scrolled to bottom
- **THEN** all content remains visible above footer
