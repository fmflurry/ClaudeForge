# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **Design System Overhaul**: Introduced ZardUI design system with Tailwind v4 integration, replacing bespoke HTML/CSS with vendored, reusable component primitives.
- **Semantic Color Tokens**: Implemented OKLCH-based semantic design tokens (`--primary`, `--background`, `--destructive`, etc.) as the single source of truth for all colors. All token pairs validated for WCAG 2.1 AA contrast compliance.
- **Yellow-Forward Professional Palette**: Brand color palette centered on a vivid, professional yellow (`--primary`) balanced by neutral backgrounds and text. Separate light and dark mode palettes with all tokens re-validated for readability in each mode.
- **Dark Mode with SSR Flash Prevention**: Implemented signals-based theme service with persistent localStorage storage (key: `cf.theme`). Pre-hydration inline script in `index.html` applies `.dark` class before Angular bootstrap, preventing flash of wrong theme on SSR. Theme toggle control in application shell.
- **ZardUI Component Library**: Vendored ZardUI components in `src/app/shared/components/`:
  - Button component with variants (primary, secondary, destructive, outline, ghost, link)
  - Card component for content grouping
  - Badge component for labels and status indicators
  - Documented workflow for adding future components
- **Application Shell Migration**: Refactored application shell (header, navigation, footer) to use ZardUI button components and semantic tokens. All hardcoded colors replaced with CSS variable references.
- **Home/Landing Page Migration**: Refactored home/landing page (hero section, content cards, CTAs, labels) to use ZardUI components (button, card, badge) and semantic tokens.
- **Design System Documentation**: Created `frontend/docs/DESIGN_SYSTEM.md` with component inventory, usage patterns, theming guidelines, dark mode mechanism, and SSR configuration.
- **Token Reference Documentation**: Created `frontend/docs/DESIGN_TOKENS.md` with complete list of semantic tokens, OKLCH values, light/dark variants, and validated WCAG AA contrast ratios.
- **Frontend Styling Rule**: Updated `CONTRIBUTING.md` to require all custom SCSS to use semantic tokens (`var(--token-name)`) instead of hardcoded colors. Rule enforced in code review.

### Changed

- **Build Pipeline**: Updated `angular.json` to process `src/theme.css` (Tailwind entry) before `src/styles.scss` (custom SCSS). Integrated PostCSS plugin `@tailwindcss/postcss` via `.postcssrc.json`.
- **Dependencies**: Added `tailwindcss`, `@tailwindcss/postcss`, `@angular/cdk`, `class-variance-authority`, `clsx`, `tailwind-merge`, `lucide-angular`.
- **TypeScript Configuration**: Added `"ignoreDeprecations": "6.0"` in `tsconfig.json` to suppress TypeScript 6 deprecation warnings.

### Technical Notes

- **Node Requirement**: Node.js >= 22.22.3 (required by Angular 22)
- **Build Status**: Production build succeeds with 2 pre-existing bundle budget warnings (initial bundle ~725 kB > 500 kB; landing-page component styles ~4.87 kB > 4 kB).
- **Test Coverage**: 2057/2057 unit tests pass (82 files, 82% coverage).
- **Theming Model**: Tailwind directives and semantic tokens coexist; custom SCSS consumes tokens via CSS variables. Dark mode switches via `.dark` class on document root; all color tokens automatically resolve to dark-mode values.
- **Icons**: All icons sourced from `@ng-icons/lucide` (ZardUI icon set) for visual consistency.

---

## Notes

- Visit `frontend/docs/DESIGN_SYSTEM.md` for complete design system guide.
- See `frontend/docs/DESIGN_TOKENS.md` for token definitions and contrast validation.
- Remaining application pages defer ZardUI migration to future phases; they may use legacy styling but can reference tokens for color consistency.
