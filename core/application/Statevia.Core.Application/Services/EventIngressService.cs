using System.Text.Json;
using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Services;

namespace Statevia.Core.Application.Services;

/// <summary><see cref="IEventIngressService"/> 実装。</summary>
/// <remarks>
/// <para>照合は読み取りトランザクション。一致した Resume ワークは一括 enqueue する。</para>
/// <para>topic / key は正規化後の厳密一致。配信者は遷移名（event）を指定しない。</para>
/// </remarks>
internal sealed class EventIngressService(
    ICoreTransactionExecutor transactions,
    IExecutionWaitRepository waits,
    IExecutionWorkQueue workQueue,
    IIdGenerator idGenerator) : IEventIngressService
{
    /// <inheritdoc />
    public async Task PublishAsync(
        string topic,
        string key,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var normalizedTopic = topic.Trim();
        var normalizedKey = NormalizeKey(key);

        var matches = await transactions.ExecuteReadOnlyAsync(
            (uow, innerCt) => waits.ListMatchingSubscriptionsAsync(
                uow,
                normalizedTopic,
                normalizedKey,
                innerCt),
            ct).ConfigureAwait(false);

        if (matches.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var items = matches
            .Select(match => new ExecutionWorkItemRow
            {
                WorkItemId = idGenerator.NewSequentialGuid(),
                ExecutionId = match.ExecutionId,
                Kind = ExecutionWorkItemKinds.Resume,
                Payload = JsonSerializer.Serialize(
                    new ExecutionResumeWorkItemPayload(
                        ExecutionResumeWorkItemModes.Event,
                        match.NodeId,
                        match.ResumeEventName),
                    ExecutionWorkItemPayloadJson.Options),
                AvailableAt = now,
                Attempts = 0,
                CreatedAt = now
            })
            .ToList();

        await workQueue.EnqueueManyAsync(items, ct).ConfigureAwait(false);
    }

    /// <summary>key 未指定・空白を空文字へ正規化する。</summary>
    private static string NormalizeKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
