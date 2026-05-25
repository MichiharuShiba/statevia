using Statevia.Core.Api.Abstractions.Persistence;
using Statevia.Core.Api.Application.Security;
using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Abstractions.Services;

/// <summary>project_accesses ベースの認可（truth）。visibility は参照しない。</summary>
internal interface IProjectAuthorizationService
{
    /// <summary>Reader 以上の権限を要求する。不足時は <see cref="NotFoundException"/>。</summary>
    Task EnsureCanReadAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Executor 以上の権限を要求する。Reader のみの場合は <see cref="ForbiddenException"/>。</summary>
    Task EnsureCanExecuteAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Publisher 以上の権限を要求する。</summary>
    Task EnsureCanPublishAsync(
        ICoreUnitOfWork uow,
        Guid tenantInternalId,
        Guid projectId,
        CancellationToken ct);
}
