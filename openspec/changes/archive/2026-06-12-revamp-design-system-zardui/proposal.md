# ZardUI Design System Revamp — Proposal

## Why

The application currently relies on hand-rolled SCSS styling with minimal component composition, resulting in a dated visual experience that does not convey the professional, polished identity the platform should project. Adopting **ZardUI** — a modern Angular-native component library built on Tailwind CSS v4 and signals — provides a cohesive design language, reduces styling maintenance burden, and enables rapid iteration on a fresh, professional aesthetic. The chosen direction adopts a yellow-forward brand identity, with yellow as the dominant color across prominent surfaces, balanced by neutral backgrounds and strong contrast to ensure a polished, professional appearance rather than playful or loud.

## What Changes

- **Styling infrastructure**: Introduction of **Tailwind CSS v4** into the build pipeline to power ZardUI components and tokens. This is a **BREAKING** change to the build pipeline; however, the application's own custom SCSS styling will continue to coexist and remain the primary mechanism for application-level styles. Requires coordination of PostCSS configuration and asset handling to ensure SCSS and Tailwind processes work harmoniously without class-name collisions.
- **Component library**: Replacement of ad-hoc HTML and plain CSS with **ZardUI component primitives**, installed via the shadcn-style CLI (`npx zard-cli init`) — meaning component source code is scaffolded directly into the repository rather than pulled from an npm package.
- **New dependencies**: Introduction of `@angular/cdk`, `class-variance-authority`, `clsx`, `tailwind-merge`, `lucide-angular`, and Tailwind v4 itself into the build and package.json.
- **UI layer migration**: Refactoring of shell and home page components (and all future UI) to compose ZardUI primitives instead of bespoke HTML and SCSS.
- **Design tokens and theming**: Adoption of OKLCH-based CSS variables (`--primary`, `--primary-foreground`, `--background`, `--foreground`, `--card`, `--secondary`, `--destructive`, `--border`, `--input`, `--ring`) for a semantic color system. Both light and dark palettes are in scope, with switching via `.dark` class selector and custom color extensions (e.g., `--color-warning` for yellow).

## Capabilities

### New Capabilities

- **`design-system`**: Establish ZardUI as the component foundation for the application. Covers Tailwind v4 setup, integration of ZardUI component scaffolding, definition of the in-repo component inventory, and the migration path for existing shell and home UI to use ZardUI primitives.
- **`theming`**: Define and implement the semantic color system and design tokens. Specifies the yellow-forward professional palette in OKLCH format (yellow as dominant brand color with neutral balance), the mapping of semantic tokens to light and dark color values, and extensibility for custom colors.

### Modified Capabilities

None. The `openspec/specs/` directory is currently empty, so there are no pre-existing capabilities to modify.

## Impact

### Build and Tooling
- **PostCSS and Tailwind integration**: The build pipeline must be updated to process Tailwind CSS v4 directives. Potential risk of conflicts between SCSS and Tailwind (both processing stylesheets). Requires careful coordination of asset pipelines and class-name collision prevention.
- **New dependencies**: Adding `@angular/cdk`, `class-variance-authority`, `clsx`, `tailwind-merge`, `lucide-angular`, and Tailwind v4 increases the dependency graph. Package size and build time impact must be assessed.

### Version Compatibility
- **Angular 22 / TypeScript 6 alignment**: The application currently targets Angular ^22.0.0 and TypeScript ~6.0.2. ZardUI targets a modern signals + Tailwind v4 stack, but it has not been verified against Angular ^22.0.0 / TypeScript ~6.0.2 specifically. **Compatibility must be validated as the first implementation step** (e.g. a spike installing ZardUI in the project) before committing to the migration — treat this as the primary open risk.

### Code and Source Structure
- **In-repo component ownership**: Unlike traditional npm packages, ZardUI components are scaffolded into the repository. This means:
  - New source files in `src/components/` (or similar structure).
  - Direct control over component variants and customizations.
  - Maintenance responsibility for component code.
- **Shell and home UI refactoring**: Existing HTML and SCSS in these modules will be replaced with ZardUI component composition. This is a breaking change to the visual layer and requires design review and QA.

### i18n and Localization
- The app already uses `@jsverse/transloco` for i18n (recently consolidated). ZardUI components are signal-based and agnostic to i18n frameworks; integration is expected to be straightforward, but any ZardUI-provided labels or slots must be tested with the current translation strategy.

### Browser and CSS Support
- **OKLCH color format support**: Semantic tokens use OKLCH notation (e.g., `oklch(0.84 0.16 84)` for yellow). OKLCH has excellent browser support in modern browsers; verification required for the app's minimum supported browser targets.
- **.dark class selector**: Light/dark mode toggling via CSS class is standard and supported across all modern browsers.

### Migration Timeline
- This is a foundational change; migration of the entire UI layer is expected to be phased (shell and home first, then the rest of the app). Initial setup (Tailwind + ZardUI CLI scaffolding) is high-risk; subsequent component-by-component migration is lower-risk but labor-intensive.

---

**Capabilities created**: 
- `design-system`
- `theming`

**No modified capabilities** (openspec/specs is empty).
