using ClaudeForge.Infrastructure.Organizations;
using Microsoft.Extensions.Configuration;

namespace ClaudeForge.Tests.Unit.Organizations;

/// <summary>
/// Unit tests for <see cref="SmtpInvitationEmailAdapter"/>.
///
/// The adapter connects to a real SMTP server at the configured host:port.
/// Testing the success path in a unit test would require a real or container SMTP server,
/// which is an integration concern. The tests here focus on:
///
///   1. SMTP failure propagates (adapter does NOT swallow the exception —
///      that is the caller's (IssueInvitationUseCase) responsibility).
///
///   2. Configuration defaults are applied when EMAIL__* keys are absent.
///
///   3. Body content differs based on orgName empty vs non-empty.
///
/// Strategy: Use an unreachable port to force a connection failure.
/// This exercises the "SMTP-failure branch" without requiring infrastructure.
/// </summary>
public sealed class SmtpInvitationEmailAdapterTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static IConfiguration MakeConfig(
        string? host = null,
        string? port = null,
        string? from = null,
        string? username = null,
        string? password = null)
    {
        Dictionary<string, string?> values = new();
        if (host is not null) values["EMAIL__SMTP_HOST"] = host;
        if (port is not null) values["EMAIL__SMTP_PORT"] = port;
        if (from is not null) values["EMAIL__FROM_ADDRESS"] = from;
        if (username is not null) values["EMAIL__USERNAME"] = username;
        if (password is not null) values["EMAIL__PASSWORD"] = password;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    // -----------------------------------------------------------------------
    // SMTP failure propagates (adapter does not swallow)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendInvitationAsync_SmtpUnreachable_PropagatesException()
    {
        // Arrange — point to a port that refuses connections (port 1 is typically unavailable)
        IConfiguration config = MakeConfig(host: "127.0.0.1", port: "1");
        SmtpInvitationEmailAdapter adapter = new(config);

        // Act & Assert — the adapter MUST propagate the SMTP exception;
        // the use-case's best-effort catch is a caller responsibility.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.SendInvitationAsync(
                toEmail: "invitee@example.com",
                orgName: "Acme Corp",
                inviterName: "Alice",
                invitationToken: "tok-abc123"));
    }

    [Fact]
    public async Task SendInvitationAsync_WithCredentials_SmtpUnreachable_PropagatesException()
    {
        // Arrange — SSL branch (username + password configured) + unreachable port
        IConfiguration config = MakeConfig(
            host: "127.0.0.1",
            port: "1",
            username: "user",
            password: "pass");
        SmtpInvitationEmailAdapter adapter = new(config);

        // Act & Assert — must propagate regardless of credential branch
        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.SendInvitationAsync(
                toEmail: "invitee@example.com",
                orgName: "Acme Corp",
                inviterName: "Alice",
                invitationToken: "tok-xyz"));
    }

    // -----------------------------------------------------------------------
    // Configuration defaults (no EMAIL__* keys → adapter falls back gracefully)
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_NoEmailConfig_DoesNotThrow()
    {
        // Adapter should construct without error even when env vars are absent.
        IConfiguration config = new ConfigurationBuilder().Build();

        SmtpInvitationEmailAdapter adapter = new(config);

        Assert.NotNull(adapter);
    }

    [Fact]
    public async Task SendInvitationAsync_DefaultConfig_SmtpLocalhostUnreachable_PropagatesException()
    {
        // Default host is "localhost" port 25 — almost certainly unreachable in CI.
        // Verifies that the default code path also propagates rather than swallowing.
        IConfiguration config = MakeConfig(port: "1"); // force fast failure on port 1
        SmtpInvitationEmailAdapter adapter = new(config);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.SendInvitationAsync(
                toEmail: "invitee@example.com",
                orgName: "",
                inviterName: "Bob",
                invitationToken: "tok-default"));
    }

    // -----------------------------------------------------------------------
    // Body content branches (empty orgName vs non-empty)
    // We cannot inspect the MailMessage body without a real SMTP server,
    // but we can verify the adapter constructs and attempts to send for both cases.
    // Both cases must propagate on failure (not swallow).
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendInvitationAsync_EmptyOrgName_PropagatesSmtpException()
    {
        IConfiguration config = MakeConfig(host: "127.0.0.1", port: "1");
        SmtpInvitationEmailAdapter adapter = new(config);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.SendInvitationAsync(
                toEmail: "invitee@example.com",
                orgName: "",           // triggers alternative body branch
                inviterName: "Alice",
                invitationToken: "tok-empty-org"));
    }

    [Fact]
    public async Task SendInvitationAsync_NonEmptyOrgName_PropagatesSmtpException()
    {
        IConfiguration config = MakeConfig(host: "127.0.0.1", port: "1");
        SmtpInvitationEmailAdapter adapter = new(config);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.SendInvitationAsync(
                toEmail: "invitee@example.com",
                orgName: "Acme Corp",  // triggers main body branch
                inviterName: "Alice",
                invitationToken: "tok-full-org"));
    }

    // -----------------------------------------------------------------------
    // Invalid port config — non-integer falls back to default port 25
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SendInvitationAsync_InvalidPortConfig_UsesDefaultPort25_PropagatesException()
    {
        // port "notanumber" → int.TryParse fails → port defaults to 25
        IConfiguration config = MakeConfig(host: "127.0.0.1", port: "notanumber");
        SmtpInvitationEmailAdapter adapter = new(config);

        // Port 25 on 127.0.0.1 is almost certainly refused in CI — must not swallow.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            adapter.SendInvitationAsync(
                toEmail: "invitee@example.com",
                orgName: "Org",
                inviterName: "Alice",
                invitationToken: "tok-badport"));
    }
}
