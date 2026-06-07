namespace ClaudeForge.Core.Modules.Organizations.Ports;

/// <summary>
/// Port for appending entries to the organization audit log.
/// The log is append-only and never exposed through the API.
/// </summary>
public interface IOrgAuditLogPort
{
    /// <summary>
    /// Appends an audit entry for the given organization action.
    /// </summary>
    Task AppendAsync(
        Guid orgId,
        Guid actorUserId,
        string action,
        string target,
        CancellationToken ct = default);
}
