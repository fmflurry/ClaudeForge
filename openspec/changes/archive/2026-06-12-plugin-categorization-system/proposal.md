# Plugin Categorization System

## Why

Current marketplace tagging treats all dimensions equally — type, language, use-case are flat tags. Users don't think in dimensions. They think "I need code review" or "I need Postgres integration." The existing system has no single primary axis to organize discovery. Result: overwhelming filter UI, no clear mental model, hard to browse.

Two problems:
1. **No spine.** Flat tags force users to know the taxonomy before searching. 11+ categories with no hierarchy = cognitive overload.
2. **Mechanism-first, not intent-first.** Current "kind" axis (SWE/Engineering, Product, UX/UI, DevOps) is too coarse and doesn't map to what users actually want to do.

Need: domain-first primary category (the spine), structural tags as secondary filters, free-form keywords for cross-cutting concerns.

## What Changes

Replace the flat multi-tag model with a three-layer categorization:

**Layer 1 — Domain category (single-select, required):** Each plugin gets ONE primary functional category. This is the spine of marketplace browsing. Categories: code-intelligence, language-framework, external-service, workflow-orchestration, security, testing-qa, devops-infrastructure, data-analytics, documentation, productivity-utilities, domain-vertical.

**Layer 2 — Structural type (multi-select, optional tags):** Tag which plugin components are present: skill, subagent, command, hook, mcp-server. Users who know what mechanism they need can filter here.

**Layer 3 — Free-form keywords (multi-select, optional):** Language names, integration targets, domain specifics. Powers search and cross-cutting filters (e.g. "show me everything TypeScript").

**marketplace.json schema:** `category` field becomes the Layer 1 value (validated against controlled vocabulary). `keywords` array holds Layer 2 + Layer 3 values (structural tags + free-form keywords in one list). Existing plugins get migrated: old use-case/kind tags mapped to nearest domain category.

## Capabilities

### New Capabilities

- `categorization-schema`: Defines the three-layer taxonomy, controlled vocabularies for domain category and structural type, validation rules for marketplace.json, and keyword format constraints.
- `category-migration`: Maps existing plugin tags (use-case/kind/type dimensions) to new domain categories. Provides fallback logic for ambiguous mappings. Generates migration script for marketplace registry.
- `category-filter-ui`: Marketplace browsing UI with domain category as primary filter (sidebar/nav), structural type as secondary filter (checkboxes), keyword search as tertiary. Combines: single-select domain + multi-select structure + keyword search.

### Modified Capabilities

- `plugin-tagging-and-filters` — **BREAKING**: Domain category becomes required single-select (was optional multi-tag). Structural tags replace old "type" dimension. Filter logic changes from OR-within/AND-across flat dimensions to domain-first hierarchy.

## Impact

- **marketplace.json schema**: `category` field semantics change. `keywords` now carries structural + free-form tags. Old fields (`type`, `use-case`, `kind`) removed.
- **Plugin publishing**: Validation adds domain category requirement. Existing published plugins need re-tagging.
- **Discovery/filtering API**: Query params change. Old dimension filters deprecated.
- **UI**: Filter panel rebuilt. Old bookmarks/deep-links to filtered views break.
- **External**: Third-party plugin authors must update manifests. Migration guide needed.
