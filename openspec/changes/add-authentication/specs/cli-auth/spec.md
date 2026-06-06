# CLI Authentication — OpenSpec

## ADDED Requirements

### Requirement: CLI Login Flow with Browser-Based OAuth

The system SHALL provide a `claude-plugin login` command that initiates a browser-based OAuth sign-in flow (PKCE with loopback callback or device-code fallback), obtains an application JWT token, and stores it securely in the user's local filesystem.

#### Scenario: User runs login command and completes OAuth in browser
- **WHEN** a user runs `claude-plugin login` in the CLI
- **THEN** the CLI SHALL generate a PKCE code_challenge and launch the user's default browser pointing to the authorization endpoint (e.g., `http://localhost:8888/auth/authorize?provider=google&code_challenge=...`)
- **AND** the browser SHALL open to an identity provider sign-in page
- **AND** after the user authenticates, the identity provider SHALL redirect back to a loopback callback server (http://127.0.0.1:PORT) listening on the CLI
- **AND** the CLI SHALL receive the authorization code via the loopback redirect
- **AND** the CLI SHALL exchange the authorization code for a JWT from the backend
- **AND** the CLI SHALL store the JWT securely in `~/.claude-plugins/credentials.json` with file permissions 0600 (readable/writable by owner only)
- **AND** the CLI SHALL print a success message showing the authenticated user email and organization

#### Scenario: User selects provider during login (Google or Microsoft)
- **WHEN** a user runs `claude-plugin login` and is not given a default provider via flag (e.g., `--provider google`)
- **THEN** the CLI SHALL prompt the user to choose a provider: Google or Microsoft
- **AND** the authorization URL SHALL include the selected provider as a query parameter

#### Scenario: Browser is not available; device-code flow fallback
- **WHEN** a user runs `claude-plugin login` and no browser can be automatically launched (e.g., headless server)
- **THEN** the CLI SHALL fall back to the device-code flow and display a URL and code to the user (e.g., "Go to https://www.example.com/device and enter code ABC123")
- **AND** the CLI SHALL poll the backend's device-code token endpoint until the user completes authentication in a browser
- **AND** after successful authentication, the CLI SHALL store the JWT as in the primary flow

#### Scenario: User cancels authentication in the browser
- **WHEN** a user closes the browser or clicks "Cancel" during the OIDC sign-in flow
- **THEN** the loopback server on the CLI SHALL not receive an authorization code
- **AND** the CLI SHALL time out after a configurable window (default 5 minutes) and exit with a non-zero exit code and message "Authentication cancelled or timed out"
- **AND** no token SHALL be stored

#### Scenario: Authorization code exchange fails
- **WHEN** the CLI exchanges the authorization code at the backend but the backend returns an error (e.g., invalid code or client credentials misconfigured)
- **THEN** the CLI SHALL print an error message including the backend's error detail
- **AND** the CLI SHALL exit with a non-zero exit code
- **AND** no token SHALL be stored

---

### Requirement: Token Storage & File Security

The system SHALL store the JWT token securely in the user's home directory with restricted file permissions, ensuring only the owner can read or modify the token file.

#### Scenario: Token is stored with secure permissions
- **WHEN** the `claude-plugin login` command successfully obtains a JWT
- **THEN** the CLI SHALL write the JWT to `~/.claude-plugins/credentials.json` (or an equivalent secrets directory)
- **AND** the file permissions SHALL be 0600 (owner read/write only; no group or world access)
- **AND** if the file already exists, it SHALL be overwritten (previous token replaced)

#### Scenario: Token file permissions are verified on each CLI invocation
- **WHEN** the CLI loads the stored token at the start of an authenticated command
- **THEN** the CLI SHALL verify the file permissions are 0600
- **AND** if permissions are overly permissive (e.g., world-readable), the CLI SHALL warn the user and refuse to use the token until permissions are corrected

#### Scenario: Credentials directory does not exist
- **WHEN** `claude-plugin login` runs and `~/.claude-plugins/` does not exist
- **THEN** the CLI SHALL create the directory with permissions 0700 (owner rwx only)

#### Scenario: Token file contains sensitive data and is not logged
- **WHEN** the CLI loads and uses the stored token
- **THEN** the CLI SHALL never print the token to stdout, logs, or error messages
- **AND** debug/verbose logs SHALL redact the token (e.g., show only the first 8 characters and `****`)

---

### Requirement: Authenticated CLI Requests with Token Attachment

The system SHALL attach the stored JWT token to authenticated CLI requests (e.g., `publish`, `pull-private`) in the Authorization header and gracefully handle requests that do not require authentication.

#### Scenario: User runs authenticated command with valid stored token
- **WHEN** a user runs `claude-plugin publish [plugin]` and a valid, unexpired token exists in `~/.claude-plugins/credentials.json`
- **THEN** the CLI SHALL read the token from the credentials file
- **AND** the CLI SHALL attach the token in the `Authorization: Bearer <JWT>` header of the HTTP request to the backend
- **AND** the backend SHALL process the request with the authenticated user's context

#### Scenario: User runs authenticated command without a stored token
- **WHEN** a user runs `claude-plugin publish [plugin]` and no token exists in `~/.claude-plugins/credentials.json`
- **THEN** the CLI SHALL print an error message: "Not authenticated. Please run 'claude-plugin login' first."
- **AND** the CLI SHALL exit with a non-zero exit code and not attempt to publish

#### Scenario: Public plugin download does not require authentication
- **WHEN** a user runs `claude-plugin pull public-plugin-id` for a public plugin
- **THEN** the CLI SHALL make the request without an Authorization header even if a token is stored
- **AND** the backend SHALL serve the public plugin content regardless of authentication

#### Scenario: Private plugin pull requires authentication and membership
- **WHEN** a user runs `claude-plugin pull private-plugin-id` for a private plugin
- **THEN** the CLI SHALL attach the stored token in the Authorization header
- **AND** the backend SHALL verify the user is a member of the owning organization
- **AND** if the user is not a member, the backend SHALL return HTTP 403 Forbidden

#### Scenario: Stored token is expired; user is prompted to re-login
- **WHEN** the CLI attaches an expired JWT to a request
- **THEN** the backend SHALL return HTTP 401 Unauthorized
- **AND** the CLI SHALL catch the 401 response and print: "Session expired. Please run 'claude-plugin login' to re-authenticate."
- **AND** the CLI SHALL exit with a non-zero exit code

---

### Requirement: CLI Logout & Token Removal

The system SHALL provide a `claude-plugin logout` command that removes the stored token from the user's local filesystem and prevents further authenticated requests until `login` is run again.

#### Scenario: User runs logout command successfully
- **WHEN** a user runs `claude-plugin logout`
- **THEN** the CLI SHALL locate and delete the token file at `~/.claude-plugins/credentials.json`
- **AND** the CLI SHALL print a success message: "Logged out successfully."
- **AND** the CLI SHALL exit with status code 0

#### Scenario: Logout when no token is stored
- **WHEN** a user runs `claude-plugin logout` and no token file exists at `~/.claude-plugins/credentials.json`
- **THEN** the CLI SHALL print a message: "No active session found. Already logged out."
- **AND** the CLI SHALL exit with status code 0 (idempotent; no error)

#### Scenario: Subsequent requests after logout require authentication
- **WHEN** a user runs `claude-plugin logout` and then immediately runs `claude-plugin publish [plugin]`
- **THEN** the CLI SHALL find no stored token and return the "Not authenticated" error as per token attachment requirements

---

### Requirement: CLI Whoami — Display Current Identity

The system SHALL provide a `claude-plugin whoami` command that displays the authenticated user's identity and current organization context.

#### Scenario: Authenticated user runs whoami
- **WHEN** a user runs `claude-plugin whoami` and a valid stored token exists
- **THEN** the CLI SHALL send the token to the backend's `/auth/me` endpoint
- **AND** the backend SHALL return the user's email, display name, and list of organizations
- **AND** the CLI SHALL print the user's identity in a human-readable format (e.g., "Logged in as alice@example.com" and "Current organization: ACME Corp")

#### Scenario: Unauthenticated user runs whoami
- **WHEN** a user runs `claude-plugin whoami` and no token is stored or the token is expired
- **THEN** the CLI SHALL print a message: "Not authenticated. Run 'claude-plugin login' to sign in."
- **AND** the CLI SHALL exit with a non-zero exit code

#### Scenario: Whoami displays current organization if user is in multiple organizations
- **WHEN** a user is a member of multiple organizations and runs `claude-plugin whoami`
- **THEN** the CLI SHALL show all organizations the user belongs to
- **AND** the CLI SHALL indicate which organization is currently active (or the default)

---

### Requirement: Token Expiry Detection & Re-Login Prompting

The system SHALL detect expired tokens, prevent their use for authenticated requests, and gracefully prompt the user to re-authenticate.

#### Scenario: Expired token is detected before making a request
- **WHEN** the CLI loads a stored token and checks its expiry (`exp` claim) before making an authenticated request
- **THEN** if the token has already expired, the CLI SHALL NOT attempt to send it to the backend
- **AND** the CLI SHALL print: "Session expired. Please run 'claude-plugin login' to re-authenticate."
- **AND** the CLI SHALL exit with a non-zero exit code

#### Scenario: Backend rejects request due to expired token
- **WHEN** the CLI sends a request with a token that the backend determines is expired
- **THEN** the backend SHALL return HTTP 401 Unauthorized with error detail "Token expired"
- **AND** the CLI SHALL catch the 401 response and prompt the user to re-login
- **AND** the CLI SHALL offer to automatically run `claude-plugin login` (optional; otherwise user must run it manually)

#### Scenario: Token nearing expiry is automatically refreshed (optional enhancement)
- **WHEN** the CLI detects a token is nearing expiry (e.g., within 24 hours of expiration) before an authenticated command
- **THEN** the CLI MAY attempt to refresh the token using a refresh-token endpoint (out of scope for initial release; this scenario documents future enhancement)
