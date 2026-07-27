namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// Wait / Resume のセマンティクスを提供します。
/// Wait は指定イベントが発行されるまで状態実行を一時停止し、
/// Resume（ノードスコープ）または Signal / PublishEvent で同一の状態実行を継続します。
/// 各 Wait は一度だけ再開できます。
/// </summary>
public interface IEventProvider
{
    /// <summary>指定イベントが発行されるか、キャンセルされるまで待機します。</summary>
    Task WaitAsync(string eventName, CancellationToken ct);

    /// <summary>
    /// 指定ノードでいずれかのイベントが発行されるまで待機し、受信したイベント名を返す。
    /// </summary>
    /// <param name="nodeId">待機中 Wait ノード ID。</param>
    /// <param name="eventNames">受付イベント名一覧（非空）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>実際に Resume されたイベント名。</returns>
    /// <remarks>1 ノードにつき同時に 1 待機のみ。Resume 成功時に当該ノードの waiter を除去する。</remarks>
    Task<string> WaitForEventAsync(string nodeId, IReadOnlyList<string> eventNames, CancellationToken ct);

    /// <summary>
    /// 実行スコープ内シグナルを待機します（<see cref="WaitAsync"/> のエイリアス）。
    /// </summary>
    Task WaitForSignalAsync(string signalName, CancellationToken ct) => WaitAsync(signalName, ct);

    /// <summary>実行スコープ内の待機中シグナルを再開します。</summary>
    void Signal(string signalName);

    /// <summary>
    /// 待機中ノードを指定イベントで再開する。該当 waiter が無い、またはイベントが許可外のときは何もしない。
    /// </summary>
    /// <param name="nodeId">待機中 Wait ノード ID。</param>
    /// <param name="eventName">発生させるイベント名。</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Naming",
        "CA1716:Identifiers should not match keywords",
        Justification = "wait-multi-event-routing 設計正本のメソッド名 Resume(nodeId, eventName) に合わせる。")]
    void Resume(string nodeId, string eventName);

    /// <summary>システムスコープのトピックへイベントを発行します（外部 bus 未接続時は dispatch ログのみ）。</summary>
    void PublishTopic(string topic, object? payloadSummary);
}
