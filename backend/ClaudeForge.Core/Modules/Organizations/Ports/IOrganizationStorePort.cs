using ClaudeForge.Core.Modules.Organizations.UseCases;

namespace ClaudeForge.Core.Modules.Organizations.Ports;

/// <summary>
/// Port for persisting and querying organizations.
/// </summary>
public interface IOrganizationStorePort
{
    /// <summary>
    /// Returns the organization whose <c>name_normalized</c> matches, or <c>null</c> if none exists.
    /// </summary>
    Task<OrganizationDto?> FindByNameNormalizedAsync(string nameNormalized, CancellationToken ct = default);

    /// <summary>
    /// Creates a new organization record and returns the resulting DTO.
    /// </summary>
    Task<OrganizationDto> CreateAsync(CreateOrganizationRecord record, CancellationToken ct = default);
}
