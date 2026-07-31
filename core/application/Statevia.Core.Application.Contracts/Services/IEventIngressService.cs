namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// 外部イベントを現在テナントの durable EventWait へ配送するユースケース。
/// </summary>
/// <remarks>
/// <para>
/// 照合は <c>execution_waits</c>、再開は永続ワークキューへの Resume 投入で行う。
/// 一致 0 件でも成功（呼び出し元は 204 等で受理を返す想定）。
/// HTTP 入口では correlationKey / topic の少なくとも一方を必須とする（API バリデーション）。
/// </para>
/// </remarks>
public interface IEventIngressService
{
    /// <summary>
    /// 配送条件に一致する EventWait ごとに Resume ワークを投入する。
    /// </summary>
    /// <param name="eventName">イベント名（必須。前後空白は trim）。</param>
    /// <param name="correlationKey">相関キー。空白は未指定扱い。</param>
    /// <param name="topic">トピック。空白は未指定扱い。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task PublishAsync(
        string eventName,
        string? correlationKey,
        string? topic,
        CancellationToken ct);
}
