namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// Wait / Resume のセマンティクスを提供します。
/// Wait は指定イベントが発行されるまで状態実行を一時停止し、
/// Resume（Signal / PublishEvent）で同一の状態実行を継続します。
/// 各 Wait は一度だけ再開できます。
/// </summary>
public interface IEventProvider
{
    /// <summary>指定イベントが発行されるか、キャンセルされるまで待機します。</summary>
    Task WaitAsync(string eventName, CancellationToken ct);

    /// <summary>
    /// 実行スコープ内シグナルを待機します（<see cref="WaitAsync"/> のエイリアス）。
    /// </summary>
    Task WaitForSignalAsync(string signalName, CancellationToken ct) => WaitAsync(signalName, ct);

    /// <summary>実行スコープ内の待機中シグナルを再開します。</summary>
    void Signal(string signalName);

    /// <summary>システムスコープのトピックへイベントを発行します（外部 bus 未接続時は dispatch ログのみ）。</summary>
    void PublishTopic(string topic, object? payloadSummary);
}
