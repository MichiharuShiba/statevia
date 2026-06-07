using System.Security.Claims;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Infrastructure.Security;

namespace Statevia.Core.Api.Hosting;

/// <summary>
/// JWT / X-Tenant-Id から <see cref="ITenantContextAccessor"/> を解決する。停止テナントは fail-closed。
/// </summary>
internal sealed class TenantContextMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly JwtTokenService _jwtTokenService;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public TenantContextMiddleware(RequestDelegate next, JwtTokenService jwtTokenService)
    {
        _next = next;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>リクエストパイプラインを実行する。</summary>
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContextAccessor tenantContextAccessor,
        IPlatformDataAccess platformDataAccess)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContextAccessor);
        ArgumentNullException.ThrowIfNull(platformDataAccess);

        if (ShouldSkipTenantResolution(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var headerTenantKey = context.Request.Headers[TenantHeader.HeaderName].FirstOrDefault() ?? TenantHeader.DefaultTenantId;
        var resolvedIdentity = await ResolveIdentityAsync(context, headerTenantKey).ConfigureAwait(false);

        var tenant = await platformDataAccess
            .FindTenantByKeyAsync(resolvedIdentity.TenantKey, context.RequestAborted)
            .ConfigureAwait(false);

        if (tenant is null)
            throw new UnauthorizedException("Tenant not found.", "TENANT_UNRESOLVED");

        EnsureTenantActive(tenant.Lifecycle);

        if (resolvedIdentity.TenantInternalId is not null && tenant.TenantId != resolvedIdentity.TenantInternalId)
            throw new ForbiddenException("JWT tenant does not match resolved tenant.", "TENANT_HEADER_MISMATCH");

        if (RequiresPrincipal(context.Request.Path) && resolvedIdentity.PrincipalId is null)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");

        var state = new TenantContextState(
            tenant.TenantId,
            tenant.TenantKey,
            resolvedIdentity.PrincipalId,
            tenant.Lifecycle,
            resolvedIdentity.EffectivePermissionKeys);
        using (tenantContextAccessor.SetContext(state))
        {
            context.Items["Statevia.TenantKey"] = tenant.TenantKey;
            context.Items["Statevia.TenantInternalId"] = tenant.TenantId;
            await _next(context).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// JWT または API キーを検証して解決済み ID を取得する。
    /// </summary>
    /// <param name="context">HTTP コンテキスト。</param>
    /// <param name="headerTenantKey">ヘッダーのテナントキー。</param>
    /// <returns>解決済み ID。</returns>
    private async Task<ResolvedIdentity> ResolveIdentityAsync(HttpContext context, string headerTenantKey)
    {
        var jwtPrincipal = TryValidateBearer(context);
        if (jwtPrincipal is not null)
            return ResolveJwtIdentity(context, headerTenantKey, jwtPrincipal);

        var plainApiKey = TryGetApiKey(context);
        if (plainApiKey is null)
            return new ResolvedIdentity(headerTenantKey, null, null);

        var apiKeyAuthenticationService = context.RequestServices.GetRequiredService<IApiKeyAuthenticationService>();
        var apiKeyResult = await apiKeyAuthenticationService
            .ValidateAsync(plainApiKey, context.RequestAborted)
            .ConfigureAwait(false);
        if (apiKeyResult is null)
            throw new UnauthorizedException("Invalid API key.", "UNAUTHORIZED");
        return new ResolvedIdentity(
            apiKeyResult.Tenant.TenantKey,
            apiKeyResult.Tenant.TenantId,
            apiKeyResult.Principal.PrincipalId,
            apiKeyResult.EffectiveScopes);
    }

    /// <summary>
    /// JWT クレームを検証して解決済み ID を取得する。
    /// </summary>
    /// <param name="context">HTTP コンテキスト。</param>
    /// <param name="headerTenantKey">ヘッダーのテナントキー。</param>
    /// <param name="jwtPrincipal">JWT クレーム。</param>
    /// <returns>解決済み ID。</returns>
    private static ResolvedIdentity ResolveJwtIdentity(
        HttpContext context,
        string headerTenantKey,
        ClaimsPrincipal jwtPrincipal)
    {
        var tenantInternalId = ParseGuidClaim(jwtPrincipal, JwtTokenService.TenantIdClaim);
        var jwtTenantKey = jwtPrincipal.FindFirstValue(JwtTokenService.TenantKeyClaim);
        var principalId = ParseGuidClaim(jwtPrincipal, JwtTokenService.PrincipalIdClaim);
        if (tenantInternalId is null || string.IsNullOrWhiteSpace(jwtTenantKey) || principalId is null)
            throw new UnauthorizedException("Invalid token claims.", "UNAUTHORIZED");

        if (context.Request.Headers.ContainsKey(TenantHeader.HeaderName) &&
            !string.Equals(headerTenantKey, jwtTenantKey, StringComparison.Ordinal))
            throw new ForbiddenException("X-Tenant-Id does not match JWT tenant.", "TENANT_HEADER_MISMATCH");

        return new ResolvedIdentity(jwtTenantKey, tenantInternalId, principalId);
    }

    /// <summary>
    /// テナント解決をスキップするかどうかを判定する。
    /// </summary>
    /// <param name="path">リクエストパス。</param>
    /// <returns>テナント解決をスキップする場合は true。</returns>
    private static bool ShouldSkipTenantResolution(PathString path) =>
        path.StartsWithSegments("/v1/auth/login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/v1/health", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Principal が必要かどうかを判定する。
    /// </summary>
    /// <param name="path">リクエストパス。</param>
    /// <returns>Principal が必要な場合は true。</returns>
    private static bool RequiresPrincipal(PathString path) =>
        path.StartsWithSegments("/v1/definitions", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/v1/executions", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/v1/graphs", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/v1/admin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/v1/auth/me", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// JWT を検証する。
    /// </summary>
    /// <param name="context">HTTP コンテキスト。</param>
    /// <returns>JWT クレーム。</returns>
    private ClaimsPrincipal? TryValidateBearer(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authorization["Bearer ".Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : _jwtTokenService.ValidateToken(token);
    }

    /// <summary>
    /// API キーを取得する。
    /// </summary>
    /// <param name="context">HTTP コンテキスト。</param>
    /// <returns>API キー。</returns>
    private static string? TryGetApiKey(HttpContext context)
    {
        var header = context.Request.Headers[ApiKeyHeaderName].FirstOrDefault();
        return string.IsNullOrWhiteSpace(header) ? null : header.Trim();
    }

    /// <summary>
    /// 解決済み ID。
    /// </summary>
    /// <param name="TenantKey">テナントキー。</param>
    /// <param name="TenantInternalId">テナント内部 ID。</param>
    /// <param name="PrincipalId">Principal ID。</param>
    /// <param name="EffectivePermissionKeys">API キー認証時の交差済み permission。</param>
    private sealed record ResolvedIdentity(
        string TenantKey,
        Guid? TenantInternalId,
        Guid? PrincipalId,
        IReadOnlySet<string>? EffectivePermissionKeys = null);

    /// <summary>
    /// GUID クレームを解析する。
    /// </summary>
    /// <param name="principal">クレーム。</param>
    /// <param name="claimType">クレームタイプ。</param>
    /// <returns>GUID。</returns>
    private static Guid? ParseGuidClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>
    /// テナントがアクティブかどうかを確認する。
    /// </summary>
    /// <param name="lifecycle">テナントライフサイクル。</param>
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
