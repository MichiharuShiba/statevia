namespace Statevia.Core.Abstractions;

/// <summary>
/// 定義駆動型ワークフローエンジンのメインインターフェース。
/// 事実駆動型 FSM に基づき、YAML/JSON で定義されたワークフローを実行します。
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>コンパイル済み定義からワークフローインスタンスを開始し、その ID を返します。</summary>
    string Start(CompiledWorkflowDefinition definition);

    /// <summary>イベントを発行し、待機中の状態を再開します（Wait / Resume 仕様）。</summary>
    void PublishEvent(string eventName);

    /// <summary>ワークフローに協調的キャンセルをリクエストします。エンジンは強制終了しません。</summary>
    Task CancelAsync(string workflowId);

    /// <summary>ワークフローインスタンスの現在のスナップショットを取得します。</summary>
    WorkflowSnapshot? GetSnapshot(string workflowId);

    /// <summary>実行グラフを JSON としてエクスポートします（デバッグ・可視化用）。</summary>
    string ExportExecutionGraph(string workflowId);
}
