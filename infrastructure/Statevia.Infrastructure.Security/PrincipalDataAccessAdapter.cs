using Statevia.Core.Application.Contracts.Security;

namespace Statevia.Infrastructure.Security;

/// <summary>
/// <see cref="IPrincipalDataAccess"/> の実装。内部の <see cref="IPlatformDataAccess"/> に委譲する。
/// </summary>
internal sealed class PrincipalDataAccessAdapter : IPrincipalDataAccess
{
    private readonly IPlatformDataAccess _platform;

    public PrincipalDataAccessAdapter(IPlatformDataAccess platform)
    {
        _platform = platform;
    }

    public async Task<PrincipalInfo?> FindPrincipalAsync(Guid principalId, CancellationToken cancellationToken)
    {
        var row = await _platform.FindPrincipalAsync(principalId, cancellationToken).ConfigureAwait(false);
        if (row is null) return null;

        return new PrincipalInfo(
            row.PrincipalId,
            row.PrincipalType,
            row.IsActive,
            row.DisabledAt,
            row.DeletedAt);
    }

    public Task<bool> IsTenantAdminAsync(Guid principalId, CancellationToken cancellationToken) =>
        _platform.IsTenantAdminAsync(principalId, cancellationToken);

    public Task<IReadOnlyList<string>> ExpandPrincipalPermissionKeysAsync(
        Guid principalId, CancellationToken cancellationToken) =>
        _platform.ExpandPrincipalPermissionKeysAsync(principalId, cancellationToken);

    public Task<IReadOnlyList<GroupSnapshot>> GetGroupSnapshotsForPrincipalAsync(
        Guid principalId, CancellationToken cancellationToken) =>
        _platform.GetGroupSnapshotsForPrincipalAsync(principalId, cancellationToken);
}
