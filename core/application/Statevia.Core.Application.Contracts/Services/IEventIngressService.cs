namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// 外部イベントを現在テナントの durable Wait 購読へ配送するユースケース。
/// </summary>
/// <remarks>
/// <para>
/// 照合は <c>execution_wait_subscriptions</c>、再開は永続ワークキューへの Resume 投入で行う。
/// 一致 0 件でも成功（呼び出し元は 204 等で受理を返す想定）。
/// HTTP 入口では topic 必須、key 任意（省略時は空文字）。配信者は遷移名を指定しない。
/// <c>executions.write</c> を要求する。テナント未解決なら 0 件（他テナントへ積まない）。
/// </para>
/// </remarks>
public interface IEventIngressService
{
    /// <summary>
    /// 配送条件に一致する購読ごとに Resume ワークを投入する。
    /// </summary>
    /// <param name="topic">トピック（必須。前後空白は trim）。</param>
    /// <param name="key">相関キー（未指定・空白は空文字）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task PublishAsync(
        string topic,
        string key,
        CancellationToken ct);
}
