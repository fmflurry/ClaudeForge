namespace ClaudeForge.Core.Modules.Organizations.Ports;

/// <summary>
/// Port for sending invitation emails. Failure is best-effort — the use-case
/// catches exceptions and continues so that the invitation is still recorded.
/// </summary>
public interface IInvitationEmailPort
{
    /// <summary>
    /// Sends an invitation email to the specified address.
    /// </summary>
    Task SendInvitationAsync(
        string toEmail,
        string orgName,
        string inviterName,
        string invitationToken,
        CancellationToken ct = default);
}
