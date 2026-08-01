namespace Statevia.Core.Application.Contracts.Persistence;

/// <summary>
/// <c>execution_wait_subscriptions</c> の購読行（1 Wait ノードあたり 0 件以上）。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Topic"/> と <see cref="CorrelationKey"/> は正規化済み文字列。
/// 未指定の key は空文字（NULL は使わない）。照合は両列の厳密一致。
/// </para>
/// </remarks>
public sealed class ExecutionWaitSubscriptionRow
{
    /// <summary>購読行 ID（PK。列名 <c>subscription_id</c>）。</summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>実行インスタンス ID。</summary>
    public Guid ExecutionId { get; set; }

    /// <summary>待機中 Wait ノード ID。</summary>
    public required string NodeId { get; set; }

    /// <summary>購読トピック。</summary>
    public required string Topic { get; set; }

    /// <summary>相関キー（未指定は空文字）。</summary>
    public required string CorrelationKey { get; set; }

    /// <summary>Resume に渡す内部イベント名。</summary>
    public required string ResumeEventName { get; set; }

    /// <summary>行作成時刻（UTC）。</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 集合配送照合で見つかった再開対象。
/// </summary>
/// <param name="ExecutionId">対象実行 ID。</param>
/// <param name="NodeId">Wait ノード ID。</param>
/// <param name="ResumeEventName">Resume に渡す内部イベント名。</param>
public sealed record MatchingWaitSubscription(
    Guid ExecutionId,
    string NodeId,
    string ResumeEventName);
