## ADDED Requirements

### Requirement: Yellow-dominant landing surface

The landing page SHALL present a yellow/cream-dominant visual surface from top to bottom. Dark surfaces SHALL be used only as small accents (for example the CLI command code block and the footer) and SHALL NOT dominate any primary content section.

#### Scenario: No oversized dark hero slab
- **WHEN** the landing page renders
- **THEN** there SHALL be no full-bleed near-black hero section; the hero SHALL render on a yellow/cream surface

#### Scenario: Dark used only as accent
- **WHEN** the landing page renders
- **THEN** any dark-surfaced element SHALL be a contained accent (such as the install command block or footer), not a dominant content band

### Requirement: Install-showcase hero

The landing page hero SHALL be built around a designed "How to install a plugin" showcase. The showcase SHALL display the featured plugin and render a copy-ready CLI command to install it. The hero SHALL also present the product headline and primary calls to action (browse, publish).

#### Scenario: Hero shows install showcase
- **WHEN** a visitor opens the landing page and a featured plugin is available
- **THEN** the hero SHALL display the featured plugin and the CLI command to install it, with the headline and primary CTAs present

#### Scenario: Install command is the only dark element in the hero
- **WHEN** the install-showcase hero renders
- **THEN** the CLI command SHALL be presented in a dark code block while the surrounding hero surface remains yellow/cream-dominant

#### Scenario: Graceful fallback when no plugin is featured
- **WHEN** the landing page renders and no featured plugin is available (none flagged or the fetch fails)
- **THEN** the showcase SHALL display a generic install command placeholder and SHALL NOT render a broken or error state

### Requirement: Category-filter discovery entry points

The landing page SHALL surface plugin category tags (the "kind of plugin" axis, e.g. SWE, Product, UX/UI, DevOps) as discovery entry points. Selecting a category SHALL navigate the visitor to the catalog/search with that category filter preselected. Category labels SHALL be sourced from the category vocabulary rather than hardcoded.

#### Scenario: Category chips navigate with filter applied
- **WHEN** a visitor selects a category entry point on the landing page
- **THEN** the visitor SHALL be navigated to the catalog/search results filtered to that category

#### Scenario: Category labels are data-driven
- **WHEN** the landing page renders its category entry points
- **THEN** the displayed category labels SHALL come from the category vocabulary returned by the catalog, not from hardcoded strings
