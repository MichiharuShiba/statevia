using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Configuration;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;
using Statevia.Core.Api.Infrastructure.Security;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Hosting;

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
        var (token, _) = jwt.IssueAccessToken(TestTenantIds.DefaultInternalId, "default", Guid.NewGuid());

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
        var (token, _) = jwt.IssueAccessToken(TestTenantIds.DefaultInternalId, "default", principalId);
        var platform = new PlatformDataAccess(database.Factory);
        var accessor = new SettableTenantContextAccessor();
        var nextInvoked = false;

        var middleware = new TenantContextMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, jwt);

        var context = new DefaultHttpContext();
        context.Request.Path = "/v1/workflows";
        context.Request.Headers.Authorization = $"Bearer {token}";

        // Act
        await middleware.InvokeAsync(context, accessor, platform);

        // Assert
        Assert.True(nextInvoked);
        Assert.Equal("default", context.Items["Statevia.TenantKey"]);
        Assert.Equal(TestTenantIds.DefaultInternalId, context.Items["Statevia.TenantInternalId"]);
    }

    /// <summary>JWT なし X-Tenant-Id のみでも既定テナントを解決する。</summary>
    [Fact]
    public async Task InvokeAsync_HeaderOnly_ResolvesDefaultTenant()
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
        context.Request.Path = "/v1/workflows";
        context.Request.Headers[TenantHeader.HeaderName] = "default";

        // Act
        await middleware.InvokeAsync(context, accessor, platform);

        // Assert
        Assert.True(nextInvoked);
        Assert.Equal("default", context.Items["Statevia.TenantKey"]);
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
        context.Request.Path = "/v1/workflows";
        context.Request.Headers[TenantHeader.HeaderName] = "unknown-tenant";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            middleware.InvokeAsync(context, accessor, platform));

        Assert.Equal("TENANT_UNRESOLVED", ex.Code);
    }

    /// <summary>JWT の tenant_id が DB と不一致の場合 403。</summary>
    [Fact]
    public async Task InvokeAsync_JwtTenantInternalIdMismatch_ThrowsForbidden()
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
        context.Request.Path = "/v1/workflows";
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
        context.Request.Path = "/v1/workflows";
        context.Request.Headers.Authorization = $"Bearer {tokenString}";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            middleware.InvokeAsync(context, accessor, platform));

        Assert.Equal("UNAUTHORIZED", ex.Code);
    }
}
