## ADDED Requirements

### Requirement: Yellow-Forward Brand Palette

The design system color palette SHALL use yellow as the dominant brand color across prominent surfaces, balanced for a professional (not loud or playful) appearance.

#### Scenario: Primary button uses yellow background

- **WHEN** a user sees a primary action button in the application
- **THEN** the button background is yellow (as defined by the `--primary` token)
- **AND** the yellow is vivid enough to be prominent and professional
- **AND** contrasting text/icons on the yellow ensure readability (verified separately under contrast requirements)

#### Scenario: Navigation highlights use yellow accent

- **WHEN** a user navigates to an active page
- **THEN** the active tab or link indicator uses yellow or a yellow-derived accent
- **AND** the yellow does not appear harsh or toy-like in the application context

#### Scenario: Yellow balancing with neutral colors

- **WHEN** the full interface is viewed
- **THEN** yellow prominent surfaces (buttons, accents) are balanced by neutral backgrounds and text
- **AND** the overall appearance is professional and polished, not overwhelmingly bright

---

### Requirement: Semantic Design Tokens

Colors across the design system SHALL be defined as semantic OKLCH CSS variable tokens that serve as the single source of truth for both ZardUI components and custom SCSS.

#### Scenario: Standard semantic tokens are defined

- **WHEN** the theme is initialized
- **THEN** CSS variables exist for: `--primary`, `--primary-foreground`, `--background`, `--foreground`, `--card`, `--secondary`, `--secondary-foreground`, `--muted`, `--muted-foreground`, `--accent`, `--accent-foreground`, `--destructive`, `--border`, `--input`, `--ring`
- **AND** each token is defined in OKLCH format (e.g., `oklch(0.85 0.17 95)`)
- **AND** these tokens appear in the compiled CSS output

#### Scenario: Semantic tokens are referenced by name, not hex values

- **WHEN** a component or SCSS file needs a color
- **THEN** it uses the semantic token name (e.g., `background-color: var(--primary)`) rather than hardcoding a hex or RGB value
- **AND** this indirection allows a single token definition to control appearance globally

#### Scenario: Custom SCSS consumes tokens via CSS variables

- **WHEN** a custom SCSS file (e.g., `src/styles.scss`) applies a color
- **THEN** the code uses `var(--semantic-name)` to reference the token value
- **AND** the compiled CSS resolves the variable at render time
- **AND** changing the token value updates all code that references it

---

### Requirement: Light and Dark Mode Palettes

The design system SHALL provide complete light and dark color palettes. Dark mode SHALL activate via a `.dark` class on the document root.

#### Scenario: Light mode uses light palette values

- **WHEN** the `.dark` class is NOT present on the document root
- **THEN** CSS tokens resolve to their light-mode values
- **AND** the page renders with a light background, dark text, and light-mode ZardUI components
- **AND** all interactive elements are visible and readable in light mode

#### Scenario: Dark mode uses dark palette values

- **WHEN** the `.dark` class IS present on the document root
- **THEN** CSS tokens resolve to their dark-mode values (defined in `.dark {}` CSS rule)
- **AND** the page renders with a dark background, light text, and dark-mode ZardUI components
- **AND** all interactive elements are visible and readable in dark mode

#### Scenario: Token values differ between light and dark modes

- **WHEN** comparing a token like `--primary` between light and dark modes
- **THEN** the light-mode value is distinct from the dark-mode value (both defined in CSS)
- **AND** the difference is intentional to ensure readability and visual balance in each mode

#### Scenario: Entire page palette switches when .dark class is toggled

- **WHEN** the `.dark` class is added or removed from the document root
- **THEN** all text, backgrounds, borders, and other color-dependent properties immediately reflect the new palette
- **AND** no reload or page refresh is required
- **AND** users see a smooth transition between light and dark themes

---

### Requirement: Theme Selection and Persistence

A user SHALL be able to switch between light and dark themes, and the choice SHALL persist across browser reloads and sessions.

#### Scenario: User can toggle theme via UI control

- **WHEN** a user interacts with a theme toggle (e.g., button or dropdown in navigation)
- **THEN** the theme switches between light and dark
- **AND** the `.dark` class is added or removed from the document root
- **AND** the page re-renders with the selected palette

#### Scenario: Theme preference is stored

- **WHEN** a user selects a theme
- **THEN** the choice is persisted (e.g., in localStorage or a cookie)
- **AND** the storage key is reliable and documented

#### Scenario: Stored theme is restored on reload

- **WHEN** a user returns to the application after closing it
- **THEN** the previously selected theme is automatically applied
- **AND** the `.dark` class is added if dark mode was selected
- **AND** no default or flash-of-wrong-theme occurs

#### Scenario: Theme setting is synchronized across tabs

- **WHEN** a user switches themes in one browser tab
- **THEN** the theme is updated in all other open tabs of the application (optional: via storage events)
- **AND** users see consistent theming across all sessions

---

### Requirement: SSR Flash-of-Wrong-Theme Prevention

When the application uses server-side rendering, the chosen theme SHALL be applied before client hydration, preventing a flash of the incorrect theme on initial load.

#### Scenario: Server renders with correct theme class

- **WHEN** a user with a stored dark-mode preference navigates to the application
- **THEN** the server-rendered HTML includes the `.dark` class on the document root
- **AND** the initial HTML payload contains the correct theme styles
- **AND** no client-side script is needed to correct the theme after hydration

#### Scenario: Pre-hydration inline script applies theme

- **WHEN** the application is served with SSR enabled
- **THEN** an inline script in `index.html` runs before Angular bootstrap
- **AND** this script reads the stored theme preference and applies the `.dark` class if needed
- **AND** by the time the page is visible to the user, the correct theme is already applied
- **AND** no flash or visual flicker occurs

#### Scenario: SSR and client hydration agree on theme

- **WHEN** the application hydrates on the client after SSR
- **THEN** the theme applied by the server matches the theme stored on the client
- **AND** the client theme service recognizes the already-applied theme and does not toggle it unnecessarily

---

### Requirement: WCAG 2.1 AA Contrast Compliance

Every foreground and background color token pair SHALL achieve WCAG 2.1 AA contrast. In particular, text and icons on the yellow primary color SHALL use a dark foreground to ensure readability.

#### Scenario: Primary button passes WCAG AA contrast

- **WHEN** the primary button renders with yellow background and dark text
- **THEN** the contrast ratio between text color and yellow background is at least 4.5:1
- **AND** this meets WCAG 2.1 AA standards for normal text
- **AND** contrast is verified using a WCAG-compliant tool (e.g., WebAIM Contrast Checker, Polished, axe)

#### Scenario: All semantic token pairs meet AA contrast

- **WHEN** every color token pair in light mode is tested
- **THEN** the contrast ratio is at least 4.5:1 for text (or 3:1 for large text/UI components)
- **AND** the same threshold applies to dark-mode token pairs
- **AND** a compliance report is documented before the palette is finalized

#### Scenario: Focus ring is visually distinct with adequate contrast

- **WHEN** a component displays a focus ring (e.g., via the `--ring` token)
- **THEN** the ring color contrasts with both the component background and the page background
- **AND** the contrast ratio is sufficient for users with low vision to perceive the focus state
- **AND** this is verified in both light and dark modes

#### Scenario: Destructive action color is readable

- **WHEN** a destructive action button or warning uses the `--destructive` token
- **THEN** the text and/or icon on the destructive background meets WCAG AA contrast
- **AND** users can clearly perceive the action as destructive and can read any label
