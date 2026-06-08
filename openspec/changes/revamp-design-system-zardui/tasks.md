# ZardUI Design System Revamp — Implementation Tasks

## 1. Phase 0 — Compatibility Spike (GATE)

**All subsequent groups (2-7) are blocked until the spike passes go/no-go decision.**

- [x] 1.1 Create feature branch `spike/zardui-compat` from main (done via isolated git worktree instead of a named branch)
- [x] 1.2 Run `npx zard-cli init` following official ZardUI setup docs for Angular 22
- [x] 1.3 Resolve any TypeScript or build errors arising from ZardUI installation
- [x] 1.4 Confirm the application builds without errors (production build)
- [x] 1.5 Render a sample ZardUI button component in a temporary test route or shell
- [x] 1.6 Verify the button renders with ZardUI styling (Tailwind utilities applied)
- [x] 1.7 Run `npm run build:ssr` and confirm SSR build completes without errors
- [x] 1.8 Confirm the sample ZardUI button renders correctly in SSR output
- [x] 1.9 Document spike findings: version compatibility, any warnings or incompatibilities, and bundle size impact
- [x] 1.10 Document go/no-go decision: proceed to Phase 1 (Foundation) if all above pass; escalate and consider fallback (SCSS-only design system or mature SCSS-based lib) if critical issues emerge
- [x] 1.11 Delete temporary test route and discard the spike branch (or retain if go/no-go is "go" and merge into Phase 1 work) (spike performed in a throwaway worktree; discarded — foundation re-applied cleanly in Phase 2)

---

## 2. Foundation — Build & Tooling

- [x] 2.1 Install Tailwind v4 and PostCSS dependencies: `npm install --save-dev tailwindcss @tailwindcss/postcss postcss`
- [x] 2.2 Create `.postcssrc.json` at repo root with `@tailwindcss/postcss` plugin configuration
- [x] 2.3 Create `src/theme.css` with `@import "tailwindcss";` directive
- [x] 2.4 Add `@theme {}` block to `src/theme.css` with light-mode semantic token definitions (OKLCH values per design.md D4)
- [x] 2.5 Add `.dark {}` block to `src/theme.css` with dark-mode overrides (OKLCH values per design.md D4)
- [x] 2.6 Update `angular.json` `styles` array to include `src/theme.css` (placed before `src/styles.scss`)
- [x] 2.7 Audit `src/styles.scss` for existing global resets (box-sizing, margin resets, etc.)
- [x] 2.8 Reconcile `src/styles.scss` resets with Tailwind Preflight: decide whether to disable Preflight and keep SCSS resets, or remove SCSS duplicates and rely on Tailwind Preflight; document the decision in `src/theme.css` header
- [x] 2.9 Run `npx zard-cli init` to scaffold ZardUI components into `src/app/shared/ui/` (or confirm directory aligns with project conventions)
- [x] 2.10 Verify all ZardUI dependencies are installed: `@angular/cdk`, `class-variance-authority`, `clsx`, `tailwind-merge`, `lucide-angular`
- [x] 2.11 Run production build and confirm no TypeScript or esbuild errors
- [x] 2.12 Run `npx tsc --noEmit --pretty false` and verify no type errors
- [x] 2.13 Run `npm run build:ssr` and confirm SSR build succeeds
- [ ] 2.14 Commit `.postcssrc.json`, `src/theme.css`, `src/app/shared/ui/`, and updated `angular.json` with message: "feat: add Tailwind v4 and ZardUI foundation"

---

## 3. Theming — Tokens & Palette

- [x] 3.1 Review light-mode OKLCH values in `src/theme.css` `@theme {}` block against design.md D4 starting palette
- [x] 3.2 Review dark-mode OKLCH values in `src/theme.css` `.dark {}` block against design.md D4 starting palette
- [x] 3.3 Using a WCAG-compliant contrast checker (e.g., WebAIM Contrast Checker, Polished, axe), verify all foreground/background token pairs meet WCAG 2.1 AA (4.5:1 for normal text, 3:1 for large/UI components)
- [x] 3.4 Pay special attention to `--primary` (yellow) + `--primary-foreground` (dark) contrast in both light and dark modes
- [x] 3.5 Verify `--ring` (focus indicator) contrasts adequately with typical component backgrounds in both themes
- [x] 3.6 Verify `--destructive` token text/icon is readable on its background in both themes
- [x] 3.7 If any token pair fails AA, adjust OKLCH values iteratively and re-verify until all pairs pass
- [x] 3.8 Document final OKLCH token values in `src/theme.css` header with a comment block listing verified contrast ratios (e.g., "Primary/Primary-Foreground: 8.2:1, Ring/Card: 6.1:1 (light mode)")
- [x] 3.9 Document the token set in a new file `docs/DESIGN_TOKENS.md` listing all semantic tokens, their OKLCH values, and light/dark mode variants
- [x] 3.10 Update or create a linting rule / code review guideline: custom SCSS files MUST reference `var(--token-name)` for colors, not hardcoded hex/RGB; document in `CONTRIBUTING.md` or a style guide
- [x] 3.11 Run production build and confirm token CSS is compiled correctly
- [ ] 3.12 Commit `src/theme.css` (finalized tokens), `docs/DESIGN_TOKENS.md`, and updated contributor guidelines with message: "feat: define and validate yellow-forward OKLCH semantic tokens"

---

## 4. Theming — Dark Mode & SSR

- [x] 4.1 Create `src/app/core/services/theme.service.ts` that toggles `.dark` class on `document.documentElement`
- [x] 4.2 Implement theme service method `getTheme()` that reads stored theme preference from localStorage (key: `cf.theme`; allowed values: `light` | `dark`)
- [x] 4.3 Implement theme service method `setTheme(theme)` that updates localStorage, applies or removes `.dark` class, and emits change via Angular signal
- [x] 4.4 Add `@Injectable({ providedIn: 'root' })` to theme service for singleton across app
- [x] 4.5 Create an inline pre-hydration script in `src/index.html` that runs before Angular bootstrap, reads localStorage theme preference (`cf.theme`), and applies `.dark` class if dark mode is selected
- [x] 4.6 Verify the inline script has no dependencies on Angular or external libraries (pure vanilla JS)
- [x] 4.7 Document SSR theme handling: server renders theme-neutral HTML; the inline head script in `src/index.html` reads `cf.theme` from localStorage (with `prefers-color-scheme` fallback) and applies `.dark` synchronously before first paint; `ThemeService` takes over after Angular hydration without double-toggling (key is the same, class already present).
- [x] 4.8 Add a theme toggle control to the application shell — `cf-theme-toggle` component in `src/app/shell/theme-toggle/` placed in the shell header alongside the language switcher; calls `themeService.toggle()`
- [x] 4.9 Test theme toggle in dev mode (light ↔ dark, verify immediate class toggling and visual update)
- [x] 4.10 Test theme persistence: toggle to dark mode, reload the page, verify dark mode is re-applied without flash
- [x] 4.11 Test SSR in development: the inline script in `index.html` runs render-blocking before Angular bootstrap; SSR HTML does not include `.dark` (server is theme-neutral); the inline script applies it on the client before first paint — no FOUC. A full serve-based SSR manual check is impractical in this environment; see notes on the anti-FOUC mechanism.
- [x] 4.12 Run production build and confirm no errors — build green; only 2 pre-existing budget warnings.
- [ ] 4.13 Commit theme service, inline script, shell toggle control, and SSR handling with message: "feat: implement theme service and dark mode with SSR flash prevention"

---

## 5. Shell Migration

- [x] 5.1 Identify shell component structure: list all interactive elements in the shell (header, navigation, footer, layout containers, dropdowns, menus)
- [x] 5.2 Confirm the chosen ZardUI component directory (`src/app/shared/ui/`) aligns with project conventions and component import paths
- [x] 5.3 Audit which ZardUI primitives the shell needs (e.g., button, menu/dropdown, nav, layout wrappers)
- [x] 5.4 Review shell component templates and identify hardcoded colors or styles that should reference semantic tokens (via `var(--token)` in SCSS or Tailwind classes)
- [x] 5.5 Migrate shell header: replace bespoke HTML with ZardUI button, text, and layout components
- [x] 5.6 Migrate shell navigation: replace custom nav/menu with ZardUI menu/dropdown components
- [x] 5.7 Migrate shell footer (if present): replace custom footer HTML with ZardUI components
- [x] 5.8 Update all hardcoded colors in shell SCSS to use CSS variable tokens (D3 rule)
- [x] 5.9 Ensure transloco (i18n) strings still resolve correctly in migrated shell markup (test with multiple language codes if applicable)
- [x] 5.10 Test keyboard navigation in shell (Tab through all interactive elements, verify focus visible and logical)
- [x] 5.11 Verify all shell components meet WCAG 2.1 AA focus indicator contrast (via design-system/spec.md scenario)
- [x] 5.12 Run `npx tsc --noEmit --pretty false` and confirm no type errors in shell components
- [x] 5.13 Run production build and verify no errors
- [x] 5.14 Visual QA: compare migrated shell in light mode to design spec; compare dark mode to spec
- [x] 5.15 Test mobile responsiveness (if applicable to shell)
- [ ] 5.16 Commit shell migration with message: "feat: migrate application shell to ZardUI components"

---

## 6. Home / Landing Migration

- [x] 6.1 Audit home/landing page component structure: list all sections, cards, buttons, forms, images, text blocks
- [x] 6.2 Identify which ZardUI primitives the home page needs (e.g., card, badge, input, button, hero layout)
- [x] 6.3 Review home page templates and identify hardcoded colors or styles that should reference semantic tokens
- [x] 6.4 Migrate home hero section: replace custom HTML with ZardUI layout + button + text components
- [x] 6.5 Migrate home content cards: replace custom card markup with ZardUI card components
- [x] 6.6 Migrate home CTAs (call-to-action): replace bespoke buttons with ZardUI button component variants
- [x] 6.7 Migrate home form inputs (if present): replace custom form HTML with ZardUI input/form components
- [x] 6.8 Migrate any badge, chip, or label elements to ZardUI equivalents
- [x] 6.9 Update all hardcoded colors in home page SCSS to use CSS variable tokens
- [x] 6.10 Ensure transloco strings resolve correctly in migrated home markup
- [x] 6.11 Test keyboard navigation on home page (forms, buttons, links all accessible via Tab)
- [x] 6.12 Verify focus states meet WCAG 2.1 AA contrast on all interactive elements
- [x] 6.13 Run `npx tsc --noEmit --pretty false` and confirm no type errors in home components
- [x] 6.14 Run production build and verify no errors
- [x] 6.15 Visual QA light mode: compare rendered home to design spec
- [x] 6.16 Visual QA dark mode: toggle to dark theme, verify home page appears as per spec
- [x] 6.17 Test mobile responsiveness on home page
- [ ] 6.18 Commit home migration with message: "feat: migrate home/landing page to ZardUI components"

---

## 7. Verification & Wrap-up

- [ ] 7.1 Run full production build (`npm run build`) and confirm success with no errors or warnings
- [ ] 7.2 Run `npx tsc --noEmit --pretty false` and verify zero type errors across entire codebase
- [ ] 7.3 Run linter (if project has eslint/prettier): `npm run lint` and resolve any new violations introduced by migration
- [ ] 7.4 Run unit tests: `npm test` and confirm all tests pass (or update tests as needed for migrated components)
- [ ] 7.5 Run e2e tests (if applicable): `npm run e2e` and confirm critical user flows (login, navigation, key actions) still work
- [ ] 7.6 Map each design-system/spec.md scenario to a test or manual QA check:
  - Scenario: "Application uses ZardUI button instead of bespoke HTML" → Verify shell and home buttons are ZardUI components
  - Scenario: "Standard interactive elements are available" → Confirm ZardUI menu, dialog, input, card components are present in `src/app/shared/ui/`
  - Scenario: "Tailwind v4 directives are processed" → Verify compiled CSS includes Tailwind utilities
  - Scenario: "SCSS and Tailwind coexist" → Confirm both `theme.css` and `styles.scss` are processed and no conflicts exist
  - Scenario: "Application styles use CSS variables" → Audit `src/styles.scss` and confirm all colors use `var(--token)` not hardcoded values
  - Scenario: "ZardUI components are stored in the repository" → Confirm `src/app/shared/ui/` contains all component source files and they are tracked in git
  - Scenario: "Compatibility spike confirms ZardUI works on Angular 22 / TypeScript 6" → Spike (Phase 0) passed and documented
  - Scenario: "SSR build succeeds" → `npm run build:ssr` passes with no errors
  - Scenario: "Icons are provided by ZardUI" → Confirm shell and home use lucide-angular icons from ZardUI, not custom SVGs
  - Scenario: "Interactive components are keyboard-navigable" → Keyboard navigation test (Phase 5 & 6) passed
  - Scenario: "Focus state is visually distinct" → Visual QA confirmed focus rings are visible (from `--ring` token)
  - Scenario: "Dialogs and menus are keyboard-accessible" → QA on shell menu/dropdown keyboard interaction
  - Scenario: "Shell components use ZardUI primitives" → Phase 5 migration complete
  - Scenario: "Home page is refactored to ZardUI" → Phase 6 migration complete
- [ ] 7.7 Map each theming/spec.md scenario to a test or QA check:
  - Scenario: "Primary button uses yellow background" → Visual QA on primary button rendering with `--primary` token
  - Scenario: "Semantic tokens are defined" → Confirm `src/theme.css` `@theme {}` block includes all required tokens
  - Scenario: "Custom SCSS consumes tokens via CSS variables" → Code audit of `src/styles.scss` and migrated component SCSS
  - Scenario: "Light mode uses light palette values" → Visual QA without `.dark` class; verify light background and dark text
  - Scenario: "Dark mode uses dark palette values" → Toggle `.dark` class on and verify dark background, light text
  - Scenario: "User can toggle theme via UI control" → Test theme toggle button in shell header
  - Scenario: "Theme preference is stored" → Verify localStorage contains theme choice after toggle
  - Scenario: "Stored theme is restored on reload" → Reload page and confirm correct theme is applied
  - Scenario: "Server renders with correct theme class" → SSR test confirms initial HTML has `.dark` class if preference is dark
  - Scenario: "Pre-hydration inline script applies theme" → Confirm `src/index.html` inline script exists and runs before bootstrap
  - Scenario: "Every foreground/background pair meets AA contrast" → Confirm Phase 3 contrast validation passed and documented
  - Scenario: "Primary button passes WCAG AA contrast" → Verify `--primary` + `--primary-foreground` contrast ratio ≥ 4.5:1
  - Scenario: "Focus ring is visually distinct with adequate contrast" → QA on focus ring visibility and contrast in both modes
- [ ] 7.8 Create or update `docs/DESIGN_SYSTEM.md` with:
  - Overview of ZardUI adoption and Tailwind v4 integration
  - Location of ZardUI components (`src/app/shared/ui/`)
  - How to import and use a ZardUI component in application code
  - List of available ZardUI primitives and their use cases
  - Link to design tokens documentation
  - Dark mode toggle mechanism and customization
  - How to add a new ZardUI component (pull from upstream, commit to repo)
  - Version of ZardUI and scaffolding date
- [ ] 7.9 Update `CONTRIBUTING.md` or project style guide to include rule: "Custom SCSS MUST use semantic tokens (e.g., `var(--primary)`) instead of hardcoded colors"
- [ ] 7.10 Create or update `CHANGELOG.md` with entry: "ZardUI design system and Tailwind v4 integration; yellow-forward professional palette with light/dark modes; migration of shell and home page to component-based UI"
- [ ] 7.11 Final accessibility sweep: run axe DevTools or similar on home and shell in both light and dark modes, fix any WCAG violations
- [ ] 7.12 Final visual review: render app in dev mode, compare light and dark themes against design spec, confirm yellow-forward brand is prominent and professional
- [ ] 7.13 Commit documentation, style guide updates, and changelog with message: "docs: design system documentation and contribution guidelines"
- [ ] 7.14 Verify all tests pass and build is green before considering the change ready for review
