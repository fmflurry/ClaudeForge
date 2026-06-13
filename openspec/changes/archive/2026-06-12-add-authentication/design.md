# Add Authentication & Authorization Design

## Context

ClaudeForge marketplace launched open and anonymous, enabling frictionless plugin sharing and discovery. As adoption grows, teams require private, organization-controlled plugins alongside public-plugin anonymity. This design adds a greenfield authentication layer—OIDC-integrated Google and Microsoft single sign-on, explicit Organizations with invitations, and plugin visibility control—without replacing the existing plugin-marketplace modules or PostgreSQL infrastructure. The integration preserves anonymous access to public plugins while enabling authenticated publish, org-membership enforcement, and private-plugin access control.

**Stack Constraints**:
- Backend: .NET 8 Clean Architecture (Application/Core/Infrastructure), Hexagonal ports/adapters
- Frontend: Angular 22 standalone, signal-based store, facade pattern
- CLI: Node.js/TypeScript
- Database: PostgreSQL (additive schema, EF migrations)
- Global Rules: no `any` type, facades only in components, immutability, validate at boundaries, fail-fast on missing secrets

**Scope**: This change integrates with (does not replace) plugin-marketplace modules and preserves backwards-compatible anonymous plugin download and search.

---

## Goals / Non-Goals

### Goals
1. **Authentication**: OAuth 2.0 via Google and Microsoft OIDC providers, backend-issued JWT tokens, SPA + CLI token transport
2. **Organizations**: First-class Org entities with membership, invitation state machine, and role-based access
3. **Plugin Visibility**: Public/private flag controlling anonymous vs member-only download and all-authenticated publish
4. **CLI Authentication**: Loopback PKCE login, persistent refresh token, authenticated CLI commands for publish and private pulls

### Non-Goals
- Legacy "claim ownership" flow for anonymous plugins (future follow-up)
- OS-keychain integration for CLI token storage (future hardening)
- Popup-based login (full-page redirect only)
- Fine-grained per-plugin ACLs beyond organization membership
- Additional SSO providers beyond Google and Microsoft; extensibility seam only

---

## Decisions

### 1. Token Strategy

**Decision**: Backend issues its own token after OIDC sign-in; IdP id_token consumed once at callback, never re-presented. Two-token Bearer scheme serving SPA + CLI identically:

**(a) Access Token** = short-lived JWT (default 15 min), `Authorization: Bearer`, RS256 asymmetric, validated statelessly via JWKS at `/.well-known/jwks.json`.

**(b) Refresh Token** = long-lived (30 days), opaque random string, stored server-side hashed (SHA-256) in `refresh_tokens` table (jti, user_id, expires_at, revoked_at, rotated_to), one-time-use rotation with reuse-detection that revokes the chain.

**Access JWT Claims**: `sub` (our user UUID), `email`, `name`, `provider`, `iss`, `aud`, `iat`, `exp`, `jti`. Org memberships are NOT embedded—fetched per-request from DB by authz layer (avoids stale-membership bugs, keeps tokens small).

**Revocation/Sign-out**: Access tokens short-lived, not denylisted on hot path. Sign-out revokes refresh token. Optional small denylist of `jti` (Postgres `revoked_jti` table or Redis, TTL = remaining token life) checked only when present for immediate-revocation scenario.

**Reconcile "7 days" spec**: 7-day = refresh window; access = 15 min (separate concerns).

**Why**: One scheme serves SPA+CLI (only storage differs); RS256 lets API verify with public key + CLI pre-validate exp locally; DB-fetched memberships take effect immediately.

**Alternatives Considered**:
- Session cookie (useless for CLI, CSRF concerns) → rejected
- HS256 with shared secret (requires secret on every validator, difficult rotation) → rejected
- Memberships embedded in JWT (stale on membership change) → rejected
- Stateless no-refresh scheme (cannot honor immediate sign-out) → rejected

**Risk → Mitigation**:
- Per-request membership read latency → 30–60s in-memory cache (user_id → org_ids) invalidated on membership mutation
- Refresh token theft → one-time-use rotation + reuse-detection; hashed SHA-256 at rest
- Denylist growth (revoked_jti) → TTL expires entries equal to remaining access-token lifetime

---

### 2. OIDC Integration

**Decision**: Generic outgoing port `IIdentityProviderPort` (Core) with `GoogleIdentityProviderAdapter` + `MicrosoftIdentityProviderAdapter`, selected via `IIdentityProviderRegistry.Resolve(name)` (unknown provider → `UnsupportedProviderException` → HTTP 400).

**Port Signature**:
- `BuildAuthorizationUrl(provider, codeChallenge, state, redirectUri)` → URL
- `ExchangeCode(provider, code, codeVerifier, redirectUri)` → `RawIdToken`
- `ValidateIdToken(provider, rawIdToken)` → `VerifiedIdentity{subject, email, emailVerified, name}`

**Libraries**: Use `Microsoft.IdentityModel.Protocols.OpenIdConnect` + `Microsoft.IdentityModel.Tokens` for discovery, JWKS caching, and id_token validation. Hand-roll thin authorize-URL build + code→token POST per adapter (full PKCE/loopback control). Use `Microsoft.AspNetCore.Authentication.JwtBearer` to validate OUR issued JWT (not IdP token). Do NOT use the `AddOpenIdConnect` cookie middleware (it owns redirect/callback, assumes cookie session—wrong for API+CLI).

**JWKS Caching**: via `ConfigurationManager<OpenIdConnectConfiguration>` per provider (24-hour refresh + `RefreshOnSignatureFailure`).

**Discovery URLs**:
- Google: `accounts.google.com/.well-known/openid-configuration`
- Microsoft: `login.microsoftonline.com/<tenant>/v2.0/.well-known/openid-configuration` (tenant 'common' configurable)

**Account Linking**: Keyed by `(provider, subject)`, linked by VERIFIED email only. Unverified email → no auto-link. Existing users with verified email on different providers linked via `user_identities` row.

**Why**: Port + registry = extensible without call-site changes. Validation primitives keep hexagon clean. `ConfigurationManager` is standard, self-refreshing JWKS cache.

**Alternatives Considered**:
- Full cookie middleware (couples to cookie sessions, incompatible with CLI) → rejected
- Manual JWKS + validate (reinvents, subtle bugs) → rejected
- Link by unverified email (account takeover vector) → rejected

**Risk → Mitigation**:
- Verified-email takeover → trust only `email_verified==true`; config flag to disable cross-provider linking for high-security deployments
- JWKS rotation mid-request → `RefreshOnSignatureFailure` re-fetches on validation failure
- PKCE/state CSRF → store state + code_verifier server-side (short TTL) for web; CLI holds verifier in-process

---

### 3. Backend Module / Layering

**Decision**: Two new reflection-discovered modules (Application + Core + Infrastructure):

**Identity Module** (`Module/Identity/`): OIDC initiate/callback, token issue/refresh, `/auth/me`, sign-out, device-code.

**Ports**:
- `IIdentityProviderPort` (OIDC exchange)
- `ITokenIssuerPort` (JWT generation, RS256 keys)
- `IUserStorePort` (user CRUD)
- `IRefreshTokenStorePort` (token store + rotation)

**Organizations Module** (`Module/Organizations/`): Org CRUD, membership, invitations state machine, roles.

**Ports**:
- `IOrganizationStorePort`
- `IMembershipStorePort`
- `IInvitationStorePort`
- `IInvitationEmailPort`

**Cross-Cutting Authorization Seam** (Shared Kernel, not inside either module):
- `ICurrentUser` (UserId?, IsAuthenticated, Email—populated per-request from JWT)
- `IOrgMembershipQueryPort` (GetOrgIdsForUser(userId), IsMember(userId, orgId, minRole?)—returns primitives Guid[]/bool only)
- `IPluginAccessPolicy` (pure domain service: given ICurrentUser + plugin (visibility, owner_org_id) + caller org-id set → Allow|NotFound|Unauthenticated; encodes 404-not-401 rule)

**Existing Module Integration**: PluginDistribution, PluginPublishing, PluginCatalog, PluginSearch consume via interfaces only:
- Download use-case injects `ICurrentUser` + `IOrgMembershipQueryPort` + `IPluginAccessPolicy` (private+anon→401; private+non-member→404)
- Publish requires `IsAuthenticated` (401) + `IsMember(owner_org_id)` for private plugins (403); sets `owner_org_id` + `author`
- Catalog/Search take optional `viewerOrgIds` arg injected into query (anonymous = empty set)

**AuthN** at framework layer (JwtBearer), **AuthZ** at use-case level.

**Why**: Mirrors established module pattern. Shared-kernel contracts are only cross-module touchpoints (preserves NetArchTest isolation). Pure `IPluginAccessPolicy` centralizes 401/403/404 logic and is unit-testable. `viewerOrgIds` is additive (no existing query rewrite).

**Alternatives Considered**:
- One combined Auth+Orgs module (violates cohesion) → rejected
- All checks in middleware (re-loads resources, inefficient) → rejected
- Memberships in JWT (stale) → rejected

**Risk → Mitigation**:
- Port leaking Org types → return primitives only; NetArchTest rule forbidding Organizations namespace refs from Catalog/Search/Distribution/Publishing Core
- Forgotten checks on new endpoints → wide integration tests per protected endpoint asserting 401/403/404

---

### 4. Data Model

**Decision**: New tables + additive plugins columns, UUID PKs, TIMESTAMPTZ, forward-only EF migration.

```sql
users(
  id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  email TEXT NOT NULL,
  email_normalized TEXT NOT NULL UNIQUE,
  display_name TEXT NOT NULL,
  created_at TIMESTAMPTZ DEFAULT now(),
  updated_at TIMESTAMPTZ DEFAULT now(),
  deleted_at TIMESTAMPTZ NULL
);

user_identities(
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL REFERENCES users ON DELETE CASCADE,
  provider TEXT NOT NULL,
  subject TEXT NOT NULL,
  created_at TIMESTAMPTZ DEFAULT now(),
  UNIQUE(provider, subject)
);

organizations(
  id UUID PRIMARY KEY,
  name TEXT NOT NULL,
  name_normalized TEXT NOT NULL UNIQUE,
  slug TEXT NOT NULL UNIQUE,
  created_by UUID NOT NULL REFERENCES users,
  created_at TIMESTAMPTZ DEFAULT now()
);

organization_members(
  org_id UUID NOT NULL REFERENCES organizations ON DELETE CASCADE,
  user_id UUID NOT NULL REFERENCES users ON DELETE CASCADE,
  role TEXT NOT NULL /* owner|admin|member */,
  created_at TIMESTAMPTZ DEFAULT now(),
  PRIMARY KEY(org_id, user_id)
);

organization_invitations(
  id UUID PRIMARY KEY,
  org_id UUID NOT NULL REFERENCES organizations ON DELETE CASCADE,
  email_normalized TEXT NOT NULL,
  invited_by UUID NOT NULL REFERENCES users,
  role TEXT NOT NULL DEFAULT 'member',
  status TEXT NOT NULL DEFAULT 'pending' /* pending|accepted|revoked|expired */,
  token TEXT NOT NULL UNIQUE,
  created_at TIMESTAMPTZ DEFAULT now(),
  expires_at TIMESTAMPTZ NOT NULL,
  accepted_at TIMESTAMPTZ NULL,
  revoked_at TIMESTAMPTZ NULL,
  UNIQUE(org_id, email_normalized) WHERE status='pending'
);

refresh_tokens(
  id UUID PRIMARY KEY,
  user_id UUID NOT NULL REFERENCES users ON DELETE CASCADE,
  token_hash CHAR(64) NOT NULL UNIQUE,
  expires_at TIMESTAMPTZ NOT NULL,
  revoked_at TIMESTAMPTZ NULL,
  rotated_to UUID NULL REFERENCES refresh_tokens,
  created_at TIMESTAMPTZ DEFAULT now()
);

org_audit_log(
  id UUID PRIMARY KEY,
  org_id UUID NOT NULL REFERENCES organizations ON DELETE CASCADE,
  actor_user_id UUID NOT NULL REFERENCES users,
  action TEXT NOT NULL,
  target TEXT NOT NULL,
  created_at TIMESTAMPTZ DEFAULT now()
);

ALTER TABLE plugins ADD visibility TEXT NOT NULL DEFAULT 'public';
ALTER TABLE plugins ADD owner_org_id UUID NULL REFERENCES organizations;
ALTER TABLE plugins ADD owner_user_id UUID NULL REFERENCES users;
ALTER TABLE plugins ADD CONSTRAINT chk_visibility_owner
  CHECK(visibility='public' OR owner_org_id IS NOT NULL);
CREATE INDEX idx_plugins_visibility_org ON plugins(visibility, owner_org_id);
```

**Invitation State Machine**: `pending` → `accepted` (creates membership), `pending` → `revoked` (owner/admin), `pending` → `expired` (lazy/sweep). Accept on non-pending → HTTP 410 Gone.

**Visibility Filtering**: Every catalog/search/list query takes optional `viewerOrgIds UUID[]` and appends `WHERE plugins.visibility='public' OR plugins.owner_org_id = ANY(@viewerOrgIds)` (empty array = public only). Single-plugin fetch returns row only if it passes same predicate; private fail → no row → 404 (non-disclosure); anonymous-on-private download → 401.

**Why**: `user_identities` separate from users = clean multi-provider linking. `name_normalized UNIQUE` + partial-unique pending invite enforce HTTP 409 at DB. Visibility predicate in SQL guarantees no leak into counts/pagination/ranking. CHECK makes "private requires org" a DB invariant.

**Alternatives Considered**:
- Domain-derived orgs (fixed constraint, inflexible) → rejected
- App-side post-filter (breaks pagination, leaks via counts) → rejected
- Hard-delete users (breaks FKs, audit loss) → soft-delete instead

**Risk → Mitigation**:
- Ownerless legacy rows (no owner_org_id) → intended (public, ownerless); CHECK allows NULL when visibility='public'
- ANY(@viewerOrgIds) performance → index `(visibility, owner_org_id)` + small org counts per user
- Sole-owner removal → use-case counts owners, rejects with HTTP 400

---

### 5. Authorization Enforcement

**Decision**: Three tiers, authZ at use-case/domain layer, authN at framework.

**(1) JwtBearer Authentication** (Framework): Authenticates token only → ClaimsPrincipal → `ICurrentUser`. Endpoint policies:
- **Anonymous-allowed**: public listing/search/download (no `[Authorize]`)
- **Authn-required-flat**: POST upload, `/auth/me`, all `/orgs/*` mutations (`[Authorize]` policy `RequireAuthenticatedUser`, returns HTTP 401)
- **Authn + Resource-scoped**: private download, private publish, org member-only (`[Authorize]` for 401, then use-case checks for 403/404)

**(2) Resource-Scoped AuthZ** (Use-Case Layer): Depends on resource ownership data middleware lacks. Uses `IPluginAccessPolicy` + `IOrgMembershipQueryPort`:
- private + anon → 401
- private + authenticated non-member → 404 (non-disclosure)
- private publish by non-member → 403
- org op wrong role → 403

**(3) Search/List Filtering** (Query Layer): Via `viewerOrgIds` inclusion filter; private plugins simply absent from result set.

**Org Role Checks**: Via `IOrgMembershipQueryPort.IsMember(userId, orgId, minRole?)`.

**Why**: 404-not-403 rule is resource-data-dependent and varies per operation. Framework `[Authorize]` handles binary token gate (401) cleanly. Single pure policy makes matrix testable and identical across modules.

**Alternatives Considered**:
- Pure ASP.NET resource-based authorization (handler must load plugin; 404-not-403 awkward) → rejected but available for org-role checks
- All-in-middleware (inefficient, couples to framework) → rejected
- UI-only filtering (catastrophic security failure) → rejected

**Risk → Mitigation**:
- Inconsistent enforcement → centralize in policy + port; wide integration tests per endpoint
- 403-vs-404 confusion → documented rule (plugin read/download = 404 for non-members; write/org-view = 403); encoded in policy enum
- Timing oracle → both paths follow same query + policy logic

---

### 6. Frontend (Angular 22)

**Decision**: New `auth` and `organizations` domains, components touch facades only.

**Login Flow**: Full-page REDIRECT (not popup) for both providers:
- Component → `AuthFacade.login(provider)`
- Backend `/auth/authorize?provider=...` (backend builds PKCE URL + stores state)
- IdP → SPA callback `/auth/callback?code=...&state=...`
- `AuthFacade.completeLogin` → backend `/auth/token` → `{access, refresh}`

**State Management**: `AuthStore` extends `BaseStore` holding `ResourceState<CurrentUser>` + token.
- `AuthFacade` exposes `currentUser`, `isAuthenticated`, `activeOrgId` signals

**Token Storage**:
- **Access token** IN MEMORY (signal, re-acquired via refresh on reload)
- **Refresh token** in HTTP-only SameSite=Strict Secure cookie scoped to `/auth/refresh` (SPA never holds long-lived secret in JS)

**HTTP Interceptor**: Attaches Bearer access to API calls (skips public catalog/search/download + IdP URLs). On HTTP 401 calls `/auth/refresh` once + retries; on failure clears store + routes to login (single-flight refresh).

**Route Guards**:
- `FunctionalAuthGuard` for publish/private/org routes
- `OrgMemberGuard` for org-scoped operations

**Organization Switcher**: `OrgContextFacade` exposes `organizations` + `activeOrg` signals. Switching updates signal-driven catalog/search. Cross-domain via Context Registry (not direct injection).

**Guarded Actions**: `@if(isAuthenticated())`-gated; server-enforced.

**Sign-out**: `AuthFacade.logout()` → `/auth/signout` (revoke refresh, clear cookie) → clear store → home.

**Why**: Redirect robust across browsers/mobile + GSI/Microsoft cookie policies. PKCE + state server-side. Access-in-memory + refresh-in-httponly-cookie = XSS-resilient best practice. Facade + signal store mandated by rules.

**Alternatives Considered**:
- Popup login (fragile with cookie policies) → rejected
- Both tokens in localStorage (XSS → full takeover) → rejected
- NgRx (violates facade+signal rule) → rejected

**Risk → Mitigation**:
- In-memory access lost on reload → silent refresh on bootstrap (check cookie + fetch new access)
- Refresh-cookie CSRF → SameSite=Strict + custom header double-submit; refresh mints only (no mutation)
- Interceptor refresh storms → single-flight shared observable

---

### 7. CLI Auth

**Decision**: Loopback PKCE primary (`claude-plugin login [--provider google|microsoft]`):
1. CLI generates verifier/challenge
2. Starts ephemeral `127.0.0.1:<random-port>` server
3. Opens browser to `/auth/authorize?provider=...&code_challenge=...&redirect_uri=http://127.0.0.1:<port>/callback`
4. Receives code on loopback
5. POSTs `/auth/token` (same endpoint as SPA) with `code` + `verifier` → `{access, refresh}`

**Device-Code Fallback** (headless): `/auth/device/code` + `/auth/device/token`; display URL+code, poll.

**Token Storage**: `~/.claude-plugins/credentials.json` perms 0600 (dir 0700):
```json
{
  "access": "...",
  "refresh": "...",
  "expiresAt": "2026-06-07T...",
  "user": "user@example.com",
  "provider": "google"
}
```
Verify perms each run; warn + refuse if looser. Never log token (redact in output).

**Authenticated Commands** (publish, private pull):
- Read file, check expiry locally first
- If expired, attempt `/auth/refresh`
- On failure: "Session expired. Please run 'claude-plugin login'" (exit non-zero)

**Public Pull**: Sends NO Authorization header even if token exists.

**`whoami`**: GET `/auth/me` prints email + orgs + active org.

**`logout`**: Revoke refresh (best-effort) + delete `credentials.json`; idempotent ("Already logged out" exit 0).

**Active Org**: `~/.claude-plugins/config.json` (`activeOrg`); `--org` per-command override.

**Why**: Loopback PKCE = OAuth-for-native standard (RFC 8252), no client secret on device. Reuses same `/auth/token` + `/auth/refresh` as SPA. CLI uses refresh token (file) where SPA uses cookie.

**Alternatives Considered**:
- Device-code primary (more friction) → fallback only
- OS keychain (native deps, cross-platform complexity) → file now, keychain later
- Long-lived non-refreshable token (inflexible, no revocation) → rejected

**Risk → Mitigation**:
- Loopback hijack → bind `127.0.0.1` only, validate state, single-use 5-min timeout, exact `redirect_uri` match
- File perms tampered → verify 0600 each run; refuse to use if loose
- Plaintext refresh at rest → 0600 mode + short access TTL; keychain hardening later

---

### 8. Config / Secrets

**Decision**: All via environment variables, validated at startup (fail-fast). Missing required secrets prevent service startup.

```
OIDC__GOOGLE__CLIENTID
OIDC__GOOGLE__CLIENTSECRET
OIDC__GOOGLE__REDIRECTURI

OIDC__MICROSOFT__CLIENTID
OIDC__MICROSOFT__CLIENTSECRET
OIDC__MICROSOFT__TENANT (common or tenant UUID)
OIDC__MICROSOFT__REDIRECTURI

OIDC__ALLOWEDLOOPBACKREDIRECT (e.g., http://127.0.0.1 — validated prefix for CLI)
OIDC__ENABLEDPROVIDERS=google,microsoft

JWT__ISSUER
JWT__AUDIENCE
JWT__SIGNINGKEY__PRIVATEPEM (RS256 private PEM, secret)
JWT__ACCESSTOKENMINUTES=15
JWT__REFRESHTOKENDAYS=30

EMAIL__PROVIDER
EMAIL__APIKEY
EMAIL__FROM
```

**Startup Validator** (`IValidateOptions` / hosted check):
- Assert every required key per `ENABLED` provider present
- Validate RS256 key parses
- Missing → throw before listening (fail-fast)

**Provider List**: Config-driven via `OIDC__ENABLEDPROVIDERS`.

**Dev Environment**: `appsettings.Development.json` + `.env` (git-ignored).

**Production (OVH)**: Secret manager / Kubernetes secrets; redirect URIs differ per env.

**Docker Compose**: Pass new env to api service; no new service needed (refresh tokens/denylist in Postgres; Redis denylist optional).

**JWT Private Key**: Mount as secret, not baked in image.

**Why**: 12-factor + fail-fast matches marketplace design + global rule. Per-provider prefix keeps OIDC extensibility honest. RS256 key as mounted secret avoids leaking in process listings.

**Alternatives Considered**:
- HS256 env secret (weak rotation, single point of compromise) → rejected
- Secrets baked in image (catastrophic) → rejected

**Risk → Mitigation**:
- Key rotation → JWKS with `kid` field; two active keys during rotation window
- Prod redirect mismatch → validator checks absolute HTTPS in Production profile

---

### 9. Cross-Cutting & Migration

**Decision**: BREAKING change (anonymous upload removed), phased 3-phase rollout with feature flag rollback:

**Phase 1 (Additive)**:
- Schema migration: new tables + plugins columns (`visibility DEFAULT 'public'`, `owner_org_id` NULL, `owner_user_id` NULL)
- Legacy rows remain public + ownerless (backward-compatible)

**Phase 2 (Visibility Available)**:
- Deploy auth endpoints + read-side visibility filtering
- Upload still anonymous (adoption window for users to set up orgs/auth)

**Phase 3 (Breaking Gate)**:
- Flip upload gate via `Features:RequireAuthForUpload` feature flag (the breaking moment)
- Announce version + CLI changes
- Flag allows instant rollback via image tag / config update

**Legacy Plugins**: Public, ownerless, downloadable/listable anonymously. Claim flow OUT OF SCOPE (future).

**Rate Limiting**: Per-IP on `/auth/authorize`, `/auth/token`, `/auth/refresh`, `/auth/device/token`, invite-send (ASP.NET rate limiting; extend marketplace's approach; tighter on token/refresh).

**Minimal PII / GDPR**: Store only email, display name, provider subject. Account deletion = soft-delete user + cascade memberships + revoke refresh. Solely-owned orgs with no other members removed. Auth data excluded from telemetry.

**Audit**: Append-only `org_audit_log` (invite sent/accepted/revoked, member removed, role changed, visibility changed); internal-only.

**Why**: 3-phase decouples "auth available" from "auth required" with migration window + single revert flag. Legacy=public+ownerless preserves anonymous downloads. Reuse existing rate-limit/migration/secrets machinery.

**Alternatives Considered**:
- Hard cutover (breaks all publishers) → rejected
- Auto-assign legacy to synthetic org (invents unverifiable ownership) → rejected
- No audit log (compliance risk) → rejected

**Risk → Mitigation**:
- Old CLI clients break at Phase 3 flip → ship login/whoami in Phase 1/2 + announce + flag rollback
- CHECK rejects legacy rows → satisfied by `DEFAULT 'public'` + `visibility='public' OR owner_org_id IS NOT NULL`; dry-run on prod snapshot
- Invite email delivery failure → record still created pending; accept via token later; surface status in UI

---

## Risks / Trade-offs

| Risk | Mitigation |
|------|-----------|
| **Stale memberships** (user removed from org, still downloads private plugin within cache window) | 30–60s in-memory cache on membership queries; immediate invalidation on membership mutation; trade-off: small window vs. fresh data every request (latency) |
| **Refresh token theft** (attacker steals token.json or SPA cookie) | One-time-use rotation + reuse-detection revokes entire chain; hashed SHA-256 at rest; SPA: HTTP-only SameSite=Strict cookie; CLI: 0600 file perms with validation |
| **403-vs-404 confusion** (inconsistent 401/403/404 responses) | Single `IPluginAccessPolicy` service encodes rule matrix (read=404, write=403); wide per-endpoint integration tests |
| **Breaking upload cutover** (old CLI / publishers hit wall at Phase 3) | 3-phase rollout with adoption window (Phases 1–2); CLI `login`/`whoami` shipped Phase 1; feature flag rollback; versioned announcement |
| **JWKS rotation** (signing key rolls, mid-flight requests see old kid) | `RefreshOnSignatureFailure` re-fetches JWKS if kid not found; 24-hour cache refresh; two active keys in brief window |
| **Loopback hijack** (attacker binds 127.0.0.1:port before CLI) | Bind 127.0.0.1 only (not 0.0.0.0); validate state param; single-use 5-min code; exact `redirect_uri` match |
| **Refresh-cookie CSRF** (attacker tricks SPA into minting new access token for attacker) | SameSite=Strict (prevents cross-site POST); custom header double-submit (`X-Requested-With`); refresh endpoint returns no mutation (only token mint) |
| **Membership query latency on every request** (N+1 if not indexed/cached) | `(visibility, owner_org_id)` index; 30–60s in-memory cache reduces load; acceptable tradeoff for fresh membership data |
| **Stale org email (user email changes on IdP side, doesn't sync)** | Sync email on each login via id_token; update user.email; not stored in JWT (only name/email at token issue time); email change detected next login |

---

## Migration Plan

### Schema Evolution

**Migration: 001-add-auth-schema** (EF, additive only, no downtime):
1. Create `users`, `user_identities`, `organizations`, `organization_members`, `organization_invitations`, `refresh_tokens`, `org_audit_log`
2. Alter `plugins`: add `visibility TEXT NOT NULL DEFAULT 'public'`, `owner_org_id UUID NULL`, `owner_user_id UUID NULL`
3. Add CHECK constraint and index; existing rows inherit `visibility='public'`, `owner_*=NULL`
4. No data seeding; anonymous legacy data remains as-is

### Code Rollout

**Phase 1: Auth Endpoints + Visibility Filter (Additive)**
- Deploy schema + auth endpoints (`/auth/authorize`, `/auth/callback`, `/auth/token`, `/auth/refresh`, `/auth/me`, `/auth/signout`)
- Deploy visibility filtering in PluginCatalog, PluginSearch, PluginDistribution reads (private plugins filtered from results)
- Upload still accepts anonymous requests (`[Authorize]` not yet enforced)
- CLI `login`, `logout`, `whoami` commands available but not required
- Frontend auth UI available; org creation/invite flows available
- **Announcement**: "Authentication now available; sign up for private plugins (optional)"

**Phase 2: Adoption Window**
- No code changes; let Phase 1 run 2–4 weeks
- Migrate published plugins to orgs, set visibility
- Internal/test orgs created and populated
- CLI client users run `login` once, establish credentials

**Phase 3: Require Auth (Breaking Gate)**
- Feature flag `Features:RequireAuthForUpload` → true (config/env)
- POST `/plugins/upload` now enforces `[Authorize]` + `IsMember(owner_org_id)` for private
- Anonymous upload rejected (HTTP 401)
- Old CLI clients (without `login`) break; users must upgrade + authenticate
- **Announcement**: "Anonymous upload ended; please log in to publish"

### Rollback

- Flag false → Phase 2 state restored (upload anonymous again)
- If catastrophic: revert image tag + config, skip to Phase 1 cleanup (optional)
- No data loss (all plugins remain, visibility preserved)

### Pre-Production Verification

- Dry-run migration on prod snapshot (PIT, non-destructive)
- Verify CHECK constraint on legacy rows (must pass)
- Load test membership queries (cache performance)
- End-to-end test: anonymous → auth → private plugin → org → publish

---

## Open Questions

1. **Access-token lifetime vs spec "7 days"**
   - *Adopted Default*: 15-minute access + 30-day refresh token (read "7 days" as refresh window, not access lifetime)
   - Confirm this interpretation aligns with security/UX expectations (frequent silent refresh vs. long-lived risk)

2. **Denylist store for immediate sign-out**
   - *Adopted Default*: Postgres `revoked_jti` table (no new infra) over Redis, given short (15-min) access TTL
   - Confirm if hard real-time revocation needed for your security posture; if yes, Redis option remains available

3. **SPA refresh-token transport**
   - *Adopted Default*: HTTP-only SameSite=Strict Secure cookie scoped to `/auth/refresh`
   - Confirm cookie transport acceptable (alternative: body in exchange for in-memory risk)

4. **Cross-provider verified-email linking**
   - *Adopted Default*: Auto-link on verified email; config flag to disable for high-security deployments
   - Confirm auto-linking acceptable or require explicit user confirmation per provider

5. **Microsoft tenant scope**
   - *Adopted Default*: `common` tenant (multi-tenant + personal accounts) for an open marketplace
   - Confirm vs. fixed tenant—if enterprise-only, tenant UUID required; product decision

6. **Legacy plugin "claim ownership" flow**
   - *Adopted Default*: Out of scope; future follow-up change
   - Confirm deferred (post-MVP) without affecting auth/org rollout

7. **Invitation email provider on OVH**
   - *Adopted Default*: Abstract behind `IInvitationEmailPort`; default SMTP relay; concrete provider TBD (Sendgrid, Postmark, etc.)
   - Confirm email-sending infra + provider selection before Phase 1

