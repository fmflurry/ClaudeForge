# Design: landing-footer-sticky-howto-styling

## Context
Landing page component (`landing-page.component.ts`) contains inline styles for all sections. Current footer uses static positioning. "Comment ça marche" (How it works) section uses `.lp-how` class with muted background.

## Goals / Non-Goals

**Goals:**
- Make footer sticky at viewport bottom
- Add yellow-ish background + spacing to "Comment ça marche" section border
- Minimal CSS changes

**Non-Goals:**
- Restructure component hierarchy
- Add new dependencies
- Modify non-CSS behavior

## Decisions

### Footer Sticky Positioning
**Decision:** Use `position: fixed` with `bottom: 0` and `width: 100%`
**Rationale:** 
- Simple, reliable approach for sticky footer
- Already in proposal
- Works with existing layout structure
- No need for flexbox/grid wrapper changes

**Alternatives considered:**
- `position: sticky` → Requires parent container height, more complex
- Grid layout with footer row → Requires structural HTML changes

### "Comment ça marche" Section Styling
**Decision:** Modify `.lp-how` class with:
- `background-color: var(--secondary)` (existing yellow/cream variable)
- Add `padding` for spacing
- Add `border-radius` for visual separation

**Rationale:**
- Uses existing design system variable (`--secondary` = yellow/cream)
- Minimal change to existing class
- Maintains consistency with hero section

**Alternatives considered:**
- New CSS variable → Unnecessary, `--secondary` already exists
- Inline style → Against maintainability

## Risks / Trade-offs

**[Risk]** Fixed footer may overlap content on short pages → **Mitigation:** Add `padding-bottom` to main content container equal to footer height

**[Risk]** Yellow background may not match user expectation → **Mitigation:** Use existing `--secondary` variable which is already the hero's yellow/cream color

## Migration Plan
1. Modify `.lp-footer` styles: add `position: fixed`, `bottom: 0`, `width: 100%`
2. Add `padding-bottom` to `.lp-supporting` (main content wrapper) to prevent footer overlap
3. Modify `.lp-how` styles: add `background-color: var(--secondary)`, add `padding`, add `border-radius`
4. Test responsive behavior

## Open Questions
- Exact padding values for spacing (default to 1rem)
- Exact border-radius value (default to 0.5rem)
