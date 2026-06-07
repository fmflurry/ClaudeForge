## ADDED Requirements

### Requirement: Stats band component displays marketplace metrics
The landing page SHALL include a stats band UI component (e.g., above the "Popular plugins" section) that displays the four marketplace metrics: total plugins, total downloads, number of publishers, and number of categories. Each metric SHALL be displayed as a visual card with a large number, a label, and optionally an icon.

#### Scenario: Stats band renders with live metrics
- **WHEN** stats are successfully loaded
- **THEN** the stats band displays four metric cards with values from the facade (e.g., "2,547 Plugins", "18,392 Downloads", etc.)

#### Scenario: Stats band shows loading state
- **WHEN** stats are being fetched
- **THEN** the stats band displays a loading message (e.g., "Loading marketplace stats…") or skeleton placeholders

#### Scenario: Stats band shows error state
- **WHEN** the stats fetch fails
- **THEN** the stats band displays an error message and offers a retry button or graceful degradation (e.g., "Could not load stats — try refreshing")

#### Scenario: Stats band never blocks page render
- **WHEN** the landing page renders
- **THEN** the stats band loads asynchronously and does not block rendering of other sections (search, featured plugins, footer)

### Requirement: Stats band uses metrics facade
The stats band component SHALL inject the home metrics facade and call `loadStats()` on component init. The component SHALL use signal subscriptions (via @let or computed signals) to reactively update the template when `isLoadingStats`, `stats`, or `statsError` signals change.

#### Scenario: Component injects facade
- **WHEN** the stats band component is instantiated
- **THEN** the home metrics facade is injected via constructor

#### Scenario: Component calls loadStats on init
- **WHEN** ngOnInit lifecycle hook runs
- **THEN** the facade method `loadStats()` is invoked

#### Scenario: Template uses signals reactively
- **WHEN** a metric value changes in the facade
- **THEN** the template automatically re-renders with the new value without manual change detection

### Requirement: Number formatting for metrics display
The stats band component SHALL format large numbers for readability (e.g., 1,000,000 displays as "1M", 18,392 displays as "18.4K", 247 displays as "247"). The formatting logic SHALL match the existing `formatDownloads()` utility in the landing page component.

#### Scenario: Large numbers formatted as millions
- **WHEN** total downloads is 2,500,000
- **THEN** the stats band displays "2.5M Downloads"

#### Scenario: Numbers formatted as thousands
- **WHEN** total plugins is 1,247
- **THEN** the stats band displays "1.2K Plugins"

#### Scenario: Small numbers displayed as-is
- **WHEN** publisher count is 89
- **THEN** the stats band displays "89 Publishers"

### Requirement: Stats band is accessible
The stats band component SHALL have proper ARIA labels, semantic HTML structure, and sufficient color contrast. The loading and error states SHALL have appropriate ARIA roles and live regions to announce state changes to screen readers.

#### Scenario: Metric cards have semantic structure
- **WHEN** the stats band renders
- **THEN** each metric card uses semantic HTML (e.g., `<article>` or `<div role="region">`) with descriptive heading

#### Scenario: Loading state announces to screen readers
- **WHEN** stats are loading
- **THEN** the loading message has `role="status" aria-live="polite"` or equivalent

#### Scenario: Error state announces to screen readers
- **WHEN** stats fetch fails
- **THEN** the error message has `role="alert"` so screen readers announce the error immediately
