
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Contracts.Auth;
using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Services;

/// <summary>認証ユースケース。</summary>
public interface IAuthService
{
    /// <summary>パスワードログインし JWT を発行する。</summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>認証済み Principal の情報を返す。</summary>
    Task<AuthMeResponse> GetMeAsync(Guid tenantId, Guid principalId, CancellationToken cancellationToken);

    /// <summary>本人が現行パスワード確認付きで自分のハッシュを更新する。</summary>
    Task ChangeOwnPasswordAsync(
        Guid tenantId,
        Guid principalId,
        ChangeOwnPasswordRequest request,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
internal sealed class AuthService : IAuthService
{
    private const string UnauthorizedCode = "UNAUTHORIZED";
    private const string InvalidCredentialsMessage = "Invalid credentials.";

    private readonly IPlatformDataAccess _platformDataAccess;
    private readonly JwtTokenService _jwtTokenService;
    private readonly PasswordCredentialService _passwordCredentialService;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public AuthService(
        IPlatformDataAccess platformDataAccess,
        JwtTokenService jwtTokenService,
        PasswordCredentialService passwordCredentialService)
    {
        _platformDataAccess = platformDataAccess;
        _jwtTokenService = jwtTokenService;
        _passwordCredentialService = passwordCredentialService;
    }

    /// <inheritdoc />
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await _platformDataAccess
            .FindLoginCredentialAsync(request.TenantKey, request.Username, cancellationToken)
            .ConfigureAwait(false);

        if (lookup is null)
            throw new UnauthorizedException(InvalidCredentialsMessage, UnauthorizedCode);

        EnsureTenantActive(lookup.Tenant.Lifecycle);

        if (!_passwordCredentialService.VerifyPassword(request.Password, lookup.User.PasswordHash))
            throw new UnauthorizedException(InvalidCredentialsMessage, UnauthorizedCode);

        var (token, expiresAt) = _jwtTokenService.IssueAccessToken(
            lookup.Tenant.TenantId,
            lookup.Tenant.TenantKey,
            lookup.Principal.PrincipalId);

        return new LoginResponse
        {
            AccessToken = token,
            ExpiresAt = expiresAt,
            TenantId = lookup.Tenant.TenantId,
            TenantKey = lookup.Tenant.TenantKey,
            PrincipalId = lookup.Principal.PrincipalId
        };
    }

    /// <inheritdoc />
    public async Task<AuthMeResponse> GetMeAsync(Guid tenantId, Guid principalId, CancellationToken cancellationToken)
    {
        var lookup = await _platformDataAccess
            .FindUserPrincipalAsync(tenantId, principalId, cancellationToken)
            .ConfigureAwait(false);

        if (lookup is null)
            throw new UnauthorizedException("Principal not found.", UnauthorizedCode);

        EnsureTenantActive(lookup.Tenant.Lifecycle);

        return new AuthMeResponse
        {
            TenantId = lookup.Tenant.TenantId,
            TenantKey = lookup.Tenant.TenantKey,
            PrincipalId = principalId,
            Username = lookup.User.Username,
            Email = lookup.User.Email,
            IsTenantAdmin = lookup.User.IsTenantAdmin
        };
    }

    /// <inheritdoc />
    public async Task ChangeOwnPasswordAsync(
        Guid tenantId,
        Guid principalId,
        ChangeOwnPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var lookup = await _platformDataAccess
            .FindUserPrincipalAsync(tenantId, principalId, cancellationToken)
            .ConfigureAwait(false);

        if (lookup is null)
            throw new UnauthorizedException(InvalidCredentialsMessage, UnauthorizedCode);

        EnsureTenantActive(lookup.Tenant.Lifecycle);

        if (!_passwordCredentialService.VerifyPassword(request.CurrentPassword, lookup.User.PasswordHash))
            throw new UnauthorizedException(InvalidCredentialsMessage, UnauthorizedCode);

        var passwordHash = _passwordCredentialService.HashPassword(request.NewPassword);
        var updated = await _platformDataAccess
            .TryUpdateUserPasswordHashAsync(tenantId, lookup.User.UserId, passwordHash, cancellationToken)
            .ConfigureAwait(false);
        if (!updated)
            throw new UnauthorizedException(InvalidCredentialsMessage, UnauthorizedCode);
    }

    private static void EnsureTenantActive(TenantLifecycle lifecycle)
    {
        switch (lifecycle)
        {
            case TenantLifecycle.Suspended:
                throw new ForbiddenException("Tenant is suspended.", "TENANT_SUSPENDED");
            case TenantLifecycle.Archived:
                throw new ForbiddenException("Tenant is archived.", "TENANT_ARCHIVED");
            case TenantLifecycle.Active:
                return;
            default:
                throw new ForbiddenException("Tenant is not active.", "FORBIDDEN");
        }
    }
}
