# Add Authentication & Authorization Proposal

## Why

The plugin marketplace launched open and anonymous, lowering barriers to sharing and discovery. As adoption grows, teams need to share **private, org-internal plugins** and control who can publish—without losing frictionless anonymous access to **public plugins** that remain free for all. SSO via Google and Microsoft is what enterprises already use; supporting both providers via a generic OIDC layer lowers adoption friction while keeping the architecture extensible for future identity providers. Organizations as explicit, first-class entities (not derived from email domain) give teams fine-grained control over plugin access and authorship attribution.

## What Changes

**Authentication Layer (Backend & Frontend)**
- New OIDC identity integration supporting Google Identity Services (GSI) and Microsoft Entra ID (Azure AD SSO) via a pluggable OIDC handler
- Session and JWT token issuance; current-user endpoint for frontend context
- Web-based sign-in UI (OAuth redirect flow) and sign-out
- Session middleware and authorization policies to enforce access control

**Organizations Model**
- Organizations as first-class entities in the domain (not email-domain derived)
- Users can create organizations and be invited to join them
- Invitations: send, accept, and revoke (email-optional; primarily email-based)
- Org membership determines access to private plugins and publishing permissions

**Plugin Visibility & Access Control**
- New `visibility` flag on plugins: `public` or `private`
- **Public plugins**: downloadable by anyone without authentication (anonymous users unaffected)
- **Private plugins**: downloadable only by authenticated users who are members of the owning organization
- **Publishing**: all plugin uploads now require authentication (users must sign in to publish, public or private)
- Existing published plugins authored anonymously treated as public with no owner org

**CLI Authentication**
- New `claude-plugin login` command: browser-based OAuth flow (PKCE + device fallback)
- Authenticated commands (`publish`, `pull-private`) now send JWT token from local storage (~/.claude-plugins/token.json)
- `claude-plugin logout` to clear stored token

**Deferred Breaking Change**
- **BREAKING (relative to MVP)**: Plugin upload now requires authentication. Existing anonymous uploads stop working; users must authenticate to publish going forward.

## Capabilities

### New Capabilities

- `authentication` — OIDC sign-in (Google + Microsoft), session/JWT token issuance, current-user endpoint, sign-out
- `organizations` — First-class org entities: create, membership management, send/accept/revoke invitations
- `plugin-visibility` — Public/private flag on plugins; access-control rules (public = anonymous access; private = authN + same-org membership; publish = all require authN)
- `cli-auth` — Browser OAuth login (`login`/`logout`), local token storage, authenticated CLI requests for protected operations

### Modified Capabilities

None — the upstream `plugin-upload` and `plugin-download` specs are not yet in `openspec/specs/`; the authorization rules they will obey are captured here and integrated when both changes are applied.

## Impact

**Backend (Add Auth Module)**
- New **Authentication/Identity** module: OIDC handlers (Google, Microsoft), token generation, session management, current-user logic
- New **Organizations** module: org CRUD, membership, invitations, org-scoped access policies
- Authorization middleware: guard endpoints by auth status and org membership
- Database schema additions: `users` (email, provider subject ID, name), `organizations` (owner, created_at), `organization_memberships` (user, org, role), `invitations` (from_user, to_email, org, status); `plugins` field additions: `owner_org_id` (nullable for legacy), `visibility` enum
- OIDC client ID/secret configuration per provider (env vars; fail-fast if missing at startup)

**Frontend (Web UI)**
- Sign-in/sign-out flow: OAuth redirect, OIDC callback handler, session state management
- Auth guard: protect publishing UI, private plugin access
- New org switcher component to select active organization context
- Current-user header showing logged-in user and current org
- Update plugin-list to show visibility and enforce org-membership access for private plugins

**CLI**
- New `login` command: PKCE/device-flow browser OAuth, token→~/.claude-plugins/token.json
- `logout` command: delete stored token
- All authenticated requests (publish, pull-private) include Authorization header with stored JWT
- Graceful fallback for public-plugin pulls (no token required)

**Configuration & Operations**
- Environment variables for OIDC client IDs/secrets (Google, Microsoft); startup validation
- PostgreSQL reused (no new datastore); schema migrations for users, orgs, memberships, invitations
- Docker Compose updated to inject OIDC secrets
- Documentation: OIDC provider setup (Google Cloud Console, Azure Entra), user onboarding for sign-in/org creation, CLI login flow

**Privacy**
- Minimal PII: email, display name, and provider's subject ID (opaque identifier, not reused across providers)
- No browsing history, profile data, or plugin usage tracking beyond what MVP telemetry already collects
- Users can delete account (cascades to orgs owned by them if no other members; clears memberships)

**No New Datastore Required**
- PostgreSQL extended for identity and org data; reuses existing connection pool and schema migration tooling
