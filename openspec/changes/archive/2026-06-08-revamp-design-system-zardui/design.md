# ZardUI Design System Revamp — Technical Design

## Context

The application currently uses hand-rolled SCSS styling with minimal component composition, resulting in a dated visual experience. This design document specifies how to integrate **ZardUI** — an Angular-native, signals-based component library that uses Tailwind CSS v4 — into the existing build pipeline and application codebase. The target is a cohesive, yellow-forward professional design system with light and dark mode support, achieved through careful coexistence of Tailwind utilities (for ZardUI) and custom SCSS (for application styling).

**Current stack**:
- Angular ^22.0.0, TypeScript ~6.0.2
- SCSS with single entry `src/styles.scss` (no Tailwind, no @angular/cdk today)
- SSR enabled (transloco transfer-state loader in use)
- i18n via @jsverse/transloco ^8.3.0

**ZardUI stack**:
- Angular-native, shadcn-style component library
- 100% Tailwind v4 utility classes (e.g., `bg-primary text-primary-foreground inline-flex rounded-lg`)
- Component source code scaffolded into the repo via `npx zard-cli init`
- Theming via OKLCH CSS variables and Tailwind v4 `@theme` directive
- Dependencies: @angular/cdk, class-variance-authority, clsx, tailwind-merge, lucide-angular

---

## Phase 0 Spike Findings (GO)

**Decision: Proceed to Phase 1.** A compatibility spike verified that ZardUI and Tailwind v4 integrate cleanly with the existing Angular 22 / TypeScript 6 stack with zero build errors, minimal bundle impact, and production SSR support.

**Verified stack:**
- Angular 22.0.0 / @angular/cli 22.0.0 / @angular/build 22.0.0 (zard-cli 1.0.0-beta.80 is compatible; no peer conflicts)
- Tailwind v4 officially supported (@angular/build lists tailwindcss `^2 || ^3 || ^4` as optional peer)
- Resolved dependencies: tailwindcss 4.3.0, @tailwindcss/postcss 4.3.0, @angular/cdk 22.0.0, class-variance-authority 0.7.1, clsx 2.1.1, tailwind-merge 3.6.0, @ng-icons/core + @ng-icons/lucide 33.2.3, tailwindcss-animate 1.0.7
- TypeScript 6.0.2 (with required workaround; see below)
- **Node.js requirement:** 22.22.x (earlier 22.13.0 failed; record as environment prerequisite)

**Build and render verification:**
- Production build (browser + SSR via single `ng build` with `outputMode: "server"`) completes without errors
- `tsc --noEmit` produces zero type errors
- SSR serve and HTML render both succeed; sample ZardUI button rendered with compiled Tailwind utilities and linked stylesheet
- No `--force` or `--legacy-peer-deps` needed

**Bundle impact:**
- Browser JS: +~25 kB raw (+3.6% relative to baseline)
- Global CSS: +~22 kB raw (Tailwind v4 base/reset + theme variables; v4 emits only used utilities)
- SSR server bundle: unchanged

### Required workarounds (carry into Phase 2)

1. **TypeScript 6.0 `baseUrl` deprecation (TS5101)**  
   zard-cli writes `"baseUrl": "./"` to tsconfig.json. TypeScript 6 treats this as a hard deprecation error. **Workaround:** Add `"ignoreDeprecations": "6.0"` to tsconfig.json root. **Tech-debt note:** `baseUrl` is removed in TypeScript 7; plan to migrate to explicit `paths` or `imports` map before TS 7 adoption.

2. **Global styles entry must be `.css`, not `.scss`**  
   Angular runs `.scss` through Sass preprocessing BEFORE PostCSS. Sass mishandles Tailwind v4's `@import "tailwindcss";` (currently a deprecation warning, becomes hard error in Dart Sass 3.0). **Therefore:** The Tailwind / token global entry (src/theme.css) must be a plain CSS file processed directly by PostCSS. This decision **confirms design choice D2** (dedicated CSS entry for Tailwind/tokens, separate from SCSS). Component-level styles may remain SCSS (`inlineStyleLanguage: "scss"` is unaffected), and a separate global `.scss` entry may coexist in angular.json as long as it does NOT contain the Tailwind import. **Important:** `zard-cli init` OVERWRITES whichever global CSS file path is provided with the ZardUI theme — point it at the new dedicated CSS entry, not at any file holding existing custom styles.

---

## Goals / Non-Goals

**Goals:**
- Establish a cohesive, yellow-forward professional design via ZardUI components
- Enable SCSS and Tailwind to coexist in the build without conflicts
- Implement light and dark mode palettes using OKLCH semantic tokens
- Provide a phased, low-risk migration path (shell → home → rest of app)
- Ensure type safety and accessibility (WCAG AA contrast ratios on all tokens)

**Non-Goals:**
- Logo or brand-identity redesign (colors only)
- Changes to app features, architecture, or routing
- Full component migration in a single change (phased rollout in scope)
- Adoption of Tailwind utilities as the team's primary authoring style for custom application CSS (SCSS remains the authoring standard)
- Automatic or continuous synchronization of ZardUI components with upstream (manual pull model)

---

## Decisions

### D1 — Tailwind v4 Build Integration with Angular 22

**Decision:**  
Integrate Tailwind v4 via its PostCSS plugin (`@tailwindcss/postcss`) declared in `.postcssrc.json`, using Tailwind v4's CSS-first config syntax (`@import "tailwindcss";` + `@theme {}` in CSS files) rather than a legacy `tailwind.config.js`.

**Rationale:**  
Angular 22's esbuild/Vite application builder owns the Vite/esbuild pipeline. The PostCSS plugin path is the officially supported, least-invasive hook. It runs after other CSS loaders, avoiding conflicts. Tailwind v4's CSS-first syntax aligns with this approach and is more composable than JavaScript config.

**Alternative considered:**  
Use the Tailwind Vite plugin directly. Rejected because the Angular builder controls the Vite config; exposing it to custom plugins introduces coupling and maintenance risk. PostCSS is the stable public API.

**Implementation note:**  
`.postcssrc.json` will be committed to the repo:
```json
{
  "plugins": {
    "@tailwindcss/postcss": {}
  }
}
```

---

### D2 — SCSS and Tailwind Separation of Concerns

**Decision:**  
Maintain a dedicated CSS entry point (e.g., `src/theme.css`) that holds all Tailwind directives (`@import "tailwindcss";`), the `@theme` semantic token definitions, and dark mode overrides (`.dark` class rules). Keep `src/styles.scss` for the team's custom application styling. Register both in `angular.json`'s `styles` array.

**Rationale:**  
Tailwind v4's `@import "tailwindcss"` and `@theme` directives behave poorly when nested inside SCSS (SCSS preprocesses `@import`/`@use` differently, breaking Tailwind's cascade). Isolating Tailwind and tokens in a plain CSS file avoids preprocessor conflicts while allowing both systems to coexist cleanly. The team's custom SCSS continues to be written in `src/styles.scss` and benefits from SCSS features (variables, mixins, nesting) without interference.

**Alternative considered:**  
Force Tailwind through the SCSS entry (preprocessed by node-sass/dart-sass). Rejected — fragile, mixes preprocessor semantics, risks breaking Tailwind's cascade and token resolution.

**Implementation note:**  
`angular.json` `styles` array:
```json
"styles": [
  "src/theme.css",
  "src/styles.scss"
]
```

---

### D3 — Token Bridge Between ZardUI and Custom SCSS

**Decision:**  
ZardUI's semantic OKLCH tokens (`--primary`, `--background`, `--card`, etc.) are the single source of truth. Custom SCSS code references them via CSS custom properties (e.g., `color: var(--primary);`) rather than hardcoding hex values.

**Rationale:**  
One palette definition drives both ZardUI components and bespoke application SCSS. Theme and dark-mode switching remain consistent across the entire UI layer. This prevents palette drift and simplifies maintenance.

**Alternative considered:**  
Duplicate color variable definitions in SCSS (Tailwind tokens in CSS, SCSS tokens in SCSS). Rejected — introduces drift risk, increases maintenance burden, and defeats the purpose of a unified design system.

**Implementation detail:**  
Custom SCSS can still use SCSS features (e.g., computed opacity adjustments), but the base token values always come from CSS custom properties.

---

### D4 — Yellow-Forward OKLCH Palette (Light and Dark)

**Decision:**  
Use a vivid professional yellow as `--primary` with a near-black `--primary-foreground` (ensuring WCAG AA contrast on yellow text/icons). Provide concrete starting OKLCH values for light and dark modes as a proposal for design review and refinement.

**Rationale:**  
Yellow is luminous and must be paired with dark text/icons for readability and accessibility. The proposed values balance professional appearance with strong contrast, avoiding a playful or loud aesthetic.

**Light mode starting palette:**
```css
--background: oklch(0.99 0.01 95);
--foreground: oklch(0.20 0.02 95);
--primary: oklch(0.85 0.17 95);
--primary-foreground: oklch(0.20 0.03 95);
--secondary: oklch(0.96 0.01 95);
--secondary-foreground: oklch(0.25 0.02 95);
--muted: oklch(0.96 0.005 95);
--muted-foreground: oklch(0.50 0.02 95);
--accent: oklch(0.90 0.06 95);
--accent-foreground: oklch(0.22 0.03 95);
--card: oklch(1 0 0);
--border: oklch(0.90 0.01 95);
--input: oklch(0.90 0.01 95);
--ring: oklch(0.85 0.17 95);
--destructive: oklch(0.58 0.22 27);
--radius: 0.625rem;
```

**Dark mode starting palette (.dark class):**
```css
.dark {
  --background: oklch(0.18 0.01 95);
  --foreground: oklch(0.97 0.01 95);
  --primary: oklch(0.86 0.17 95);
  --primary-foreground: oklch(0.20 0.03 95);
  --secondary: oklch(0.25 0.01 95);
  --secondary-foreground: oklch(0.95 0.01 95);
  --muted: oklch(0.30 0.01 95);
  --muted-foreground: oklch(0.70 0.02 95);
  --accent: oklch(0.88 0.06 95);
  --accent-foreground: oklch(0.20 0.02 95);
  --card: oklch(0.22 0.01 95);
  --border: oklch(0.30 0.01 95);
  --input: oklch(0.30 0.01 95);
  --ring: oklch(0.86 0.17 95);
  --destructive: oklch(0.62 0.20 27);
}
```

**Note:** These are starting points. They must be tuned and validated against WCAG AA contrast requirements (especially `--primary` / `--primary-foreground` and focus rings) during design review before implementation.

**Alternative considered:**  
Use a subtle accent approach (yellow as a secondary highlight, neutral as primary). Rejected per user preference for yellow-forward branding.

---

### D5 — Dark-Mode Mechanism and SSR Flash Prevention

**Decision:**  
Toggle the `.dark` class on the document root via a dedicated theme service. To prevent flash-of-unstyled-theme (FOUC) under SSR, set the initial theme on the server and/or via an inline pre-hydration script that runs before Angular bootstraps, reading the persisted theme preference and applying the class immediately.

**Rationale:**  
CSS class toggling is the standard, performant approach for light/dark switching. Under SSR, if the theme is set only on the client after bootstrap, users see a brief flash of the wrong theme before the script runs. A server-side render hint (or inline script) ensures the correct class is present in the initial HTML.

**Alternative considered:**  
Client-only toggle (read preference from localStorage and apply after bootstrap). Rejected — causes FOUC, poor UX under SSR.

**Implementation outline:**  
- Create a `ThemeService` that reads stored preference and applies `.dark` class to `document.documentElement`
- On the server (if using Angular SSR), detect the user's stored preference and include a note in the rendered HTML
- Include an inline script (in `index.html`) that runs before Angular bootstrap to apply `.dark` class if needed, preventing flash

---

### D6 — ZardUI Component Ownership and Location

**Decision:**  
Scaffold ZardUI components into a dedicated in-repo directory (proposed: `src/app/shared/ui/`), owned and maintained by the team. Document that future ZardUI library updates are manually pulled (shadcn model), not automatically synchronized.

**Rationale:**  
The shadcn model gives full ownership and control. The team can customize components, add project-specific variants, and evolve them independently. Manual pulls are explicit and reviewed, avoiding silent breaking changes.

**Alternative considered:**  
Treat ZardUI as an npm package dependency. Rejected — ZardUI is designed for in-repo scaffolding to maximize customization; npm distribution would defeat that purpose.

**Open question:**  
Confirm the exact directory path against the project's existing `src/app/shared/` conventions (if any).

---

### D7 — Tailwind Preflight vs Existing Global SCSS Resets

**Decision:**  
Be deliberate about the interaction between Tailwind's Preflight base reset and any existing global SCSS resets in `src/styles.scss`. Either adopt Tailwind's Preflight wholesale and remove duplicates, or scope/disable Preflight and rely on existing resets.

**Rationale:**  
Tailwind's Preflight applies base resets (e.g., `box-sizing: border-box`, margin resets, etc.). If the app already has equivalent resets in SCSS, they will conflict, leading to double-application or specificity confusion. An explicit decision prevents silent bugs.

**Alternative considered:**  
Leave both in place and hope they harmonize. Rejected — causes maintenance confusion and potential specificity issues.

**Recommended approach:**  
Audit existing resets in `src/styles.scss`. If they align with Preflight, comment them out and rely on Tailwind. If custom, configure Tailwind to disable Preflight and keep the SCSS resets.

---

## Risks / Trade-offs

**[Angular 22 / TypeScript 6 Compatibility with ZardUI + Tailwind v4]**  
→ **Mitigation:**  
Run a compatibility spike as the first implementation step (before committing further resources). Install ZardUI via `npx zard-cli init` in a temporary branch, verify the build succeeds, and render a simple ZardUI component in the app. Timebox to 1-2 days. If compatibility issues arise, fall back to an SCSS-only design system or a mature SCSS-based component library (e.g., Clarity, ng-bootstrap).

---

**[SSR Flash-of-Wrong-Theme (FOUC)]**  
→ **Mitigation:**  
Implement the pre-hydration inline script (D5) to apply `.dark` class before Angular bootstrap. Test under SSR to confirm the correct theme is rendered in the initial HTML payload.

---

**[Tailwind Preflight vs Existing Global Resets]**  
→ **Mitigation:**  
Audit `src/styles.scss` before Tailwind integration. Reconcile duplicates: either disable Preflight or remove SCSS resets. Document the chosen approach in the theme.css header.

---

**[Yellow-Forward Accessibility and Contrast]**  
→ **Mitigation:**  
Validate all token pairs (especially `--primary` / `--primary-foreground`, focus rings, and border visibility) against WCAG AA (4.5:1 for text) and AAA where feasible. Use a contrast checker tool (e.g., WebAIM Contrast Checker, polished/color-contrast). Iterate on the palette until all pairs meet the threshold. Document final OKLCH values in theme.css with contrast ratios as comments.

---

**[OKLCH Browser Support]**  
→ **Mitigation:**  
OKLCH has excellent support in modern evergreen browsers (Chrome 111+, Firefox 113+, Safari 16.4+, Edge 111+). Confirm the app's minimum supported browser target and, if needed, provide an `@supports` fallback to RGB/hex equivalents. Document the min target in the design doc.

---

**[Bundle Size Growth (CDK + lucide-angular + tailwind-merge)]**  
→ **Mitigation:**  
Measure initial build size after Tailwind + ZardUI integration. Tree-shake unused lucide icons by configuring the icon resolver to only include icons actually imported. Consider lazy-loading icon sets if the app grows. Re-measure after phase 2 (home) migration and adjust if necessary.

---

**[In-Repo Component Drift (No Auto-Updates)]**  
→ **Mitigation:**  
Establish a component update process: designate an owner for the `src/app/shared/ui/` directory, document how to apply ZardUI library updates, and perform periodic reviews (e.g., quarterly) to pull new ZardUI components or security patches. Keep a COMPONENTS.md in the ui/ directory listing all scaffolded components and their source version.

---

**[SCSS + Tailwind CSS Conflicts at Build Time]**  
→ **Mitigation:**  
The D2 separation (dedicated theme.css for Tailwind, styles.scss for custom SCSS) is the primary safeguard. If conflicts arise (e.g., CSS variable redefinition, class name collisions), disable Preflight or scope Tailwind utilities to specific selectors (e.g., `.z-component` for ZardUI, `.app-` for custom styles).

---

## Migration Plan

### Phase 0: Compatibility Spike (1–2 days)
1. Create a feature branch (`spike/zardui-compat`)
2. Install ZardUI via `npx zard-cli init` (follow official ZardUI setup docs for Angular 22)
3. Verify the build succeeds (no TypeScript or esbuild errors)
4. Render a simple ZardUI component (e.g., button) in a test route
5. Confirm SSR build and render (if applicable)
6. Document findings and risks; decide go/no-go before phase 1

**Fallback:** If critical incompatibilities emerge, pause and escalate. Consider alternative design systems.

### Phase 1: Foundation (PostCSS + Theme + Dark Mode, No UI Changes Yet)
1. Commit `.postcssrc.json` with `@tailwindcss/postcss` plugin
2. Create `src/theme.css` with:
   - `@import "tailwindcss";`
   - `@theme {}` block with semantic tokens (light mode)
   - `.dark {}` block with dark-mode overrides
3. Update `angular.json` to include `src/theme.css` and `src/styles.scss` in `styles`
4. Create `ThemeService` to toggle `.dark` class and persist preference
5. Add inline pre-hydration script to `index.html` (SSR flash prevention)
6. Verify build, no visual changes yet
7. Commit and merge

### Phase 2: Shell Migration
1. Audit the shell component (header, navigation, layout)
2. Replace hand-rolled HTML with ZardUI primitives (button, nav, dropdown, etc.)
3. Update SCSS to use CSS variable tokens instead of hardcoded colors
4. Test light and dark mode switching
5. QA visual design against spec
6. Commit and merge

### Phase 3: Home Page Migration
1. Similar process: refactor home page HTML to ZardUI components
2. Update SCSS; ensure all colors reference tokens
3. Visual QA and design review
4. Commit and merge

### Phase 4: Remaining UI (Out of Scope Detail)
- Phased migration of remaining pages/components
- Each phase includes visual QA and design review

### Rollback
Changes are additive until components are swapped. To rollback:
1. Remove `src/theme.css` from `angular.json` `styles`
2. Remove `@tailwindcss/postcss` from `.postcssrc.json` (or delete the file)
3. Remove the ZardUI components directory (`src/app/shared/ui/`)
4. Revert component HTML to prior SCSS-based versions
5. Rebuild and verify

**Note:** Rollback is straightforward because Tailwind and ZardUI are isolated; reverting does not require refactoring the entire app.

---

## Open Questions

1. **ZardUI Component Directory Convention:**  
   What is the established path for shared utilities and components in this repo? Confirm whether `src/app/shared/ui/` aligns with project conventions, or propose an alternative.

2. **Theme Toggle UX and Placement:**  
   Where should the theme toggle (light/dark switcher) appear in the UI? (e.g., top-right navbar, settings modal, floating button) Should it persist across sessions? Any i18n labels needed for the toggle?

3. **Final Palette Tuning and Contrast Validation:**  
   The proposed OKLCH values (D4) are starting points. Which team member will own the design review, contrast checking, and finalization of the light/dark palettes? What is the acceptance criteria (specific WCAG level)?

4. **Minimum Supported Browser Targets:**  
   What is the app's minimum supported browser version? (Affects OKLCH fallback strategy and CSS variable support.) Should we provide RGB fallbacks for older browsers, or is modern evergreen sufficient?

5. **Global SCSS Resets and Preflight:**  
   Audit `src/styles.scss` and confirm: which existing resets are present, and should they be kept or replaced by Tailwind Preflight? (Needed to finalize D7 decision.)

6. **Lucide Icon Subsetting:**  
   Will the app use a subset of lucide-angular icons, or pull the full library? If subset, which icons should be included? (Affects bundle size and tree-shaking strategy.)

---

## Summary of Key Decisions

| ID | Decision | Rationale |
|----|----------|-----------|
| D1 | PostCSS + Tailwind v4 CSS-first config, not Vite plugin | Angular builder owns pipeline; PostCSS is stable public API |
| D2 | Separate theme.css (Tailwind) + styles.scss (custom SCSS) | Avoids SCSS/Tailwind preprocessor conflicts; coexistence by design |
| D3 | ZardUI tokens are single source of truth; custom SCSS references via `var()` | One palette definition, consistent theming, minimal drift |
| D4 | Vivid professional yellow (`--primary` + dark foreground), light/dark OKLCH values proposed | Yellow-forward branding; contrast validated during design review |
| D5 | Theme service + inline pre-hydration script for `.dark` class (SSR) | Prevents flash-of-wrong-theme; server-aware theme toggle |
| D6 | ZardUI scaffolded into `src/app/shared/ui/`, team-owned, manual updates | Full customization control; shadcn model; explicit pull workflow |
| D7 | Explicit reconciliation of Tailwind Preflight vs existing SCSS resets | Avoid conflicts; audit-driven decision before integration |
