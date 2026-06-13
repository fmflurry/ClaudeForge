# Authentication — OpenSpec

## ADDED Requirements

### Requirement: OIDC Provider Sign-In Initiation

The system SHALL support initiating sign-in workflows with Google Identity Services and Microsoft Entra ID via OIDC. A client SHALL select a provider, be redirected to the identity provider's authorization endpoint with application credentials and PKCE code challenge, and the identity provider SHALL redirect the user back to the application's callback endpoint after authentication.

#### Scenario: User initiates sign-in with Google
- **WHEN** a user selects "Sign in with Google" on the front-end
- **THEN** the frontend SHALL redirect to an authorization endpoint (e.g., `/auth/authorize?provider=google`) that exchanges the provider name for a Google OIDC authorization URL with PKCE code_challenge
- **AND** the browser SHALL navigate to Google's authorization endpoint
- **AND** after user grants consent, Google SHALL redirect back to the application's callback URL with an authorization code

#### Scenario: User initiates sign-in with Microsoft
- **WHEN** a user selects "Sign in with Microsoft" on the front-end
- **THEN** the frontend SHALL redirect to an authorization endpoint (e.g., `/auth/authorize?provider=microsoft`) that exchanges the provider name for a Microsoft Entra ID OIDC authorization URL with PKCE code_challenge
- **AND** the browser SHALL navigate to Microsoft's authorization endpoint
- **AND** after user grants consent, Microsoft SHALL redirect back to the application's callback URL with an authorization code

#### Scenario: User selects an unsupported provider
- **WHEN** a user attempts to initiate sign-in with an unknown provider
- **THEN** the authorization endpoint SHALL reject the request with HTTP 400 Bad Request and an error message indicating unsupported provider

#### Scenario: OIDC client credentials are missing at startup
- **WHEN** the backend starts and required OIDC client IDs or secrets are missing from environment variables
- **THEN** the application SHALL fail fast with a clear error message and refuse to start

---

### Requirement: OIDC Callback Handling & Token Issuance

The system SHALL accept the OIDC callback from the identity provider, validate the authorization code and PKCE verifier, exchange the code for an ID token, validate the ID token's signature and claims, and issue an application session token (JWT) to the authenticated user.

#### Scenario: Valid authorization code is exchanged for ID token
- **WHEN** the callback endpoint receives a valid authorization code and PKCE verifier from the identity provider
- **THEN** the backend SHALL exchange the code for an ID token from the provider's token endpoint
- **AND** the backend SHALL validate the ID token signature using the provider's public key
- **AND** the backend SHALL verify the token has not expired and contains required claims (sub, email, name)

#### Scenario: Backend issues session JWT after successful ID token validation
- **WHEN** the ID token is validated successfully
- **THEN** the backend SHALL issue an application JWT (access token) containing user identity, expiry (configurable; default 7 days), and sign it with the application's private key
- **AND** the backend SHALL return the JWT to the frontend (via response body or secure HTTP-only cookie, depending on frontend architecture)

#### Scenario: Tampered authorization code is rejected
- **WHEN** the callback endpoint receives a modified or forged authorization code
- **THEN** the identity provider's token endpoint SHALL reject it, and the backend SHALL return HTTP 401 Unauthorized to the frontend

#### Scenario: Authorization code has expired
- **WHEN** the callback endpoint receives an authorization code that exceeds the provider's expiry window (typically 10 minutes)
- **THEN** the identity provider's token endpoint SHALL reject it, and the backend SHALL return HTTP 401 Unauthorized to the frontend

#### Scenario: PKCE verifier does not match the code challenge
- **WHEN** the callback endpoint receives a PKCE verifier that does not produce the code_challenge originally sent to the provider
- **THEN** the provider's token endpoint SHALL reject the exchange, and the backend SHALL return HTTP 401 Unauthorized to the frontend

---

### Requirement: User Provisioning & Account Linking

The system SHALL create a new user record on first sign-in (keyed by provider + subject ID) and update the record on subsequent sign-ins. Users signing in with different providers but the same verified email address SHALL be linked to the same user account.

#### Scenario: New user signs in for the first time with Google
- **WHEN** a user signs in with Google and no record exists for the Google subject ID
- **THEN** the backend SHALL create a new user record with the verified email, display name from the ID token, provider identifier (Google), and provider subject ID
- **AND** the backend SHALL return the user's new user ID in the session JWT

#### Scenario: Existing user signs in again with the same provider
- **WHEN** a user signs in with Google and a record already exists for that Google subject ID
- **THEN** the backend SHALL retrieve the existing user record and update display name and email if the ID token provides different values
- **AND** the backend SHALL return the user's user ID in the session JWT without creating a duplicate account

#### Scenario: User signs in with a different provider but the same verified email
- **WHEN** a user first signs in with Google (email `alice@example.com`), then later signs in with Microsoft (same email `alice@example.com`)
- **THEN** the backend SHALL recognize the verified email match and link the Microsoft provider account to the existing user record
- **AND** the user SHALL have a single account associated with both provider identities

#### Scenario: Multiple distinct users share the same email domain but are different people
- **WHEN** one user signs in with Google and another with Microsoft, both claiming `alice@example.com` but with different provider subject IDs
- **THEN** the system SHALL treat them as a single user (email-based linking takes precedence)
- **AND** the backend SHALL document this behavior and consider additional verification steps for high-security deployments (out of scope for MVP)

---

### Requirement: Current User Endpoint

The system SHALL provide an authenticated endpoint that returns the identity and session information of the currently authenticated user.

#### Scenario: Authenticated user requests current user endpoint
- **WHEN** a frontend client sends a valid JWT in the Authorization header to `GET /auth/me`
- **THEN** the backend SHALL validate the JWT signature, expiry, and claims
- **AND** the backend SHALL return HTTP 200 with a JSON payload containing user ID, email, display name, and list of organization memberships

#### Scenario: Unauthenticated request to current user endpoint
- **WHEN** a client sends a request to `GET /auth/me` without a JWT or with an invalid/expired token
- **THEN** the backend SHALL return HTTP 401 Unauthorized with an error message

#### Scenario: Valid JWT with tampered payload is rejected
- **WHEN** a client modifies the JWT payload (e.g., changing the user ID) and the signature no longer matches
- **THEN** the backend SHALL reject the token as invalid and return HTTP 401 Unauthorized

---

### Requirement: Sign-Out & Token Invalidation

The system SHALL provide an endpoint to sign out the user and invalidate the session token, preventing further use of that token for authenticated requests.

#### Scenario: User signs out successfully
- **WHEN** an authenticated user sends a POST request to `POST /auth/signout` with a valid JWT
- **THEN** the backend SHALL validate the JWT
- **AND** the backend SHALL add the token to a revocation list (or blacklist) to prevent its reuse
- **AND** the backend SHALL return HTTP 200 OK
- **AND** the frontend SHALL clear any cached session state and redirect to the public home page

#### Scenario: Sign-out request without authentication
- **WHEN** a client sends a POST request to `POST /auth/signout` without a JWT or with an expired token
- **THEN** the backend SHALL return HTTP 401 Unauthorized (user already not authenticated, but explicit failure is clear)

#### Scenario: Revoked token is rejected on subsequent requests
- **WHEN** a user signs out and then attempts to use the revoked JWT for another authenticated request
- **THEN** the backend SHALL check the revocation list, find the token listed, and return HTTP 401 Unauthorized

---

### Requirement: Token Validation & Expiry Enforcement

The system SHALL validate JWT tokens on every authenticated request, ensuring the signature is correct, the token has not expired, and the claims are well-formed.

#### Scenario: Valid unexpired token is accepted
- **WHEN** a frontend client sends a request with a valid, unexpired JWT in the Authorization header
- **THEN** the backend SHALL verify the signature using the application's public key
- **AND** the backend SHALL verify the token's `exp` claim is greater than the current time
- **AND** the backend SHALL allow the request to proceed

#### Scenario: Expired token is rejected
- **WHEN** a frontend client sends a request with a JWT whose `exp` claim is in the past
- **THEN** the backend SHALL reject the token and return HTTP 401 Unauthorized with an error message indicating token expiry

#### Scenario: Token signed with incorrect key is rejected
- **WHEN** a frontend client sends a request with a JWT signed with a different private key than the application's current key
- **THEN** the backend SHALL fail signature verification and return HTTP 401 Unauthorized

#### Scenario: Malformed JWT (missing parts or invalid encoding) is rejected
- **WHEN** a frontend client sends a request with a malformed JWT (e.g., missing payload or signature)
- **THEN** the backend SHALL return HTTP 401 Unauthorized with an error message indicating malformed token

---

### Requirement: Protected Resource Access Control

The system SHALL reject unauthenticated requests to protected resources with HTTP 401 Unauthorized and prevent access to resources the authenticated user is not authorized for.

#### Scenario: Unauthenticated user requests a protected resource
- **WHEN** a client requests a protected resource (e.g., private plugin details) without a JWT
- **THEN** the backend SHALL return HTTP 401 Unauthorized

#### Scenario: Authenticated user without sufficient permissions requests a protected resource
- **WHEN** a user authenticated but not a member of the owning organization requests a private plugin
- **THEN** the backend SHALL return HTTP 403 Forbidden with an error message indicating insufficient permissions

#### Scenario: Authenticated user with correct permissions accesses protected resource
- **WHEN** an authenticated user who is a member of the owning organization requests a private plugin
- **THEN** the backend SHALL return HTTP 200 with the plugin details
