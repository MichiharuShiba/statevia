using Microsoft.AspNetCore.Mvc;
using Statevia.Core.Api.Abstractions.Security;
using Statevia.Core.Api.Contracts.Admin;
using Statevia.Core.Api.Controllers;
using Statevia.Core.Api.Services;
using Statevia.Core.Api.Tests.Infrastructure;

namespace Statevia.Core.Api.Tests.Controllers;

/// <summary><see cref="AdminController"/> の管理者 API。</summary>
public sealed class AdminControllerTests
{
    /// <summary>ユーザー一覧はサービス結果を返す。</summary>
    [Fact]
    public async Task ListUsers_DelegatesToService_ReturnsOk()
    {
        // Arrange
        var expected = new List<AdminUserListItemDto>
        {
            new()
            {
                UserId = Guid.NewGuid(),
                PrincipalId = Guid.NewGuid(),
                Email = "admin@example.com",
                DisplayName = "Admin",
                IsTenantAdmin = true,
                IsActive = true
            }
        };
        var accessor = new SettableTenantContextAccessor();
        accessor.Set(TestTenantIds.DefaultContext with { PrincipalId = expected[0].PrincipalId });
        var controller = new AdminController(new FakeAdminService { Users = expected }, accessor);

        // Act
        var result = await controller.ListUsers(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsAssignableFrom<IReadOnlyList<AdminUserListItemDto>>(ok.Value);
        Assert.Single(payload);
    }

    private sealed class FakeAdminService : ITenantAdministrationService
    {
        public IReadOnlyList<AdminUserListItemDto>? Users { get; init; }

        public Task<IReadOnlyList<PermissionDefinitionDto>> ListPermissionsAsync(
            Guid callerPrincipalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PermissionDefinitionDto>>(Array.Empty<PermissionDefinitionDto>());

        public Task<IReadOnlyList<AdminUserListItemDto>> ListUsersAsync(
            Guid callerPrincipalId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Users ?? Array.Empty<AdminUserListItemDto>());

        public Task<AdminUserListItemDto> CreateUserAsync(
            Guid callerPrincipalId,
            CreateAdminUserRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AdminUserListItemDto> UpdateUserAsync(
            Guid callerPrincipalId,
            Guid userId,
            UpdateAdminUserRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<AdminGroupListItemDto>> ListGroupsAsync(
            Guid callerPrincipalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminGroupListItemDto>>(Array.Empty<AdminGroupListItemDto>());

        public Task<AdminGroupDetailDto> CreateGroupAsync(
            Guid callerPrincipalId,
            CreateAdminGroupRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AdminGroupDetailDto> GetGroupAsync(
            Guid callerPrincipalId,
            Guid groupId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AdminGroupDetailDto> SetGroupMembersAsync(
            Guid callerPrincipalId,
            Guid groupId,
            SetAdminGroupMembersRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<AdminGroupDetailDto> SetGroupPermissionsAsync(
            Guid callerPrincipalId,
            Guid groupId,
            SetAdminGroupPermissionsRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<IReadOnlyList<AdminApiKeyListItemDto>> ListApiKeysAsync(
            Guid callerPrincipalId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AdminApiKeyListItemDto>>(Array.Empty<AdminApiKeyListItemDto>());

        public Task<CreatedAdminApiKeyDto> CreateApiKeyAsync(
            Guid callerPrincipalId,
            CreateAdminApiKeyRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task RevokeApiKeyAsync(
            Guid callerPrincipalId,
            Guid apiKeyId,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }
}
