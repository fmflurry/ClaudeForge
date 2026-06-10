## Why

The landing page currently wastes vertical and horizontal space, causing important content such as the footer to fall below the initial viewport and creating duplicate or disabled calls to action. The header and hero also need clearer navigation labels and auth-aware controls so visitors can scan and act faster.

## What Changes

- Fit the landing page within the screen height so the footer remains visible without excessive scrolling.
- Preserve the header logo.
- Rename the header `Catalogue` navigation item to `Plugins`.
- Remove the header `Rechercher` navigation item.
- Show `Tableau de bord` only when the user is logged in.
- Keep the `Documentation` navigation item.
- Rework the landing body to better use side deadspace.
- Keep the hero title on one line instead of wrapping.
- Remove the disabled hero login button.
- Remove the duplicate lower search area because the browse plugins CTA already provides plugin discovery access.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `landing-page`: Update landing layout requirements for initial viewport fit, horizontal space usage, one-line hero title, removal of disabled hero login CTA, and removal of duplicate lower search area.
- `app-shell-header`: Update header navigation requirements for `Plugins`, removal of `Rechercher`, auth-gated `Tableau de bord`, preserved logo, and preserved `Documentation`.

## Impact

- Affects landing/home page layout and hero CTA rendering.
- Affects application shell header navigation labels and auth-aware visibility.
- No API, dependency, or data model changes expected.
