using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts.Admin;
using Statevia.Service.Api.Infrastructure.Security;

namespace Statevia.Service.Api.Controllers;

/// <summary>Module load 状態の管理者向け API。</summary>
[ApiController]
[Route("v1/admin/modules")]
public sealed class AdminModulesController : ControllerBase
{
    private readonly IModuleManagementService _moduleManagement;
    private readonly ITenantAdminAuthorization _tenantAdminAuthorization;
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public AdminModulesController(
        IModuleManagementService moduleManagement,
        ITenantAdminAuthorization tenantAdminAuthorization,
        ITenantContextAccessor tenantContext)
    {
        _moduleManagement = moduleManagement;
        _tenantAdminAuthorization = tenantAdminAuthorization;
        _tenantContext = tenantContext;
    }

    /// <summary>GET /v1/admin/modules — Module load catalog 一覧。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminModuleListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminModuleListItemDto>>> ListModules(CancellationToken ct)
    {
        await TenantAdminAuthorizationGate.EnsureTenantAdminAsync(_tenantContext, _tenantAdminAuthorization, ct)
            .ConfigureAwait(false);
        return Ok(_moduleManagement.ListModules());
    }
}
