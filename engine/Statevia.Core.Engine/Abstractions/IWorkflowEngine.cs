namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// 定義駆動型ワークフローエンジンのメインインターフェース。
/// 事実駆動型 FSM に基づき、YAML/JSON で定義されたワークフローを実行します。
/// </summary>
public interface IWorkflowEngine
{
    /// <summary>コンパイル済み定義からワークフローインスタンスを開始し、その ID を返します。workflowId を指定した場合はその ID を使用し、null の場合はエンジンが生成します。</summary>
    /// <param name="workflowInput">初期状態の <see cref="IStateExecutor.ExecuteAsync"/> に渡す入力（省略時は <c>null</c>）。</param>
    string Start(CompiledWorkflowDefinition definition, string? workflowId = null, object? workflowInput = null);

    /// <summary>指定ワークフローにのみイベントを発行し、待機中の状態を再開します（Wait / Resume 仕様）。</summary>
    void PublishEvent(string workflowId, string eventName);

    /// <summary>全ワークフローにイベントをブロードキャストし、待機中の状態を再開します。</summary>
    void PublishEvent(string eventName);

    /// <summary>ワークフローに協調的キャンセルをリクエストします。エンジンは強制終了しません。</summary>
    Task CancelAsync(string workflowId);

    /// <summary>ワークフローインスタンスの現在のスナップショットを取得します。</summary>
    WorkflowSnapshot? GetSnapshot(string workflowId);

    /// <summary>実行グラフを JSON としてエクスポートします（デバッグ・可視化用）。</summary>
    string ExportExecutionGraph(string workflowId);
}
