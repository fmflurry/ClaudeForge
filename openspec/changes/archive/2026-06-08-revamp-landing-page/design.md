## Context

The landing page and global header don't reflect the product's intended identity, and the header exposes a "team" concept that isn't real. Grounding facts from the current codebase:

- **Theme** (`frontend/src/theme.css`): the palette is already yellow-forward (primary, hue 95). The header (`shell-layout.component.ts`) renders on the `--sidebar*` tokens, which are near-white in light mode — so the header reads neutral, not "secondary". The landing hero (`landing-page.component.ts` `.lp-hero`) uses `background: var(--foreground)` (near-black), producing the oversized dark slab that makes yellow read as secondary.
- **Header composition**: brand → nav → `cf-team-switcher` → `cf-org-switcher` → `cf-language-switcher` → `cf-theme-toggle` → auth. The shell also renders `cf-team-welcome` overlay in `<main>` gated by `facade.needsInit()`.
- **Categories already exist end-to-end for *reading*.** There is a `categories` controlled-vocabulary table (dimensions `type`, `language`, `use_case`), a `plugin_categories` join table (multi-tag by construction), a `ListCategoriesUseCase` + `GET categories` API, and a fully-working frontend filter layer (`CatalogFilterQuery`, `filterMatches`, `getCategories()`). The `use_case` dimension is the "kind of plugin" axis (SWE, Product, UX/UI, DevOps, …).
- **Publish drops tags.** The plugin manifest carries `Types`, `Languages`, `UseCaseTags`, but `CreatePluginCommand` has no tag fields and no publishing adapter writes `plugin_categories`. Today categories are only populated by `PluginDataSeeder`. So multi-tag-on-upload is a real gap.
- **CLI**: published bin is `claude-plugin`; install subcommand is `install <pluginName> [version]`. The marketplace install command is therefore `claude-plugin install <name>`.
- **Featured plugin**: no concept exists. `Plugin` (domain) and `PluginEntity` (EF) have no featured flag.

Constraints: KISS (no new infra unless it earns its place); strict TS (no `any`); immutability; clean-architecture layering (presentation → application/facade → domain → infrastructure); components talk to facades, never use-cases; Angular SSR-safe code; WCAG 2.1 AA contrast per the documented token-validation discipline in `theme.css`.

## Goals / Non-Goals

**Goals:**
- Make the landing page yellow/cream-dominant top-to-bottom; dark only as a small accent (CLI code block, footer).
- Replace the dark hero slab with a yellow-dominant hero built around a designed "How to install a plugin" showcase that renders the CLI command for a rotatable featured plugin.
- Give the header a distinct **non-yellow secondary (violet)** surface so the brand yellow pops.
- Remove the team concept from the UI (switcher + welcome overlay) and the now-dead wiring.
- Relocate the language switcher and theme toggle to sit immediately before the login button.
- Surface category-tag filters (the `use_case` dimension) as discovery entry points on the landing page.
- Close the publish gap so uploading a plugin persists its multiple category tags.

**Non-Goals:**
- No admin UI for choosing the featured plugin (flag flipped via seed/DB for now).
- No team/organization-membership feature (deferred). Org-switcher stays as-is.
- No redesign of the catalog/search pages themselves (only the entry points from the landing page).
- No new category controlled-vocabulary values beyond what's needed to express SWE/Product/UX-UI/DevOps in the `use_case` dimension.
- No change to auth.

## Decisions

### D1 — Header secondary surface: retune existing `--sidebar*` tokens to violet
The header already consumes `--sidebar*`. Rather than introduce new `--header*` tokens, repaint the `--sidebar*` surface to a saturated violet (hue ≈ 285 — the cool counterpart to yellow's hue 95) with a light `--sidebar-foreground`. Candidate values (validated for contrast in implementation, not assumed final):
- Light: `--sidebar: oklch(0.45 0.13 285)`, `--sidebar-foreground: oklch(0.97 0.01 285)`, `--sidebar-border: oklch(0.38 0.11 285)`, `--sidebar-accent: oklch(0.55 0.14 285)`.
- Dark: `--sidebar: oklch(0.30 0.10 285)` with the same light foreground.
- **Why**: the header is the only `--sidebar` consumer, so repainting it is the minimal-surface change (KISS) and keeps the brand yellow popping against a cool field. **Alternative considered**: new dedicated `--header*` tokens — rejected (extra token surface for one consumer). **Alternative**: blue (hue ≈ 265) — violet chosen as the more distinctive pairing with yellow; final hue is a visual call during implementation within 265–290.
- **Constraint**: nav/auth text and the focus ring on the violet surface must pass WCAG (text ≥ 4.5:1, ring ≥ 3:1); validated and recorded the same way the existing contrast table in `theme.css` is.

### D2 — Featured plugin: boolean column + partial unique index (single featured)
Add `IsFeatured` (bool, default false) to `Plugin` (domain) and `PluginEntity` (EF), plus a **partial unique index** `WHERE is_featured = true` so the database guarantees at most one featured plugin. Rotation = flip the flag (unset previous, set next) in a transaction.
- **Why**: smallest possible data-model change; the DB enforces the "exactly one" invariant instead of application code. **Alternatives**: (a) a dedicated single-row `featured_plugin` table with an FK — rejected as heavier for one boolean; (b) a config/env featured slug — rejected because the user explicitly wants a rotatable DB entry. (c) enforcing single-featured in app code — rejected; a partial unique index is race-proof.
- Read API: `GET /api/plugins/featured` → the featured plugin summary (name, slug, latest version) or 404/empty envelope when none is featured. New read use case in `PluginCatalog`.
- Admin/rotation: out of scope for UI; flipped via `PluginDataSeeder`/SQL for now.

### D3 — "How to install" showcase as the hero (frontend home feature)
The landing hero is rebuilt: cream/yellow background, headline + CTAs, and a designed showcase card whose only dark element is the CLI command code block. The featured plugin is fetched through a new home-feature slice following clean architecture:
- `domain/ports/featured-plugin.port.ts` (abstract), `domain/models/featured-plugin.model.ts`.
- `infrastructure/adapter/featured-plugin-http.adapter.ts` calling `GET /api/plugins/featured`.
- `application/facades/featured-plugin.facade.ts` exposing a signal; the component injects the **facade** only (never the use case / adapter).
- Install command is composed client-side as `claude-plugin install <identifier>` from the featured plugin.
- **Why**: mirrors the existing `home` feature structure (e.g. `marketplace-stats` port/adapter/facade) and the global rule that components use facades. SSR-safe (no direct DOM/window in fetch path).
- **Graceful degradation**: if no plugin is featured or the fetch fails, the showcase falls back to a generic command (`claude-plugin install <plugin-name>`) so the hero never renders broken — consistent with the existing disabled-CTA / empty-state patterns on this page.

### D4 — Install identifier: render the plugin **slug**
The showcase renders `claude-plugin install <slug>`. Slugs are URL/CLI-safe and already unique; `Name` may contain spaces/casing. The CLI's `install <pluginName>` resolves by the marketplace identifier, which the slug represents safely.
- **Why**: avoids quoting/casing ambiguity in a copy-pasteable command. **Alternative**: render `Name` — rejected (may need quoting). The `featured` endpoint returns both, so this is reversible if the CLI expects `Name`.

### D5 — Category filters on the landing page are deep-link entry points
The landing page surfaces the `use_case` dimension values (SWE, Product, UX/UI, DevOps, …) as filter chips that **navigate to the catalog/search with the filter preselected** (query params consumed by the existing `CatalogFilterQuery`), rather than filtering plugins in-place on the landing page.
- **Why**: the catalog already owns filtering (`filterMatches`, `getCategories`); the landing page stays a thin discovery surface (KISS, no duplicated filter state). **Alternative**: in-place filtering on landing — rejected (duplicates catalog logic, muddies the landing's role). Chip labels come from `getCategories()` display names, not hardcoded, so the vocabulary stays data-driven.

### D6 — Close the publish tagging gap end-to-end
Extend the publishing pipeline so manifest `Types` / `Languages` / `UseCaseTags` are persisted into `plugin_categories`:
- Add tag arrays to `CreatePluginCommand`.
- The publishing use case resolves each (dimension, value) against the `categories` controlled vocabulary and the repository writes the `plugin_categories` rows in the same transaction as plugin creation.
- **Unknown values are rejected** with a validation error (controlled-vocabulary integrity), matching the CLI's existing `VALID_TYPES` allow-list approach for `types`. Languages/use-cases validate against the seeded vocabulary.
- **Why**: tags are already in the manifest and the read/filter side already exists — this just stops dropping them on write. **Alternative**: auto-create missing vocabulary rows — rejected (uncontrolled vocabulary fragments the filter taxonomy). **Alternative**: defer the publish gap out of this change — rejected because "allow multiple tags on upload" is an explicit requirement.

### D7 — Remove the team concept (UI + dead wiring)
Remove `TeamSwitcherComponent` and `TeamWelcomeOverlayComponent` from `shell-layout.component.ts`, drop the `TeamContextStore`/`TeamContextFacade` providers and the `needsInit()` gating, and delete the `features/team-context` feature directory plus its specs once a reference check confirms nothing else depends on it. Org-switcher and org context are untouched.
- **Why**: leaving the feature in place is dead code (refactor hygiene); the user states team is not built. **Alternative**: only unwire from the shell, keep the feature — rejected to avoid dead code, but contingent on the importer check (see risk). This is a **UI-BREAKING** change (onboarding overlay disappears).

### D8 — Header layout: right-aligned control cluster
After removing team, the header becomes: brand → nav → org-switcher → (margin-auto spacer) → **language → theme → login** as one right-aligned cluster.
- **Why**: satisfies "language and theme right before login"; keeps org-switcher with the nav group. Minimal CSS change (move `margin-left:auto` onto the lang/theme/auth cluster wrapper).

## Risks / Trade-offs

- **[Violet header fails WCAG in one mode]** → Treat candidate OKLCH values in D1 as starting points; compute and record contrast ratios (text ≥ 4.5:1, ring ≥ 3:1) for both light and dark before finalizing, updating the `theme.css` contrast table.
- **[Deleting `team-context` breaks other importers/specs]** → Before deletion, grep importers of `team-context` (shell + any cross-domain registry usage); if anything outside the shell depends on it, downgrade D7 to "unwire from shell only" and leave the directory. Remove team-context specs in the same change.
- **[Single-featured invariant violated during rotation]** → Partial unique index makes a double-true state impossible at the DB; perform unset+set in one transaction. The endpoint tolerates "none featured" (empty/404) so the UI degrades gracefully (D3).
- **[Unknown manifest tags rejected → publish friction]** → Acceptable trade-off for taxonomy integrity; surface a clear validation error listing invalid values and the allowed vocabulary. Seed the `use_case` vocabulary with SWE/Product/UX-UI/DevOps (+ existing values) so common tags validate.
- **[Install identifier mismatch (slug vs name)]** → The `featured` endpoint returns both; switching the rendered token is a one-line change if the CLI turns out to require `Name`.
- **[i18n drift]** → New showcase/filter keys and removed team keys must land in both en and fr Transloco files (existing project discipline) to avoid missing-key fallbacks.

## Migration Plan

1. **DB**: EF Core migration adds `is_featured boolean NOT NULL DEFAULT false` to `plugins` + partial unique index `ux_plugins_featured ON plugins(is_featured) WHERE is_featured`. Seed flips one existing plugin to featured.
2. **Backend**: add `IsFeatured` to domain/entity/mappers; add the featured read use case + endpoint; extend `CreatePluginCommand` + publishing repo to persist `plugin_categories`; seed `use_case` vocabulary for the required categories.
3. **Frontend**: add the home featured-plugin port/adapter/facade; rebuild the hero showcase; repaint `--sidebar*` to violet; reorder header controls; remove team-context; add landing category-filter chips; update en/fr i18n.
4. **Deploy** backend + frontend together (frontend depends on the new endpoint; it degrades gracefully if the endpoint is absent).
5. **Rollback**: revert the frontend; the migration is reversible (drop index + column). The publish-tagging change is additive (no destructive migration).

## Open Questions

- Should the showcase also display an `npx @claudeforge/claude-plugin-cli install <slug>` fallback for users without the CLI installed globally, or only the short `claude-plugin install <slug>` form? (Leaning: short form only, with a small "via the ClaudeForge CLI" caption.)
- Final header hue within the 265–290 (blue↔violet) range — a visual judgment to lock during implementation.
- Confirm the canonical `use_case` values for "UX/UI" and "SWE" (slug spelling, e.g. `ux-ui`, `swe` vs `engineering`) against any existing seeded vocabulary to avoid duplicates.
