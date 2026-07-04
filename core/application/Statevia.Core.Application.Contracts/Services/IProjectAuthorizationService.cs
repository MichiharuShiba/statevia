namespace Statevia.Core.Application.Contracts.Services;

/// <summary>project_accesses ベースの認可（truth）。visibility は参照しない。</summary>
public interface IProjectAuthorizationService
{
    /// <summary>Reader 以上の権限を要求する。不足時は <see cref="NotFoundException"/>。</summary>
    Task EnsureCanReadAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Executor 以上の権限を要求する。Reader のみの場合は <see cref="Security.ForbiddenException"/>。</summary>
    Task EnsureCanExecuteAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);

    /// <summary>Publisher 以上の権限を要求する。</summary>
    Task EnsureCanPublishAsync(
        Persistence.ICoreUnitOfWork uow,
        Guid tenantId,
        Guid projectId,
        CancellationToken ct);
}
