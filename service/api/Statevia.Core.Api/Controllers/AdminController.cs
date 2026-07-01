using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Contracts.Admin;
using Statevia.Core.Api.Services;

namespace Statevia.Core.Api.Controllers;

/// <summary>テナント管理者向け users / groups API。</summary>
[ApiController]
[Route("v1/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly ITenantAdministrationService _administration;
    private readonly ITenantContextAccessor _tenantContext;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public AdminController(ITenantAdministrationService administration, ITenantContextAccessor tenantContext)
    {
        _administration = administration;
        _tenantContext = tenantContext;
    }

    /// <summary>GET /v1/admin/permissions — 権限カタログ。</summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionDefinitionDto>>> ListPermissions(CancellationToken ct) =>
        Ok(await _administration.ListPermissionsAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>GET /v1/admin/users — ユーザー一覧。</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminUserListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminUserListItemDto>>> ListUsers(CancellationToken ct) =>
        Ok(await _administration.ListUsersAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>POST /v1/admin/users — ユーザー作成。</summary>
    [HttpPost("users")]
    [ProducesResponseType(typeof(AdminUserListItemDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminUserListItemDto>> CreateUser(
        [FromBody] CreateAdminUserRequest request,
        CancellationToken ct)
    {
        var created = await _administration.CreateUserAsync(RequirePrincipalId(), request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(ListUsers), created);
    }

    /// <summary>PATCH /v1/admin/users/{userId} — ユーザー更新。</summary>
    [HttpPatch("users/{userId:guid}")]
    [ProducesResponseType(typeof(AdminUserListItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminUserListItemDto>> UpdateUser(
        Guid userId,
        [FromBody] UpdateAdminUserRequest request,
        CancellationToken ct) =>
        Ok(await _administration.UpdateUserAsync(RequirePrincipalId(), userId, request, ct).ConfigureAwait(false));

    /// <summary>GET /v1/admin/groups — グループ一覧。</summary>
    [HttpGet("groups")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminGroupListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminGroupListItemDto>>> ListGroups(CancellationToken ct) =>
        Ok(await _administration.ListGroupsAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>POST /v1/admin/groups — グループ作成。</summary>
    [HttpPost("groups")]
    [ProducesResponseType(typeof(AdminGroupDetailDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<AdminGroupDetailDto>> CreateGroup(
        [FromBody] CreateAdminGroupRequest request,
        CancellationToken ct)
    {
        var created = await _administration.CreateGroupAsync(RequirePrincipalId(), request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetGroup), new { groupId = created.GroupId }, created);
    }

    /// <summary>GET /v1/admin/groups/{groupId} — グループ詳細。</summary>
    [HttpGet("groups/{groupId:guid}")]
    [ProducesResponseType(typeof(AdminGroupDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminGroupDetailDto>> GetGroup(Guid groupId, CancellationToken ct) =>
        Ok(await _administration.GetGroupAsync(RequirePrincipalId(), groupId, ct).ConfigureAwait(false));

    /// <summary>PUT /v1/admin/groups/{groupId}/members — メンバー置換。</summary>
    [HttpPut("groups/{groupId:guid}/members")]
    [ProducesResponseType(typeof(AdminGroupDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminGroupDetailDto>> SetGroupMembers(
        Guid groupId,
        [FromBody] SetAdminGroupMembersRequest request,
        CancellationToken ct) =>
        Ok(await _administration.SetGroupMembersAsync(RequirePrincipalId(), groupId, request, ct).ConfigureAwait(false));

    /// <summary>PUT /v1/admin/groups/{groupId}/permissions — 権限置換。</summary>
    [HttpPut("groups/{groupId:guid}/permissions")]
    [ProducesResponseType(typeof(AdminGroupDetailDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminGroupDetailDto>> SetGroupPermissions(
        Guid groupId,
        [FromBody] SetAdminGroupPermissionsRequest request,
        CancellationToken ct) =>
        Ok(await _administration.SetGroupPermissionsAsync(RequirePrincipalId(), groupId, request, ct).ConfigureAwait(false));

    /// <summary>GET /v1/admin/api-keys — API キー一覧（平文なし）。</summary>
    [HttpGet("api-keys")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminApiKeyListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminApiKeyListItemDto>>> ListApiKeys(CancellationToken ct) =>
        Ok(await _administration.ListApiKeysAsync(RequirePrincipalId(), ct).ConfigureAwait(false));

    /// <summary>POST /v1/admin/api-keys — API キー発行（平文は応答のみ）。</summary>
    [HttpPost("api-keys")]
    [ProducesResponseType(typeof(CreatedAdminApiKeyDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatedAdminApiKeyDto>> CreateApiKey(
        [FromBody] CreateAdminApiKeyRequest request,
        CancellationToken ct)
    {
        var created = await _administration.CreateApiKeyAsync(RequirePrincipalId(), request, ct).ConfigureAwait(false);
        return CreatedAtAction(nameof(ListApiKeys), created);
    }

    /// <summary>DELETE /v1/admin/api-keys/{apiKeyId} — API キー失効。</summary>
    [HttpDelete("api-keys/{apiKeyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeApiKey(Guid apiKeyId, CancellationToken ct)
    {
        await _administration.RevokeApiKeyAsync(RequirePrincipalId(), apiKeyId, ct).ConfigureAwait(false);
        return NoContent();
    }

    private Guid RequirePrincipalId()
    {
        if (!_tenantContext.IsResolved || _tenantContext.PrincipalId is not { } principalId)
            throw new UnauthorizedException("Authentication required.", "UNAUTHORIZED");
        return principalId;
    }
}
