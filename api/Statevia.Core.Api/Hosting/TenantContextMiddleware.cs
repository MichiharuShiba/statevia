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

        var headerTenantKey = context.Request.Headers[TenantHeader.HeaderName].FirstOrDefault()
            ?? TenantHeader.DefaultTenantId;

        var jwtPrincipal = TryValidateBearer(context);
        string resolvedTenantKey;
        Guid? jwtTenantInternalId = null;
        Guid? jwtPrincipalId = null;

        if (jwtPrincipal is not null)
        {
            jwtTenantInternalId = ParseGuidClaim(jwtPrincipal, JwtTokenService.TenantIdClaim);
            var jwtTenantKey = jwtPrincipal.FindFirstValue(JwtTokenService.TenantKeyClaim);
            jwtPrincipalId = ParseGuidClaim(jwtPrincipal, JwtTokenService.PrincipalIdClaim);

            if (jwtTenantInternalId is null || string.IsNullOrWhiteSpace(jwtTenantKey) || jwtPrincipalId is null)
                throw new UnauthorizedException("Invalid token claims.", "UNAUTHORIZED");

            if (context.Request.Headers.ContainsKey(TenantHeader.HeaderName) &&
                !string.Equals(headerTenantKey, jwtTenantKey, StringComparison.Ordinal))
                throw new ForbiddenException("X-Tenant-Id does not match JWT tenant.", "TENANT_HEADER_MISMATCH");

            resolvedTenantKey = jwtTenantKey;
        }
        else
        {
            resolvedTenantKey = headerTenantKey;
        }

        var tenant = await platformDataAccess
            .FindTenantByKeyAsync(resolvedTenantKey, context.RequestAborted)
            .ConfigureAwait(false);

        if (tenant is null)
            throw new UnauthorizedException("Tenant not found.", "TENANT_UNRESOLVED");

        EnsureTenantActive(tenant.Lifecycle);

        if (jwtTenantInternalId is not null && tenant.TenantId != jwtTenantInternalId)
            throw new ForbiddenException("JWT tenant does not match resolved tenant.", "TENANT_HEADER_MISMATCH");

        var state = new TenantContextState(tenant.TenantId, tenant.TenantKey, jwtPrincipalId, tenant.Lifecycle);
        using (tenantContextAccessor.SetContext(state))
        {
            context.Items["Statevia.TenantKey"] = tenant.TenantKey;
            context.Items["Statevia.TenantInternalId"] = tenant.TenantId;
            await _next(context).ConfigureAwait(false);
        }
    }

    private static bool ShouldSkipTenantResolution(PathString path) =>
        path.StartsWithSegments("/v1/auth/login", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/v1/health", StringComparison.OrdinalIgnoreCase);

    private ClaimsPrincipal? TryValidateBearer(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = authorization["Bearer ".Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : _jwtTokenService.ValidateToken(token);
    }

    private static Guid? ParseGuidClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = principal.FindFirstValue(claimType);
        return Guid.TryParse(value, out var id) ? id : null;
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
