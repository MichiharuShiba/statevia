namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// Cancel / Publish 等の Serializable 永続化と競合時の再試行。
/// </summary>
public interface IExecutionMutationPersistence
{
    /// <summary>
    /// Serializable で永続化を試み、PostgreSQL の直列化失敗・デッドロック時は設定に従い再試行する。
    /// </summary>
    Task ExecuteSerializableWithRetryAsync(
        Guid tenantId,
        Guid executionId,
        Guid clientEventId,
        Func<ICoreUnitOfWork, CancellationToken, Task> applyAsync,
        CancellationToken cancellationToken = default);
}
