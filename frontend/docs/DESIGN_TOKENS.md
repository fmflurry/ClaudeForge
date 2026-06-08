# Design Tokens Reference

This document describes every semantic OKLCH CSS variable defined in
`frontend/src/theme.css`. These tokens are the **single source of truth** for
the yellow-forward ClaudeForge design system (design decision D3/D4).

All token values are in [OKLCH](https://www.w3.org/TR/css-color-4/#the-oklch-notation)
format: `oklch(L C H)` — Lightness (0–1), Chroma (0–0.4 typical), Hue (0–360°).

Contrast ratios were computed using the full OKLCH → OKLab → Linear sRGB →
relative luminance pipeline per CSS Color Level 4 and WCAG 2.1 (not via
eyeballing). Target: 4.5:1 for normal text, 3:1 for large text / UI components
and focus indicators.

---

## Core Semantic Tokens

### `--background` / `--foreground`

The page canvas and default body text.

| | Light | Dark |
|---|---|---|
| `--background` | `oklch(0.99 0.01 95)` | `oklch(0.18 0.01 95)` |
| `--foreground` | `oklch(0.20 0.02 95)` | `oklch(0.97 0.01 95)` |

Contrast ratio: **17.59:1 (light)** / **17.25:1 (dark)** — target 4.5:1 — PASS

---

### `--card` / `--card-foreground`

Elevated card surfaces (e.g., content panels, data cards).

| | Light | Dark |
|---|---|---|
| `--card` | `oklch(1 0 0)` | `oklch(0.22 0.01 95)` |
| `--card-foreground` | `oklch(0.20 0.02 95)` | `oklch(0.97 0.01 95)` |

Contrast ratio: **18.09:1 (light)** / **15.88:1 (dark)** — target 4.5:1 — PASS

---

### `--popover` / `--popover-foreground`

Floating overlay surfaces (dropdowns, tooltips, popovers).

| | Light | Dark |
|---|---|---|
| `--popover` | `oklch(1 0 0)` | `oklch(0.22 0.01 95)` |
| `--popover-foreground` | `oklch(0.20 0.02 95)` | `oklch(0.97 0.01 95)` |

Contrast ratio: **18.09:1 (light)** / **15.88:1 (dark)** — target 4.5:1 — PASS

---

### `--primary` / `--primary-foreground`

The dominant brand color — vivid professional yellow. Used on primary action
buttons, active indicators, and key brand surfaces. Because yellow is luminous,
`--primary-foreground` is near-black to guarantee readability.

| | Light | Dark |
|---|---|---|
| `--primary` | `oklch(0.85 0.17 95)` | `oklch(0.86 0.17 95)` |
| `--primary-foreground` | `oklch(0.20 0.03 95)` | `oklch(0.20 0.03 95)` |

Contrast ratio: **11.46:1 (light)** / **11.84:1 (dark)** — target 4.5:1 — PASS

> Note: `--primary` is intentionally vivid (chroma 0.17). Do not desaturate it
> toward beige — the high lightness + near-black foreground already satisfies AA.

---

### `--secondary` / `--secondary-foreground`

Subdued secondary surfaces — used for secondary buttons, tags, and
less-prominent interactive elements.

| | Light | Dark |
|---|---|---|
| `--secondary` | `oklch(0.96 0.01 95)` | `oklch(0.25 0.01 95)` |
| `--secondary-foreground` | `oklch(0.25 0.02 95)` | `oklch(0.95 0.01 95)` |

Contrast ratio: **14.24:1 (light)** / **13.83:1 (dark)** — target 4.5:1 — PASS

---

### `--muted` / `--muted-foreground`

Low-emphasis surfaces and text — disabled states, placeholder text,
supplementary metadata.

| | Light | Dark |
|---|---|---|
| `--muted` | `oklch(0.96 0.005 95)` | `oklch(0.30 0.01 95)` |
| `--muted-foreground` | `oklch(0.50 0.02 95)` | `oklch(0.70 0.02 95)` |

Contrast ratio: **5.33:1 (light)** / **5.11:1 (dark)** — target 4.5:1 — PASS

---

### `--accent` / `--accent-foreground`

Light yellow-tinted highlight surface — used for hover states on secondary
controls and subtle callout backgrounds.

| | Light | Dark |
|---|---|---|
| `--accent` | `oklch(0.90 0.06 95)` | `oklch(0.88 0.06 95)` |
| `--accent-foreground` | `oklch(0.22 0.03 95)` | `oklch(0.20 0.02 95)` |

Contrast ratio: **12.88:1 (light)** / **12.64:1 (dark)** — target 4.5:1 — PASS

---

### `--destructive` / `--destructive-foreground`

Error and danger states — delete confirmations, error alerts, destructive action buttons.

| | Light | Dark |
|---|---|---|
| `--destructive` | `oklch(0.58 0.22 27)` | `oklch(0.56 0.20 27)` |
| `--destructive-foreground` | `oklch(0.98 0 0)` | `oklch(0.98 0 0)` |

Contrast ratio: **4.52:1 (light)** / **4.86:1 (dark)** — target 4.5:1 — PASS

> Dark mode fix: lightness reduced from 0.62 to 0.56 to meet 4.5:1 (was 3.78:1).

---

### `--border`

Default border color for dividers, inputs, and panel outlines.

| | Light | Dark |
|---|---|---|
| `--border` | `oklch(0.90 0.01 95)` | `oklch(0.30 0.01 95)` |

No foreground pairing required for borders.

---

### `--input`

Background of form input fields (used alongside `--border`).

| | Light | Dark |
|---|---|---|
| `--input` | `oklch(0.90 0.01 95)` | `oklch(0.30 0.01 95)` |

---

### `--ring`

Focus ring color shown on keyboard-focused interactive elements.
Must meet 3:1 contrast against all typical surfaces (background, card, sidebar).

| | Light | Dark |
|---|---|---|
| `--ring` | `oklch(0.64 0.17 95)` | `oklch(0.86 0.17 95)` |

Contrast ratio vs background: **3.24:1 (light)** / **12.30:1 (dark)** — target 3:1 — PASS
Contrast ratio vs card: **3.33:1 (light)** / **11.33:1 (dark)** — PASS
Contrast ratio vs sidebar: **3.06:1 (light)** / **11.33:1 (dark)** — PASS

> Light mode fix: lightness reduced from 0.85 to 0.64 to meet 3:1 on all near-white
> surfaces. Yellow hue family preserved (chroma unchanged at 0.17).

---

## Sidebar Tokens

Sidebar-specific overrides that mirror the core palette with a slightly
differentiated sidebar background.

| Token | Light | Dark |
|---|---|---|
| `--sidebar` | `oklch(0.97 0.005 95)` | `oklch(0.22 0.01 95)` |
| `--sidebar-foreground` | `oklch(0.20 0.02 95)` | `oklch(0.97 0.01 95)` |
| `--sidebar-primary` | `oklch(0.85 0.17 95)` | `oklch(0.86 0.17 95)` |
| `--sidebar-primary-foreground` | `oklch(0.20 0.03 95)` | `oklch(0.20 0.03 95)` |
| `--sidebar-accent` | `oklch(0.90 0.06 95)` | `oklch(0.88 0.06 95)` |
| `--sidebar-accent-foreground` | `oklch(0.22 0.03 95)` | `oklch(0.20 0.02 95)` |
| `--sidebar-border` | `oklch(0.90 0.01 95)` | `oklch(0.30 0.01 95)` |
| `--sidebar-ring` | `oklch(0.64 0.17 95)` | `oklch(0.86 0.17 95)` |

Sidebar contrast ratios match core tokens (sidebar/sidebar-foreground: 16.59:1 / 15.88:1).

---

## Chart Tokens

Color-only tokens used for data visualization. No foreground pairing — legibility
depends on application context and chart library labeling.

| Token | Light | Dark |
|---|---|---|
| `--chart-1` | `oklch(0.646 0.222 41.116)` | `oklch(0.488 0.243 264.376)` |
| `--chart-2` | `oklch(0.6 0.118 184.704)` | `oklch(0.696 0.17 162.48)` |
| `--chart-3` | `oklch(0.398 0.07 227.392)` | `oklch(0.769 0.188 70.08)` |
| `--chart-4` | `oklch(0.828 0.189 84.429)` | `oklch(0.627 0.265 303.9)` |
| `--chart-5` | `oklch(0.769 0.188 70.08)` | `oklch(0.645 0.246 16.439)` |

---

## Utility Token

| Token | Value | Purpose |
|---|---|---|
| `--radius` | `0.625rem` | Base border-radius; used by `--radius-sm/md/lg/xl` |

---

## WCAG Contrast Summary Table

All ratios computed: OKLCH → OKLab → Linear sRGB → relative luminance (CSS Color Level 4).

| Token pair | Light | Dark | Target | Status |
|---|---|---|---|---|
| background / foreground | 17.59:1 | 17.25:1 | 4.5:1 | PASS |
| card / card-foreground | 18.09:1 | 15.88:1 | 4.5:1 | PASS |
| popover / popover-foreground | 18.09:1 | 15.88:1 | 4.5:1 | PASS |
| primary / primary-foreground | 11.46:1 | 11.84:1 | 4.5:1 | PASS |
| secondary / secondary-foreground | 14.24:1 | 13.83:1 | 4.5:1 | PASS |
| muted / muted-foreground | 5.33:1 | 5.11:1 | 4.5:1 | PASS |
| accent / accent-foreground | 12.88:1 | 12.64:1 | 4.5:1 | PASS |
| destructive / destructive-foreground | 4.52:1 | 4.86:1 | 4.5:1 | PASS |
| ring on background (focus) | 3.24:1 | 12.30:1 | 3.0:1 | PASS |
| ring on card (focus) | 3.33:1 | 11.33:1 | 3.0:1 | PASS |
| ring on sidebar (focus) | 3.06:1 | 11.33:1 | 3.0:1 | PASS |

---

## How to Use Tokens in SCSS

Per design decision D3, custom SCSS **must always** reference tokens via
`var(--token-name)`. Never hardcode hex, RGB, HSL, or OKLCH values directly in
component SCSS files.

```scss
// Correct
.page-header {
  background-color: var(--primary);
  color: var(--primary-foreground);
  border-bottom: 1px solid var(--border);
}

// Correct — SCSS features (opacity, calc) are fine on top of tokens
.overlay {
  background-color: oklch(from var(--foreground) l c h / 0.5);
}

// Wrong — hardcoded values break dark mode and bypass the token system
.page-header {
  background-color: #f5c800;
  color: #1a1a1a;
}
```

See also `CONTRIBUTING.md` for the full project guideline.
