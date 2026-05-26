namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// 定義駆動型ワークフローエンジンのメインインターフェース。
/// 事実駆動型 FSM に基づき、YAML/JSON で定義されたワークフローを実行します。
/// </summary>
public interface IExecutionEngine
{
    /// <summary>コンパイル済み定義からワークフローインスタンスを開始し、その ID を返します。executionId を指定した場合はその ID を使用し、null の場合はエンジンが生成します。</summary>
    /// <param name="definition">コンパイル済みワークフロー定義。</param>
    /// <param name="executionId">使用する実行インスタンス ID。省略時はエンジンが生成する。</param>
    /// <param name="input">初期状態の <see cref="IStateExecutor.ExecuteAsync"/> に渡す入力（省略時は <c>null</c>）。</param>
    string Start(CompiledWorkflowDefinition definition, string? executionId = null, object? input = null);

    /// <summary>指定ワークフローにのみイベントを発行し、待機中の状態を再開します（Wait / Resume 仕様）。</summary>
    void PublishEvent(string executionId, string eventName);

    /// <summary>
    /// <see cref="PublishEvent(string, string)"/> と同様にイベントを発行するが、同一インスタンス内で
    /// <paramref name="clientEventId"/> が既に処理済みのときは発行せず <see cref="ApplyResult.AlreadyApplied"/> を返す。
    /// </summary>
    ApplyResult PublishEvent(string executionId, string eventName, Guid clientEventId);

    /// <summary>
    /// 全ワークフローにイベントをブロードキャストし、待機中の状態を再開する。
    /// あるインスタンスで例外が発生しても、残りのインスタンスへの発行は継続する。
    /// すべてのインスタンスを処理したあと、失敗が一つでもあれば例外をスローする（複数件は <see cref="AggregateException"/>、一件のときはその例外をそのままスローする）。
    /// </summary>
    void PublishEvent(string eventName);

    /// <summary>
    /// 各ワークフローに対して <see cref="PublishEvent(string, string, Guid)"/> と同様にブロードキャストする。
    /// あるインスタンスで例外が発生しても、残りのインスタンスへの発行は継続する。
    /// すべてのインスタンスを処理したあと、失敗が一つでもあれば例外をスローする（複数件は <see cref="AggregateException"/>、一件のときはその例外をそのままスローする）。
    /// 例外がない場合、いずれか一つでも新規適用があれば <see cref="ApplyResult.Applied"/>、すべて <see cref="ApplyResult.AlreadyApplied"/> ならそれを返す。
    /// </summary>
    ApplyResult PublishEvent(string eventName, Guid clientEventId);

    /// <summary>ワークフローに協調的キャンセルをリクエストします。エンジンは強制終了しません。</summary>
    Task CancelAsync(string executionId);

    /// <summary>
    /// <see cref="CancelAsync(string)"/> と同様に協調的キャンセルを行うが、同一 <paramref name="clientEventId"/> の再呼び出しは No-Op となる。
    /// </summary>
    Task<ApplyResult> CancelAsync(string executionId, Guid clientEventId);

    /// <summary>ワークフローインスタンスの現在のスナップショットを取得します。</summary>
    ExecutionSnapshot? GetSnapshot(string executionId);

    /// <summary>実行グラフを JSON としてエクスポートします（デバッグ・可視化用）。</summary>
    string ExportExecutionGraph(string executionId);

    /// <summary>
    /// ノード完了（通常ステート / Join 合成ノード）通知ハンドラを登録または解除します。
    /// 引数は実行インスタンス ID です。
    /// </summary>
    /// <remarks>
    /// 直近の登録で上書きされます。<paramref name="handler"/> に <c>null</c> を渡すと解除します。
    /// </remarks>
    void SetNodeCompletedHandler(Func<string, Task>? handler);
}
