## 1. Backend — Featured plugin (data + domain)

- [x] 1.1 Add `IsFeatured` (bool, default false) to `PluginEntity` (`backend/ClaudeForge.Infrastructure/Persistence/Entities/PluginEntity.cs`).
- [x] 1.2 Add `IsFeatured` to the `Plugin` domain record (`backend/ClaudeForge.Core/Domain/Plugins/Plugin.cs`) and update any entity↔domain mappers.
- [x] 1.3 Configure the column in `MarketplaceDbContext` and add a **partial unique index** `WHERE is_featured = true` so at most one plugin can be featured.
- [x] 1.4 Create the EF Core migration (`is_featured` column + partial unique index, default false) and verify the down migration drops both. _(Migration `20260608160542_AddPluginIsFeatured`.)_
- [x] 1.5 Update `PluginDataSeeder` to flag exactly one existing plugin as featured. _(Featured: "TypeScript Linter".)_

## 2. Backend — Featured plugin read API

- [x] 2.1 Add a `GetFeaturedPlugin` read use case in the `PluginCatalog` module returning the featured plugin summary (name, slug, latest version) or an explicit "none" result.
- [x] 2.2 Add the repository port + EF adapter query to fetch the single featured plugin (include latest version).
- [x] 2.3 Add the `GET /api/v1/plugins/featured` endpoint mapping the use case; return 404 when no plugin is featured. _(Path is `/api/v1/plugins/featured` to match the existing API versioning convention.)_
- [x] 2.4 Unit/integration tests: returns the featured plugin; signals absence when none featured; never returns a non-featured plugin; single-featured invariant holds after rotation.

## 3. Backend — Persist category tags on publish

- [x] 3.1 Add `Types`, `Languages`, `UseCaseTags` arrays to `CreatePluginCommand` (`PluginPublishing/Ports`).
- [x] 3.2 In the publishing use case, resolve each (dimension, value) against the `categories` controlled vocabulary; reject unknown values with a validation error naming the invalid value(s). _(`UnknownCategoryTagException`, HTTP 400.)_
- [x] 3.3 In the publishing repository adapter, write the resolved `plugin_categories` rows in the same transaction as plugin creation.
- [x] 3.4 Wire the publish endpoint/handler to pass manifest tag arrays into `CreatePluginCommand`.
- [x] 3.5 Seed/confirm the `use_case` vocabulary includes the plugin-kind values (SWE/Engineering, Product, UX/UI, DevOps); reconcile slug spelling to avoid duplicates. _(New slugs: `engineering`, `product`, `ux-ui`; `devops` pre-existing.)_
- [x] 3.6 Tests: multiple use-case tags persisted; known tags accepted; unknown tag rejected with validation error.

## 4. Frontend — Theme (violet header surface)

- [x] 4.1 Repaint `--sidebar*` tokens to a violet secondary surface (hue 285) for light and dark modes in `frontend/src/theme.css`.
- [x] 4.2 Compute and record WCAG contrast ratios (text ≥ 4.5:1, ring ≥ 3:1) for header text/login/focus-ring on the new surface in both modes; update the contrast table. _(Light text 7.13:1 / ring 5.76:1; dark text 12.87:1 / ring 9.20:1 — all pass.)_
- [x] 4.3 Verify header button/link overrides in `shell-layout.component.ts` still read correctly against the violet surface. _(Focus outlines switched to `--sidebar-ring`.)_

## 5. Frontend — Header restructure (team removal + control order)

- [x] 5.1 Grep importers of `features/team-context` to confirm nothing outside the shell depends on it (decide delete vs unwire-only per design D7). _(External consumers found: `app.config.ts`, `core/context/context-registry.ts`, `dashboard.facade.ts` → unwire-only.)_
- [x] 5.2 Remove `cf-team-switcher` and `cf-team-welcome` from `shell-layout.component.ts`; drop the `TeamContextStore`/`TeamContextFacade` providers and the `needsInit()` gating.
- [x] 5.3 Delete the `features/team-context` directory and its specs. _(Superseded by Group 10 — now doing the FULL deletion incl. external consumers per user request.)_
- [x] 5.4 Reorder header controls into a right-aligned cluster: org-switcher (left of cluster) → language → theme → login; move `margin-left:auto` onto the lang/theme/auth cluster.
- [x] 5.5 Update shell-layout spec/tests for the new structure (no team switcher, no overlay, control order, org switcher present).

## 6. Frontend — Featured plugin slice (home feature)

- [x] 6.1 Add `domain/models/featured-plugin.model.ts` and abstract `domain/ports/featured-plugin.port.ts`.
- [x] 6.2 Add `infrastructure/adapter/featured-plugin-http.adapter.ts` calling `GET /api/v1/plugins/featured`; map DTO → model; handle the "none featured" response (404/error → null).
- [x] 6.3 Add `application/facades/featured-plugin.facade.ts` exposing a signal for the featured plugin (component consumes the facade only).
- [x] 6.4 Provide the port→adapter binding in the home feature providers. _(Provided in `app.config.ts`.)_
- [x] 6.5 Tests: adapter maps response and handles absence; facade exposes featured/empty state.

## 7. Frontend — Landing page redesign

- [x] 7.1 Remove the dark `.lp-hero` slab; rebuild the hero on a yellow/cream surface with headline + CTAs.
- [x] 7.2 Add the "How to install a plugin" showcase card; render `claude-plugin install <slug>` in a dark code block (the only dark element in the hero) with a copy affordance.
- [x] 7.3 Wire the showcase to the `FeaturedPluginFacade`; render the generic fallback when none featured / fetch fails (no broken state).
- [x] 7.4 Ensure the landing page is yellow-dominant top-to-bottom; confine remaining dark to small accents (code block, footer).
- [x] 7.5 Add category-filter entry-point chips sourced from `getCategories()` (use-case dimension) that navigate to the catalog with the filter preselected via query params. _(Also wired `CatalogPageComponent` to consume the `useCases` query param so results actually filter.)_
- [x] 7.6 Update landing-page spec/tests for the new hero, showcase, fallback, and category entry points.

## 8. i18n

- [x] 8.1 Add en + fr Transloco keys for the install-showcase (heading, caption, copy button, fallback) and category entry points.
- [x] 8.2 Remove team-related Transloco keys (en + fr) left orphaned by the team removal. _(13 `team-context.*` keys removed from each.)_

## 9. Verification

- [x] 9.1 Backend: `dotnet build` clean (0 errors); 630 unit tests pass (independently re-run).
- [x] 9.2 Frontend: `npx tsc --noEmit` clean; full `ng test` suite run with Node 22.22.3 → exit 0 (all specs pass, incl. updated catalog/landing/shell specs).
- [x] 9.3 Migration applied to a fresh Postgres (`dotnet ef database update`, no pending model changes); booted the API live → `GET /api/v1/plugins/featured` returns the seeded "TypeScript Linter" (HTTP 200, correct payload) and HTTP 404 when none featured; partial unique index `ux_plugins_featured` blocks a second featured plugin (verified via SQL: 2nd `is_featured=true` insert → `duplicate key value violates unique constraint`).
- [ ] 9.4 Manual/visual browser check (yellow-dominant landing, navy header, lang/theme before login, no team UI, install command renders; light + dark). _(Backend live-verified; browser visual left for final eyeball.)_

## 10. Full team-context removal (follow-up)

- [x] 10.1 Remove `groupsByTeam` + `TeamContextFacade` from `dashboard.facade.ts` and delete the now-unused `groupPluginsByTeam` rule (`dashboard-grouping.rules.ts`) + its tests _(verified dead: not rendered anywhere)_.
- [x] 10.2 Remove team-context providers from `app.config.ts` (`TeamContextStoragePort` / `LocalStorageTeamContextAdapter`).
- [x] 10.3 Remove the `TEAM_CONTEXT` entry from `core/context/context-registry.ts` (confirm no remaining references).
- [x] 10.4 Delete the shared team storage port + adapters: `shared/domain/ports/team-context-storage.port.ts`, `shared/infrastructure/storage/local-storage-team-context.adapter.ts`, `shared/infrastructure/storage/in-memory-team-context.adapter.ts` (+ specs).
- [x] 10.5 Delete the `features/team-context` directory and its specs.
- [x] 10.6 Verify no dangling imports/references to team-context remain; `tsc` + tests clean.

## 11. Dashboard polish

- [x] 11.1 Add a page header/title + a stats summary row (installed count, updates-available count) to `dashboard-page.component.ts`, sourced from the facade (add an `updatesAvailableCount` computed if needed).
- [x] 11.2 Frame the installed-plugins table in a card and apply the design-system tokens (yellow/navy); wire the currently-dead search input to filter installed plugins.
- [x] 11.3 Update dashboard spec/tests for the header, stats, and working search.

## 12. Navy header + Marketplace wording + install page

- [x] 12.1 Retune `--sidebar*` tokens in `theme.css` to a darker navy (deeper, less light; hue ~255–265) for light + dark; re-validate WCAG and update the contrast table.
- [x] 12.2 i18n: change the hero title so "place de marché" is not translated — use "Marketplace" in both en and fr (`home.hero-title`).
- [x] 12.3 Create a new `/install` page (route + standalone component + en/fr i18n): how to install the ClaudeForge CLI, how to install plugins, and intro info about plugins.
- [x] 12.4 Make the showcase caption "via the ClaudeForge CLI / via le CLI de ClaudeForge" a clickable link (routerLink) to `/install`.
- [x] 12.5 Update landing-page spec/tests for the caption link.

## 13. Verification (follow-up)

- [x] 13.1 Frontend `npx tsc --noEmit` clean; `ng test` (Node 22.22.3) green.
- [ ] 13.2 Visual: navy header, "Marketplace" in fr, caption links to /install, polished dashboard, no team UI anywhere.
