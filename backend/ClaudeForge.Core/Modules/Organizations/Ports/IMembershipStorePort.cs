using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.UseCases;

namespace ClaudeForge.Core.Modules.Organizations.Ports;

/// <summary>
/// Port for persisting and querying organization memberships.
/// </summary>
public interface IMembershipStorePort
{
    /// <summary>Adds a member to the organization with the specified role.</summary>
    Task AddMemberAsync(Guid orgId, Guid userId, OrgRole role, CancellationToken ct = default);

    /// <summary>Removes a member from the organization.</summary>
    Task RemoveMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default);

    /// <summary>Updates the role of an existing member.</summary>
    Task UpdateMemberRoleAsync(Guid orgId, Guid userId, OrgRole newRole, CancellationToken ct = default);

    /// <summary>Returns the count of members with the owner role in the organization.</summary>
    Task<int> CountOwnersAsync(Guid orgId, CancellationToken ct = default);

    /// <summary>Returns the member DTO, or <c>null</c> if the user is not a member.</summary>
    Task<MemberDto?> FindMemberAsync(Guid orgId, Guid userId, CancellationToken ct = default);

    /// <summary>Returns the member DTO matched by email, or <c>null</c> if not found.</summary>
    Task<MemberDto?> FindMemberByEmailAsync(Guid orgId, string emailNormalized, CancellationToken ct = default);

    /// <summary>Returns all members of the specified organization.</summary>
    Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid orgId, CancellationToken ct = default);

    /// <summary>Returns all organizations the user is a member of, with their role.</summary>
    Task<IReadOnlyList<OrgSummaryDto>> ListOrgsForUserAsync(Guid userId, CancellationToken ct = default);
}
