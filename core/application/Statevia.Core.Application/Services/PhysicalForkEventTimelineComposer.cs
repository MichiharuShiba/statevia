using Statevia.Core.Application.Contracts.Persistence;
using Statevia.Core.Application.Contracts.Services;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 親と物理子の <c>event_store</c> 行を、論理 1 実行のタイムラインへ合成する（D6.1・GET 時）。
/// </summary>
/// <remarks>
/// <para>永続は変更しない。応答 <c>seq</c> は合成通番。</para>
/// <para>子孫の WorkflowStarted / WorkflowCancelled は落とす。</para>
/// </remarks>
internal static class PhysicalForkEventTimelineComposer
{
    private static readonly string TypeStarted = EventStoreEventType.WorkflowStarted.ToPersistedString();
    private static readonly string TypeCancelled = EventStoreEventType.WorkflowCancelled.ToPersistedString();
    private static readonly string TypePublished = EventStoreEventType.EventPublished.ToPersistedString();

    /// <summary>合成入力の 1 行。</summary>
    /// <param name="Row">永続イベント行。</param>
    /// <param name="IsRoot">ルート（親）execution 由来なら true。</param>
    internal readonly record struct SourceRow(EventStoreRow Row, bool IsRoot);

    /// <summary>
    /// ソース行を並べ替え・フィルタし、合成 seq でページングする。
    /// </summary>
    /// <param name="sourceRows">親＋子孫の永続行。</param>
    /// <param name="rootDisplayId">タイムラインに載せる実行表示 ID。</param>
    /// <param name="patchNodes">GraphUpdated 用の合成グラフ patch ノード。</param>
    /// <param name="afterSeq">合成 seq の排他下限（0 で先頭から）。</param>
    /// <param name="limit">返却上限。</param>
    /// <returns>ページ分のイベントと hasMore。</returns>
    public static (IReadOnlyList<TimelineEventDto> Events, bool HasMore) ComposePage(
        IReadOnlyList<SourceRow> sourceRows,
        string rootDisplayId,
        IReadOnlyList<GraphPatchNodeDto> patchNodes,
        long afterSeq,
        int limit)
    {
        ArgumentNullException.ThrowIfNull(sourceRows);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDisplayId);
        ArgumentNullException.ThrowIfNull(patchNodes);
        ArgumentOutOfRangeException.ThrowIfNegative(afterSeq);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var composed = sourceRows
            .Select(MapTimelineEvent)
            .Where(e => e is not null)
            .Select(e => e!)
            .OrderBy(e => e.OccurredAt)
            .ThenByDescending(e => e.IsRoot) // 同刻は親優先
            .ThenBy(e => e.ExecutionId)
            .ThenBy(e => e.PersistedSeq)
            .Select((e, index) => new TimelineEventDto
            {
                Seq = index + 1L,
                Type = e.Type,
                ExecutionId = rootDisplayId,
                To = e.To,
                From = null,
                Patch = e.Type == "GraphUpdated"
                    ? new GraphUpdatedPatchDto { Nodes = patchNodes }
                    : null,
                At = e.OccurredAt.ToString("O")
            })
            .Where(e => e.Seq > afterSeq)
            .Take(limit + 1)
            .ToList();

        var hasMore = composed.Count > limit;
        if (hasMore)
            composed.RemoveAt(composed.Count - 1);

        return (composed, hasMore);
    }

    private static MappedEvent? MapTimelineEvent(SourceRow source)
    {
        var row = source.Row;
        if (!source.IsRoot
            && (row.Type == TypeStarted || row.Type == TypeCancelled))
        {
            return null;
        }

        if (row.Type == TypeStarted)
        {
            return new MappedEvent(
                source.IsRoot,
                row.ExecutionId,
                row.Seq,
                row.OccurredAt,
                "ExecutionStatusChanged",
                ExecutionProjectionStatuses.Running);
        }

        if (row.Type == TypeCancelled)
        {
            return new MappedEvent(
                source.IsRoot,
                row.ExecutionId,
                row.Seq,
                row.OccurredAt,
                "ExecutionStatusChanged",
                ExecutionProjectionStatuses.Cancelled);
        }

        if (row.Type == TypePublished)
        {
            return new MappedEvent(
                source.IsRoot,
                row.ExecutionId,
                row.Seq,
                row.OccurredAt,
                "GraphUpdated",
                null);
        }

        return null;
    }

    private sealed record MappedEvent(
        bool IsRoot,
        Guid ExecutionId,
        long PersistedSeq,
        DateTime OccurredAt,
        string Type,
        string? To);
}
