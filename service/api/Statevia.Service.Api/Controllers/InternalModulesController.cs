using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Abstractions.Services;

namespace Statevia.Service.Api.Controllers;

/// <summary>Module reload 等の内部向け API。</summary>
[ApiController]
[Route("internal/modules")]
public sealed class InternalModulesController : ControllerBase
{
    private readonly IModuleManagementService _moduleManagement;
    private readonly IModuleManagementAuthorization _moduleManagementAuthorization;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="moduleManagement">Module の discover / load 操作。</param>
    /// <param name="moduleManagementAuthorization">reload 認可。</param>
    public InternalModulesController(
        IModuleManagementService moduleManagement,
        IModuleManagementAuthorization moduleManagementAuthorization)
    {
        _moduleManagement = moduleManagement;
        _moduleManagementAuthorization = moduleManagementAuthorization;
    }

    /// <summary>POST /internal/modules/reload — discover / load を明示実行する。</summary>
    [HttpPost("reload")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReloadModules(CancellationToken ct)
    {
        await _moduleManagementAuthorization.EnsureReloadAllowedAsync(ct).ConfigureAwait(false);
        await _moduleManagement.ReloadAsync(ct).ConfigureAwait(false);
        return NoContent();
    }
}
