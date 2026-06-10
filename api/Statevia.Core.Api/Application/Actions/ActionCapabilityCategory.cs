namespace Statevia.Core.Api.Application.Actions;

/// <summary>組み込みアクションの Execution Capability 分類。</summary>
internal enum ActionCapabilityCategory
{
    /// <summary>HTTP 呼び出し（rest）。</summary>
    Http,

    /// <summary>通知送信（notify）。</summary>
    Notification,

    /// <summary>子ワークフロー起動（workflow）。</summary>
    Workflow,

    /// <summary>時間待機（sleep）。</summary>
    Timing,

    /// <summary>実行スコープ内シグナル（signal）。</summary>
    Signal,

    /// <summary>システムイベント発行（publish）。</summary>
    Event,

    /// <summary>変換・即時完了（noop / implicit）。</summary>
    Transform,
}
