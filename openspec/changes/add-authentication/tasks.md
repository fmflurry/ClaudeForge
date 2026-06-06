## 1. Auth Data Model & EF Migration

- [ ] 1.1 Define Core domain entities/value objects as immutable types in the Identity and Organizations Cores: `User` (email, emailNormalized, displayName, deletedAt), `UserIdentity` (provider, subject), `Organization` (name, nameNormalized, slug, createdBy), `OrganizationMember` (orgId, userId, role), `OrganizationInvitation` (state, token, email, role, timestamps), `RefreshToken` (tokenHash, expiresAt, revokedAt, rotatedTo), `OrgAuditEntry`; plus `OrgRole` (owner|admin|member) and `Visibility` (public|private) value objects
- [ ] 1.2 Write failing tests for EF entity mappings (users, user_identities, organizations, organization_members, organization_invitations, refresh_tokens, org_audit_log) asserting required fields, UNIQUE constraints (`email_normalized`, `(provider,subject)`, `name_normalized`, `slug`, `token_hash`, partial-unique pending invite per `(org_id,email_normalized)`), and ON DELETE CASCADE rules
- [ ] 1.3 Define EF entity models + DbContext config for the seven new tables in `Infrastructure/Persistence`, matching the design schema (UUID PKs, TIMESTAMPTZ, the partial-unique pending-invite index, `refresh_tokens.rotated_to` self-FK)
- [ ] 1.4 Write a failing test asserting the plugins `chk_visibility_owner` CHECK rejects a private row with null `owner_org_id` and accepts a public ownerless row
- [ ] 1.5 Configure additive `plugins` column mappings: `visibility TEXT NOT NULL DEFAULT 'public'`, `owner_org_id UUID NULL` (FK organizations), `owner_user_id UUID NULL` (FK users), `chk_visibility_owner` CHECK, and `idx_plugins_visibility_org` index
- [ ] 1.6 Create the EF migration `AddAuthSchema` (forward-only, additive) generating the seven tables + plugins column additions + CHECK + index; verify `dotnet ef database update` against local Postgres produces the design's tables/indexes and that existing plugin rows inherit `visibility='public'`, `owner_*=NULL`

## 2. Shared-Kernel Authorization Seam

- [ ] 2.1 Define the shared-kernel authZ contracts (in the shared `Core`, outside both modules): `ICurrentUser` (UserId?, IsAuthenticated, Email), `IOrgMembershipQueryPort` (`GetOrgIdsForUser(userId) → Guid[]`, `IsMember(userId, orgId, minRole?) → bool` — primitives only), and the `IPluginAccessPolicy` pure-domain-service interface returning an `AccessDecision` enum (`Allow|NotFound|Unauthenticated|Forbidden`)
- [ ] 2.2 Write failing tests for `IPluginAccessPolicy` covering the full read/download matrix: public+anon→Allow, public+auth→Allow, private+anon→Unauthenticated(401), private+auth-non-member→NotFound(404), private+member→Allow
- [ ] 2.3 Implement the pure `PluginAccessPolicy` domain service (no I/O; takes `ICurrentUser` + plugin visibility/owner_org_id + caller org-id set) encoding the 401/404 read rule and the 403 write rule
- [ ] 2.4 Implement `OrgMembershipQueryAdapter` (EF) for `IOrgMembershipQueryPort` returning Guid[]/bool only, with a 30–60s in-memory cache (user_id → org_ids) and an invalidation hook callable on membership mutation; unit-test cache hit/miss + invalidation
- [ ] 2.5 Add NetArchTest rules in `ArchTests`: existing modules' Core (PluginCatalog/PluginSearch/PluginDistribution/PluginPublishing) must not reference the Organizations or Identity namespaces; the shared authZ ports expose no Organizations domain types; wire into CI

## 3. Token Service (RS256 + Refresh Rotation + JWKS)

- [ ] 3.1 Define Identity Core ports: `ITokenIssuerPort` (issue access JWT from identity claims), `IRefreshTokenStorePort` (create hashed, find-by-hash, rotate, revoke-chain), and `IJwksProvider` (public keys as JWKS)
- [ ] 3.2 Write failing tests for access-token issuance/validation: RS256 sign with private PEM, claims `sub/email/name/provider/iss/aud/iat/exp/jti`, configurable 15-min expiry, signature-verifies-against-public-key, tampered-payload rejected, expired rejected, wrong-key rejected, malformed rejected
- [ ] 3.3 Implement `RsaTokenIssuerAdapter` (RS256, `kid` header for rotation) reading the private PEM from config; implement validation parameters used by JwtBearer
- [ ] 3.4 Write failing tests for refresh-token rotation + reuse-detection: opaque random string stored SHA-256-hashed, one-time-use rotation sets `rotated_to`, presenting an already-rotated token revokes the entire chain, expired refresh rejected, 30-day default expiry
- [ ] 3.5 Implement `RefreshTokenStoreAdapter` (EF) with create/rotate/revoke-chain + reuse-detection
- [ ] 3.6 Implement the JWKS endpoint `GET /.well-known/jwks.json` exposing current (and during-rotation, prior) public keys with `kid`; integration-test it returns parseable JWKS
- [ ] 3.7 Implement the optional `revoked_jti` Postgres denylist (TTL = remaining access-token life) and a denylist check hooked into validation only when an entry is present; unit-test denylisted-jti rejection and TTL expiry

## 4. OIDC Integration

- [ ] 4.1 Define `IIdentityProviderPort` in Identity Core (`BuildAuthorizationUrl(provider, codeChallenge, state, redirectUri)`, `ExchangeCode(provider, code, codeVerifier, redirectUri) → RawIdToken`, `ValidateIdToken(provider, rawIdToken) → VerifiedIdentity{subject,email,emailVerified,name}`) and `IIdentityProviderRegistry` (`Resolve(name)`, unknown → `UnsupportedProviderException`)
- [ ] 4.2 Write failing tests for the registry: resolves google/microsoft, unknown provider raises `UnsupportedProviderException`, only `OIDC__ENABLEDPROVIDERS` are resolvable
- [ ] 4.3 Implement `GoogleIdentityProviderAdapter` and `MicrosoftIdentityProviderAdapter`: hand-rolled authorize-URL build + code→token POST, id_token validation via `ConfigurationManager<OpenIdConnectConfiguration>` (24h refresh + `RefreshOnSignatureFailure`) using `Microsoft.IdentityModel.*`; cover authorize-URL shape, code exchange, valid/expired/tampered id_token, PKCE-mismatch rejection
- [ ] 4.4 Implement `IIdentityProviderRegistry` selecting adapters from config (`OIDC__ENABLEDPROVIDERS`, per-provider client id/secret/redirect/tenant)
- [ ] 4.5 Write failing tests for `IUserStorePort` provisioning + verified-email account linking: new `(provider,subject)` creates user, repeat sign-in updates name/email without duplicate, second provider with same VERIFIED email links via `user_identities`, unverified email does NOT auto-link
- [ ] 4.6 Implement `IUserStorePort` + EF adapter handling first-sign-in provisioning, idempotent updates, and `(provider,subject)`/verified-email linking; add the config flag to disable cross-provider linking for high-security deployments

## 5. Identity Module Endpoints & AuthN Wiring

- [ ] 5.1 Implement the startup secret/config validator (`IValidateOptions`/hosted check): assert every required key per enabled provider present, RS256 private PEM parses, absolute-HTTPS redirect in Production; fail fast before listening; cover missing-key and bad-key cases with tests
- [ ] 5.2 Configure `Microsoft.AspNetCore.Authentication.JwtBearer` to validate OUR issued JWT (issuer/audience/RS256 public key via `IJwksProvider`), populating `ClaimsPrincipal` → `ICurrentUser`; add a `RequireAuthenticatedUser` authorization policy
- [ ] 5.3 Write failing tests for PKCE/state server-side store (short TTL, single-use) used by the web authorize/callback flow
- [ ] 5.4 Implement `InitiateSignInUseCase` + `GET /auth/authorize?provider=...` building the PKCE authorization URL + storing state/verifier; unknown provider → 400 (spec: "User selects an unsupported provider")
- [ ] 5.5 Implement `CompleteSignInUseCase` + `GET /auth/callback` and `POST /auth/token` (code + verifier → exchange → validate id_token → provision/link user → issue access JWT + refresh): valid code → tokens; tampered/expired code → 401; PKCE mismatch → 401
- [ ] 5.6 Implement `POST /auth/refresh` (rotate refresh, mint new access; reuse → chain revoke → 401) and `GET /auth/me` (`[Authorize]` 401, returns userId/email/displayName/org memberships); cover unauthenticated/expired/tampered → 401
- [ ] 5.7 Implement `POST /auth/signout` (revoke refresh token, optional jti denylist add; 401 if unauthenticated); cover revoked-token-rejected-on-subsequent-request
- [ ] 5.8 Implement device-code endpoints `POST /auth/device/code` (issue user-code + verification URL) and `POST /auth/device/token` (poll: pending/slow-down/approved→tokens/expired); cover happy + pending + expired paths
- [ ] 5.9 Write integration tests (WebApplicationFactory + test Postgres) for the full auth endpoint surface asserting status codes + ProblemDetails bodies across authorize/callback/token/refresh/me/signout/device

## 6. Organizations Module

- [ ] 6.1 Define Organizations Core ports: `IOrganizationStorePort`, `IMembershipStorePort`, `IInvitationStorePort`, `IInvitationEmailPort`, `IOrgAuditLogPort`; implement EF adapters for the first four; append-only audit adapter for the last
- [ ] 6.2 Write failing tests for `CreateOrganizationUseCase`: authenticated create → 201 + creator gets owner role; duplicate name → 409; unauthenticated → 401
- [ ] 6.3 Implement `CreateOrganizationUseCase` (unique name/slug normalization, creator-as-owner) + `POST /orgs`
- [ ] 6.4 Write failing tests for membership/listing: list user orgs (with role) requires auth (401 unauth); list members returns email/name/role to members; non-member list → 403 (non-disclosure)
- [ ] 6.5 Implement `ListUserOrganizationsUseCase`, `ListOrgMembersUseCase` + endpoints `GET /orgs`, `GET /orgs/{orgId}/members`
- [ ] 6.6 Write failing tests for the invitation state machine: owner/admin invite → 201 pending + email sent; invite existing member → 409; member (non-owner/admin) invite → 403; unauthenticated → 401
- [ ] 6.7 Implement `IssueInvitationUseCase` (role check via `IOrgMembershipQueryPort`, pending record, `IInvitationEmailPort` send best-effort, audit) + `POST /orgs/{orgId}/invitations`
- [ ] 6.8 Write failing tests for accept/revoke: accept valid pending → 200 + member role; accept non-existent/not-your-email → 404; accept revoked/expired (non-pending) → 410 Gone; owner/admin revoke pending → 200
- [ ] 6.9 Implement `AcceptInvitationUseCase` (token + email match, non-pending → 410, creates membership, invalidates membership cache, audit) + `RevokeInvitationUseCase`, endpoints `POST /orgs/{orgId}/invitations/{id}/accept` and `POST /orgs/{orgId}/invitations/{id}/revoke`
- [ ] 6.10 Write failing tests for member removal + role change: owner/admin remove member → 204 + cache invalidated; member removing another → 403; sole owner self-removal → 400; owner promotes member→admin → 200 (owner-only)
- [ ] 6.11 Implement `RemoveMemberUseCase` (sole-owner guard counts owners, audit) and `ChangeMemberRoleUseCase` (owner-only) + endpoints `DELETE /orgs/{orgId}/members/{userId}` and `PATCH /orgs/{orgId}/members/{userId}`
- [ ] 6.12 Implement the default SMTP `IInvitationEmailPort` adapter (config-driven from `EMAIL__*`); test failure path records pending invite anyway and surfaces status
- [ ] 6.13 Write integration tests covering the full org lifecycle (create → invite → accept → list members → role change → remove) plus the 401/403/404/409/410 matrix and append-only audit-log entries

## 7. Wire AuthZ into Existing Marketplace Modules

- [ ] 7.1 Write failing tests for private-download enforcement in PluginDistribution: public+anon→200, private+member→200, private+anon→401, private+auth-non-member→404, member-of-different-org→404
- [ ] 7.2 Wire `ICurrentUser` + `IOrgMembershipQueryPort` + `IPluginAccessPolicy` into `DownloadPluginUseCase`; map `AccessDecision` to 200/401/404 with non-disclosure on 404
- [ ] 7.3 Write failing tests for the upload gate behind `Features:RequireAuthForUpload`: flag off → anonymous upload still works (Phase 2); flag on → unauthenticated upload → 401; authenticated public upload → 201; default visibility public when unspecified
- [ ] 7.4 Wire the `[Authorize]` gate (behind `Features:RequireAuthForUpload`) into `POST /api/v1/plugins/upload` and set `owner_user_id`/`author` from `ICurrentUser`
- [ ] 7.5 Write failing tests for private publishing + visibility change: member publishes private for own org → 201 + owner_org_id set; non-member → 403; private without org → 400; owner/admin/publisher change visibility → 200 (clears owner_org_id when→public); non-owner change → 403; unauthenticated change → 401
- [ ] 7.6 Implement private-publish org-membership check + visibility field handling in `UploadPluginUseCase`/`PublishVersionUseCase` and a `ChangePluginVisibilityUseCase` + endpoint, with audit on visibility change
- [ ] 7.7 Write failing tests for `viewerOrgIds` filtering in PluginCatalog + PluginSearch: anon sees public only; member sees public + own-org private; other-org private excluded from list/search/counts/pagination; multi-org member sees all their private plugins
- [ ] 7.8 Thread optional `viewerOrgIds UUID[]` (from `ICurrentUser` + `IOrgMembershipQueryPort`, empty when anonymous) into `ListPluginsUseCase`, `GetPluginDetailsUseCase`, `SearchPluginsUseCase`, `DiscoverPluginsUseCase` and their SQL predicates (`visibility='public' OR owner_org_id = ANY(@viewerOrgIds)`); single-plugin fail → 404
- [ ] 7.9 Write wide integration tests per protected endpoint asserting the complete 401/403/404 matrix (private+anon→401, private+non-member→404, private publish non-member→403, org wrong-role→403) and that timing/query paths are identical for member vs non-member reads

## 8. Rate Limiting on Auth Endpoints

- [ ] 8.1 Apply per-IP rate limiting (extending the marketplace's ASP.NET rate-limiting approach, tighter buckets on token/refresh) to `/auth/authorize`, `/auth/token`, `/auth/refresh`, `/auth/device/token`, and invitation-send; integration-test 429 on burst

## 9. Frontend — Auth Domain

- [ ] 9.1 Write failing tests for `AuthStore`/`AuthFacade` state transitions (idle/authenticating/authenticated/error, access token in-memory signal, `currentUser`/`isAuthenticated`/`activeOrgId` signals)
- [ ] 9.2 Implement the `auth` domain (models, `AuthPort`, mappers) + HTTP adapter for `/auth/authorize`, `/auth/token`, `/auth/refresh`, `/auth/me`, `/auth/signout` (no `any`)
- [ ] 9.3 Implement `AuthStore` (extends `BaseStore`, access token in memory only) + `AuthFacade` (`login(provider)`, `completeLogin`, `logout`, exposes signals) — components consume facade only
- [ ] 9.4 Write failing tests for the HTTP interceptor single-flight refresh: attaches Bearer to API calls, skips public catalog/search/download + IdP URLs, on 401 refreshes once via shared observable and retries, on refresh failure clears store + routes to login
- [ ] 9.5 Implement the auth HTTP interceptor (single-flight refresh) and bootstrap silent-refresh (check HTTP-only cookie + fetch new access on reload)
- [ ] 9.6 Write failing tests for `FunctionalAuthGuard` and `OrgMemberGuard` (allow authenticated/member, redirect/deny otherwise)
- [ ] 9.7 Implement `FunctionalAuthGuard` (publish/private/org routes) and `OrgMemberGuard` (org-scoped routes)
- [ ] 9.8 Build login UI (full-page redirect for Google + Microsoft via `AuthFacade.login`), `/auth/callback` route handler (`completeLogin`), current-user header, and sign-out action; component tests for login/callback/signout states

## 10. Frontend — Organizations Domain

- [ ] 10.1 Write failing tests for the `organizations` domain (models, mappers, role rules) and `OrgContextFacade` (`organizations`/`activeOrg` signals)
- [ ] 10.2 Implement the `organizations` domain + HTTP adapter for `/orgs`, members, and invitation endpoints
- [ ] 10.3 Implement org store + `OrganizationsFacade` (create org, list members, invite, accept/revoke, remove member, change role) and `OrgContextFacade` for active-org selection
- [ ] 10.4 Wire org switching through the Context Registry so the active org updates signal-driven catalog/search (no direct cross-domain injection); unit-test the published switch event
- [ ] 10.5 Build org UI: create-org form, members table with role management, invitations send/accept/manage, org switcher component, and `@if(isAuthenticated())`-gated publish/private/org actions; component tests covering empty/loaded/error + role-gated states

## 11. CLI Authentication

- [ ] 11.1 Write failing tests for credentials/config storage: `~/.claude-plugins/credentials.json` written at 0600 (dir 0700), perms verified each run + refuse-if-looser, token redaction in logs, `~/.claude-plugins/config.json` `activeOrg` read/write
- [ ] 11.2 Implement the credentials + active-org store modules (immutable read/write, perm enforcement, redaction helper)
- [ ] 11.3 Write failing tests for loopback PKCE login: verifier/challenge generation, ephemeral `127.0.0.1:<random-port>` server bound to loopback only, state validation, exact `redirect_uri` match, 5-min single-use timeout → non-zero exit "Authentication cancelled or timed out", code→`/auth/token` exchange → store tokens, exchange error → non-zero exit + no token stored
- [ ] 11.4 Implement `login` command (loopback PKCE primary, `--provider google|microsoft` or prompt, opens browser, success prints user email + org)
- [ ] 11.5 Write failing tests for device-code fallback (display URL+code, poll `/auth/device/token`, store on approval) and implement it as the headless fallback path
- [ ] 11.6 Implement `whoami` (GET `/auth/me`, prints email + orgs + active org; unauthenticated → message + non-zero exit) and `logout` (best-effort revoke + delete credentials.json; idempotent "Already logged out" exit 0)
- [ ] 11.7 Write failing tests for token attachment: `publish`/private `pull` attach `Authorization: Bearer`, local-expiry pre-check → "Session expired…" non-zero exit, 401 from backend → same prompt, public `pull` sends NO auth header even if token exists, private pull non-member → 403 surfaced
- [ ] 11.8 Wire token attachment + active-org (`--org` override over config.json) into the existing `publish` and `pull` commands, preserving anonymous public pulls; CLI integration tests against a mocked API for each command's happy + error paths

## 12. Config / Secrets / Docker Compose

- [ ] 12.1 Add all new env vars to `.env.example` (`OIDC__GOOGLE__*`, `OIDC__MICROSOFT__*`, `OIDC__ALLOWEDLOOPBACKREDIRECT`, `OIDC__ENABLEDPROVIDERS`, `JWT__ISSUER/AUDIENCE/ACCESSTOKENMINUTES/REFRESHTOKENDAYS`, `JWT__SIGNINGKEY__PRIVATEPEM`, `EMAIL__*`, `Features:RequireAuthForUpload`) and git-ignore real `.env`
- [ ] 12.2 Update dev `docker-compose.yml` to inject the new auth env into the `api` service and mount the RS256 private key as a secret (not baked into the image); update the prod overlay with Production redirect URIs + secret-manager wiring
- [ ] 12.3 Author provider-setup docs (Google Cloud Console + Azure Entra app registration, redirect URIs per env, required scopes) and a JWT key-generation/rotation note (two active `kid` keys during rotation)

## 13. GDPR / Account Deletion & Audit Verification

- [ ] 13.1 Write failing tests for account deletion: soft-delete user (set `deleted_at`), cascade membership removal, revoke all refresh tokens, sole-owner orgs with no other members removed, auth data excluded from telemetry
- [ ] 13.2 Implement `DeleteAccountUseCase` (soft-delete + cascade + refresh revoke + sole-owner-org cleanup) + endpoint; invalidate membership cache
- [ ] 13.3 Write integration tests asserting append-only `org_audit_log` entries for invite sent/accepted/revoked, member removed, role changed, and visibility changed (internal-only, never API-exposed)

## 14. End-to-End Verification

- [ ] 14.1 Run backend verification gate: `dotnet build` clean + xUnit suite green + NetArchTest isolation rules (existing modules' Core free of Identity/Organizations namespaces) passing
- [ ] 14.2 Run frontend verification gate: `npx tsc --noEmit --pretty false`, ESLint (no `any`), Angular unit/component tests green
- [ ] 14.3 Run CLI verification gate: typecheck + lint + CLI test suite green
- [ ] 14.4 Run full integration smoke against docker-compose: anonymous public download → sign in (Google + Microsoft) → create org → invite + accept → publish private plugin → member downloads (200) / non-member (404) / anonymous (401) → search/catalog visibility filtering → CLI login/whoami/publish/pull/logout → sign-out revokes refresh
- [ ] 14.5 Confirm ≥80% coverage across backend, frontend, and CLI for the new auth/org/visibility/cli-auth code; record coverage report and close any gaps

Phase 2 / deferred (not tracked as checkboxes): legacy plugin "claim ownership" flow; OS-keychain CLI credential storage (file 0600 now); Redis-backed `revoked_jti` denylist for hard real-time revocation (Postgres denylist now); additional OIDC providers beyond Google/Microsoft (registry seam exists); CLI auto-refresh of tokens nearing expiry (re-login prompt now); explicit per-provider user confirmation before cross-provider verified-email linking (auto-link + disable-flag now). These are seamed by the ports/flags above but intentionally out of MVP scope.

## Notes, Dependencies & Reconciled Conflicts

**Source-of-truth reconciliations baked in:** The authentication spec says access JWT "default 7 days" and describes sign-out as a token "blacklist"; design.md overrides both (15-min access + 30-day rotating refresh; sign-out revokes refresh, optional `jti` denylist). Tasks 3.2/3.4/5.7 follow design.md. The spec says private non-member returns 403 in one place (`Protected Resource Access Control`) but the plugin-visibility spec + design mandate 404 non-disclosure for reads; tasks 2.2/7.1 follow the 404-read / 403-write rule from design.md.

**Dependency on marketplace mid-build:** Groups 7, 9, 10, and 11 assume the in-progress `plugin-marketplace` change has landed its Groups 4–7 (PluginCatalog/PluginPublishing/PluginDistribution/PluginSearch use-cases + endpoints) and its frontend catalog/search domains. The scaffold (marketplace Group 1) is done, but those use-cases are still pending. Group 7 (wiring authZ into existing modules) and the frontend/CLI integration tasks (9, 10, 11) are BLOCKED until the corresponding marketplace groups land first.
