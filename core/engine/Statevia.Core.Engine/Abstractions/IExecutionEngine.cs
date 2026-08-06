namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// 定義駆動型実行エンジンのメインインターフェース。
/// 事実駆動型 FSM に基づき、コンパイル済みワークフロー定義を実行します。
/// </summary>
public interface IExecutionEngine
{
    /// <summary>コンパイル済み定義から実行を開始し、execution ID を返します。executionId を指定した場合はその ID を使用し、null の場合はエンジンが生成します。</summary>
    /// <param name="definition">コンパイル済みワークフロー定義。</param>
    /// <param name="executionId">使用する実行インスタンス ID。省略時はエンジンが生成する。</param>
    /// <param name="input">初期状態の <see cref="IStateExecutor.ExecuteAsync"/> に渡す入力（省略時は <c>null</c>）。</param>
    /// <param name="initialState">開始状態名。省略時は <see cref="CompiledWorkflowDefinition.InitialState"/>。</param>
    string Start(
        CompiledWorkflowDefinition definition,
        string? executionId = null,
        object? input = null,
        string? initialState = null);

    /// <summary>
    /// 待機中 Wait ノードを指定イベントで再開する（ランタイム正本）。
    /// </summary>
    /// <param name="executionId">実行インスタンス ID。</param>
    /// <param name="nodeId">待機中 Wait ノード ID。</param>
    /// <param name="eventName">発生させるイベント名（WaitEventRouteTable のキー）。</param>
    /// <exception cref="InvalidOperationException">実行・ノード・イベントが不正なとき。</exception>
    void ResumeWaitNode(string executionId, string nodeId, string eventName);

    /// <summary>
    /// 非推奨シム。アクティブな Wait がちょうど 1 ノードかつそのイベントが 1 件のときのみ
    /// <see cref="ResumeWaitNode"/> に委譲する。
    /// </summary>
    void PublishEvent(string executionId, string eventName);

    /// <summary>
    /// <see cref="PublishEvent(string, string)"/> と同様にイベントを発行するが、同一インスタンス内で
    /// <paramref name="clientEventId"/> が既に処理済みのときは発行せず <see cref="ApplyResult.AlreadyApplied"/> を返す。
    /// </summary>
    ApplyResult PublishEvent(string executionId, string eventName, Guid clientEventId);

    /// <summary>実行インスタンスに協調的キャンセルをリクエストします。エンジンは強制終了しません。</summary>
    Task CancelAsync(string executionId);

    /// <summary>
    /// <see cref="CancelAsync(string)"/> と同様に協調的キャンセルを行うが、同一 <paramref name="clientEventId"/> の再呼び出しは No-Op となる。
    /// </summary>
    Task<ApplyResult> CancelAsync(string executionId, Guid clientEventId);

    /// <summary>実行インスタンスの現在のスナップショットを取得します。</summary>
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

    /// <summary>
    /// Wait 登録直後の suspend 通知ハンドラを登録または解除します。
    /// 引数は (executionId, nodeId) です。ホストが checkpoint 保存と Unload に使います。
    /// </summary>
    /// <remarks>単体 DLL 利用時は未登録のままでよい（メモリ常駐）。</remarks>
    void SetSuspendHandler(Func<string, string, Task>? handler);

    /// <summary>
    /// Fork 到達時の物理子展開ハンドラを登録または解除します。
    /// </summary>
    /// <remarks>
    /// <para>登録時は論理 Fork（同一インスタンス上の分岐 Schedule）を行わず、ハンドラのみ呼び出す。</para>
    /// <para>未登録時は従来どおり論理 Fork する（スタンドアロン Engine）。</para>
    /// </remarks>
    void SetForkExpansionHandler(Func<ForkExpansionEvent, Task>? handler);

    /// <summary>再開可能なランタイム状態をエクスポートする。</summary>
    /// <param name="executionId">実行インスタンス ID。</param>
    /// <returns>チェックポイント。未ロードなら null。</returns>
    ExecutionRuntimeCheckpoint? ExportCheckpoint(string executionId);

    /// <summary>
    /// チェックポイントから実行を復元し、未完了 Wait の継続と完了フロンティア遷移を再開する。
    /// </summary>
    /// <param name="definition">チェックポイントの定義名と一致するコンパイル済み定義。</param>
    /// <param name="checkpoint">保存済みチェックポイント。</param>
    /// <remarks>
    /// <para>未完了 Wait は待ち直し。完了済みで出辺のないノードは <c>ProcessFact</c> 相当で次状態へ進む
    /// （ステップ完了時点の checkpoint からの recovery 用）。</para>
    /// </remarks>
    void ImportCheckpoint(CompiledWorkflowDefinition definition, ExecutionRuntimeCheckpoint checkpoint);

    /// <summary>
    /// 実行をメモリから除去する。進行中 Wait は <see cref="ExecutionUnloadException"/> で打ち切る（ノードは未完了のまま）。
    /// </summary>
    /// <param name="executionId">実行インスタンス ID。</param>
    /// <returns>除去できたとき true。</returns>
    bool Unload(string executionId);
}
