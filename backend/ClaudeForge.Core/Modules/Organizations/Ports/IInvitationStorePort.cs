using ClaudeForge.Core.Modules.Organizations.UseCases;

namespace ClaudeForge.Core.Modules.Organizations.Ports;

/// <summary>
/// Port for persisting and querying organization invitations.
/// </summary>
public interface IInvitationStorePort
{
    /// <summary>Creates a new invitation and returns the resulting DTO.</summary>
    Task<InvitationDto> CreateAsync(CreateInvitationRecord record, CancellationToken ct = default);

    /// <summary>Returns the invitation by ID, or <c>null</c> if not found.</summary>
    Task<InvitationDto?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns a pending invitation for the given organization and normalized email,
    /// or <c>null</c> if none exists.
    /// </summary>
    Task<InvitationDto?> FindPendingByOrgAndEmailAsync(Guid orgId, string emailNormalized, CancellationToken ct = default);

    /// <summary>
    /// Updates the status of an invitation along with optional timestamps.
    /// </summary>
    Task UpdateStatusAsync(
        Guid id,
        string newStatus,
        DateTimeOffset? acceptedAt,
        DateTimeOffset? revokedAt,
        CancellationToken ct = default);
}
