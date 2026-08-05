using Microsoft.EntityFrameworkCore;

namespace Statevia.Infrastructure.Persistence.Repositories;

/// <summary><c>execution_branches</c> 永続化。</summary>
internal sealed class ExecutionBranchRepository : IExecutionBranchRepository
{
    /// <inheritdoc />
    public async Task InsertBranchesIdempotentAsync(
        ICoreUnitOfWork uow,
        IReadOnlyList<ExecutionBranchRow> branches,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(branches);
        if (branches.Count == 0)
            return;

        var db = uow.GetDb();
        foreach (var branch in branches)
        {
            ValidateBranch(branch);

            var existing = await db.ExecutionBranches
                .FirstOrDefaultAsync(
                    x => x.ParentExecutionId == branch.ParentExecutionId
                         && x.ForkState == branch.ForkState
                         && x.BranchState == branch.BranchState,
                    ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                db.ExecutionBranches.Add(new ExecutionBranchRow
                {
                    ParentExecutionId = branch.ParentExecutionId,
                    ExecutionId = branch.ExecutionId,
                    ForkState = branch.ForkState,
                    JoinState = branch.JoinState,
                    BranchState = branch.BranchState,
                    Status = branch.Status,
                    OutputJson = branch.OutputJson,
                    CreatedAt = branch.CreatedAt,
                    UpdatedAt = branch.UpdatedAt
                });
                continue;
            }

            if (existing.ExecutionId != branch.ExecutionId)
            {
                throw new InvalidOperationException(
                    $"execution_branches conflict: parent={branch.ParentExecutionId:D} fork={branch.ForkState} branch={branch.BranchState} already bound to execution {existing.ExecutionId:D}.");
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionBranchRow>> ListByParentExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid parentExecutionId,
        CancellationToken ct) =>
        await uow.GetDb().ExecutionBranches.AsNoTracking()
            .Where(x => x.ParentExecutionId == parentExecutionId)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.ForkState)
            .ThenBy(x => x.BranchState)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionBranchRow>> ListByParentAndForkAsync(
        ICoreUnitOfWork uow,
        Guid parentExecutionId,
        string forkState,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(forkState);

        return await uow.GetDb().ExecutionBranches.AsNoTracking()
            .Where(x => x.ParentExecutionId == parentExecutionId && x.ForkState == forkState)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.BranchState)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ExecutionBranchRow?> GetByChildExecutionIdAsync(
        ICoreUnitOfWork uow,
        Guid childExecutionId,
        CancellationToken ct) =>
        await uow.GetDb().ExecutionBranches.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ExecutionId == childExecutionId, ct)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<bool> TryUpdateStatusAsync(
        ICoreUnitOfWork uow,
        Guid parentExecutionId,
        string forkState,
        string branchState,
        ExecutionBranchStatusUpdate update,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrWhiteSpace(forkState);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchState);
        ArgumentException.ThrowIfNullOrWhiteSpace(update.Status);

        var row = await uow.GetDb().ExecutionBranches
            .FirstOrDefaultAsync(
                x => x.ParentExecutionId == parentExecutionId
                     && x.ForkState == forkState
                     && x.BranchState == branchState,
                ct)
            .ConfigureAwait(false);

        if (row is null)
            return false;

        row.Status = update.Status;
        row.UpdatedAt = update.UpdatedAt;
        if (update.OutputJson is not null)
            row.OutputJson = update.OutputJson;

        return true;
    }

    private static void ValidateBranch(ExecutionBranchRow branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch.ForkState);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch.JoinState);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch.BranchState);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch.Status);
        if (branch.ParentExecutionId == Guid.Empty)
            throw new ArgumentException("ParentExecutionId is required.", nameof(branch));
        if (branch.ExecutionId == Guid.Empty)
            throw new ArgumentException("ExecutionId is required.", nameof(branch));
        if (branch.ParentExecutionId == branch.ExecutionId)
            throw new ArgumentException("ParentExecutionId and ExecutionId must differ.", nameof(branch));
    }
}
