

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>変異認可を常に通過させるテスト用スタブ。</summary>
internal sealed class AllowAllExecutionMutationAuthorization : IExecutionMutationAuthorization
{
    /// <inheritdoc />
    public Task EnsureMutationPermissionAsync(
        ExecutionSecuritySnapshot? snapshot,
        string permissionKey,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
