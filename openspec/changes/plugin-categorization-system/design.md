# Plugin Categorization System — Design

## Context

Current marketplace uses flat multi-tag categorization across three dimensions (type, language, use-case). All dimensions treated equally with OR-within/AND-across filtering. This creates two problems:

1. **No discovery spine.** Users see 11+ unorganized tags. They can't browse — they must already know what they're looking for.
2. **Mechanism-first, not intent-first.** Current "kind" axis (SWE/Engineering, Product, UX/UI, DevOps) is too coarse and doesn't map to what users actually want ("I need code review", "I need Postgres integration").

See `proposal.md` for full motivation and capability inventory.

## Goals / Non-Goals

**Goals:**
- Single primary category per plugin — the spine of marketplace browsing
- Structural tags as secondary filter (what mechanisms does this plugin use?)
- Free-form keywords for cross-cutting discovery ("show me everything TypeScript")
- Migration path for all existing plugins without data loss
- Backward-compatible filter API during transition

**Non-Goals:**
- Auto-categorization via AI (future enhancement, not this change)
- Plugin rating/review system changes
- Redesigning the plugin detail page — just the browsing/filter UI
- Changes to plugin manifest format beyond categorization fields
- Supporting multiple domain categories per plugin (single-select is deliberate)

## Decisions

### D1: Three-layer taxonomy stored as `category` + `keywords` in marketplace.json

**Decision:** marketplace.json gets a required `category` string field (Layer 1) and a `keywords` array (Layer 2 + 3 merged).

**Why not separate structural vs free-form arrays?**
- Simpler schema — one array field instead of two
- Structural tags are just keywords with known values; no reason to split storage
- Filter logic distinguishes them at query time via controlled vocabulary lookup
- Existing plugins already have a `keywords` field — additive change, not replacement

**Schema shape:**
```json
{
  "category": "code-intelligence",
  "keywords": ["skill", "typescript", "code-review", "ast"]
}
```

Old `type`, `use-case`, `kind` fields removed. Validation enforces `category` is from controlled vocabulary.

### D2: Controlled vocabularies for domain + structural, free-form for rest

**Decision:** Domain category and structural type validated against fixed lists. Everything else is free-form keywords.

**Domain vocabulary (11 values):** code-intelligence, language-framework, external-service, workflow-orchestration, security, testing-qa, devops-infrastructure, data-analytics, documentation, productivity-utilities, domain-vertical.

**Structural vocabulary (5 values):** skill, subagent, command, hook, mcp-server.

**Why controlled for these?**
- Domain categories drive the primary filter — must be finite, predictable, browsable
- Structural types map to plugin component types — these are architectural facts, not opinions
- Keywords are discovery/SEO — free-form lets authors describe precisely what their plugin does

**Validation rule:** publish fails if `category` not in domain vocabulary. `keywords` entries matching structural vocabulary are accepted. No validation on other keyword values.

### D3: Migration via static mapping with fallback category

**Decision:** Each existing tag maps to a domain category via a static lookup table. Plugins with ambiguous mappings get the nearest category. Migration script rewrites marketplace.json files.

**Mapping approach:**
- `use-case: "code-review"` → `code-intelligence`
- `kind: "DevOps"` → `devops-infrastructure`
- `type: "skill"` → structural keyword
- Ambiguous cases (e.g. `use-case: "testing"`) → `testing-qa` (closest domain)
- Unknown/missing tags → `productivity-utilities` (safe default)

**Why static, not heuristic?**
- Finite number of existing plugins (~dozen) — manual review feasible
- Predictable output — no surprises in migration
- Can be reviewed in PR before merge
- AI-based categorization deferred to post-launch (see Non-Goals)

### D4: Filter UI — domain sidebar, structural checkboxes, keyword search

**Decision:** Three-panel filter layout:
1. **Domain categories** — sidebar list, single-select (radio behavior). Always visible. Shows plugin count per category.
2. **Structural types** — checkbox group below sidebar. Multi-select. Only shows values present in current domain selection.
3. **Keyword search** — text input at top. Searches across keyword array. Combines with active filters.

**Why single-select for domain?**
- Domain is the spine — users pick one bucket to browse
- Multi-select defeats the purpose of a primary organizing axis
- Keeps UI simple — one decision at a time

**Filter combination logic:**
- Domain: exact match (AND with other filters)
- Structural: OR within selection, AND with domain
- Keywords: OR match, AND with domain + structural
- Changed from current: OR-within/AND-across all dimensions → domain-first hierarchy

### D5: Backward-compatible filter API during migration

**Decision:** Old filter params (`type`, `use-case`, `kind`) accepted but deprecated. New params: `category` (string), `structural` (array), `keywords` (string). Both sets work during transition. Old params return deprecation header.

**Why keep old params?**
- Third-party integrations may depend on old API
- Migration guide needs time to propagate
- Breaking change deferred to next major version

## Risks / Trade-offs

### Risk: Migration mapping errors
Some existing plugins may be miscategorized. Mitigation: migration script produces a report showing before/after for manual review. Plugin authors can override post-migration.

### Risk: Domain vocabulary too coarse or too fine
11 categories may not fit all plugins perfectly. Mitigation: `productivity-utilities` acts as catch-all. Domain vocabulary can be extended in future without breaking existing plugins (additive change).

### Risk: Breaking deep-links and bookmarks
Old filtered URLs (`/marketplace?use-case=code-review`) break. Mitigation: redirect map from old params to new. Document in migration guide.

### Risk: Third-party plugin authors slow to update
External plugins with old schema rejected on next publish. Mitigation: deprecation warning period (2 versions) before hard rejection. Clear error messages pointing to migration guide.

### Trade-off: Simplicity vs flexibility
Single domain category per plugin is less flexible than multi-tag. Deliberate choice — browsability requires a spine. Plugins that genuinely span domains (e.g. "security + testing") pick the primary one and use keywords for the rest.

### Trade-off: Controlled vocabulary maintenance
Adding new domain categories requires spec change. Acceptable — domain categories should be stable and well-considered, not frequently changed.
