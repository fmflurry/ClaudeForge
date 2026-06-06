# Plugin Visibility Specification

## ADDED Requirements

### Requirement: Plugin Visibility Attribute

Each plugin SHALL have a visibility attribute with value `public` or `private`. Public visibility is the default for plugins without an owning organization. Private plugins SHALL have an `owner_org_id` reference.

#### Scenario: Create public plugin
- **WHEN** an authenticated user publishes a plugin and specifies public visibility
- **THEN** the plugin is persisted with visibility set to public
- **AND** owner_org_id is null or omitted
- **AND** a 201 Created response is returned

#### Scenario: Create private plugin with organization
- **WHEN** an authenticated user publishes a plugin and specifies private visibility with an owning organization
- **THEN** the plugin is persisted with visibility set to private
- **AND** owner_org_id is set to the specified organization
- **AND** a 201 Created response is returned

#### Scenario: Default visibility is public
- **WHEN** a user publishes a plugin without explicitly specifying visibility
- **THEN** the plugin defaults to public visibility
- **AND** a 201 Created response is returned

### Requirement: Public Plugin Pull Without Authentication

An unauthenticated (anonymous) user SHALL download a public plugin without providing credentials.

#### Scenario: Anonymous user downloads public plugin
- **WHEN** an unauthenticated user requests to download a public plugin
- **THEN** the plugin is downloaded and returned with 200 OK
- **AND** no authentication credentials are required

#### Scenario: Authenticated user downloads public plugin
- **WHEN** an authenticated user requests to download a public plugin
- **THEN** the plugin is downloaded and returned with 200 OK
- **AND** authentication does not affect the response

### Requirement: Private Plugin Pull Requires Organization Membership

An authenticated user SHALL download a private plugin only if they are a member of the owning organization. Anonymous users and non-members SHALL be denied access (404 Not Found).

#### Scenario: Member downloads private plugin
- **WHEN** an authenticated user who is a member of the owning organization requests a private plugin
- **THEN** the plugin is downloaded and returned with 200 OK

#### Scenario: Non-member downloads private plugin
- **WHEN** an authenticated user who is not a member of the owning organization requests a private plugin
- **THEN** a 404 Not Found response is returned
- **AND** no information about the plugin's existence is disclosed

#### Scenario: Anonymous user downloads private plugin
- **WHEN** an unauthenticated user requests to download a private plugin
- **THEN** a 401 Unauthorized response is returned
- **AND** the user is prompted to authenticate

#### Scenario: Member of different organization downloads private plugin
- **WHEN** a user who is a member of a different organization requests a private plugin
- **THEN** a 404 Not Found response is returned

### Requirement: Plugin Listing and Search Filtering by Visibility

Plugin listings and search results SHALL include all public plugins. Private plugins SHALL appear only to authenticated members of the owning organization.

#### Scenario: Anonymous user sees only public plugins in listing
- **WHEN** an unauthenticated user requests the plugin catalog
- **THEN** only plugins with public visibility are returned
- **AND** private plugins are excluded from results

#### Scenario: Organization member sees private plugins in own org
- **WHEN** an authenticated user requests the plugin catalog
- **THEN** public plugins are included
- **AND** private plugins owned by their organization are included
- **AND** private plugins from other organizations are excluded

#### Scenario: User searches catalog for plugins
- **WHEN** a user searches the plugin catalog with a query term
- **THEN** public plugins matching the query are returned
- **AND** for authenticated users, private plugins matching the query in their organizations are included
- **AND** for unauthenticated users, only public plugins matching the query are returned

#### Scenario: Member of multiple organizations sees relevant private plugins
- **WHEN** a user who belongs to multiple organizations searches the catalog
- **THEN** public plugins and private plugins from all their organizations are included in results

### Requirement: Plugin Publishing Requires Authentication

Publishing (uploading) any plugin, whether public or private, SHALL require the user to be authenticated.

#### Scenario: Authenticated user publishes public plugin
- **WHEN** an authenticated user submits a plugin upload with public visibility
- **THEN** the plugin is published
- **AND** a 201 Created response is returned

#### Scenario: Authenticated user publishes private plugin
- **WHEN** an authenticated user submits a plugin upload with private visibility and owning organization
- **THEN** the plugin is published
- **AND** a 201 Created response is returned

#### Scenario: Unauthenticated user attempts to publish
- **WHEN** an unauthenticated user attempts to upload a plugin
- **THEN** a 401 Unauthorized response is returned
- **AND** the plugin is not published

#### Scenario: Breaking change—legacy anonymous upload blocked
- **WHEN** a user (authenticated or not) attempts to publish a plugin without providing authentication context
- **THEN** a 401 Unauthorized response is returned
- **AND** the system no longer accepts anonymous uploads

### Requirement: Private Plugin Publishing Requires Organization Membership

Publishing a private plugin SHALL require the publisher to be an authenticated member of the designated owning organization.

#### Scenario: Organization member publishes private plugin for their org
- **WHEN** an authenticated member of an organization publishes a private plugin with that organization as owner
- **THEN** the plugin is published
- **AND** owner_org_id is set correctly
- **AND** a 201 Created response is returned

#### Scenario: Non-member attempts to publish private plugin for org
- **WHEN** an authenticated user who is not a member of the organization attempts to publish a private plugin with that organization as owner
- **THEN** a 403 Forbidden response is returned
- **AND** the plugin is not published

#### Scenario: User publishes private plugin without specified organization
- **WHEN** an authenticated user publishes a plugin with private visibility but without specifying an owning organization
- **THEN** a 400 Bad Request response is returned
- **AND** an error message indicates the organization is required for private plugins

### Requirement: Change Plugin Visibility

An authorized user (organization owner/admin or the publisher) SHALL change a plugin's visibility.

#### Scenario: Organization owner changes public to private
- **WHEN** an organization owner changes their public plugin to private with their organization as owner
- **THEN** the visibility is updated to private
- **AND** owner_org_id is set
- **AND** a 200 OK response is returned

#### Scenario: Organization owner changes private to public
- **WHEN** an organization owner changes a private plugin to public
- **THEN** the visibility is updated to public
- **AND** owner_org_id is cleared (set to null)
- **AND** a 200 OK response is returned

#### Scenario: Non-owner attempts to change visibility
- **WHEN** an authenticated user who is not the publisher or organization owner attempts to change visibility
- **THEN** a 403 Forbidden response is returned
- **AND** visibility is not changed

#### Scenario: Unauthenticated user attempts to change visibility
- **WHEN** an unauthenticated user attempts to change plugin visibility
- **THEN** a 401 Unauthorized response is returned

#### Scenario: Admin changes private plugin visibility
- **WHEN** an organization admin changes a private plugin's visibility
- **THEN** the visibility is updated
- **AND** a 200 OK response is returned
