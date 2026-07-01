using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Statevia.Service.Api.Contracts.Admin;

namespace Statevia.Service.Api.Tests.Hosting;

/// <summary><c>/v1/admin</c> の認可・CRUD 統合テスト。</summary>
public sealed class AdminApiIntegrationTests : IClassFixture<SecurityIntegrationWebApplicationFactory>
{
    private readonly SecurityIntegrationWebApplicationFactory _factory;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public AdminApiIntegrationTests(SecurityIntegrationWebApplicationFactory factory) =>
        _factory = factory;

    /// <summary>非管理者は users 一覧で 403。</summary>
    [Fact]
    public async Task ListUsers_NonAdmin_ReturnsForbidden()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("member@example.com", "password", isTenantAdmin: false);
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.GetAsync(new Uri("/v1/admin/users", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>管理者は users 一覧を取得できる。</summary>
    [Fact]
    public async Task ListUsers_TenantAdmin_ReturnsOk()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("admin-list@example.com", "password", isTenantAdmin: true);
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.GetAsync(new Uri("/v1/admin/users", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var users = await response.Content.ReadFromJsonAsync<List<AdminUserListItemDto>>();
        Assert.NotNull(users);
        Assert.NotEmpty(users);
    }

    /// <summary>ユーザー作成で email 未指定は 422。</summary>
    [Fact]
    public async Task CreateUser_MissingEmail_ReturnsUnprocessableEntity()
    {
        // Arrange
        var principalId = await _factory.SeedUserPrincipalAsync("admin-create@example.com", "password", isTenantAdmin: true);
        using var client = CreateAuthenticatedClient(principalId);

        // Act
        var response = await client.PostAsJsonAsync(
            new Uri("/v1/admin/users", UriKind.Relative),
            new { password = "password" });

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>管理者はグループを作成しメンバー・権限を設定できる。</summary>
    [Fact]
    public async Task CreateGroup_SetMembersAndPermissions_ReturnsOk()
    {
        // Arrange
        var adminPrincipalId = await _factory.SeedUserPrincipalAsync("admin-groups@example.com", "password", isTenantAdmin: true);
        var memberPrincipalId = await _factory.SeedUserPrincipalAsync("member-groups@example.com", "password", isTenantAdmin: false);
        using var client = CreateAuthenticatedClient(adminPrincipalId);

        var usersResponse = await client.GetAsync(new Uri("/v1/admin/users", UriKind.Relative));
        var users = await usersResponse.Content.ReadFromJsonAsync<List<AdminUserListItemDto>>();
        Assert.NotNull(users);
        var memberUser = users!.First(u => u.PrincipalId == memberPrincipalId);

        // Act — create group
        var createResponse = await client.PostAsJsonAsync(
            new Uri("/v1/admin/groups", UriKind.Relative),
            new CreateAdminGroupRequest { Name = $"test-{Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var group = await createResponse.Content.ReadFromJsonAsync<AdminGroupDetailDto>();
        Assert.NotNull(group);

        var membersResponse = await client.PutAsJsonAsync(
            new Uri($"/v1/admin/groups/{group!.GroupId}/members", UriKind.Relative),
            new SetAdminGroupMembersRequest { UserIds = [memberUser.UserId] });
        Assert.Equal(HttpStatusCode.OK, membersResponse.StatusCode);

        var permissionsResponse = await client.PutAsJsonAsync(
            new Uri($"/v1/admin/groups/{group.GroupId}/permissions", UriKind.Relative),
            new SetAdminGroupPermissionsRequest { PermissionKeys = ["definitions.read"] });
        Assert.Equal(HttpStatusCode.OK, permissionsResponse.StatusCode);
        var updated = await permissionsResponse.Content.ReadFromJsonAsync<AdminGroupDetailDto>();

        // Assert
        Assert.NotNull(updated);
        Assert.Contains(memberUser.UserId, updated!.MemberUserIds);
        Assert.Contains("definitions.read", updated.PermissionKeys);
    }

    /// <summary>管理者は API キーを発行し失効できる。</summary>
    [Fact]
    public async Task CreateApiKey_Revoke_ReturnsPlainKeyThenNoContent()
    {
        // Arrange
        var adminPrincipalId = await _factory.SeedUserPrincipalAsync("admin-apikeys@example.com", "password", isTenantAdmin: true);
        using var client = CreateAuthenticatedClient(adminPrincipalId);

        // Act — create
        var createResponse = await client.PostAsJsonAsync(
            new Uri("/v1/admin/api-keys", UriKind.Relative),
            new CreateAdminApiKeyRequest
            {
                Name = "integration-key",
                AllowedScopes = ["executions.read"]
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreatedAdminApiKeyDto>();
        Assert.NotNull(created);
        Assert.StartsWith("stv_", created!.PlainKey, StringComparison.Ordinal);

        var listResponse = await client.GetAsync(new Uri("/v1/admin/api-keys", UriKind.Relative));
        var list = await listResponse.Content.ReadFromJsonAsync<List<AdminApiKeyListItemDto>>();
        Assert.NotNull(list);
        Assert.Contains(list!, item => item.ApiKeyId == created.ApiKeyId);

        var revokeResponse = await client.DeleteAsync(
            new Uri($"/v1/admin/api-keys/{created.ApiKeyId}", UriKind.Relative));

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
    }

    private HttpClient CreateAuthenticatedClient(Guid principalId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _factory.IssueBearerToken(principalId));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", "default");
        return client;
    }
}
