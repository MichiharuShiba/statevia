using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Abstractions.Services;
using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Controllers;

/// <summary>Module load 状態の管理者向け API。</summary>
[ApiController]
[Route("v1/admin/modules")]
public sealed class AdminModulesController : ControllerBase
{
    private readonly IModuleManagementService _moduleManagement;
    private readonly IModuleManagementAuthorization _moduleManagementAuthorization;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="moduleManagement">Module の discover / load 操作。</param>
    /// <param name="moduleManagementAuthorization">一覧認可。</param>
    public AdminModulesController(
        IModuleManagementService moduleManagement,
        IModuleManagementAuthorization moduleManagementAuthorization)
    {
        _moduleManagement = moduleManagement;
        _moduleManagementAuthorization = moduleManagementAuthorization;
    }

    /// <summary>GET /v1/admin/modules — Module load catalog 一覧。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminModuleListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminModuleListItemDto>>> ListModules(CancellationToken ct)
    {
        await _moduleManagementAuthorization.EnsureReadAllowedAsync(ct).ConfigureAwait(false);
        return Ok(_moduleManagement.ListModules());
    }
}
