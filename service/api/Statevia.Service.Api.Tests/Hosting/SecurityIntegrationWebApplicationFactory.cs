using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Statevia.Infrastructure.Modules;
using Statevia.Service.Api.Hosting;
using Statevia.Infrastructure.Security;
using Statevia.Infrastructure.Persistence;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary>セキュリティ統合テスト用の in-memory SQLite <see cref="WebApplicationFactory{TEntryPoint}"/>。</summary>
public sealed class SecurityIntegrationWebApplicationFactory : WebApplicationFactory<Statevia.Service.Api.Program>
{
    private readonly SqliteTestDatabase _database = new();
    private string? _cachedApiKeyPlainText;

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Environments.Development);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IDbContextFactory<CoreDbContext>>();
            services.AddSingleton<IDbContextFactory<CoreDbContext>>(_database.Factory);
            services.Configure<ModuleHostOptions>(options =>
                options.OwnerTenantId = TestTenantIds.DefaultTenantId.ToString("D"));
        });
    }

    /// <summary>統合テスト DB にユーザーをシードする。</summary>
    /// <param name="loginIdentifier">ログイン識別子（@ ありならメール、なしなら username のみ）。</param>
    /// <param name="password">平文パスワード。</param>
    /// <param name="isTenantAdmin">テナント管理者フラグ。</param>
    /// <returns>作成した Principal ID。</returns>
    public async Task<Guid> SeedUserPrincipalAsync(
        string loginIdentifier,
        string password,
        bool isTenantAdmin = false)
    {
        await using var scope = Services.CreateAsyncScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
        return await SecurityTestSeed.SeedUserAsync(
            _database,
            loginIdentifier,
            password,
            isTenantAdmin: isTenantAdmin);
    }

    /// <summary>統合テスト DB にグループ権限付きユーザーをシードする。</summary>
    /// <param name="loginIdentifier">ログイン識別子（@ ありならメール、なしなら username のみ）。</param>
    /// <param name="password">平文パスワード。</param>
    /// <param name="permissionKeys">付与する semantic permission key。</param>
    /// <returns>作成した Principal ID。</returns>
    public async Task<Guid> SeedUserWithPermissionsAsync(
        string loginIdentifier,
        string password,
        params string[] permissionKeys)
    {
        await using var scope = Services.CreateAsyncScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
        return await SecurityTestSeed.SeedUserWithGroupPermissionsAsync(
            _database,
            loginIdentifier,
            password,
            permissionKeys);
    }

    /// <summary>統合テスト DB に API キーをシードする。</summary>
    public async Task<string> SeedApiKeyPlainTextAsync()
    {
        if (_cachedApiKeyPlainText is not null)
            return _cachedApiKeyPlainText;

        await using var scope = Services.CreateAsyncScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
        var (_, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(_database);
        _cachedApiKeyPlainText = plainKey;
        return plainKey;
    }

    /// <summary>指定 permission の API キーをシードし平文を返す（キャッシュしない）。</summary>
    /// <param name="permissionKeys">グループ権限かつ allowed_scopes。</param>
    /// <returns>平文 API キー。</returns>
    public async Task<string> SeedApiKeyWithPermissionsAsync(params string[] permissionKeys)
    {
        ArgumentNullException.ThrowIfNull(permissionKeys);
        if (permissionKeys.Length == 0)
            throw new ArgumentException("permissionKeys must contain at least one key.", nameof(permissionKeys));

        await using var scope = Services.CreateAsyncScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDataAccess>();
        await platform.EnsurePermissionCatalogAsync(CancellationToken.None);
        var plainKey = $"statevia-test-key-{Guid.NewGuid():N}";
        var allowedScopesJson = System.Text.Json.JsonSerializer.Serialize(permissionKeys);
        var (_, _, created) = await SecurityTestSeed.SeedApiKeyAsync(
            _database,
            plainKey,
            allowedScopesJson,
            permissionKeys: permissionKeys);
        return created;
    }

    /// <summary>テスト用 JWT を発行する。</summary>
    /// <param name="principalId">Principal ID。</param>
    /// <returns>Bearer トークン文字列。</returns>
    public string IssueBearerToken(Guid principalId)
    {
        using var scope = Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<JwtTokenService>();
        var (token, _) = jwt.IssueAccessToken(TestTenantIds.DefaultTenantId, "default", principalId);
        return token;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _database.Dispose();

        base.Dispose(disposing);
    }
}
