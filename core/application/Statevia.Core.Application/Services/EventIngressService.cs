using System.Text.Json;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Services;

namespace Statevia.Core.Application.Services;

/// <summary><see cref="IEventIngressService"/> 実装。</summary>
/// <remarks>
/// <para>照合は読み取りトランザクション。一致した Resume ワークは一括 enqueue する。</para>
/// </remarks>
internal sealed class EventIngressService(
    ICoreTransactionExecutor transactions,
    IExecutionWaitRepository waits,
    IExecutionWorkQueue workQueue) : IEventIngressService
{
    /// <inheritdoc />
    public async Task PublishAsync(
        string eventName,
        string? correlationKey,
        string? topic,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var normalizedEventName = eventName.Trim();
        var normalizedCorrelationKey = NormalizeOptional(correlationKey);
        var normalizedTopic = NormalizeOptional(topic);

        var matchingWaits = await transactions.ExecuteReadOnlyAsync(
            (uow, innerCt) => waits.ListMatchingEventWaitsAsync(
                uow,
                normalizedEventName,
                normalizedCorrelationKey,
                normalizedTopic,
                innerCt),
            ct).ConfigureAwait(false);

        if (matchingWaits.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var items = matchingWaits
            .Select(wait => new ExecutionWorkItemRow
            {
                WorkItemId = Guid.NewGuid(),
                ExecutionId = wait.ExecutionId,
                Kind = ExecutionWorkItemKinds.Resume,
                Payload = JsonSerializer.Serialize(
                    new ExecutionResumeWorkItemPayload(wait.NodeId, normalizedEventName)),
                AvailableAt = now,
                Attempts = 0,
                CreatedAt = now
            })
            .ToList();

        await workQueue.EnqueueManyAsync(items, ct).ConfigureAwait(false);
    }

    /// <summary>空白を null へ正規化する。</summary>
    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
