using Microsoft.EntityFrameworkCore;
using Statevia.Infrastructure.Persistence;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary>execution_waits 永続化。</summary>
internal sealed class ExecutionWaitRepository : IExecutionWaitRepository
{
    /// <inheritdoc />
    public async Task ReplaceWaitsAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        IReadOnlyList<ExecutionWaitRow> waits,
        CancellationToken ct)
    {
        // 当該 execution_id の execution_waits 行を取得
        var existingRows = await uow.GetDb().ExecutionWaits
            .Where(x => x.ExecutionId == executionId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // 存在しない node_id の execution_waits 行を削除
        var desiredNodeIds = waits.Select(x => x.NodeId).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in existingRows.Where(x => !desiredNodeIds.Contains(x.NodeId)))
            uow.GetDb().ExecutionWaits.Remove(stale);

        var existingByNodeId = existingRows.ToDictionary(x => x.NodeId, StringComparer.Ordinal);
        foreach (var wait in waits)
        {
            // 存在する node_id の execution_waits 行を更新
            if (existingByNodeId.TryGetValue(wait.NodeId, out var existing))
            {
                existing.WaitKind = wait.WaitKind;
                existing.ResumeToken = wait.ResumeToken;
                existing.ExpiresAt = wait.ExpiresAt;
                existing.CreatedAt = wait.CreatedAt;
                continue;
            }

            // 存在しない node_id の execution_waits 行を追加
            uow.GetDb().ExecutionWaits.Add(new ExecutionWaitRow
            {
                ExecutionId = executionId,
                NodeId = wait.NodeId,
                WaitKind = wait.WaitKind,
                ResumeToken = wait.ResumeToken,
                ExpiresAt = wait.ExpiresAt,
                CreatedAt = wait.CreatedAt
            });
        }
    }

    /// <inheritdoc />
    public async Task DeleteByResumeTokenAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        string resumeToken,
        CancellationToken ct)
    {
        // resume_token 一致行を取得
        var rows = await uow.GetDb().ExecutionWaits
            .Where(x => x.ExecutionId == executionId && x.ResumeToken == resumeToken)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (rows.Count == 0)
            return;

        // resume_token 一致行を削除
        uow.GetDb().ExecutionWaits.RemoveRange(rows);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionWaitRow>> ListByExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid executionId,
        CancellationToken ct) =>
        await uow.GetDb().ExecutionWaits.AsNoTracking()
            .Where(x => x.ExecutionId == executionId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.NodeId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
}
