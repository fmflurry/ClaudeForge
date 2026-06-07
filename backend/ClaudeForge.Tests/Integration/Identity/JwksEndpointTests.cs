using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using ClaudeForge.Infrastructure.Persistence;
using ClaudeForge.Tests.Integration.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaudeForge.Tests.Integration.Identity;

/// <summary>
/// Integration tests for Group 3, Task 3.6 — JWKS endpoint GET /.well-known/jwks.json.
///
/// These tests are RED because the production types listed below do not yet exist.
/// The coder MUST create:
///
///   NAMESPACE: ClaudeForge.Core.Identity.Ports
///
///   sealed record JwksKey
///     string Kty    — key type, always "RSA" for RS256
///     string Use    — intended use, always "sig" for signing keys
///     string Alg    — algorithm, always "RS256"
///     string Kid    — key identifier
///     string N      — base64url-encoded RSA modulus
///     string E      — base64url-encoded RSA public exponent
///
///   sealed record JwksDocument
///     IReadOnlyList&lt;JwksKey&gt; Keys  — JSON property name "keys"
///
///   interface IJwksProvider
///     /// Returns current (and during rotation: prior) public keys as a JWKS document.
///     /// Must always contain at least one key.
///     JwksDocument GetCurrentKeys();
///
///   NAMESPACE: ClaudeForge.Infrastructure.Identity
///
///   sealed class RsaJwksProvider : IJwksProvider
///     Constructor: RsaJwksProvider(IReadOnlyList&lt;(string PublicPem, string Kid)&gt; activeKeys)
///     - Each entry is a PEM-encoded RSA public key + a kid string
///     - GetCurrentKeys() returns a JwksKey per entry with correct N/E/kid/kty="RSA"/use="sig"/alg="RS256"
///
///   JWKS ENDPOINT in ClaudeForge.Api or a Module:
///     GET /.well-known/jwks.json
///     - Returns HTTP 200
///     - Content-Type: application/json
///     - Body: JwksDocument (serialized with camelCase property names)
///     - No authentication required (public endpoint)
///     - Registered via the module system (MapModuleEndpoints or equivalent)
///
///   REVOKED JTI DENYLIST (Task 3.7) — same namespace ClaudeForge.Core.Identity.Ports
///
///   interface IRevokedJtiStorePort
///     /// Add a jti to the denylist. TTL = remaining token life.
///     Task AddAsync(string jti, DateTimeOffset tokenExpiresAt, CancellationToken ct = default);
///
///     /// Returns true if the jti is in the denylist AND the entry has not expired.
///     Task&lt;bool&gt; IsRevokedAsync(string jti, CancellationToken ct = default);
///
///   NAMESPACE: ClaudeForge.Infrastructure.Identity
///
///   sealed class PostgresRevokedJtiStoreAdapter : IRevokedJtiStorePort
///     Constructor: PostgresRevokedJtiStoreAdapter(MarketplaceDbContext db)
///     - Uses revoked_jti table (Guid jti PK, DateTimeOffset expires_at)
///     - AddAsync: inserts; on conflict (duplicate jti) silently ignores
///     - IsRevokedAsync: SELECT WHERE jti = @jti AND expires_at > now()
///
///   EF entity:
///   NAMESPACE: ClaudeForge.Infrastructure.Persistence.Entities
///
///   sealed class RevokedJtiEntity
///     string         Jti       (PK, Guid stored as string)
///     DateTimeOffset ExpiresAt (NOT NULL)
///
///   The revoked_jti table must be configured in MarketplaceDbContext:
///     table name: "revoked_jti"
///     jti column: TEXT PRIMARY KEY
///     expires_at column: TIMESTAMPTZ NOT NULL
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class JwksEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    // RSA key pair used to configure the test WebApplicationFactory
    private static readonly (string PrivatePem, string PublicPem, string Kid) TestKey = GenerateTestKey();

    private static (string PrivatePem, string PublicPem, string Kid) GenerateTestKey()
    {
        using RSA rsa = RSA.Create(keySizeInBits: 2048);
        return (rsa.ExportRSAPrivateKeyPem(), rsa.ExportRSAPublicKeyPem(), "jwks-test-kid-1");
    }

    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public JwksEndpointTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        await ctx.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                org_audit_log,
                organization_invitations,
                organization_members,
                refresh_tokens,
                user_identities,
                organizations,
                users,
                telemetry_aggregates,
                telemetry_events,
                plugin_categories,
                plugin_versions,
                plugins,
                categories
            RESTART IDENTITY CASCADE
            """);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private WebApplicationFactory<Program> CreateFactory(
        IReadOnlyList<(string PublicPem, string Kid)>? extraKeys = null)
    {
        IReadOnlyList<(string PublicPem, string Kid)> allKeys = extraKeys is not null
            ? new[] { (TestKey.PublicPem, TestKey.Kid) }.Concat(extraKeys).ToList()
            : [(TestKey.PublicPem, TestKey.Kid)];

        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace DbContext with the test Postgres
                    ServiceDescriptor? optDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<MarketplaceDbContext>));
                    if (optDescriptor is not null) services.Remove(optDescriptor);

                    ServiceDescriptor? ctxDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(MarketplaceDbContext));
                    if (ctxDescriptor is not null) services.Remove(ctxDescriptor);

                    services.AddDbContext<MarketplaceDbContext>(options =>
                        options.UseNpgsql(_fixture.ConnectionString));

                    // Register a test IJwksProvider with our test public key(s)
                    ServiceDescriptor? jwksDescriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(ClaudeForge.Core.Identity.Ports.IJwksProvider));
                    if (jwksDescriptor is not null) services.Remove(jwksDescriptor);

                    services.AddSingleton<ClaudeForge.Core.Identity.Ports.IJwksProvider>(
                        _ => new ClaudeForge.Infrastructure.Identity.RsaJwksProvider(allKeys));
                });
            });
    }

    // =========================================================================
    // HAPPY PATH — single key
    // =========================================================================

    [Fact]
    public async Task Get_WellKnownJwks_Returns200Ok()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/.well-known/jwks.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WellKnownJwks_ContentType_IsApplicationJson()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/.well-known/jwks.json");

        string? contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/json", contentType);
    }

    [Fact]
    public async Task Get_WellKnownJwks_BodyContains_KeysArray()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);

        // Top-level "keys" array must exist
        Assert.True(doc.RootElement.TryGetProperty("keys", out JsonElement keysElement));
        Assert.Equal(JsonValueKind.Array, keysElement.ValueKind);
    }

    [Fact]
    public async Task Get_WellKnownJwks_KeysArray_ContainsAtLeastOneEntry()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement keysElement = doc.RootElement.GetProperty("keys");

        Assert.True(keysElement.GetArrayLength() >= 1, "JWKS must contain at least one key");
    }

    [Fact]
    public async Task Get_WellKnownJwks_KeyEntry_HasRequiredFields_kty_use_alg_kid_n_e()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement firstKey = doc.RootElement.GetProperty("keys")[0];

        // All six fields are mandatory for a JWKS RSA signing key (RFC 7517)
        Assert.True(firstKey.TryGetProperty("kty", out _), "kty must be present");
        Assert.True(firstKey.TryGetProperty("use", out _), "use must be present");
        Assert.True(firstKey.TryGetProperty("alg", out _), "alg must be present");
        Assert.True(firstKey.TryGetProperty("kid", out _), "kid must be present");
        Assert.True(firstKey.TryGetProperty("n", out _), "n (RSA modulus) must be present");
        Assert.True(firstKey.TryGetProperty("e", out _), "e (RSA exponent) must be present");
    }

    [Fact]
    public async Task Get_WellKnownJwks_KeyEntry_kty_IsRSA()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement firstKey = doc.RootElement.GetProperty("keys")[0];

        Assert.Equal("RSA", firstKey.GetProperty("kty").GetString());
    }

    [Fact]
    public async Task Get_WellKnownJwks_KeyEntry_use_IsSig()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement firstKey = doc.RootElement.GetProperty("keys")[0];

        Assert.Equal("sig", firstKey.GetProperty("use").GetString());
    }

    [Fact]
    public async Task Get_WellKnownJwks_KeyEntry_alg_IsRS256()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement firstKey = doc.RootElement.GetProperty("keys")[0];

        Assert.Equal("RS256", firstKey.GetProperty("alg").GetString());
    }

    [Fact]
    public async Task Get_WellKnownJwks_KeyEntry_kid_MatchesConfiguredKid()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement keysElement = doc.RootElement.GetProperty("keys");

        bool hasExpectedKid = keysElement.EnumerateArray()
            .Any(k => k.TryGetProperty("kid", out JsonElement kid) &&
                      kid.GetString() == TestKey.Kid);

        Assert.True(hasExpectedKid, $"No key found with kid={TestKey.Kid}");
    }

    [Fact]
    public async Task Get_WellKnownJwks_KeyEntry_N_IsNonEmptyBase64Url()
    {
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement firstKey = doc.RootElement.GetProperty("keys")[0];

        string? n = firstKey.GetProperty("n").GetString();
        Assert.NotNull(n);
        Assert.NotEmpty(n);
        // Must be decodable as base64url
        // Base64url uses - and _ instead of + and /; pad with = to make length a multiple of 4
        string padded = n!.PadRight(n!.Length + (4 - n!.Length % 4) % 4, '=')
                         .Replace('-', '+')
                         .Replace('_', '/');
        byte[] decoded = Convert.FromBase64String(padded);
        Assert.True(decoded.Length >= 256, "RSA 2048 modulus must be at least 256 bytes");
    }

    [Fact]
    public async Task Get_WellKnownJwks_PublicKey_N_and_E_MatchTheConfiguredPublicKey()
    {
        // Independently extract N and E from the test public PEM and compare with JWKS response
        using RSA rsaExpected = RSA.Create();
        rsaExpected.ImportFromPem(TestKey.PublicPem);
        RSAParameters expectedParams = rsaExpected.ExportParameters(includePrivateParameters: false);
        string expectedN = Base64UrlEncode(expectedParams.Modulus!);
        string expectedE = Base64UrlEncode(expectedParams.Exponent!);

        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement targetKey = doc.RootElement.GetProperty("keys")
            .EnumerateArray()
            .FirstOrDefault(k => k.TryGetProperty("kid", out JsonElement kid) &&
                                 kid.GetString() == TestKey.Kid);

        Assert.NotEqual(default, targetKey);
        Assert.Equal(expectedN, targetKey.GetProperty("n").GetString());
        Assert.Equal(expectedE, targetKey.GetProperty("e").GetString());
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    // =========================================================================
    // KEY ROTATION — prior key present alongside current key
    // =========================================================================

    [Fact]
    public async Task Get_WellKnownJwks_DuringRotation_BothCurrentAndPriorKeyPresent()
    {
        // Generate a second (prior) key to simulate mid-rotation JWKS
        using RSA priorRsa = RSA.Create(keySizeInBits: 2048);
        string priorPublicPem = priorRsa.ExportRSAPublicKeyPem();
        string priorKid = "prior-key-1";

        // Factory configured with two keys: current (TestKey) + prior
        using WebApplicationFactory<Program> factory = CreateFactory(
            extraKeys: [(priorPublicPem, priorKid)]);
        using HttpClient client = factory.CreateClient();

        string body = await client.GetStringAsync("/.well-known/jwks.json");
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement keysElement = doc.RootElement.GetProperty("keys");

        bool hasCurrentKey = keysElement.EnumerateArray()
            .Any(k => k.TryGetProperty("kid", out JsonElement kid) && kid.GetString() == TestKey.Kid);
        bool hasPriorKey = keysElement.EnumerateArray()
            .Any(k => k.TryGetProperty("kid", out JsonElement kid) && kid.GetString() == priorKid);

        Assert.True(hasCurrentKey, "Current key must be present in JWKS during rotation");
        Assert.True(hasPriorKey, "Prior key must also be present in JWKS during rotation");
        Assert.True(keysElement.GetArrayLength() >= 2,
            "JWKS must contain both current and prior keys during rotation");
    }

    // =========================================================================
    // NO AUTHENTICATION REQUIRED
    // =========================================================================

    [Fact]
    public async Task Get_WellKnownJwks_NoAuthorizationHeader_Returns200()
    {
        // The JWKS endpoint is public — must not require a Bearer token
        using WebApplicationFactory<Program> factory = CreateFactory();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Remove("Authorization");

        HttpResponseMessage response = await client.GetAsync("/.well-known/jwks.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>
/// Integration tests for Group 3, Task 3.7 — revoked_jti Postgres denylist.
///
/// These tests are RED because IRevokedJtiStorePort and PostgresRevokedJtiStoreAdapter
/// do not yet exist (see class-level doc above for contract).
/// </summary>
[Collection(PostgresFixture.CollectionName)]
public sealed class RevokedJtiDenylistTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public RevokedJtiDenylistTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using MarketplaceDbContext ctx = _fixture.CreateContext();
        // Truncate only the revoked_jti table (minimal isolation for this suite)
        await ctx.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE revoked_jti RESTART IDENTITY CASCADE");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort MakeStore() =>
        new ClaudeForge.Infrastructure.Identity.PostgresRevokedJtiStoreAdapter(_fixture.CreateContext());

    // =========================================================================
    // HAPPY PATH — add and detect
    // =========================================================================

    [Fact]
    public async Task IsRevokedAsync_JtiNotInDenylist_ReturnsFalse()
    {
        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();
        string unknownJti = Guid.NewGuid().ToString();

        bool result = await store.IsRevokedAsync(unknownJti);

        Assert.False(result);
    }

    [Fact]
    public async Task AddAsync_ThenIsRevokedAsync_ReturnsTrue()
    {
        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();
        string jti = Guid.NewGuid().ToString();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        await store.AddAsync(jti, expiresAt);
        bool result = await store.IsRevokedAsync(jti);

        Assert.True(result);
    }

    // =========================================================================
    // TTL = REMAINING TOKEN LIFE
    // =========================================================================

    [Fact]
    public async Task IsRevokedAsync_AfterTtlExpiry_ReturnsFalse()
    {
        // Denylist entry with expiresAt already in the past — must not be considered revoked
        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();
        string jti = Guid.NewGuid().ToString();
        // expires_at is 1 second in the past
        DateTimeOffset expiredAt = DateTimeOffset.UtcNow.AddSeconds(-1);

        await store.AddAsync(jti, expiredAt);
        bool result = await store.IsRevokedAsync(jti);

        Assert.False(result, "Expired denylist entry must be treated as not-revoked (TTL honored)");
    }

    [Fact]
    public async Task IsRevokedAsync_EntryNotYetExpired_ReturnsTrue()
    {
        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();
        string jti = Guid.NewGuid().ToString();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);

        await store.AddAsync(jti, expiresAt);
        bool result = await store.IsRevokedAsync(jti);

        Assert.True(result);
    }

    // =========================================================================
    // IDEMPOTENCY — duplicate add must not throw
    // =========================================================================

    [Fact]
    public async Task AddAsync_DuplicateJti_DoesNotThrow()
    {
        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();
        string jti = Guid.NewGuid().ToString();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        await store.AddAsync(jti, expiresAt);
        // Second add with same jti — must be silently ignored (idempotent)
        await store.AddAsync(jti, expiresAt);
    }

    // =========================================================================
    // MULTIPLE JTIs — isolation between entries
    // =========================================================================

    [Fact]
    public async Task IsRevokedAsync_RevokedJti_DoesNotAffectUnrelatedJti()
    {
        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();
        string revokedJti = Guid.NewGuid().ToString();
        string cleanJti = Guid.NewGuid().ToString();

        await store.AddAsync(revokedJti, DateTimeOffset.UtcNow.AddMinutes(15));

        bool revokedResult = await store.IsRevokedAsync(revokedJti);
        bool cleanResult = await store.IsRevokedAsync(cleanJti);

        Assert.True(revokedResult, "Revoked jti must be flagged");
        Assert.False(cleanResult, "Unrelated jti must not be flagged");
    }

    // =========================================================================
    // INTEGRATION WITH TOKEN VALIDATION — denylisted jti rejected
    // =========================================================================

    [Fact]
    public async Task TokenValidation_DenylistedJti_ShouldBeRejected_ByCallerCheckingDenylist()
    {
        // This test verifies the interaction pattern:
        // 1. Issue a token with a known jti
        // 2. Add that jti to the denylist
        // 3. Caller validates token structurally (signature OK) but ALSO checks denylist
        // 4. Denylist reports revoked → caller rejects the token
        //
        // Note: the denylist check is NOT inside ITokenIssuerPort.ValidateAccessToken because
        // that would require the pure crypto adapter to take an I/O dependency. Instead, the
        // use-case or middleware calls IsRevokedAsync after structural validation.
        //
        // This test verifies that the denylist store correctly identifies jti as revoked so
        // the caller can reject the token at the use-case layer.

        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();
        string jti = Guid.NewGuid().ToString();
        DateTimeOffset tokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        // Token is structurally valid but jti is on the denylist
        await store.AddAsync(jti, tokenExpiresAt);

        bool isRevoked = await store.IsRevokedAsync(jti);

        Assert.True(isRevoked, "Use-case must be able to detect revoked jti and reject the request");
    }

    // =========================================================================
    // EDGE CASES
    // =========================================================================

    [Fact]
    public async Task IsRevokedAsync_EmptyJti_ReturnsFalse()
    {
        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();

        bool result = await store.IsRevokedAsync(string.Empty);

        Assert.False(result);
    }

    [Fact]
    public async Task AddAsync_SpecialCharactersInJti_StoredAndQueriedCorrectly()
    {
        // jti values are UUIDs in practice but the store should handle any string safely
        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();
        string jti = "jti-with-special-chars-!@#$%^&*()";
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        await store.AddAsync(jti, expiresAt);
        bool result = await store.IsRevokedAsync(jti);

        Assert.True(result);
    }

    [Fact]
    public async Task AddAsync_LargeNumberOfJtis_AllStoredAndQueryable()
    {
        // Performance edge case: 1 000 distinct jtis
        ClaudeForge.Core.Identity.Ports.IRevokedJtiStorePort store = MakeStore();
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);

        string[] jtis = Enumerable.Range(0, 1000)
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();

        foreach (string jti in jtis)
        {
            await store.AddAsync(jti, expiresAt);
        }

        // Spot-check a random sample
        foreach (string jti in jtis.Take(50))
        {
            bool result = await store.IsRevokedAsync(jti);
            Assert.True(result, $"jti {jti} must be revoked");
        }
    }
}
