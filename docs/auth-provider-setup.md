<!-- slug: auth-provider-setup | category: ops -->

# Authentication Provider Setup

This document covers:
1. [Google Cloud Console — OAuth 2.0 client setup](#google-cloud-console-oauth-20-client-setup)
2. [Azure Entra (Microsoft) — App registration](#azure-entra-microsoft-app-registration)
3. [JWT Key Generation and Rotation](#jwt-key-generation-and-rotation)
4. [Environment variable reference](#environment-variable-reference)

The env var names referenced throughout match `.env.example` and the Docker Compose
service definitions in `infra/docker-compose.yml` (dev) and `infra/docker-compose.prod.yml`
(production overlay).

---

## Google Cloud Console — OAuth 2.0 Client Setup

### 1. Create or select a project

1. Go to [https://console.cloud.google.com](https://console.cloud.google.com).
2. Select an existing project or create a new one (e.g. `claudeforge-prod`).

### 2. Enable the APIs

1. Navigate to **APIs & Services > Enabled APIs & services**.
2. Enable the **Google Identity** (People API) if you need profile data beyond the
   id_token. The OIDC discovery flow itself (`accounts.google.com`) requires no
   separate API to enable.

### 3. Configure the OAuth consent screen

1. Go to **APIs & Services > OAuth consent screen**.
2. Choose **External** (open marketplace) or **Internal** (G Suite domain only).
3. Fill in app name, support email, and developer contact.
4. Add the following **scopes** (minimum required):
   - `openid`
   - `email`
   - `profile`
5. Add test users if the app is in "Testing" publication status.
6. Save and continue.

### 4. Create the OAuth 2.0 client ID

1. Go to **APIs & Services > Credentials**.
2. Click **Create Credentials > OAuth client ID**.
3. Application type: **Web application**.
4. Add **Authorized redirect URIs** for each environment:

   | Environment | Redirect URI |
   |-------------|-------------|
   | Development | `http://localhost:5010/auth/callback` |
   | Production  | `https://api.claudeforge.dev/auth/callback` |
   | CLI loopback | Not added here — the CLI uses dynamic loopback ports validated by `OIDC__ALLOWEDLOOPBACKREDIRECT` on the backend; Google does not need the individual loopback port registered |

5. Click **Create**. Copy the **Client ID** and **Client Secret**.

### 5. Populate environment variables

```dotenv
OIDC__GOOGLE__CLIENTID=<client-id>.apps.googleusercontent.com
OIDC__GOOGLE__CLIENTSECRET=<client-secret>
OIDC__GOOGLE__REDIRECTURI=http://localhost:5010/auth/callback   # dev
# OIDC__GOOGLE__REDIRECTURI=https://api.claudeforge.dev/auth/callback  # prod
```

### Notes

- Google's OIDC discovery URL is `https://accounts.google.com/.well-known/openid-configuration`.
  The backend fetches this automatically via `ConfigurationManager<OpenIdConnectConfiguration>`.
- Credentials are rotated in Google Cloud Console under the same OAuth client — generate a new
  secret, update the secret in your secret manager, restart the API, then delete the old secret.

---

## Azure Entra (Microsoft) — App Registration

### 1. Register an application

1. Go to [https://entra.microsoft.com](https://entra.microsoft.com) (or the Azure Portal).
2. Navigate to **Identity > Applications > App registrations**.
3. Click **New registration**.
4. Fill in:
   - **Name**: `ClaudeForge` (or `ClaudeForge-Dev` / `ClaudeForge-Prod` per environment)
   - **Supported account types**: Choose based on your target audience:
     - *Accounts in any organizational directory and personal Microsoft accounts* —
       corresponds to tenant `common` (recommended for open marketplace; set `OIDC__MICROSOFT__TENANT=common`)
     - *Accounts in this organizational directory only* — use the specific tenant UUID
       (set `OIDC__MICROSOFT__TENANT=<tenant-uuid>`)
   - **Redirect URI**: Web platform, enter the appropriate value per environment (see table below).
5. Click **Register**.

### 2. Add redirect URIs

In the registered app, go to **Authentication > Platform configurations > Web** and add:

| Environment | Redirect URI |
|-------------|-------------|
| Development | `http://localhost:5010/auth/callback` |
| Production  | `https://api.claudeforge.dev/auth/callback` |

The CLI loopback flow uses `http://127.0.0.1:<random-port>/callback`. Azure requires you to
enable **Allow public client flows** (under **Authentication > Advanced settings**) for
loopback URIs using dynamic ports. Alternatively, the backend validates the loopback prefix
via `OIDC__ALLOWEDLOOPBACKREDIRECT` — no individual port needs to be pre-registered in Azure.

### 3. Add API permissions / scopes

1. Go to **API permissions**.
2. Click **Add a permission > Microsoft Graph > Delegated permissions**.
3. Add the following:
   - `openid`
   - `email`
   - `profile`
4. Click **Grant admin consent** (required for the consent screen to be pre-approved
   in enterprise tenants; optional for `common` multi-tenant registrations).

### 4. Create a client secret

1. Go to **Certificates & secrets > Client secrets**.
2. Click **New client secret**, set an expiry (12 or 24 months recommended).
3. Copy the **Value** immediately — it is shown only once.

### 5. Collect identifiers

From the app **Overview** page:
- **Application (client) ID** → `OIDC__MICROSOFT__CLIENTID`
- **Directory (tenant) ID** → `OIDC__MICROSOFT__TENANT` (if using tenant-specific; use `common` otherwise)

### 6. Populate environment variables

```dotenv
OIDC__MICROSOFT__CLIENTID=<application-client-id>
OIDC__MICROSOFT__CLIENTSECRET=<client-secret-value>
OIDC__MICROSOFT__TENANT=common
OIDC__MICROSOFT__REDIRECTURI=http://localhost:5010/auth/callback   # dev
# OIDC__MICROSOFT__REDIRECTURI=https://api.claudeforge.dev/auth/callback  # prod
```

### Notes

- Microsoft's OIDC discovery URL is:
  `https://login.microsoftonline.com/<tenant>/v2.0/.well-known/openid-configuration`
  (where `<tenant>` is `common` or your tenant UUID).
- Client secrets expire. Set up a calendar reminder to rotate before expiry (see rotation
  procedure below).

---

## JWT Key Generation and Rotation

ClaudeForge issues RS256-signed access JWTs. The private key signs tokens; the public key
is published at `GET /.well-known/jwks.json` and used by validators.

### Generating the key pair

Run the following on a secure workstation (not in CI/CD):

```bash
# Generate a 4096-bit RSA private key (PEM format)
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:4096 \
  -out jwt_private.pem

# Set restrictive permissions
chmod 600 jwt_private.pem

# Extract the public key (optional — the backend derives it from the private key)
openssl rsa -in jwt_private.pem -pubout -out jwt_public.pem
```

#### Dev: place the key in the secrets mount directory

```bash
# From the repo root
mkdir -p infra/secrets
cp /path/to/jwt_private.pem infra/secrets/jwt_private.pem
chmod 600 infra/secrets/jwt_private.pem
```

The `infra/secrets/` directory is git-ignored; the key is mounted into the container at
`/run/secrets/jwt_private_pem` via the Docker Compose `secrets:` block.
The backend reads `JWT__SIGNINGKEY__PRIVATEPEM_FILE` to locate the file.

#### Production: provision via secret manager

Do not copy the file to the server manually. Use your secret manager:

- **Docker Swarm**: `docker secret create jwt_private_pem /path/to/jwt_private.pem`
- **Kubernetes**: `kubectl create secret generic jwt-private-pem --from-file=jwt_private_pem=jwt_private.pem`
  then mount as a volume at `/run/secrets/jwt_private_pem` inside the api pod.
- **OVH Secret Manager / Vault**: store the PEM content as a secret; inject via
  environment variable `JWT__SIGNINGKEY__PRIVATEPEM` or file mount — the backend
  accepts either (file mount preferred to avoid env var exposure in `docker inspect`).

### Key rotation (two-active-`kid` procedure)

The JWKS endpoint (`/.well-known/jwks.json`) supports multiple active keys identified by
the `kid` (key ID) header in each issued JWT. The rotation procedure:

1. **Generate a new key pair** using the steps above.
2. **Add the new key** to the backend configuration alongside the existing key.
   The backend must be configured to sign new tokens with the NEW key and to validate
   tokens signed by EITHER the old or new key.
   - Both public keys appear in `/.well-known/jwks.json` during the rotation window.
   - Both private keys are active in the signing config (old = verify-only, new = sign + verify).
3. **Deploy** the updated configuration. Existing access tokens signed by the old `kid`
   continue to validate until they expire (maximum `JWT__ACCESSTOKENMINUTES` = 15 minutes).
4. **Wait** for the overlap window to pass (at minimum the access token TTL — 15 minutes by default).
5. **Remove the old key** from the JWKS endpoint and the signing config.
6. **Deploy** again. Old tokens issued by the old `kid` are now expired; validation with
   the old public key is no longer needed.

The `kid` value should be a stable identifier (e.g. a short UUID or date-based slug like
`2026-06-primary`). The backend embeds `kid` in every issued JWT header.

### Key expiry and rotation schedule

| Concern | Recommendation |
|---------|----------------|
| Routine rotation | Every 90–180 days |
| Suspected compromise | Immediate — revoke old key, rotate, and consider revoking all active refresh tokens |
| Provider client secret expiry | Per-provider console: 12–24 month expiry; set calendar alert 30 days ahead |

---

## Environment Variable Reference

All variables are described with their defaults in `.env.example` at the repository root.

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `OIDC__ENABLEDPROVIDERS` | Yes | `google,microsoft` | Comma-separated list of enabled providers |
| `OIDC__ALLOWEDLOOPBACKREDIRECT` | Yes | `http://127.0.0.1` | Loopback redirect prefix validated for CLI PKCE |
| `OIDC__GOOGLE__CLIENTID` | If google enabled | — | Google OAuth client ID |
| `OIDC__GOOGLE__CLIENTSECRET` | If google enabled | — | Google OAuth client secret |
| `OIDC__GOOGLE__REDIRECTURI` | If google enabled | (per env) | Registered redirect URI for Google |
| `OIDC__MICROSOFT__CLIENTID` | If microsoft enabled | — | Azure Entra application (client) ID |
| `OIDC__MICROSOFT__CLIENTSECRET` | If microsoft enabled | — | Azure Entra client secret |
| `OIDC__MICROSOFT__TENANT` | If microsoft enabled | `common` | Tenant ID or `common` |
| `OIDC__MICROSOFT__REDIRECTURI` | If microsoft enabled | (per env) | Registered redirect URI for Microsoft |
| `JWT__ISSUER` | Yes | `https://api.claudeforge.dev` | JWT `iss` claim |
| `JWT__AUDIENCE` | Yes | `claudeforge-spa-cli` | JWT `aud` claim |
| `JWT__ACCESSTOKENMINUTES` | No | `15` | Access token lifetime in minutes |
| `JWT__REFRESHTOKENDAYS` | No | `30` | Refresh token lifetime in days |
| `JWT__SIGNINGKEY__PRIVATEPEM_FILE` | Yes (in Docker) | `/run/secrets/jwt_private_pem` | Path to RS256 private key file (preferred over inline env) |
| `JWT__SIGNINGKEY__PRIVATEPEM` | Yes (non-Docker) | — | RS256 private PEM content (only if file mount is not possible) |
| `EMAIL__SMTPHOST` | Yes | — | SMTP server hostname |
| `EMAIL__SMTPPORT` | No | `587` | SMTP port (587 = STARTTLS, 465 = SMTPS) |
| `EMAIL__SMTPUSER` | Yes | — | SMTP username |
| `EMAIL__SMTPPASSWORD` | Yes | — | SMTP password |
| `EMAIL__FROM` | No | `noreply@claudeforge.dev` | From address on outbound emails |
| `Features__RequireAuthForUpload` | No | `false` | Phase 3 gate: set to `true` to require auth on upload |
