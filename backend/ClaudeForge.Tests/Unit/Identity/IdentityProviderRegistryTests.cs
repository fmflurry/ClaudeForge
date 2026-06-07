using ClaudeForge.Core.Identity.Ports;
using ClaudeForge.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;

namespace ClaudeForge.Tests.Unit.Identity;

/// <summary>
/// Unit tests for Group 4, Tasks 4.1 + 4.2 — IIdentityProviderRegistry.
///
/// These tests are RED because the following production types DO NOT YET EXIST.
/// The coder MUST create them to turn RED → GREEN.
///
/// ─── Core ports (ClaudeForge.Core.Identity.Ports) ────────────────────────────
///
///   interface IIdentityProviderPort
///     /// Builds the IdP authorization redirect URL.
///     string BuildAuthorizationUrl(
///         string provider,
///         string codeChallenge,
///         string state,
///         string redirectUri)
///
///     /// Exchanges an authorization code for the raw (signed JWT) id_token string.
///     Task&lt;string&gt; ExchangeCodeAsync(
///         string provider,
///         string code,
///         string codeVerifier,
///         string redirectUri,
///         CancellationToken ct = default)
///
///     /// Validates the id_token and returns the verified identity claims.
///     Task&lt;VerifiedIdentity&gt; ValidateIdTokenAsync(
///         string provider,
///         string rawIdToken,
///         CancellationToken ct = default)
///
///   sealed record VerifiedIdentity(
///     string Subject,
///     string Email,
///     bool   EmailVerified,
///     string Name)
///
///   interface IIdentityProviderRegistry
///     /// Resolves the named provider adapter.
///     /// Unknown or disabled provider → UnsupportedProviderException.
///     IIdentityProviderPort Resolve(string providerName)
///
///   sealed class UnsupportedProviderException : Exception
///     UnsupportedProviderException(string providerName)
///     string ProviderName { get; }
///
/// ─── Infrastructure (ClaudeForge.Infrastructure.Identity) ────────────────────
///
///   sealed class IdentityProviderRegistry : IIdentityProviderRegistry
///     IdentityProviderRegistry(IConfiguration configuration, IEnumerable&lt;IIdentityProviderPort&gt; providers)
///     /// Enabled providers sourced from OIDC__ENABLEDPROVIDERS (comma-separated list, e.g. "google,microsoft").
///     /// The registry resolves by matching provider name case-insensitively.
///     IIdentityProviderPort Resolve(string providerName)
///
/// ─── Provider adapter naming contract ────────────────────────────────────────
///   Each adapter must expose its canonical name via a static const or property:
///     GoogleIdentityProviderAdapter.ProviderName  = "google"
///     MicrosoftIdentityProviderAdapter.ProviderName = "microsoft"
///   The registry uses this name to match against OIDC__ENABLEDPROVIDERS.
///
/// ─── Test strategy ───────────────────────────────────────────────────────────
///   All tests are pure unit tests (no Docker, no network).
///   The registry receives NSubstitute fakes for the adapter slots.
/// </summary>
public sealed class IdentityProviderRegistryTests
{
    // =========================================================================
    // Helpers — build IConfiguration with a given OIDC__ENABLEDPROVIDERS value
    // =========================================================================

    private static IConfiguration MakeConfig(string enabledProviders) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OIDC__ENABLEDPROVIDERS"] = enabledProviders,
            })
            .Build();

    private static IIdentityProviderPort MakeFakeAdapter(string name)
    {
        IIdentityProviderPort adapter = NSubstitute.Substitute.For<IIdentityProviderPort>();
        // The registry should be able to inspect the provider name of each adapter.
        // Convention: adapters expose ProviderName; the registry matches against it.
        // Fakes are introduced via DI; for unit tests we inject a helper that wraps
        // the fake so the registry can discover its name.
        // Implemented via a thin named-adapter record that satisfies the port and
        // exposes a name string — the actual adapters (google/microsoft) also implement
        // this pattern but are tested separately.
        return new NamedFakeAdapter(name, adapter);
    }

    // Thin wrapper so the registry can read the provider name from a fake adapter.
    // The registry recognises any IIdentityProviderPort that also implements
    // INamedIdentityProviderPort (marker interface with ProviderName property).
    private sealed class NamedFakeAdapter : IIdentityProviderPort, INamedIdentityProviderPort
    {
        private readonly IIdentityProviderPort _inner;

        public NamedFakeAdapter(string providerName, IIdentityProviderPort inner)
        {
            ProviderName = providerName;
            _inner = inner;
        }

        public string ProviderName { get; }

        public string BuildAuthorizationUrl(
            string provider, string codeChallenge, string state, string redirectUri, string nonce = "") =>
            _inner.BuildAuthorizationUrl(provider, codeChallenge, state, redirectUri, nonce);

        public Task<string> ExchangeCodeAsync(
            string provider, string code, string codeVerifier, string redirectUri,
            CancellationToken ct) =>
            _inner.ExchangeCodeAsync(provider, code, codeVerifier, redirectUri, ct);

        public Task<VerifiedIdentity> ValidateIdTokenAsync(
            string provider, string rawIdToken, CancellationToken ct) =>
            _inner.ValidateIdTokenAsync(provider, rawIdToken, ct);
    }

    // =========================================================================
    // Resolve — resolves google and microsoft adapters by name
    // =========================================================================

    [Fact]
    public void Resolve_GoogleEnabled_ReturnsGoogleAdapter()
    {
        // Arrange
        IConfiguration config = MakeConfig("google,microsoft");
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderPort microsoftAdapter = MakeFakeAdapter("microsoft");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter, microsoftAdapter });

        // Act
        IIdentityProviderPort resolved = registry.Resolve("google");

        // Assert — must return the adapter whose name is "google"
        Assert.Same(googleAdapter, resolved);
    }

    [Fact]
    public void Resolve_MicrosoftEnabled_ReturnsMicrosoftAdapter()
    {
        // Arrange
        IConfiguration config = MakeConfig("google,microsoft");
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderPort microsoftAdapter = MakeFakeAdapter("microsoft");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter, microsoftAdapter });

        // Act
        IIdentityProviderPort resolved = registry.Resolve("microsoft");

        // Assert — must return the adapter whose name is "microsoft"
        Assert.Same(microsoftAdapter, resolved);
    }

    [Fact]
    public void Resolve_ProviderNameIsCaseInsensitive_ReturnsAdapter()
    {
        // Arrange — config stores lowercase "google"; caller passes "Google"
        IConfiguration config = MakeConfig("google");
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter });

        // Act — mixed case should still resolve
        IIdentityProviderPort resolved = registry.Resolve("Google");

        // Assert
        Assert.Same(googleAdapter, resolved);
    }

    // =========================================================================
    // Resolve — unknown provider raises UnsupportedProviderException
    // =========================================================================

    [Fact]
    public void Resolve_UnknownProvider_ThrowsUnsupportedProviderException()
    {
        // Arrange
        IConfiguration config = MakeConfig("google,microsoft");
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderPort microsoftAdapter = MakeFakeAdapter("microsoft");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter, microsoftAdapter });

        // Act & Assert
        UnsupportedProviderException ex = Assert.Throws<UnsupportedProviderException>(
            () => registry.Resolve("github"));

        Assert.Equal("github", ex.ProviderName);
    }

    [Fact]
    public void Resolve_EmptyProviderName_ThrowsUnsupportedProviderException()
    {
        // Arrange
        IConfiguration config = MakeConfig("google");
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter });

        // Act & Assert — empty string is not a valid provider
        UnsupportedProviderException ex = Assert.Throws<UnsupportedProviderException>(
            () => registry.Resolve(string.Empty));

        Assert.Equal(string.Empty, ex.ProviderName);
    }

    [Fact]
    public void Resolve_NullProviderName_ThrowsUnsupportedProviderException()
    {
        // Arrange
        IConfiguration config = MakeConfig("google");
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter });

        // Act & Assert — null is not a valid provider name
        Assert.ThrowsAny<Exception>(() => registry.Resolve(null!));
    }

    // =========================================================================
    // Resolve — only enabled providers are resolvable (OIDC__ENABLEDPROVIDERS)
    // =========================================================================

    [Fact]
    public void Resolve_ProviderExistsButNotEnabled_ThrowsUnsupportedProviderException()
    {
        // Arrange — microsoft adapter is registered but NOT in OIDC__ENABLEDPROVIDERS
        IConfiguration config = MakeConfig("google"); // only google enabled
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderPort microsoftAdapter = MakeFakeAdapter("microsoft");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter, microsoftAdapter });

        // Act & Assert — even though microsoft adapter exists, it should not be resolvable
        UnsupportedProviderException ex = Assert.Throws<UnsupportedProviderException>(
            () => registry.Resolve("microsoft"));

        Assert.Equal("microsoft", ex.ProviderName);
    }

    [Fact]
    public void Resolve_NoProvidersEnabled_ThrowsForAnyName()
    {
        // Arrange — empty enabled list
        IConfiguration config = MakeConfig(string.Empty);
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter });

        // Act & Assert
        Assert.Throws<UnsupportedProviderException>(() => registry.Resolve("google"));
    }

    [Fact]
    public void Resolve_EnabledProvidersWithWhitespace_TrimmedCorrectly()
    {
        // Arrange — OIDC__ENABLEDPROVIDERS has extra spaces (common operator error)
        IConfiguration config = MakeConfig("  google  ,  microsoft  ");
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderPort microsoftAdapter = MakeFakeAdapter("microsoft");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter, microsoftAdapter });

        // Act — both should still resolve despite whitespace in config
        IIdentityProviderPort resolvedGoogle = registry.Resolve("google");
        IIdentityProviderPort resolvedMicrosoft = registry.Resolve("microsoft");

        // Assert
        Assert.Same(googleAdapter, resolvedGoogle);
        Assert.Same(microsoftAdapter, resolvedMicrosoft);
    }

    [Fact]
    public void Resolve_OnlyGoogleEnabled_MicrosoftThrows_GoogleSucceeds()
    {
        // Arrange
        IConfiguration config = MakeConfig("google");
        IIdentityProviderPort googleAdapter = MakeFakeAdapter("google");
        IIdentityProviderPort microsoftAdapter = MakeFakeAdapter("microsoft");
        IIdentityProviderRegistry registry = new IdentityProviderRegistry(
            config,
            new[] { googleAdapter, microsoftAdapter });

        // Act — google resolves; microsoft throws
        IIdentityProviderPort resolvedGoogle = registry.Resolve("google");
        Assert.Same(googleAdapter, resolvedGoogle);

        Assert.Throws<UnsupportedProviderException>(() => registry.Resolve("microsoft"));
    }

    // =========================================================================
    // UnsupportedProviderException — contract verification
    // =========================================================================

    [Fact]
    public void UnsupportedProviderException_StoresProviderName()
    {
        // Arrange & Act
        UnsupportedProviderException ex = new("acme-sso");

        // Assert — exception must carry the provider name for error responses
        Assert.Equal("acme-sso", ex.ProviderName);
    }

    [Fact]
    public void UnsupportedProviderException_IsException()
    {
        // Assert — must derive from Exception so it can be caught at boundary
        Assert.True(typeof(Exception).IsAssignableFrom(typeof(UnsupportedProviderException)));
    }
}
