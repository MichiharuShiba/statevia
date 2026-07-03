using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Infrastructure.Security;

namespace Statevia.Service.Api.Controllers;

/// <summary>Module reload 等の内部向け API。</summary>
[ApiController]
[Route("internal/modules")]
public sealed class InternalModulesController : ControllerBase
{
    private readonly IModuleManagementService _moduleManagement;
    private readonly ITenantAdminAuthorization _tenantAdminAuthorization;
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public InternalModulesController(
        IModuleManagementService moduleManagement,
        ITenantAdminAuthorization tenantAdminAuthorization,
        ITenantContextAccessor tenantContext)
    {
        _moduleManagement = moduleManagement;
        _tenantAdminAuthorization = tenantAdminAuthorization;
        _tenantContext = tenantContext;
    }

    /// <summary>POST /internal/modules/reload — discover / load を明示実行する。</summary>
    [HttpPost("reload")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReloadModules(CancellationToken ct)
    {
        await TenantAdminAuthorizationGate.EnsureTenantAdminAsync(_tenantContext, _tenantAdminAuthorization, ct)
            .ConfigureAwait(false);
        await _moduleManagement.ReloadAsync(ct).ConfigureAwait(false);
        return NoContent();
    }
}
