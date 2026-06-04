using Microsoft.EntityFrameworkCore;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Abstractions.Services;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Contracts.Admin;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Services;

/// <summary>テナント管理者向け users / groups 管理。</summary>
public interface ITenantAdministrationService
{
    /// <summary>権限カタログを返す。</summary>
    Task<IReadOnlyList<PermissionDefinitionDto>> ListPermissionsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>テナント内ユーザーを一覧する。</summary>
    Task<IReadOnlyList<AdminUserListItemDto>> ListUsersAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>ユーザーを作成する。</summary>
    Task<AdminUserListItemDto> CreateUserAsync(
        Guid callerPrincipalId,
        CreateAdminUserRequest request,
        CancellationToken cancellationToken);

    /// <summary>ユーザーを更新する（有効化・管理者フラグ）。</summary>
    Task<AdminUserListItemDto> UpdateUserAsync(
        Guid callerPrincipalId,
        Guid userId,
        UpdateAdminUserRequest request,
        CancellationToken cancellationToken);

    /// <summary>グループを一覧する。</summary>
    Task<IReadOnlyList<AdminGroupListItemDto>> ListGroupsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken);

    /// <summary>グループを作成する。</summary>
    Task<AdminGroupDetailDto> CreateGroupAsync(
        Guid callerPrincipalId,
        CreateAdminGroupRequest request,
        CancellationToken cancellationToken);

    /// <summary>グループ詳細を返す。</summary>
    Task<AdminGroupDetailDto> GetGroupAsync(
        Guid callerPrincipalId,
        Guid groupId,
        CancellationToken cancellationToken);

    /// <summary>グループメンバーを置換する。</summary>
    Task<AdminGroupDetailDto> SetGroupMembersAsync(
        Guid callerPrincipalId,
        Guid groupId,
        SetAdminGroupMembersRequest request,
        CancellationToken cancellationToken);

    /// <summary>グループ権限を置換する。</summary>
    Task<AdminGroupDetailDto> SetGroupPermissionsAsync(
        Guid callerPrincipalId,
        Guid groupId,
        SetAdminGroupPermissionsRequest request,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
internal sealed class TenantAdministrationService : ITenantAdministrationService
{
    private const string UnauthorizedCode = "UNAUTHORIZED";
    private const string ForbiddenCode = "FORBIDDEN";

    private readonly IDbContextFactory<CoreDbContext> _dbFactory;
    private readonly ITenantContextAccessor _tenantContext;
    private readonly ITenantAdminAuthorization _tenantAdminAuthorization;
    private readonly PasswordCredentialService _passwordCredentialService;
    private readonly IIdGenerator _idGenerator;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public TenantAdministrationService(
        IDbContextFactory<CoreDbContext> dbFactory,
        ITenantContextAccessor tenantContext,
        ITenantAdminAuthorization tenantAdminAuthorization,
        PasswordCredentialService passwordCredentialService,
        IIdGenerator idGenerator)
    {
        _dbFactory = dbFactory;
        _tenantContext = tenantContext;
        _tenantAdminAuthorization = tenantAdminAuthorization;
        _passwordCredentialService = passwordCredentialService;
        _idGenerator = idGenerator;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PermissionDefinitionDto>> ListPermissionsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.PermissionDefinitions
            .AsNoTracking()
            .OrderBy(p => p.PermissionKey)
            .Select(p => new PermissionDefinitionDto
            {
                PermissionKey = p.PermissionKey,
                DisplayLabel = p.DisplayLabel,
                DisplayKey = p.DisplayKey,
                IsSystem = p.IsSystem,
                IsDeprecated = p.IsDeprecated
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminUserListItemDto>> ListUsersAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var userRows = await (
            from user in db.Users.AsNoTracking()
            join link in db.UserPrincipals.AsNoTracking() on user.UserId equals link.UserId
            join principal in db.Principals.AsNoTracking() on link.PrincipalId equals principal.PrincipalId into principals
            from principal in principals.DefaultIfEmpty()
            orderby user.Email
            select new
            {
                user.UserId,
                user.Email,
                user.IsTenantAdmin,
                user.IsActive,
                user.CreatedAt,
                link.PrincipalId,
                PrincipalDisplayName = principal != null ? principal.DisplayName : null
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (userRows.Count == 0)
            return Array.Empty<AdminUserListItemDto>();

        var userIds = userRows.Select(row => row.UserId).ToList();
        var memberships = await db.UserGroupMembers
            .AsNoTracking()
            .Where(m => userIds.Contains(m.UserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var groupsByUser = memberships
            .GroupBy(m => m.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.GroupId).ToList());

        return userRows
            .Select(row =>
            {
                groupsByUser.TryGetValue(row.UserId, out var groupIds);
                return new AdminUserListItemDto
                {
                    UserId = row.UserId,
                    PrincipalId = row.PrincipalId,
                    Email = row.Email,
                    DisplayName = row.PrincipalDisplayName ?? row.Email,
                    IsTenantAdmin = row.IsTenantAdmin,
                    IsActive = row.IsActive,
                    GroupIds = groupIds is null ? Array.Empty<Guid>() : groupIds,
                    CreatedAt = row.CreatedAt
                };
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<AdminUserListItemDto> CreateUserAsync(
        Guid callerPrincipalId,
        CreateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var tenantId = RequireTenantInternalId();
        var email = request.Email.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var emailTaken = await db.Users.AnyAsync(u => u.Email == email, cancellationToken).ConfigureAwait(false);
        if (emailTaken)
            throw new ArgumentException($"User '{email}' already exists.", nameof(request));

        var groupIds = request.GroupIds ?? Array.Empty<Guid>();
        if (groupIds.Count > 0)
            await EnsureGroupIdsExistAsync(db, groupIds, cancellationToken).ConfigureAwait(false);

        var userId = _idGenerator.NewGuid();
        var principalId = _idGenerator.NewGuid();
        var now = DateTime.UtcNow;
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email : request.DisplayName.Trim();

        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = displayName,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Users.Add(new UserRow
        {
            UserId = userId,
            TenantId = tenantId,
            Email = email,
            PasswordHash = _passwordCredentialService.HashPassword(request.Password),
            IsTenantAdmin = request.IsTenantAdmin ?? false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = principalId });
        foreach (var groupId in groupIds)
            db.UserGroupMembers.Add(new UserGroupMemberRow { UserId = userId, GroupId = groupId });

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminUserListItemDto
        {
            UserId = userId,
            PrincipalId = principalId,
            Email = email,
            DisplayName = displayName,
            IsTenantAdmin = request.IsTenantAdmin ?? false,
            IsActive = true,
            GroupIds = groupIds,
            CreatedAt = now
        };
    }

    /// <inheritdoc />
    public async Task<AdminUserListItemDto> UpdateUserAsync(
        Guid callerPrincipalId,
        Guid userId,
        UpdateAdminUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new NotFoundException("User not found.");

        var link = await db.UserPrincipals
            .FirstOrDefaultAsync(up => up.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        if (link is null)
            throw new NotFoundException("User principal link not found.");

        var principal = await db.Principals
            .FirstOrDefaultAsync(p => p.PrincipalId == link.PrincipalId, cancellationToken)
            .ConfigureAwait(false);
        if (principal is null)
            throw new NotFoundException("Principal not found.");

        var now = DateTime.UtcNow;
        if (request.IsTenantAdmin is { } isTenantAdmin)
            user.IsTenantAdmin = isTenantAdmin;
        if (request.IsActive is { } isActive)
        {
            user.IsActive = isActive;
            user.DisabledAt = isActive ? null : now;
            principal.IsActive = isActive;
            principal.DisabledAt = isActive ? null : now;
        }

        user.UpdatedAt = now;
        principal.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var groupIds = await db.UserGroupMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.GroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AdminUserListItemDto
        {
            UserId = user.UserId,
            PrincipalId = link.PrincipalId,
            Email = user.Email,
            DisplayName = principal.DisplayName,
            IsTenantAdmin = user.IsTenantAdmin,
            IsActive = user.IsActive,
            GroupIds = groupIds,
            CreatedAt = user.CreatedAt
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminGroupListItemDto>> ListGroupsAsync(
        Guid callerPrincipalId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Groups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .Select(g => new AdminGroupListItemDto
            {
                GroupId = g.GroupId,
                Name = g.Name,
                IsSystem = g.IsSystem,
                MemberCount = db.UserGroupMembers.Count(m => m.GroupId == g.GroupId),
                PermissionCount = db.GroupPermissions.Count(gp => gp.GroupId == g.GroupId),
                UpdatedAt = g.UpdatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdminGroupDetailDto> CreateGroupAsync(
        Guid callerPrincipalId,
        CreateAdminGroupRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var tenantId = RequireTenantInternalId();
        var name = request.Name.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var nameTaken = await db.Groups.AnyAsync(g => g.Name == name, cancellationToken).ConfigureAwait(false);
        if (nameTaken)
            throw new ArgumentException($"Group '{name}' already exists.", nameof(request));

        var now = DateTime.UtcNow;
        var groupId = _idGenerator.NewGuid();
        db.Groups.Add(new GroupRow
        {
            GroupId = groupId,
            TenantId = tenantId,
            Name = name,
            IsSystem = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AdminGroupDetailDto
        {
            GroupId = groupId,
            Name = name,
            IsSystem = false,
            MemberUserIds = Array.Empty<Guid>(),
            PermissionKeys = Array.Empty<string>()
        };
    }

    /// <inheritdoc />
    public async Task<AdminGroupDetailDto> GetGroupAsync(
        Guid callerPrincipalId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);
        return await LoadGroupDetailAsync(groupId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdminGroupDetailDto> SetGroupMembersAsync(
        Guid callerPrincipalId,
        Guid groupId,
        SetAdminGroupMembersRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var userIds = request.UserIds ?? Array.Empty<Guid>();
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = await db.Groups.FirstOrDefaultAsync(g => g.GroupId == groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
            throw new NotFoundException("Group not found.");

        if (userIds.Count > 0)
        {
            var found = await db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.UserId))
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (found.Count != userIds.Count)
                throw new ArgumentException("One or more user IDs are invalid.", nameof(request));
        }

        var existing = await db.UserGroupMembers
            .Where(m => m.GroupId == groupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.UserGroupMembers.RemoveRange(existing);
        foreach (var userId in userIds)
            db.UserGroupMembers.Add(new UserGroupMemberRow { UserId = userId, GroupId = groupId });

        group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await LoadGroupDetailAsync(groupId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<AdminGroupDetailDto> SetGroupPermissionsAsync(
        Guid callerPrincipalId,
        Guid groupId,
        SetAdminGroupPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantAdminAsync(callerPrincipalId, cancellationToken).ConfigureAwait(false);

        var keys = NormalizeAssignablePermissionKeys(request.PermissionKeys);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = await db.Groups.FirstOrDefaultAsync(g => g.GroupId == groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
            throw new NotFoundException("Group not found.");

        if (keys.Count > 0)
        {
            var catalog = await db.PermissionDefinitions
                .AsNoTracking()
                .Select(p => p.PermissionKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var catalogSet = catalog.ToHashSet(StringComparer.Ordinal);
            var unknown = keys.Where(k => !catalogSet.Contains(k)).ToList();
            if (unknown.Count > 0)
                throw new ArgumentException($"Unknown permission keys: {string.Join(", ", unknown)}.", nameof(request));
        }

        var existing = await db.GroupPermissions
            .Where(gp => gp.GroupId == groupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        db.GroupPermissions.RemoveRange(existing);
        foreach (var key in keys)
            db.GroupPermissions.Add(new GroupPermissionRow { GroupId = groupId, PermissionKey = key });

        group.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await LoadGroupDetailAsync(groupId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdminGroupDetailDto> LoadGroupDetailAsync(Guid groupId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var group = await db.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.GroupId == groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
            throw new NotFoundException("Group not found.");

        var memberUserIds = await db.UserGroupMembers
            .AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var permissionKeys = await db.GroupPermissions
            .AsNoTracking()
            .Where(gp => gp.GroupId == groupId)
            .Select(gp => gp.PermissionKey)
            .OrderBy(k => k)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AdminGroupDetailDto
        {
            GroupId = group.GroupId,
            Name = group.Name,
            IsSystem = group.IsSystem,
            MemberUserIds = memberUserIds,
            PermissionKeys = permissionKeys
        };
    }

    private static IReadOnlyList<string> NormalizeAssignablePermissionKeys(IReadOnlyList<string>? keys)
    {
        if (keys is null || keys.Count == 0)
            return Array.Empty<string>();

        return keys
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Where(k => !string.Equals(k, WellKnownPermissionKeys.TenantAdmin, StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static async Task EnsureGroupIdsExistAsync(
        CoreDbContext db,
        IReadOnlyList<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        var found = await db.Groups
            .AsNoTracking()
            .Where(g => groupIds.Contains(g.GroupId))
            .Select(g => g.GroupId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (found.Count != groupIds.Count)
            throw new ArgumentException("One or more group IDs are invalid.");
    }

    private Guid RequireTenantInternalId()
    {
        if (!_tenantContext.IsResolved || _tenantContext.TenantInternalId is not { } tenantId)
            throw new UnauthorizedException("Authentication required.", UnauthorizedCode);
        return tenantId;
    }

    private async Task EnsureTenantAdminAsync(Guid principalId, CancellationToken cancellationToken)
    {
        if (!await _tenantAdminAuthorization.IsTenantAdminAsync(principalId, cancellationToken).ConfigureAwait(false))
            throw new ForbiddenException("Tenant administrator required.", ForbiddenCode);
    }
}
