using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Abstractions.Services;

namespace Statevia.Core.Api.Tests.Infrastructure;

/// <summary>ユニットテスト用 — project 認可を常に許可する。</summary>
internal sealed class AllowAllProjectAuthorizationService : IProjectAuthorizationService
{
    /// <inheritdoc />
    public Task EnsureCanReadAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid projectId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantInternalId;
        _ = projectId;
        _ = ct;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureCanExecuteAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid projectId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantInternalId;
        _ = projectId;
        _ = ct;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureCanPublishAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid projectId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantInternalId;
        _ = projectId;
        _ = ct;
        return Task.CompletedTask;
    }
}
