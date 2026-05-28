using Statevia.Core.Api.Persistence;

namespace Statevia.Core.Api.Abstractions.Persistence;

/// <summary>execution_waits 永続化。</summary>
internal interface IExecutionWaitRepository
{
    /// <summary>
    /// 当該 execution の durable wait を置換する（不在行は削除、存在行は upsert）。
    /// </summary>
    Task ReplaceWaitsAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        IReadOnlyList<ExecutionWaitRow> waits,
        CancellationToken ct);

    /// <summary>resume_token に一致する wait 行を削除する（Publish / Resume 経路）。</summary>
    Task DeleteByResumeTokenAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string resumeToken,
        CancellationToken ct);

    /// <summary>execution_id で wait 行を取得する（読み取り専用）。</summary>
    Task<IReadOnlyList<ExecutionWaitRow>> ListByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct);
}
