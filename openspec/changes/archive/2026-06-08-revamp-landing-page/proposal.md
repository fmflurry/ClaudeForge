## Why

The current landing page and header are visually and conceptually off. The header surfaces a "team" selector for a feature that does not exist (you cannot select a team; teams are merely "multiple users sharing one organization", which we are not building yet), the palette reads flat instead of yellow-dominant with a complementary accent, and there is no compelling, designed call-to-action that shows a newcomer how to actually install a plugin. The result is a marketing surface that confuses (team selection) more than it converts (install a plugin).

This change rebalances the visual identity (yellow-dominant content, a distinct non-yellow secondary header surface), removes the dead "team" concept from the UI, replaces it with a discovery model based on plugin category tags, and adds a designed "How to install a plugin" showcase driven by a rotatable, admin-flagged featured plugin.

## What Changes

- **Palette rebalance.** Content stays yellow-dominant (primary, hue 95). The header background switches off the near-white/yellow-adjacent `--sidebar` surface to a complementary **secondary** (blue/violet) surface so the brand yellow pops against it. New/retuned sidebar tokens for light and dark modes, validated against WCAG 2.1 AA.
- **Remove the team concept from the UI.** **BREAKING** (UI): the header `cf-team-switcher` and the `cf-team-welcome` onboarding overlay are removed from the shell. No team selection, no custom-team naming, no "skip for now" overlay. "Team" is deferred to a future org-membership concept and is not built now.
- **Designed "How to install a plugin" hero showcase.** The existing full-bleed near-black hero slab is **removed** (this is the "weird section block" from point 4 — its near-black `--foreground` background dominated the page and made yellow read as secondary). It is replaced by a yellow-dominant hero: cream/yellow background with the headline + CTAs, and a designed "How to install a plugin" block that showcases one specific plugin. The only dark element is the CLI command rendered in a dark code block (dark used as an accent, not as the page's dominant surface). The frontend fetches the currently-featured plugin's name and composes the install command for display.
- **Rotatable featured plugin (admin-flagged).** A single plugin is marked "featured" via a manual admin flag in the database. Exactly one plugin is featured at a time; flipping the flag rotates which plugin the showcase displays. A read endpoint exposes the featured plugin to the frontend.
- **Category-tag discovery (replaces team).** Plugins are discoverable by category tags (e.g. SWE, Product, UX/UI, DevOps). Uploading a plugin supports assigning **multiple** category tags. The landing/discovery surface exposes these as filters instead of a team selector.
- **Yellow as the dominant surface.** Beyond removing the dark hero slab, the landing page reads yellow/cream-dominant top-to-bottom; dark is reserved for small accents (e.g. the install code block, footer).
- **Relocate header controls.** Move the language switcher and the dark/light theme toggle to sit immediately before the login button in the header.

## Capabilities

### New Capabilities
- `landing-page`: The marketing/discovery landing surface — a yellow-dominant hero built around the "How to install a plugin" featured showcase (replacing the removed dark hero slab), yellow-dominant theming throughout with dark used only as an accent, and category-filter entry points.
- `app-shell-header`: The global application header — brand, primary nav, control ordering (language + theme immediately before login), removal of the team switcher, and the complementary secondary (blue/violet) header surface.
- `featured-plugin`: An admin-flagged, rotatable "featured" plugin — the persistence flag (exactly one featured at a time), the read API exposing it, and the CLI install command surfaced to the frontend.
- `plugin-tagging-and-filters`: Multiple category tags per plugin assignable on upload, and category-based discovery filters (SWE, Product, UX/UI, DevOps, …) on the discovery surfaces.

### Modified Capabilities
<!-- None. openspec/specs/ is empty; all capabilities above are new. -->

## Impact

- **Frontend (Angular)**
  - `shell/shell-layout.component.ts` — remove `cf-team-switcher` + `cf-team-welcome`, reorder lang/theme before auth, apply secondary header surface.
  - `features/team-context/**` — team-context presentation (switcher, welcome overlay) removed from the shell; feature retirement/scope confirmed in design.
  - `features/home/presentation/landing-page.component.ts` — add the featured-install showcase block, remove the stray section, category-filter entry points, palette adjustments.
  - `features/home/**` (domain/application/infrastructure) — new port/adapter/facade to fetch the featured plugin.
  - `features/catalog/**` — surface category filters; verify multi-tag upload path.
  - `theme.css` — retuned/added `--sidebar*` tokens (blue/violet secondary surface) for light + dark, WCAG-validated.
  - i18n (en/fr Transloco) — new keys for the showcase block; removal of team-related keys.
- **Backend (.NET)**
  - `Core/Domain/Plugins/Plugin.cs` — add featured flag to the domain model.
  - `Infrastructure/Persistence/Entities/PluginEntity.cs` + EF Core migration — persist the featured flag (single-featured invariant) and any tag-assignment support not already present.
  - New read endpoint exposing the featured plugin (name + metadata for install-command composition).
- **CLI** — confirm the canonical install command format the showcase will render.
- **Data** — seed/admin path to flip the featured flag for rotation.
