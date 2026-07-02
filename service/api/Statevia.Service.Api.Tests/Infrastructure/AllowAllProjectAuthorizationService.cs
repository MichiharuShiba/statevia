using Statevia.Service.Api.Abstractions.Services;

namespace Statevia.Service.Api.Tests.Infrastructure;

/// <summary>ユニットテスト用 — project 認可を常に許可する。</summary>
internal sealed class AllowAllProjectAuthorizationService : IProjectAuthorizationService
{
    /// <inheritdoc />
    public Task EnsureCanReadAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantId;
        _ = projectId;
        _ = ct;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureCanExecuteAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantId;
        _ = projectId;
        _ = ct;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task EnsureCanPublishAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct)
    {
        _ = uow;
        _ = tenantId;
        _ = projectId;
        _ = ct;
        return Task.CompletedTask;
    }
}
