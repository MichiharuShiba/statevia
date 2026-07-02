using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Statevia.Service.Api.Configuration;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Hosting;
using Statevia.Service.Api.Infrastructure.Security;
using Statevia.Service.Api.Tests.Infrastructure;
using Statevia.Service.Api.Tests.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary><see cref="TenantContextMiddleware"/> のテナント解決。</summary>
public sealed class TenantContextMiddlewareTests
{
    /// <summary>JWT tenant_key と X-Tenant-Id 不一致は 403。</summary>
    [Fact]
    public async Task InvokeAsync_JwtTenantKeyMismatchHeader_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var (token, _) = jwt.IssueAccessToken(TestTenantIds.DefaultTenantId, "default", Guid.NewGuid());

        var platform = new PlatformDataAccess(database.Factory);
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask, jwt);

        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = $"Bearer {token}";
        context.Request.Headers[TenantHeader.HeaderName] = "other-tenant";
        context.Response.Body = new MemoryStream();

        var accessor = new SettableTenantContextAccessor();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            middleware.InvokeAsync(context, accessor, platform));

        Assert.Equal("TENANT_HEADER_MISMATCH", ex.Code);
    }

    /// <summary>有効 JWT でテナント文脈が解決され downstream が呼ばれる。</summary>
    [Fact]
    public async Task InvokeAsync_ValidJwt_SetsTenantContextAndInvokesNext()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var principalId = Guid.NewGuid();
        var (token, _) = jwt.IssueAccessToken(TestTenantIds.DefaultTenantId, "default", principalId);
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var nextInvoked = false;

        var middleware = new TenantContextMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, jwt);

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/executions";
        context.Request.Headers.Authorization = $"Bearer {token}";

        // Act
        await middleware.InvokeAsync(context, accessor, platform);

        // Assert
        Assert.True(nextInvoked);
        Assert.Equal("default", context.Items["Statevia.TenantKey"]);
        Assert.Equal(TestTenantIds.DefaultTenantId, context.Items["Statevia.TenantId"]);
    }

    /// <summary>Runtime API で JWT なし X-Tenant-Id のみは 401。</summary>
    [Fact]
    public async Task InvokeAsync_HeaderOnlyOnRuntimePath_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var nextInvoked = false;

        var middleware = new TenantContextMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, jwt);

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/executions";
        context.Request.Headers[TenantHeader.HeaderName] = "default";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            middleware.InvokeAsync(context, accessor, platform));
        Assert.False(nextInvoked);
        Assert.Equal("UNAUTHORIZED", ex.Code);
    }

    /// <summary>ログインエンドポイントはテナント解決をスキップする。</summary>
    [Fact]
    public async Task InvokeAsync_LoginPath_SkipsTenantResolution()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var nextInvoked = false;

        var middleware = new TenantContextMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, jwt);

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/auth/login";

        // Act
        await middleware.InvokeAsync(context, accessor, platform);

        // Assert
        Assert.True(nextInvoked);
        Assert.False(accessor.IsResolved);
    }

    /// <summary>存在しない tenant_key は 401。</summary>
    [Fact]
    public async Task InvokeAsync_UnknownTenant_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask, jwt);

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/executions";
        context.Request.Headers[TenantHeader.HeaderName] = "unknown-tenant";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            middleware.InvokeAsync(context, accessor, platform));

        Assert.Equal("TENANT_UNRESOLVED", ex.Code);
    }

    /// <summary>JWT の tenant_id が DB と不一致の場合 403。</summary>
    [Fact]
    public async Task InvokeAsync_JwtTenantIdMismatch_ThrowsForbidden()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var wrongTenantId = Guid.NewGuid();
        var (token, _) = jwt.IssueAccessToken(wrongTenantId, "default", Guid.NewGuid());
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask, jwt);

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/executions";
        context.Request.Headers.Authorization = $"Bearer {token}";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            middleware.InvokeAsync(context, accessor, platform));

        Assert.Equal("TENANT_HEADER_MISMATCH", ex.Code);
    }

    /// <summary>必須クレーム欠落 JWT は 401。</summary>
    [Fact]
    public async Task InvokeAsync_JwtMissingClaims_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var options = new JwtAuthOptions();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var jwt = new JwtTokenService(Options.Create(options));
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask, jwt);

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/executions";
        context.Request.Headers.Authorization = $"Bearer {tokenString}";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            middleware.InvokeAsync(context, accessor, platform));

        Assert.Equal("UNAUTHORIZED", ex.Code);
    }

    /// <summary>/v1/graphs も Principal 必須。</summary>
    [Fact]
    public async Task InvokeAsync_HeaderOnlyOnGraphsPath_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var nextInvoked = false;

        var middleware = new TenantContextMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, jwt);

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/graphs/graph-1";
        context.Request.Headers[TenantHeader.HeaderName] = "default";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            middleware.InvokeAsync(context, accessor, platform));
        Assert.False(nextInvoked);
        Assert.Equal("UNAUTHORIZED", ex.Code);
    }

    /// <summary>API キーで Principal を解決し Runtime API を通過する。</summary>
    [Fact]
    public async Task InvokeAsync_ValidApiKey_SetsTenantContextAndInvokesNext()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var (principalId, _, plainKey) = await SecurityTestSeed.SeedApiKeyAsync(database);
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var nextInvoked = false;
        Guid? resolvedPrincipalId = null;
        IReadOnlySet<string>? resolvedEffectiveKeys = null;
        var middleware = new TenantContextMiddleware(_ =>
        {
            nextInvoked = true;
            resolvedPrincipalId = accessor.PrincipalId;
            resolvedEffectiveKeys = accessor.EffectivePermissionKeys;
            return Task.CompletedTask;
        }, jwt);
        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/executions";
        context.Request.Headers["X-Api-Key"] = plainKey;
        AttachApiKeyAuthenticationService(context, new ApiKeyAuthenticationService(platform));

        // Act
        await middleware.InvokeAsync(context, accessor, platform);

        // Assert
        Assert.True(nextInvoked);
        Assert.Equal(TestTenantIds.DefaultTenantId, context.Items["Statevia.TenantId"]);
        Assert.Equal("default", context.Items["Statevia.TenantKey"]);
        Assert.Equal(principalId, resolvedPrincipalId);
        Assert.NotNull(resolvedEffectiveKeys);
        Assert.Contains(WellKnownPermissionKeys.ExecutionsRead, resolvedEffectiveKeys);
    }

    /// <summary>無効 API キーは 401。</summary>
    [Fact]
    public async Task InvokeAsync_InvalidApiKey_ThrowsUnauthorized()
    {
        // Arrange
        using var database = new SqliteTestDatabase();
        var jwt = new JwtTokenService(Options.Create(new JwtAuthOptions()));
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var middleware = new TenantContextMiddleware(_ => Task.CompletedTask, jwt);
        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/executions";
        context.Request.Headers["X-Api-Key"] = "invalid-key";
        AttachApiKeyAuthenticationService(context, new ApiKeyAuthenticationService(platform));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            middleware.InvokeAsync(context, accessor, platform));

        Assert.Equal("UNAUTHORIZED", ex.Code);
    }

    private static void AttachApiKeyAuthenticationService(
        HttpContext context,
        IApiKeyAuthenticationService apiKeyAuthenticationService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(apiKeyAuthenticationService);
        context.RequestServices = services.BuildServiceProvider();
    }
}
