namespace Statevia.Service.Api.Application.Actions;

/// <summary>組み込みアクションの Execution Capability 分類。</summary>
internal enum ActionCapabilityCategory
{
    /// <summary>子ワークフロー起動（workflow.invoke）。</summary>
    Workflow,

    /// <summary>時間待機（execution.sleep）。</summary>
    Timing,

    /// <summary>実行スコープ内シグナル（execution.signal）。</summary>
    Signal,

    /// <summary>システムイベント発行（event.publish）。</summary>
    Event,

    /// <summary>変換・即時完了（execution.noop / implicit）。</summary>
    Transform,
}
