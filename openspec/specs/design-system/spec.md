# design-system Specification

## Purpose

Defines the application's component-level design system: ZardUI primitives vendored in-repo, Tailwind v4 build integration alongside custom SCSS, iconography, keyboard accessibility, Angular/TypeScript compatibility, and the phased migration scope.

## Requirements

### Requirement: ZardUI Component Foundation

ZardUI component primitives (buttons, inputs, cards, dialogs, menus, and other standard UI elements) SHALL be provided by the design system rather than hand-rolled ad-hoc HTML markup and SCSS.

#### Scenario: Application uses ZardUI button instead of bespoke HTML

- **WHEN** a developer needs a button in the application
- **THEN** they import and compose a ZardUI button component (e.g., `<z-button>`) instead of authoring bespoke HTML and SCSS
- **AND** the button renders with full ZardUI styling and behavior

#### Scenario: Standard interactive elements are available

- **WHEN** a screen requires a modal, dropdown menu, or form input
- **THEN** a ZardUI component exists for that element and can be used as the default choice
- **AND** new hand-rolled HTML equivalents are discouraged in code review

---

### Requirement: Tailwind v4 Build Integration

The application build pipeline SHALL process Tailwind v4 CSS directives via PostCSS so that ZardUI components render fully styled.

#### Scenario: Build succeeds with Tailwind processing enabled

- **WHEN** a developer runs the production build
- **THEN** PostCSS processes Tailwind v4 directives in the CSS pipeline
- **AND** the build completes without errors
- **AND** ZardUI components in the rendered output are properly styled with Tailwind utility classes

#### Scenario: Tailwind utilities are available in generated CSS

- **WHEN** a ZardUI component uses Tailwind utility classes (e.g., `bg-primary text-primary-foreground`)
- **THEN** those utilities are resolved and included in the compiled CSS output
- **AND** the component renders with the intended visual appearance

---

### Requirement: SCSS and Tailwind Coexistence

The build pipeline SHALL support the application's custom SCSS alongside Tailwind v4 in the same asset pipeline without breaking either system. Custom application styles SHALL remain authorable in SCSS.

#### Scenario: Custom SCSS and Tailwind both process in the same build

- **WHEN** the application contains both a `src/theme.css` (Tailwind entry) and custom `src/styles.scss`
- **THEN** both files are processed during the build
- **AND** neither system breaks or overwrites the other's output
- **AND** CSS variables from Tailwind tokens are available to SCSS code

#### Scenario: Application styles use CSS variables instead of hardcoded colors

- **WHEN** custom SCSS needs a color
- **THEN** the code references a Tailwind token via `var(--primary)` or similar, not a hardcoded hex value
- **AND** the rendered style honors the token value and respects theme switching

---

### Requirement: In-Repo Component Ownership

ZardUI component source code SHALL be vendored into the repository (team-owned, version-controlled) rather than consumed as an opaque npm package. Updates to ZardUI components SHALL be explicit, reviewable actions performed through the team's standard pull-request workflow.

#### Scenario: ZardUI components are stored in the repository

- **WHEN** developers check out the codebase
- **THEN** all ZardUI component source files are present in `src/app/shared/ui/` (or equivalent team-chosen path)
- **AND** these files are committed to version control
- **AND** component changes appear in git history as reviewable commits

#### Scenario: Updating a ZardUI component requires a pull request

- **WHEN** a developer needs to update or customize a ZardUI component
- **THEN** they create a pull request with the change
- **AND** the change is reviewed and merged before deployment
- **AND** no automatic or silent updates occur from an external package registry

---

### Requirement: Angular and TypeScript Compatibility

The design system SHALL build and render correctly on the project's current Angular and TypeScript versions (Angular 22 / TypeScript 6). Compatibility SHALL be validated before broad migration.

#### Scenario: Compatibility spike confirms ZardUI works on Angular 22 / TypeScript 6

- **WHEN** the compatibility spike executes
- **THEN** ZardUI is installed via the standard setup process
- **AND** the application build completes without TypeScript or esbuild errors
- **AND** a sample ZardUI component (e.g., a button) renders in the running application
- **AND** if the spike fails to demonstrate these outcomes, the migration is halted and escalated

#### Scenario: SSR build succeeds with ZardUI components

- **WHEN** the application performs a server-side render build
- **THEN** ZardUI components are properly integrated in the SSR pipeline
- **AND** the build completes without errors
- **AND** rendered HTML includes the styled ZardUI output

---

### Requirement: Iconography from ZardUI Icon Set

UI icons across the application SHALL come from the ZardUI-provided icon set (lucide-angular) to ensure visual consistency.

#### Scenario: Icons are provided by ZardUI

- **WHEN** a component needs an icon (e.g., a checkbox tick, navigation arrow, or action menu)
- **THEN** the icon is sourced from the lucide-angular icon set provided by ZardUI
- **AND** developers do not author custom SVG icons or use external icon libraries for consistency

#### Scenario: Icon selection is documented

- **WHEN** a developer uses an icon
- **THEN** the icon name is explicit in the component code (e.g., `<lucide-angular name="check">`)
- **AND** the icon renders in the correct style and color inherited from ZardUI styling

---

### Requirement: Keyboard Accessibility and Focus States

Interactive components SHALL be keyboard-operable with visible focus states, meeting WCAG 2.1 AA accessibility standards.

#### Scenario: Interactive components are keyboard-navigable

- **WHEN** a user navigates the application using only the keyboard (Tab key)
- **THEN** all interactive components (buttons, inputs, dialogs, menus) are focusable
- **AND** focus order is logical and follows tab order convention

#### Scenario: Focus state is visually distinct

- **WHEN** a component has keyboard focus
- **THEN** a visible focus indicator (e.g., an outline ring or border) is displayed
- **AND** the focus indicator meets WCAG 2.1 AA contrast and visibility requirements
- **AND** the indicator is not dependent on mouse or hover-only styles

#### Scenario: Dialogs and menus are keyboard-accessible

- **WHEN** a user opens a dialog or menu via keyboard
- **THEN** they can navigate items, confirm selections, and close using keyboard commands (Arrow keys, Enter, Escape)
- **AND** no mouse interaction is required to complete a task

---

### Requirement: Phased Migration Scope

The shell (navigation, header, layout) and the home/landing page SHALL be the first surfaces migrated to ZardUI components. Remaining surfaces are migrated in subsequent phases.

#### Scenario: Shell components use ZardUI primitives

- **WHEN** the shell migration phase completes
- **THEN** the navigation, header, footer, and layout containers are composed of ZardUI components
- **AND** the shell renders consistently across light and dark modes
- **AND** subsequent components on other pages can assume the shell is stable and ZardUI-enabled

#### Scenario: Home page is refactored to ZardUI

- **WHEN** the home page migration phase completes
- **THEN** all major content sections (hero, cards, CTAs, forms) use ZardUI component composition
- **AND** the page's visual design aligns with the yellow-forward design system
- **AND** QA confirms the visual design and functionality match the specification

#### Scenario: Remaining pages are deferred

- **WHEN** the initial phases (shell + home) are complete
- **THEN** other pages in the application are NOT yet required to use ZardUI
- **AND** those pages may continue using legacy styling until explicitly scheduled for migration
- **AND** they can still reference ZardUI semantic tokens via CSS variables for color consistency
