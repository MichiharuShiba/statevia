namespace Statevia.Core.Abstractions;

/// <summary>
/// Wait / Resume のセマンティクスを提供します。
/// Wait は指定イベントが発行されるまで状態実行を一時停止し、
/// Resume（PublishEvent）で同一の状態実行を継続します。
/// 各 Wait は一度だけ再開できます。
/// </summary>
public interface IEventProvider
{
    /// <summary>指定イベントが発行されるか、キャンセルされるまで待機します。</summary>
    Task WaitAsync(string eventName, CancellationToken ct);
}
