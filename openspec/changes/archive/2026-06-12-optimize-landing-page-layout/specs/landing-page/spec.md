## MODIFIED Requirements

### Requirement: Install-showcase hero

The landing page hero SHALL be built around a designed "How to install a plugin" showcase. The showcase SHALL display the featured plugin and render a copy-ready CLI command to install it. The hero SHALL also present the product headline and primary calls to action (browse, publish). At desktop and common laptop breakpoints, the hero title SHALL remain on one line without hard-coded line breaks. The hero SHALL NOT render a disabled login call to action.

#### Scenario: Hero shows install showcase
- **WHEN** a visitor opens the landing page and a featured plugin is available
- **THEN** the hero SHALL display the featured plugin and the CLI command to install it, with the headline and primary CTAs present

#### Scenario: Install command is the only dark element in the hero
- **WHEN** the install-showcase hero renders
- **THEN** the CLI command SHALL be presented in a dark code block while the surrounding hero surface remains yellow/cream-dominant

#### Scenario: Graceful fallback when no plugin is featured
- **WHEN** the landing page renders and no featured plugin is available (none flagged or the fetch fails)
- **THEN** the showcase SHALL display a generic install command placeholder and SHALL NOT render a broken or error state

#### Scenario: Hero title stays on one line on desktop and laptop
- **WHEN** the landing page renders at desktop or common laptop breakpoints
- **THEN** the hero title SHALL remain on one line without overflow or hard-coded line breaks

#### Scenario: Disabled hero login call to action absent
- **WHEN** the landing page hero renders
- **THEN** no disabled login button or disabled login call to action SHALL be present in the hero

### Requirement: Category-filter discovery entry points

The landing page SHALL surface plugin category tags (the "kind of plugin" axis, e.g. SWE, Product, UX/UI, DevOps) as discovery entry points. Selecting a category SHALL navigate the visitor to the catalog/search with that category filter preselected. Category labels SHALL be sourced from the category vocabulary rather than hardcoded. The landing page SHALL NOT include a duplicate lower search area or duplicate lower search call to action when a browse plugins CTA already provides plugin discovery access.

#### Scenario: Category chips navigate with filter applied
- **WHEN** a visitor selects a category entry point on the landing page
- **THEN** the visitor SHALL be navigated to the catalog/search results filtered to that category

#### Scenario: Category labels are data-driven
- **WHEN** the landing page renders its category entry points
- **THEN** the displayed category labels SHALL come from the category vocabulary returned by the catalog, not from hardcoded strings

#### Scenario: Duplicate lower search area absent
- **WHEN** the landing page renders with a browse plugins CTA available
- **THEN** the page SHALL NOT render a separate lower search area or duplicate search CTA for plugin discovery

## ADDED Requirements

### Requirement: Initial viewport fit and balanced space usage

The landing page SHALL be laid out so primary landing content and the footer are visible within the initial viewport at desktop and common laptop breakpoints. The layout SHALL reduce excessive vertical gaps and use available horizontal side space before removing meaningful content. Footer content SHALL remain semantically preserved except for spacing adjustments required to meet viewport fit.

#### Scenario: Footer visible in initial viewport
- **WHEN** a visitor opens the landing page at desktop or common laptop viewport sizes
- **THEN** the footer SHALL be visible in the initial viewport without requiring excessive vertical scrolling

#### Scenario: Side deadspace reclaimed
- **WHEN** the landing page renders on a wide viewport
- **THEN** supporting landing content SHALL use available horizontal space instead of leaving large empty side regions while stacking duplicate vertical sections

#### Scenario: Footer content preserved during compaction
- **WHEN** layout spacing is adjusted to fit the initial viewport
- **THEN** existing footer meaning and links SHALL remain available
