
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Abstractions.Services;

/// <summary>project_accesses ベースの認可（truth）。visibility は参照しない。</summary>
internal interface IProjectAuthorizationService
{
    /// <summary>Reader 以上の権限を要求する。不足時は <see cref="NotFoundException"/>。</summary>
    Task EnsureCanReadAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Executor 以上の権限を要求する。Reader のみの場合は <see cref="ForbiddenException"/>。</summary>
    Task EnsureCanExecuteAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Publisher 以上の権限を要求する。</summary>
    Task EnsureCanPublishAsync(
        ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);
}
