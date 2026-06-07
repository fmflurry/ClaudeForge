## ADDED Requirements

### Requirement: Home metrics domain model
The frontend SHALL define a domain model for marketplace metrics with properties: totalPlugins (number), totalDownloads (number), publisherCount (number), categoryCount (number). The model SHALL be immutable (TypeScript readonly properties) and validated at the HTTP adapter boundary.

#### Scenario: Domain model created with readonly properties
- **WHEN** the metrics domain is defined
- **THEN** all properties are readonly and the model cannot be mutated after creation

#### Scenario: Model validation at HTTP boundary
- **WHEN** the HTTP adapter receives stats from the backend
- **THEN** it validates the shape and types before creating the domain model

### Requirement: Home metrics port (abstraction)
The frontend home feature SHALL define a port interface that abstracts data fetching for marketplace metrics. The port interface SHALL declare a method `getStats(): Promise<MarketplaceMetrics>` and may have error states. The domain SHALL never directly depend on HTTP; HTTP adapters implement the port.

#### Scenario: Port interface defines contract
- **WHEN** the port is defined
- **THEN** it declares the single method `getStats()` returning a Promise of metrics

#### Scenario: Components depend on port, not HTTP
- **WHEN** a component needs metrics
- **THEN** it receives the port via dependency injection, never directly calls HttpClient

### Requirement: Home metrics HTTP adapter
The frontend SHALL implement a concrete HTTP adapter for the metrics port. The adapter SHALL call `GET /api/v1/stats` and map the response to the domain model. On HTTP errors, the adapter SHALL return an error state; on success, return the metrics.

#### Scenario: HTTP adapter fetches stats
- **WHEN** the adapter's `getStats()` is called
- **THEN** it makes a GET request to `/api/v1/stats` and returns the mapped domain model

#### Scenario: Error handling in adapter
- **WHEN** the HTTP request fails (network error, server error)
- **THEN** the adapter returns an error state (e.g., throws or returns an error result)

### Requirement: Home metrics facade and signal-based store
The frontend home feature SHALL provide a facade that wraps the metrics port and exposes signals for the UI: `isLoadingStats()`, `stats()` (returning metrics or null), and `statsError()` (error message or undefined). The facade SHALL use Angular's signal-based store pattern (no RxJS subscriptions). Initial state is `isLoadingStats=true`, `stats=null`, `statsError=undefined`.

#### Scenario: Facade initializes with loading state
- **WHEN** the facade is created
- **THEN** isLoadingStats signal is true, stats is null, statsError is undefined

#### Scenario: Facade fetches stats on demand
- **WHEN** the facade method `loadStats()` is called
- **THEN** it sets isLoadingStats to true, calls the port, and updates stats/statsError signals based on result

#### Scenario: Facade updates signals immutably
- **WHEN** the stats are loaded successfully
- **THEN** the stats signal is updated with the new metrics without mutating the previous value

#### Scenario: Error state in facade
- **WHEN** the HTTP fetch fails
- **THEN** isLoadingStats becomes false, statsError is populated with an error message, stats remains null

### Requirement: Home metrics facade is injectable singleton
The home metrics facade SHALL be provided as an injectable service in the home feature module, with singleton scope (one instance per application lifetime). Components in the home feature SHALL inject the facade and call `loadStats()` to fetch metrics on init.

#### Scenario: Facade is injectable
- **WHEN** a component constructor includes HomMetricsFacade as a dependency
- **THEN** Angular dependency injection provides a singleton instance

#### Scenario: Component calls loadStats on init
- **WHEN** a component implementing OnInit injects the facade
- **THEN** it calls `loadStats()` in the ngOnInit lifecycle hook
