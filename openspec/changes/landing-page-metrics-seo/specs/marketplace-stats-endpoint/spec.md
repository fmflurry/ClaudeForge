## ADDED Requirements

### Requirement: GET /api/v1/stats returns marketplace aggregates
The system SHALL provide a publicly accessible REST endpoint at `GET /api/v1/stats` that returns aggregated marketplace statistics. The endpoint SHALL return a JSON response with total plugin count, total downloads summed across all plugins, number of unique publishers, and count of plugin categories. The response SHALL be cached for a short duration (5-10 minutes) to avoid excessive database queries.

#### Scenario: Successful stats fetch
- **WHEN** a client requests `GET /api/v1/stats`
- **THEN** the system returns HTTP 200 with JSON containing `totalPlugins` (integer), `totalDownloads` (integer), `publisherCount` (integer), and `categoryCount` (integer)

#### Scenario: Cached response
- **WHEN** the same request is made twice within the cache TTL
- **THEN** the second response is served from cache without querying the database

#### Scenario: Error handling
- **WHEN** the database is unavailable or an unexpected error occurs
- **THEN** the system returns HTTP 500 with RFC7807 problem detail (type, title, detail, instance)

### Requirement: Stats endpoint is documented in OpenAPI schema
The endpoint at `GET /api/v1/stats` SHALL be documented in the project's OpenAPI schema with parameter descriptions, response schema, and example response values.

#### Scenario: OpenAPI generated correctly
- **WHEN** the OpenAPI schema is generated during build
- **THEN** the schema includes endpoint definition with status code 200 response and example values

### Requirement: Stats use-case and port abstraction
The backend SHALL implement the stats retrieval logic as a use-case (application layer) with a port abstraction for the data source. The use-case SHALL accept zero parameters and return an aggregate DTO (Data Transfer Object). A concrete adapter SHALL implement the port using Entity Framework to query the database.

#### Scenario: Use-case returns aggregate DTO
- **WHEN** the stats use-case is invoked
- **THEN** it returns a stats DTO with all four aggregate fields populated

#### Scenario: Adapter queries database correctly
- **WHEN** the infrastructure adapter queries the database
- **THEN** it sums download counts, counts distinct publishers, counts all plugins, and counts distinct categories

#### Scenario: Clean Architecture boundaries observed
- **WHEN** the endpoint receives a request
- **THEN** the request is routed through the application layer (use-case) and never directly queries the database
