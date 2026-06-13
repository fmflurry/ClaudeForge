## 1. Locate Current Presentation Surfaces

- [x] 1.1 Identify the app shell header component/template/nav config that renders logo, `Catalogue`, `Rechercher`, `Documentation`, and `Tableau de bord`.
- [x] 1.2 Identify the existing auth/session state source used by presentation facades or header state so `Tableau de bord` can be conditionally rendered without adding new auth logic.
- [x] 1.3 Identify the landing page component/template/styles that render hero title, hero CTAs, install showcase, category entry points, lower search/search CTA, and footer layout.

## 2. Update Header Navigation

- [x] 2.1 Preserve the existing header logo markup, link target, and accessible label while editing nav items.
- [x] 2.2 Rename the plugin discovery nav item visible label from `Catalogue` to `Plugins` while keeping the existing route unless the app already has a distinct plugins route.
- [x] 2.3 Remove the `Rechercher` nav item from rendered header navigation.
- [x] 2.4 Keep the `Documentation` nav item visible and available.
- [x] 2.5 Render `Tableau de bord` only when the existing logged-in auth state is true; do not render disabled or visually hidden anonymous dashboard markup.

## 3. Optimize Landing Layout

- [x] 3.1 Compact landing vertical spacing so primary landing content and footer are visible in the initial viewport at desktop and common laptop breakpoints.
- [x] 3.2 Rework the landing layout to reclaim side deadspace by placing supporting content into available horizontal space before deleting meaningful content.
- [x] 3.3 Keep footer meaning and links available while adjusting footer/container spacing as needed for viewport fit.
- [x] 3.4 Keep the hero title on one line at desktop and common laptop breakpoints without hard-coded line breaks or overflow.
- [x] 3.5 Remove the disabled hero login button or disabled login CTA from the hero.
- [x] 3.6 Remove the duplicate lower search area and duplicate lower search CTA because the browse plugins CTA already provides plugin discovery access.
- [x] 3.7 Keep the browse plugins and publish CTAs prominent in the hero.
- [x] 3.8 Preserve category discovery entry points and ensure category labels remain sourced from the existing category vocabulary.

## 4. Add Tests

- [x] 4.1 Add or update header rendering tests for preserved logo, `Plugins` label, absent `Catalogue`, absent `Rechercher`, and preserved `Documentation`.
- [x] 4.2 Add or update header auth-state tests proving `Tableau de bord` renders for logged-in users and is absent for anonymous or unresolved auth state.
- [x] 4.3 Add or update landing page tests proving the disabled hero login CTA and duplicate lower search/search CTA are absent.
- [x] 4.4 Add or update landing page tests proving browse plugins CTA, publish CTA, install showcase fallback, and category discovery entry points remain available.

## 5. Verify Layout and Build

- [x] 5.1 Manually verify desktop viewport behavior: footer visible in initial viewport, header logo retained, hero title one line, and no lower duplicate search section.
- [x] 5.2 Manually verify common laptop viewport behavior with the same landing/header acceptance checks.
- [x] 5.3 Manually verify small mobile behavior remains readable even if hero title wraps responsively.
- [x] 5.4 Run focused tests for changed header and landing page specs.
- [x] 5.5 Run project typecheck/build verification and lint for changed files.

Evidence note (2026-06-09):
- 5.1-5.3: viewport checks were marked complete before this review pass; no screenshot artifact is attached in this change folder.
- 5.4: `npm run test --workspace=frontend -- --watch=false --include src/app/features/home/presentation/landing-page.component.spec.ts --include src/app/shell/shell-layout.component.spec.ts --include src/app/app.routes.spec.ts` → `Test Files 3 passed (3)`, `Tests 78 passed (78)`.
- 5.5: `npx tsc --noEmit --pretty false` → no output; exit 0. `npx eslint src/app/features/home/presentation/landing-page.component.ts src/app/features/home/presentation/landing-page.component.spec.ts src/app/app.routes.ts src/app/app.routes.spec.ts public/i18n/en.json public/i18n/fr.json` → 0 errors, 2 warnings for JSON files ignored by ESLint config. `npm run build --workspace=frontend` → build complete; budget warnings only.
