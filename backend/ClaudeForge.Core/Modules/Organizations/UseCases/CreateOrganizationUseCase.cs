using ClaudeForge.Core.Identity;
using ClaudeForge.Core.Modules.Organizations.Ports;
using ClaudeForge.Core.Shared.Authorization;
using ClaudeForge.Core.Shared.Exceptions;

namespace ClaudeForge.Core.Modules.Organizations.UseCases;

/// <summary>
/// Creates a new organization and assigns the creator as owner.
/// </summary>
public sealed class CreateOrganizationUseCase
{
    private readonly ICurrentUser _currentUser;
    private readonly IOrganizationStorePort _orgStore;
    private readonly IMembershipStorePort _membershipStore;
    private readonly IOrgAuditLogPort _auditLog;

    public CreateOrganizationUseCase(
        ICurrentUser currentUser,
        IOrganizationStorePort orgStore,
        IMembershipStorePort membershipStore,
        IOrgAuditLogPort auditLog)
    {
        _currentUser = currentUser;
        _orgStore = orgStore;
        _membershipStore = membershipStore;
        _auditLog = auditLog;
    }

    public async Task<OrganizationDto> ExecuteAsync(
        CreateOrganizationCommand command,
        CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthenticatedException();

        Guid userId = _currentUser.UserId.Value;

        string trimmedName = command.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedName))
            throw new ProblemDetailsException("Organization name must not be empty.");

        string nameNormalized = trimmedName.ToLowerInvariant();
        string slug = command.Slug ?? nameNormalized.Replace(' ', '-');

        OrganizationDto? existing = await _orgStore.FindByNameNormalizedAsync(nameNormalized, ct);
        if (existing is not null)
            throw new DuplicateOrgNameException();

        Guid orgId = Guid.NewGuid();
        CreateOrganizationRecord record = new(
            Id: orgId,
            Name: trimmedName,
            NameNormalized: nameNormalized,
            Slug: slug,
            CreatedBy: userId,
            CreatedAt: DateTimeOffset.UtcNow);

        OrganizationDto created = await _orgStore.CreateAsync(record, ct);

        await _membershipStore.AddMemberAsync(created.Id, userId, OrgRole.Owner, ct);

        await _auditLog.AppendAsync(
            orgId: created.Id,
            actorUserId: userId,
            action: "org.created",
            target: $"org:{created.Id}",
            ct: ct);

        return created;
    }
}
