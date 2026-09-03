using System.Text.Json;

namespace Statevia.Core.Application.Services;

/// <summary><see cref="IEventIngressService"/> 実装。</summary>
/// <remarks>
/// <para>照合は読み取りトランザクション。一致した Resume ワークは一括 enqueue する。</para>
/// <para>入口で <c>executions.write</c> を要求する。照合 SQL に現在 TenantId を明示する。</para>
/// <para>テナント未解決なら enqueue しない。topic / key は正規化後の厳密一致。配信者は遷移名（event）を指定しない。</para>
/// </remarks>
/// <param name="transactions">読み取りトランザクション実行。</param>
/// <param name="waits">Wait 購読照合。</param>
/// <param name="workQueue">Resume ワーク投入。</param>
/// <param name="idGenerator">ワーク ID 生成。</param>
/// <param name="runtimeAuth">Runtime permission 認可。</param>
/// <param name="tenantContext">現在テナント文脈。</param>
internal sealed class EventIngressService(
    ICoreTransactionExecutor transactions,
    IExecutionWaitRepository waits,
    IExecutionWorkQueue workQueue,
    IIdGenerator idGenerator,
    IRuntimePermissionAuthorization runtimeAuth,
    ITenantContextAccessor tenantContext) : IEventIngressService
{
    /// <inheritdoc />
    public async Task PublishAsync(
        string topic,
        string key,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        await runtimeAuth
            .EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsWrite, ct)
            .ConfigureAwait(false);

        if (tenantContext.TenantId is not { } tenantId)
            return;

        var normalizedTopic = topic.Trim();
        var normalizedKey = NormalizeKey(key);

        var matches = await transactions.ExecuteReadOnlyAsync(
            (uow, innerCt) => waits.ListMatchingSubscriptionsAsync(
                uow,
                tenantId,
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
