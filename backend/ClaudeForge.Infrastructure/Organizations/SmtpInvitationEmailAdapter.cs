using System.Net;
using System.Net.Mail;
using ClaudeForge.Core.Modules.Organizations.Ports;
using Microsoft.Extensions.Configuration;

namespace ClaudeForge.Infrastructure.Organizations;

/// <summary>
/// BCL System.Net.Mail adapter for sending invitation emails.
/// Configuration is read from EMAIL__* environment variables / app settings.
/// Failure bubbles up to the use-case which catches and swallows it (best-effort).
/// </summary>
public sealed class SmtpInvitationEmailAdapter : IInvitationEmailPort
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _fromAddress;
    private readonly string? _username;
    private readonly string? _password;

    public SmtpInvitationEmailAdapter(IConfiguration configuration)
    {
        _host = configuration["EMAIL__SMTP_HOST"] ?? "localhost";
        _port = int.TryParse(configuration["EMAIL__SMTP_PORT"], out int port) ? port : 25;
        _fromAddress = configuration["EMAIL__FROM_ADDRESS"] ?? "noreply@claudeforge.io";
        _username = configuration["EMAIL__USERNAME"];
        _password = configuration["EMAIL__PASSWORD"];
    }

    public async Task SendInvitationAsync(
        string toEmail,
        string orgName,
        string inviterName,
        string invitationToken,
        CancellationToken ct = default)
    {
        using SmtpClient smtp = new(_host, _port);

        if (!string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_password))
        {
            smtp.Credentials = new NetworkCredential(_username, _password);
            smtp.EnableSsl = true;
        }

        string body = string.IsNullOrEmpty(orgName)
            ? $"You have been invited to join an organization on ClaudeForge. Token: {invitationToken}"
            : $"You have been invited by {inviterName} to join {orgName} on ClaudeForge. Token: {invitationToken}";

        MailMessage message = new(
            from: _fromAddress,
            to: toEmail,
            subject: "You have been invited to join an organization on ClaudeForge",
            body: body);

        await smtp.SendMailAsync(message, ct);
    }
}
