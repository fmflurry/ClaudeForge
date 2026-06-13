## Context

The current landing page and app shell header create extra vertical and horizontal friction for first-time visitors. The landing content pushes the footer below the initial viewport, duplicates plugin discovery CTAs, includes a disabled login CTA in the hero, and leaves side space underused. Header navigation also includes labels that no longer match the desired information architecture.

This change is a presentation-only update. It should not introduce API, data model, persistence, dependency, or routing changes. The implementation should keep the existing header logo and documentation entry while making navigation auth-aware.

## Goals / Non-Goals

**Goals:**

- Keep the landing page within the initial screen height enough that the footer is visible.
- Preserve the header logo and `Documentation` navigation item.
- Rename `Catalogue` to `Plugins`.
- Remove `Rechercher` from header navigation.
- Show `Tableau de bord` only for logged-in users.
- Rebalance the landing layout to use horizontal deadspace instead of stacking duplicate vertical sections.
- Keep the hero title on one line at intended desktop breakpoints.
- Remove the disabled hero login button.
- Remove the lower duplicate search/search CTA because the browse plugins CTA already covers plugin discovery.

**Non-Goals:**

- No new auth mechanism or session model.
- No new plugin search feature.
- No route restructuring.
- No footer content changes beyond what is needed for viewport fit.
- No backend, database, or dependency changes.

## Decisions

### Use existing auth state for header visibility

`Tableau de bord` should be conditionally rendered from the app's existing logged-in state rather than hidden with CSS or duplicated in separate header variants.

Rationale: conditional rendering avoids exposing an inactive nav target to anonymous users and keeps header behavior aligned with current auth flow.

Alternative considered: always render `Tableau de bord` but disable or visually hide it. Rejected because hidden/disabled nav still creates accessibility and maintenance ambiguity.

### Consolidate landing discovery around the primary browse plugins CTA

The lower search/search CTA should be removed instead of restyled because plugin discovery already has a browse plugins CTA.

Rationale: removing duplicate CTA content reduces vertical height and avoids forcing users to choose between equivalent discovery paths.

Alternative considered: keep the lower area but compress it. Rejected because it still duplicates intent and competes with the hero CTA.

### Reclaim horizontal space before reducing content meaning

Landing layout should shift supporting content into available side space and reduce oversized gaps before removing meaningful copy.

Rationale: the requirement targets deadspace and viewport fit, not content deletion for its own sake.

Alternative considered: only reduce margins and font sizes. Rejected because spacing-only changes may not solve wide-screen deadspace or footer visibility consistently.

### Preserve semantic nav and accessible labels

Header changes should update visible labels while retaining semantic links and accessible names. `Plugins` should point to the same plugin/catalogue destination unless existing routes already distinguish them.

Rationale: label change should not create routing churn or break existing navigation behavior.

Alternative considered: create a new `/plugins` route. Rejected as out of scope unless the current app already uses that route.

### Keep hero title single-line through layout constraints

The hero title should remain one line by using responsive width, typography, and breakpoint constraints rather than hard-coded text splitting.

Rationale: one-line behavior is a layout requirement; hard-coded line breaks would make responsive behavior brittle.

Alternative considered: shorten the title text. Rejected unless existing text cannot fit at target breakpoints after layout fixes.

## Risks / Trade-offs

- Reduced vertical spacing could make the landing page feel cramped → Validate desktop and common laptop viewport sizes, then tune spacing tokens rather than arbitrary pixel cuts.
- One-line hero title may overflow on narrow screens → Apply requirement at desktop/tablet breakpoints and allow responsive wrapping on small mobile if needed for readability.
- Auth-aware `Tableau de bord` could flicker during session loading → Use existing auth loading conventions or defer rendering until state is known.
- Removing the lower search area may reduce a secondary discovery affordance → Keep the browse plugins CTA prominent and clearly labelled.

## Migration Plan

1. Update header nav label and visibility rules using existing navigation/auth state.
2. Refactor landing layout spacing and section composition to fit the initial viewport.
3. Remove the disabled hero login button and duplicate lower search/search CTA.
4. Verify responsive behavior across desktop, laptop, and mobile breakpoints.
5. Roll back by restoring the previous header nav config and removed landing section if regressions appear.
