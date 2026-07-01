using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Abstractions.Security;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Contracts.Auth;
using Statevia.Service.Api.Controllers;
using Statevia.Service.Api.Services;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Controllers;

/// <summary><see cref="AuthController"/> の認証 API。</summary>
public sealed class AuthControllerTests
{
    /// <summary>Login はサービス結果を 200 で返す。</summary>
    [Fact]
    public async Task Login_DelegatesToAuthService_ReturnsOk()
    {
        // Arrange
        var expected = new LoginResponse
        {
            AccessToken = "token",
            TenantKey = "default",
            TenantId = TestTenantIds.DefaultTenantId,
            PrincipalId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var controller = new AuthController(new FakeAuthService { LoginResult = expected }, new SettableTenantContextAccessor());

        // Act
        var result = await controller.Login(new LoginRequest
        {
            TenantKey = "default",
            Email = "user@example.com",
            Password = "secret"
        }, CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal(expected.AccessToken, payload.AccessToken);
    }

    /// <summary>Me は未認証文脈で UnauthorizedException を送出する。</summary>
    [Fact]
    public async Task Me_UnresolvedTenantContext_ThrowsUnauthorized()
    {
        // Arrange
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(null);
        var controller = new AuthController(new FakeAuthService(), accessor);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => controller.Me(CancellationToken.None));
    }

    /// <summary>Me は認証済み文脈で Principal 情報を返す。</summary>
    [Fact]
    public async Task Me_ResolvedContext_ReturnsAuthMeResponse()
    {
        // Arrange
        var principalId = Guid.NewGuid();
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.DefaultContext with { PrincipalId = principalId });
        var expected = new AuthMeResponse
        {
            TenantId = TestTenantIds.DefaultTenantId,
            TenantKey = "default",
            PrincipalId = principalId,
            Email = "user@example.com",
            IsTenantAdmin = true
        };
        var controller = new AuthController(new FakeAuthService { MeResult = expected }, accessor);

        // Act
        var result = await controller.Me(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AuthMeResponse>(ok.Value);
        Assert.Equal("user@example.com", payload.Email);
        Assert.True(payload.IsTenantAdmin);
    }

    private sealed class FakeAuthService : IAuthService
    {
        public LoginResponse? LoginResult { get; init; }

        public AuthMeResponse? MeResult { get; init; }

        public Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(LoginResult ?? throw new InvalidOperationException("LoginResult not set"));

        public Task<AuthMeResponse> GetMeAsync(Guid tenantId, Guid principalId, CancellationToken cancellationToken) =>
            Task.FromResult(MeResult ?? throw new InvalidOperationException("MeResult not set"));
    }
}
