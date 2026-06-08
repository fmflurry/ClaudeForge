# ClaudeForge Design System

This document describes the ClaudeForge design system, which combines **ZardUI components** with a **Tailwind v4** build pipeline and a **yellow-forward professional palette** with full light/dark mode support.

## Overview

### What Is ZardUI?

ZardUI is a component library providing pre-built, accessible UI primitives (buttons, cards, dialogs, inputs, and more) designed for Angular applications. Rather than hand-rolling bespoke HTML and styles, we use ZardUI components to ensure consistency, accessibility, and rapid composition.

### Tailwind v4 Integration

The design system uses **@tailwindcss/postcss** to process Tailwind v4 directives at build time. This provides:

- **Utility-based styling**: Tailwind utility classes (e.g., `flex`, `gap-4`, `bg-primary`) apply default spacing, sizing, and layout without extra CSS files.
- **Semantic CSS variables**: OKLCH token definitions (e.g., `--primary`, `--destructive`) centralize color decisions.
- **PostCSS pipeline**: `frontend/.postcssrc.json` configures the Tailwind PostCSS plugin; both `src/theme.css` (Tailwind entry) and `src/styles.scss` (custom SCSS) are processed in order without conflicts.

### SCSS Coexistence

The project retains `src/styles.scss` for custom application styles. Both files coexist:

- **`src/theme.css`** (Tailwind + tokens entry point)
  - Imports `@import "tailwindcss";` to enable Tailwind directives
  - Contains `@theme {}` block with light-mode semantic tokens
  - Contains `.dark {}` block with dark-mode overrides
  - Registered first in `angular.json` styles array

- **`src/styles.scss`** (custom application styles)
  - Resets, utility classes, and component-specific overrides
  - All color values MUST use CSS variable tokens (e.g., `var(--primary)`), not hardcoded hex/RGB
  - Registered after `src/theme.css` in `angular.json` to allow overrides

This model ensures:
- Tokens are available to both Tailwind and SCSS
- Dark mode switches automatically (both systems respect `.dark` class)
- A single token change propagates everywhere

## ZardUI Component Inventory

ZardUI components are vendored (copied and version-controlled) in **`frontend/src/app/shared/components/`**. The team owns and can modify any component without external package updates.

### Current Components

**Button** — versatile button component with multiple variants
- Variants: `primary`, `secondary`, `destructive`, `outline`, `ghost`, `link`
- Sizes: `default`, `sm`, `lg`
- States: disabled, loading, focus
- Usage: primary CTAs, secondary actions, navigation links
- Location: `src/app/shared/components/button/`

**Card** — container for grouped content (hero sections, plugin cards, feature blocks)
- Supports title, description, footer slots
- Themeable background and border via CSS tokens
- Usage: content cards, plugin listings, feature showcases
- Location: `src/app/shared/components/card/`

**Badge** — small label for type, version, or status indicators
- Variants: `default`, `secondary`, `destructive`, `outline`
- Sizes: `default`, `sm`
- Usage: version tags, type labels, status badges
- Location: `src/app/shared/components/badge/`

### Planned Components (Future)

The following ZardUI primitives are documented and ready to be transcribed when needed:

- **Input** — text fields, email, password, search
- **Dialog** — modal dialogs and confirmations
- **Menu / Dropdown** — navigation menus, action dropdowns
- **Textarea** — multi-line text input
- **Select** — dropdown selection lists
- **Toast / Alert** — temporary notifications
- **Tooltip** — contextual help text
- **Tabs** — content organization
- **Breadcrumb** — navigation trail
- **Pagination** — multi-page data

## How to Use a ZardUI Component

### In a Component Template

```typescript
// my-feature.component.ts
import { Component } from '@angular/core';
import { ZButtonComponent } from '@app/shared/components/button';
import { ZCardComponent } from '@app/shared/components/card';
import { ZBadgeComponent } from '@app/shared/components/badge';

@Component({
  selector: 'app-my-feature',
  standalone: true,
  imports: [ZButtonComponent, ZCardComponent, ZBadgeComponent],
  template: `
    <z-card>
      <h2 z-card-title>Feature Title</h2>
      <p z-card-description>Feature description goes here.</p>
      <div z-card-content>
        <z-badge variant="secondary">v1.2.0</z-badge>
      </div>
      <div z-card-footer>
        <z-button variant="primary">Primary Action</z-button>
        <z-button variant="outline">Secondary Action</z-button>
      </div>
    </z-card>
  `,
})
export class MyFeatureComponent {}
```

### Key Points

1. **Import the component** from `src/app/shared/components/<name>`
2. **Add to `imports[]`** in the `@Component` decorator
3. **Use the `z-*` selector** (e.g., `<z-button>`, `<z-card>`) or attribute directives (e.g., `z-card-title`)
4. **Variants and styles** are controlled via the `variant` input property or CSS classes
5. **Colors automatically respect the theme** — when `.dark` is present, CSS tokens resolve to dark palette values

### Style Overrides

If a ZardUI component needs custom styling:

```scss
// my-feature.component.scss
.my-custom-card {
  z-card {
    background-color: var(--card);  // Always use tokens, never hardcoded colors
    border-color: var(--border);
  }
}
```

## How to Add a New ZardUI Component

When you need a ZardUI component that isn't yet vendored:

### 1. Source the Component

Visit **https://zardui.com/docs/components/<component-name>** and review the official documentation. The docs include:
- Component API (inputs, outputs, methods)
- Styling and variants
- Accessibility notes
- Example usage

### 2. Transcribe the Component

Create a new folder under `frontend/src/app/shared/components/<component-name>/` following the structure of existing components:

```
src/app/shared/components/input/
├── input.component.ts        # Main component class
├── variants.ts               # Variant definitions (CVA)
├── index.ts                  # Public exports
└── README.md (optional)      # Usage documentation
```

**Example structure for `input.component.ts`:**

```typescript
import { Component, Input } from '@angular/core';
import { cva } from 'class-variance-authority';
import { cn } from '@app/shared/lib/cn';

const inputVariants = cva(
  // Base styles
  'flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-foreground ' +
  'placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 ' +
  'focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50',
  {
    variants: {
      size: {
        default: 'text-sm',
        sm: 'h-8 text-xs',
        lg: 'h-12 text-base',
      },
    },
    defaultVariants: {
      size: 'default',
    },
  }
);

@Component({
  selector: 'z-input',
  standalone: true,
  template: `<input [ngClass]="inputClass" />`,
})
export class ZInputComponent {
  @Input() size: 'default' | 'sm' | 'lg' = 'default';

  get inputClass(): string {
    return inputVariants({ size: this.size });
  }
}
```

### 3. Commit to Repository

Commit the new component with a clear message:

```bash
git add frontend/src/app/shared/components/<component-name>/
git commit -m "feat(design-system): add ZardUI <component-name> component"
```

### 4. Manual Updates

ZardUI components are NOT automatically updated from an external registry. Future ZardUI library updates (if needed) are applied manually:

1. Visit https://zardui.com/docs/components/ to check for upstream changes
2. Update the component source in the repo
3. Submit a pull request for review
4. Merge and deploy through the standard workflow

## Theming & Color Tokens

All color decisions in the design system are driven by **semantic CSS variable tokens** defined in `frontend/src/theme.css`.

### Token Reference

See **`frontend/docs/DESIGN_TOKENS.md`** for the complete list of tokens, their OKLCH values, light/dark variants, and validated contrast ratios.

### Key Tokens

- **`--primary` / `--primary-foreground`** — yellow-forward brand color + text/icon on primary
- **`--background` / `--foreground`** — page background + default text
- **`--card` / `--card-foreground`** — card container + text on card
- **`--secondary` / `--secondary-foreground`** — secondary actions
- **`--destructive`** — warning/delete actions (red)
- **`--border`** — divider lines, card borders
- **`--input`** — form input background
- **`--ring`** — keyboard focus indicator

### Light and Dark Modes

The tokens are defined twice in `src/theme.css`:

1. **Light mode** (in `@theme {}` block)
   - Light backgrounds, dark text
   - Primary (yellow) is vivid and prominent
   - Good contrast for readability

2. **Dark mode** (in `.dark {}` block)
   - Dark backgrounds, light text
   - Primary (yellow) is adjusted for dark readability
   - All tokens re-evaluated for WCAG AA compliance

**Dark mode is activated by adding the `.dark` class to `document.documentElement`.** This is handled automatically by the theme service and pre-hydration script.

## Dark Mode Toggle Mechanism

### User-Facing Control

A theme toggle button is available in the **application shell header**. Located in `frontend/src/app/shell/theme-toggle/`, it:

1. Calls `ThemeService.toggle()` on click
2. Updates localStorage key `cf.theme` with the new value
3. Applies or removes `.dark` class on `document.documentElement`
4. All color tokens immediately resolve to the new palette

### Theme Service

See **`frontend/src/app/core/services/theme.service.ts`**:

- `getTheme(): 'light' | 'dark'` — reads current theme from DOM and localStorage
- `setTheme(theme): void` — updates theme, localStorage, and DOM
- `toggle(): void` — switches between light and dark
- Exposes a signal `theme$` for reactive theme changes in components

### Customization

To change the default theme or behavior:

1. Edit `frontend/src/theme.css` to change OKLCH token values
2. The `--primary` token defines the yellow accent; adjust OKLCH lightness/chroma to shift intensity
3. Run `npm run build` to recompile and verify contrast ratios with a WCAG checker

### SSR & Flash-of-Wrong-Theme (FOUC) Prevention

When the application uses server-side rendering:

1. An **inline pre-hydration script** in `frontend/src/index.html` (before Angular bootstrap) reads `localStorage.getItem('cf.theme')`
2. If dark theme is stored, the script applies `.dark` class **synchronously** before the page paints
3. The server renders theme-neutral HTML; the client-side script applies the theme before hydration
4. Result: no flash of the wrong theme, smooth theme transitions

For SSR output, see **both** `src/index.html` and `src/index.server.html` — the pre-hydration script is present in both and runs client-side before Angular bootstrap.

## Environment & Dependencies

### Node Version

**Node.js >= 22.22.3** is required to build and run the Angular 22 application.

Verify your Node version:

```bash
node --version
# Expected output: v22.22.3 or higher
```

### Tailwind v4 Setup

Tailwind is installed via `@tailwindcss/postcss`. The PostCSS pipeline is configured in `frontend/.postcssrc.json`:

```json
{
  "plugins": {
    "@tailwindcss/postcss": {}
  }
}
```

### Foundation Workarounds

Two workarounds are in place to support the ZardUI + Tailwind + Angular 22 stack:

1. **TypeScript 6 deprecations** — In `frontend/tsconfig.json`, set `"ignoreDeprecations": "6.0"` to suppress TypeScript 6 deprecation warnings that would otherwise block the build.

2. **Tailwind entry point must be `.css`** — The Tailwind entry file (`src/theme.css`) must be a `.css` file, not `.scss`. This is because PostCSS processes the `@import "tailwindcss"` directive at the CSS layer. Custom SCSS lives separately in `src/styles.scss`.

If you rename `src/theme.css` to `.scss` or move the `@import` directive, the build will fail with "tailwindcss module not found" errors.

## Build & Deployment

### Development

```bash
npm run dev
# or
ng serve
```

### Production Build

```bash
npm run build
# Produces frontend/dist/ with optimized browser bundle

npm run build:ssr
# Produces frontend/dist/server/ with SSR artifacts
```

### CI/CD

All builds are type-checked and tested automatically:

- **TypeScript**: `npx tsc --noEmit`
- **Unit Tests**: `ng test` (2057 tests pass)
- **Linting**: `npm run lint` (if configured)
- **Bundle Budget**: Two pre-existing warnings noted (initial bundle ~725 kB > 500 kB budget; landing-page component styles ~4.87 kB > 4 kB); see `.angular-budgets.json` for configuration

## Related Documentation

- **`frontend/docs/DESIGN_TOKENS.md`** — Complete token reference with OKLCH values and contrast ratios
- **`CONTRIBUTING.md`** — Frontend styling rule (use semantic tokens, never hardcoded colors)
- **`frontend/README.md`** — Frontend workspace overview
- **`frontend/angular.json`** — Build configuration, styles array, and PostCSS setup

---

**Last Updated**: 2026-06-08  
**Design System Version**: Tailwind v4 + ZardUI (vendored)  
**Node Requirement**: >= 22.22.3
