using Statevia.Service.Api.Abstractions.Security;
using Statevia.Service.Api.Infrastructure.Security;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>ユニットテスト用 — Runtime permission 認可を常に許可する。</summary>
internal sealed class AllowAllRuntimePermissionAuthorization : IRuntimePermissionAuthorization
{
    /// <inheritdoc />
    public Task EnsurePermissionAsync(string permissionKey, CancellationToken cancellationToken)
    {
        _ = permissionKey;
        _ = cancellationToken;
        return Task.CompletedTask;
    }
}
