namespace Statevia.Core.Engine.Abstractions;

/// <summary>実行スコープ内の制御シグナル（intra-WF wait / signal 連携）。</summary>
/// <param name="Name">シグナル名。</param>
public sealed record ExecutionSignal(string Name);

/// <summary>システムスコープのドメインイベント（publish / 外部 bus 連携の要約）。</summary>
/// <param name="Topic">トピック名。</param>
/// <param name="PayloadSummary">ペイロード要約（機微値を含めない）。</param>
public sealed record DomainEvent(string Topic, object? PayloadSummary);
