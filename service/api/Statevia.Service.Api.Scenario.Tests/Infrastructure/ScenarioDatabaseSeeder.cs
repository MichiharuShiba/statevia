using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;
using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Scenario.Tests.Infrastructure;

/// <summary>
/// シナリオテスト用のテストデータ投入ヘルパー。
/// </summary>
/// <remarks>
/// <para>シナリオテストは HTTP 経由のみで動作を検証するが、ユーザー・テナント・プロジェクトの初期データは
/// DB に直接投入する必要がある。本クラスはその投入を担う。</para>
/// <para>各テストは一意の suffix（Guid）でデータを作成し、テスト間のデータ干渉を避ける。</para>
/// </remarks>
public sealed class ScenarioDatabaseSeeder(string connectionString)
{
    private readonly NpgsqlScenarioDbContextFactory _factory = CreateFactory(connectionString);

    /// <summary>
    /// テスト用ユーザーを既定テナントに投入し、ユーザー名を返す。
    /// </summary>
    /// <param name="username">ユーザー名（テナント内一意）。</param>
    /// <param name="password">平文パスワード。</param>
    /// <param name="isTenantAdmin">テナント管理者フラグ。</param>
    /// <param name="permissionKeys">
    /// 付与する permission key。省略時は <c>definitions.write</c> と <c>executions.write</c> を付与する。
    /// </param>
    /// <returns>作成したユーザー名。</returns>
    public async Task<string> SeedUserAsync(
        string username,
        string password,
        bool isTenantAdmin = true,
        string[]? permissionKeys = null)
    {
        var userId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var passwordHasher = new PasswordCredentialService();
        var keys = permissionKeys
            ?? [WellKnownPermissionKeys.DefinitionsWrite, WellKnownPermissionKeys.ExecutionsWrite];

        await using var db = await _factory.CreateDbContextAsync();

        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = ScenarioTenantIds.DefaultTenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = username,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Users.Add(new UserRow
        {
            UserId = userId,
            TenantId = ScenarioTenantIds.DefaultTenantId,
            Username = username,
            Email = null,
            PasswordHash = passwordHasher.HashPassword(password),
            IsTenantAdmin = isTenantAdmin,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = principalId });

        if (keys.Length > 0)
        {
            db.Groups.Add(new GroupRow
            {
                GroupId = groupId,
                TenantId = ScenarioTenantIds.DefaultTenantId,
                Name = $"scenario-group-{groupId:N}",
                IsSystem = false,
                CreatedAt = now,
                UpdatedAt = now
            });
            foreach (var key in keys.Distinct(StringComparer.Ordinal))
            {
                db.GroupPermissions.Add(new GroupPermissionRow
                {
                    GroupId = groupId,
                    PermissionKey = key
                });
            }
            db.UserGroupMembers.Add(new UserGroupMemberRow { UserId = userId, GroupId = groupId });
        }

        await db.SaveChangesAsync();

        return username;
    }

    /// <summary>
    /// テスト用ユーザーを既定テナントに投入し、グループ権限も付与して Principal ID を返す。
    /// </summary>
    /// <param name="username">ユーザー名。</param>
    /// <param name="password">平文パスワード。</param>
    /// <param name="permissionKeys">付与する semantic permission key の一覧。</param>
    /// <returns>作成した Principal ID。</returns>
    public async Task<Guid> SeedUserWithPermissionsAsync(
        string username,
        string password,
        params string[] permissionKeys)
    {
        var userId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var passwordHasher = new PasswordCredentialService();

        await using var db = await _factory.CreateDbContextAsync();

        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = ScenarioTenantIds.DefaultTenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = username,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Users.Add(new UserRow
        {
            UserId = userId,
            TenantId = ScenarioTenantIds.DefaultTenantId,
            Username = username,
            Email = null,
            PasswordHash = passwordHasher.HashPassword(password),
            IsTenantAdmin = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = principalId });
        db.Groups.Add(new GroupRow
        {
            GroupId = groupId,
            TenantId = ScenarioTenantIds.DefaultTenantId,
            Name = $"scenario-group-{groupId:N}",
            IsSystem = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        foreach (var key in permissionKeys.Distinct(StringComparer.Ordinal))
        {
            db.GroupPermissions.Add(new GroupPermissionRow
            {
                GroupId = groupId,
                PermissionKey = key
            });
        }
        db.UserGroupMembers.Add(new UserGroupMemberRow { UserId = userId, GroupId = groupId });
        await db.SaveChangesAsync();

        return principalId;
    }

    /// <summary>
    /// 別テナントを作成し、そのテナント ID を返す。
    /// </summary>
    /// <param name="tenantKeySuffix">テナントキーに付与するサフィックス（一意性のため Guid 推奨）。</param>
    /// <returns>作成したテナントの内部 UUID。</returns>
    public async Task<Guid> SeedTenantAsync(string tenantKeySuffix)
    {
        var tenantId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = await _factory.CreateDbContextAsync();
        db.Tenants.Add(new TenantRow
        {
            TenantId = tenantId,
            TenantKey = $"scenario-{tenantKeySuffix}",
            DisplayName = $"Scenario Tenant {tenantKeySuffix}",
            Lifecycle = TenantLifecycle.Active,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        return tenantId;
    }

    /// <summary>
    /// テナントにユーザーを投入し、ユーザー名を返す（指定テナント向け）。
    /// </summary>
    /// <param name="tenantId">投入先テナント内部 UUID。</param>
    /// <param name="username">ユーザー名。</param>
    /// <param name="password">平文パスワード。</param>
    /// <param name="isTenantAdmin">テナント管理者フラグ。</param>
    /// <param name="permissionKeys">
    /// 付与する permission key。省略時は <c>definitions.write</c> と <c>executions.write</c> を付与する。
    /// </param>
    /// <returns>作成したユーザー名。</returns>
    public async Task<string> SeedUserInTenantAsync(
        Guid tenantId,
        string username,
        string password,
        bool isTenantAdmin = true,
        string[]? permissionKeys = null)
    {
        var userId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var passwordHasher = new PasswordCredentialService();

        await using var db = await _factory.CreateDbContextAsync();
        db.Principals.Add(new PrincipalRow
        {
            PrincipalId = principalId,
            TenantId = tenantId,
            PrincipalScope = PrincipalScope.Tenant,
            PrincipalType = PrincipalType.User,
            DisplayName = username,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Users.Add(new UserRow
        {
            UserId = userId,
            TenantId = tenantId,
            Username = username,
            Email = null,
            PasswordHash = passwordHasher.HashPassword(password),
            IsTenantAdmin = isTenantAdmin,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.UserPrincipals.Add(new UserPrincipalRow { UserId = userId, PrincipalId = principalId });

        var keys = permissionKeys
            ?? [WellKnownPermissionKeys.DefinitionsWrite, WellKnownPermissionKeys.ExecutionsWrite];
        if (keys.Length > 0)
        {
            var groupId = Guid.NewGuid();
            db.Groups.Add(new GroupRow
            {
                GroupId = groupId,
                TenantId = tenantId,
                Name = $"scenario-group-{groupId:N}",
                IsSystem = false,
                CreatedAt = now,
                UpdatedAt = now
            });
            foreach (var key in keys.Distinct(StringComparer.Ordinal))
            {
                db.GroupPermissions.Add(new GroupPermissionRow
                {
                    GroupId = groupId,
                    PermissionKey = key
                });
            }
            db.UserGroupMembers.Add(new UserGroupMemberRow { UserId = userId, GroupId = groupId });
        }

        await db.SaveChangesAsync();

        return username;
    }

    /// <summary>
    /// プロジェクトを作成し、オプションで別テナントへのアクセス権を付与する。
    /// </summary>
    /// <param name="ownerTenantId">オーナーテナント内部 UUID。</param>
    /// <param name="consumerTenantId">アクセス付与先テナント UUID（null の場合は付与しない）。</param>
    /// <param name="role">付与するロール。</param>
    /// <returns>作成したプロジェクト ID。</returns>
    public async Task<Guid> SeedProjectWithAccessAsync(
        Guid ownerTenantId,
        Guid? consumerTenantId,
        ProjectAccessRole? role)
    {
        var projectId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Projects
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.OwnerTenantId == ownerTenantId && p.Slug == "default");

        if (existing is null)
        {
            db.Projects.Add(new ProjectRow
            {
                ProjectId = projectId,
                OwnerTenantId = ownerTenantId,
                Slug = "default",
                DisplayName = "Scenario Project",
                Visibility = ProjectVisibility.Private,
                CreatedAt = now
            });
        }
        else
        {
            projectId = existing.ProjectId;
        }

        if (consumerTenantId.HasValue && role.HasValue)
        {
            db.ProjectAccesses.Add(new ProjectAccessRow
            {
                ProjectId = projectId,
                TenantId = consumerTenantId.Value,
                Role = role.Value,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync();
        return projectId;
    }

    private static NpgsqlScenarioDbContextFactory CreateFactory(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new NpgsqlScenarioDbContextFactory(options);
    }

    /// <summary>シナリオテスト用の EF Core コンテキストファクトリ（テナントフィルタ無効）。</summary>
    private sealed class NpgsqlScenarioDbContextFactory(DbContextOptions<CoreDbContext> options)
        : IDbContextFactory<CoreDbContext>
    {
        public CoreDbContext CreateDbContext() =>
            new(options, new DisabledTenantContextAccessor(), DisabledTenantQueryFilterOptions.Instance);

        public Task<CoreDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    /// <summary>テナントフィルタを無効にするコンテキストアクセサー（シードデータ直接投入用）。</summary>
    private sealed class DisabledTenantContextAccessor : ITenantContextAccessor
    {
        public bool IsResolved => false;
        public Guid? TenantId => null;
        public string? TenantKey => null;
        public Guid? PrincipalId => null;
        public IReadOnlySet<string>? EffectivePermissionKeys => null;
        public IDisposable SetContext(TenantContextState? state) => NullDisposable.Instance;
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
